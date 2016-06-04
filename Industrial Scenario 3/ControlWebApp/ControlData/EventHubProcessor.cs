using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;

namespace ControlData
{
    public class EventHubProcessor
    {
        /// <summary>
        /// Attach an event processor host to an event hub.
        /// </summary>
        /// <param name="processorName">Used in blob storage for grouping stored partition offsets into a container.</param>
        /// <param name="serviceBusConnectionString">Full connection string to the service bus containing the hub.</param>
        /// <param name="offsetStorageConnectionString">Full connection string to the storage stamp for storing offsets.</param>
        /// <param name="eventHubName">Name of the event hub.</param>
        /// <param name="consumerGroupName">Name of the consumer group (use $Default if you don't know).</param>
        /// <param name="processorFactory">EventProcessorFactory instance.</param>
        /// <returns></returns>
        public static async Task<EventProcessorHost> AttachProcessorForHub(
            string processorName,
            string serviceBusConnectionString,
            string offsetStorageConnectionString,
            string eventHubName,
            string consumerGroupName,
            IEventProcessorFactory processorFactory)
        {
            var eventProcessorHost = new EventProcessorHost(processorName, eventHubName, consumerGroupName, serviceBusConnectionString, offsetStorageConnectionString);
            await eventProcessorHost.RegisterEventProcessorFactoryAsync(processorFactory);

            return eventProcessorHost;
        }
    }
}
