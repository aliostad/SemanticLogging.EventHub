﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using SemanticLogging.EventHub.Utility;

namespace SemanticLogging.EventHub
{
    public class EventHubAmqpSink : IObserver<EventEntry>, IDisposable
    {
        private readonly IEventHubClient eventHubClient;
        private string partitionKey;
        private BufferedEventPublisher<EventEntry> bufferedPublisher;
        private TimeSpan onCompletedTimeout;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private bool useAutomaticSizedBuffer;
        private string _instanceName;
        private string _roleName;
        private string _deploymentId;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventHubAmqpSink" /> class.
        /// </summary>
        /// <param name="eventHubConnectionString">The connection string for the eventhub.</param>
        /// <param name="eventHubName">The name of the eventhub.</param>
        /// <param name="bufferingInterval">The buffering interval between each batch publishing.</param>
        /// <param name="bufferingCount">The number of entries that will trigger a batch publishing.</param>
        /// <param name="maxBufferSize">The maximum number of entries that can be buffered while it's sending to the store before the sink starts dropping entries.</param>      
        /// <param name="onCompletedTimeout">Defines a timeout interval for when flushing the entries after an <see cref="OnCompleted"/> call is received and before disposing the sink.
        /// This means that if the timeout period elapses, some event entries will be dropped and not sent to the store. Normally, calling <see cref="IDisposable.Dispose"/> on 
        /// the <see cref="System.Diagnostics.Tracing.EventListener"/> will block until all the entries are flushed or the interval elapses.
        /// If <see langword="null"/> is specified, then the call will block indefinitely until the flush operation finishes.</param>
        /// <param name="partitionKey">PartitionKey is optional. If no partition key is supplied the log messages are sent to eventhub 
        /// and distributed to various partitions in a round robin manner.</param>
        public EventHubAmqpSink(
            string instanceName,
            string roleName,
            string deploymentId,
            string eventHubConnectionString, 
            string eventHubName, 
            TimeSpan bufferingInterval, 
            int bufferingCount, 
            int maxBufferSize, 
            TimeSpan onCompletedTimeout, 
            string partitionKey = null)
        {
            _deploymentId = deploymentId;
            _roleName = roleName;
            _instanceName = instanceName;
            Guard.ArgumentNotNullOrEmpty(eventHubConnectionString, "eventHubConnectionString");
            Guard.ArgumentNotNullOrEmpty(eventHubName, "eventHubName");

            var factory = MessagingFactory.CreateFromConnectionString(string.Format("{0};TransportType={1}", eventHubConnectionString, TransportType.Amqp));
            eventHubClient = new EventHubClientImp(factory.CreateEventHubClient(eventHubName));

            SetupSink(bufferingInterval, bufferingCount, maxBufferSize, onCompletedTimeout, partitionKey);
        }

        internal EventHubAmqpSink(IEventHubClient eventHubClient, TimeSpan bufferingInterval, int bufferingCount, int maxBufferSize, TimeSpan onCompletedTimeout, string partitionKey = null)
        {
            this.eventHubClient = eventHubClient;

            SetupSink(bufferingInterval, bufferingCount, maxBufferSize, onCompletedTimeout, partitionKey);
        }

        private void SetupSink(TimeSpan bufferingInterval, int bufferingCount, int maxBufferSize, TimeSpan onCompletedTimeout, string partitionKey)
        {
            useAutomaticSizedBuffer = bufferingCount == 0;

            this.partitionKey = partitionKey;
            this.onCompletedTimeout = onCompletedTimeout;

            string sinkId = string.Format(CultureInfo.InvariantCulture, "EventHubAmqpSink ({0})", Guid.NewGuid());
            bufferedPublisher = BufferedEventPublisher<EventEntry>.CreateAndStart(sinkId, PublishEventsAsync, bufferingInterval,
                bufferingCount, maxBufferSize, cancellationTokenSource.Token);
        }

        public void OnNext(EventEntry value)
        {
            bufferedPublisher.TryPost(value);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing">A value indicating whether or not the class is disposing.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "cancellationTokenSource", Justification = "Token is cancelled")]
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                cancellationTokenSource.Cancel();
                bufferedPublisher.Dispose();
            }
        }

        public void OnCompleted()
        {
            FlushSafe();
            Dispose();
        }

        public void OnError(Exception error)
        {
            FlushSafe();
            Dispose();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="EventHubAmqpSink"/> class.
        /// </summary>
        ~EventHubAmqpSink()
        {
            Dispose(false);
        }

        /// <summary>
        /// Flushes the buffer content to <see cref="PublishEventsAsync"/>.
        /// </summary>
        /// <returns>The Task that flushes the buffer.</returns>
        public Task FlushAsync()
        {
            return bufferedPublisher.FlushAsync();
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="EventHubAmqpSink"/> class.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private async Task<int> PublishEventsAsync(IList<EventEntry> collection)
        {
            try
            {
                if (useAutomaticSizedBuffer)
                {
                    return await SendAutoSizedBatchAsync(collection);
                }

                return await SendManualSizedBatchAsync(collection);
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return 0;
                }

                SemanticLoggingEventSource.Log.CustomSinkUnhandledFault(ex.ToString());
                throw;
            }
        }

        private ExtendedEventEntry GetExtendedEventEntry(EventEntry entry)
        {
            return new ExtendedEventEntry(entry)
            {
                InstanceName = _instanceName,
                DeploymentId = _deploymentId,
                RoleName = _roleName
            };
        }

        private async Task<int> SendManualSizedBatchAsync(ICollection<EventEntry> collection)
        {
            var events = collection.Select(entry =>
                        new EventData(Encoding.Default.GetBytes(JsonConvert.SerializeObject(GetExtendedEventEntry(entry) )))
                        {
                            PartitionKey = partitionKey
                        });

            await eventHubClient.SendBatchAsync(events);

            return collection.Count;
        }

        private async Task<int> SendAutoSizedBatchAsync(IEnumerable<EventEntry> collection)
        {
            var events = new List<EventData>();
            long totalSerializedSizeInBytes = 0;
            const long maxMessageSizeInBytes = 250000;

            foreach (var eventData in collection.Select(eventEntry => new EventData(Encoding.Default.GetBytes(JsonConvert.SerializeObject(
                GetExtendedEventEntry(eventEntry))))
            {
                PartitionKey = partitionKey
            }))
            {
                totalSerializedSizeInBytes += eventData.SerializedSizeInBytes;

                if (totalSerializedSizeInBytes > maxMessageSizeInBytes)
                {
                    break;
                }

                events.Add(eventData);
            }

            await eventHubClient.SendBatchAsync(events);

            return events.Count;
        }

        private void FlushSafe()
        {
            try
            {
                FlushAsync().Wait(onCompletedTimeout);
            }
            catch (AggregateException ex)
            {
                // Flush operation will already log errors. Never expose this exception to the observable.
                ex.Handle(e => e is FlushFailedException);
            }
        }
    }
}
