﻿using System;
using System.Xml.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using SemanticLogging.EventHub.Utility;

namespace SemanticLogging.EventHub.Configuration
{
    internal class EventHubAmqpSinkElement : ISinkElement
    {
        private readonly XName sinkName = XName.Get("eventHubAmqpSink", "urn:dhs.sinks.eventHubAmqpSink");

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Validated with Guard class")]
        public bool CanCreateSink(XElement element)
        {
            Guard.ArgumentNotNull(element, "element");

            return element.Name == sinkName;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Validated with Guard class")]
        public IObserver<EventEntry> CreateSink(XElement element)
        {
            Guard.ArgumentNotNull(element, "element");

            var subject = new EventEntrySubject();
            subject.LogToEventHubUsingAmqp(
                null,
                (string)element.Attribute("eventHubConnectionString"),
                (string)element.Attribute("eventHubName"),
                element.Attribute("bufferingIntervalInSeconds").ToTimeSpan(),
                (int?)element.Attribute("bufferingCount") ?? Buffering.DefaultBufferingCount,
                element.Attribute("bufferingFlushAllTimeoutInSeconds").ToTimeSpan() ?? Constants.DefaultBufferingFlushAllTimeout,
                (int?)element.Attribute("maxBufferSize") ?? Buffering.DefaultMaxBufferSize,
                (string)element.Attribute("partitionKey")
                );

            return subject;
        }
    }
}
