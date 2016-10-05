using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;

namespace SemanticLogging.EventHub
{
    internal sealed class ExtendedEventEntry
    {

        private readonly EventEntry eventEntry;

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudEventEntry"/> class.
        /// </summary>
        internal ExtendedEventEntry(EventEntry eventEntry)
        {
            this.Payload = InitializePayload(eventEntry.Payload, eventEntry.Schema);
            this.eventEntry = eventEntry;
        }

        /// <summary>
        /// Gets or sets the event identifier.
        /// </summary>
        /// <value>
        /// The event id.
        /// </value>
        public int EventId
        {
            get { return this.eventEntry.EventId; }
        }

        /// <summary>
        /// Gets or sets the event date.
        /// </summary>
        /// <value>
        /// The event date.
        /// </value>
        public DateTime EventDate
        {
            get { return this.eventEntry.Timestamp.UtcDateTime; }
        }

        /// <summary>
        /// Gets or sets the keywords for the event.
        /// </summary>
        /// <value>
        /// The keywords.
        /// </value>
        public long Keywords
        {
            get { return (long)this.eventEntry.Schema.Keywords; }
        }

        /// <summary>
        /// Gets or sets the unique identifier for the provider, which is typically the class derived from <see cref="System.Diagnostics.Tracing.EventSource"/>.
        /// </summary>
        /// <value>
        /// The provider ID.
        /// </value>
        public Guid ProviderId
        {
            get { return this.eventEntry.ProviderId; }
        }

        /// <summary>
        /// Gets or sets the friendly name of the class that is derived from the event source.
        /// </summary>
        /// <value>
        /// The name of the event source.
        /// </value>
        public string ProviderName
        {
            get { return this.eventEntry.Schema.ProviderName; }
        }

        /// <summary>
        /// Gets or sets the level of the event.
        /// </summary>
        /// <value>
        /// The level.
        /// </value>
        public int Level
        {
            get { return (int)this.eventEntry.Schema.Level; }
        }

        /// <summary>
        /// Gets or sets the message for the event.
        /// </summary>
        /// <value>
        /// The message.
        /// </value>
        public string Message
        {
            get { return this.eventEntry.FormattedMessage; }
        }

        /// <summary>
        /// Gets or sets the operation code for the event.
        /// </summary>
        /// <value>
        /// The operation code.
        /// </value>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "Opcode", Justification = "Uses casing from EventWrittenEventArgs.Opcode")]
        public int Opcode
        {
            get { return (int)this.eventEntry.Schema.Opcode; }
        }

        /// <summary>
        /// Gets or sets the task for the event.
        /// </summary>
        /// <value>
        /// The task code.
        /// </value>
        public int Task
        {
            get { return (int)this.eventEntry.Schema.Task; }
        }

        /// <summary>
        /// Gets or sets the version of the event.
        /// </summary>
        /// <value>
        /// The version.
        /// </value>
        public int Version
        {
            get { return this.eventEntry.Schema.Version; }
        }

        /// <summary>
        /// Gets or sets the process id.
        /// </summary>
        /// <value>The process id.</value>
        public int ProcessId
        {
            get { return this.eventEntry.ProcessId; }
        }

        /// <summary>
        /// Gets or sets the thread id.
        /// </summary>
        /// <value>The thread id.</value>
        public int ThreadId
        {
            get { return this.eventEntry.ThreadId; }
        }

        /// <summary>
        /// Gets or sets the activity id for the event.
        /// </summary>
        /// <value>
        /// The activity id.
        /// </value>
        public Guid ActivityId
        {
            get { return this.eventEntry.ActivityId; }
        }

        /// <summary>
        /// Gets or sets the related activity id for the event.
        /// </summary>
        /// <value>
        /// The related activity id.
        /// </value>
        public Guid RelatedActivityId
        {
            get { return this.eventEntry.RelatedActivityId; }
        }

        /// <summary>
        /// DeplymentId of the role running
        /// </summary>
        public string DeploymentId { get; set; }

        /// <summary>
        /// Name of the azure role. If you are not running in azure, use it for anything you need
        /// </summary>
        public string RoleName { get; set; }


        /// <summary>
        /// Gets or sets the instance name where the entries are generated from.
        /// </summary>
        /// <value>
        /// The name of the instance.
        /// </value>
        public string InstanceName { get; set; }

        /// <summary>
        /// Gets or sets the payload for the event.
        /// </summary>
        /// <value>
        /// The payload.
        /// </value>
        public IReadOnlyDictionary<string, object> Payload { get; private set; }


        private static IReadOnlyDictionary<string, object> InitializePayload(IList<object> payload, EventSchema schema)
        {
            var payloadDictionary = new Dictionary<string, object>(payload.Count);

            for (int i = 0; i < payload.Count; i++)
            {
                payloadDictionary.Add(schema.Payload[i], payload[i]);
            }

            return payloadDictionary;
        }
    }

}
