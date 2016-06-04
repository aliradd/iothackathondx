using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace ControlData
{
    public class EventHubReadingSource 
    {
        public EventHubReadingSource(Action<IReading> reading)
        {
            this._reading = reading;
        }

        public async Task StartAsync()
        {
            string eventHubConnectionString = string.Format("Endpoint=sb://{0}.servicebus.windows.net/;SharedAccessKeyName={1};SharedAccessKey={2}", 
               ConfigurationManager.AppSettings["ServiceBusNamespace"],
               ConfigurationManager.AppSettings["SharedAccessKeyName"],
               ConfigurationManager.AppSettings["SharedAccessKey"]);

            string storageConnectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
                ConfigurationManager.AppSettings["storageAccountName"], ConfigurationManager.AppSettings["storageAccountKey"]);

            string eventHubName = ConfigurationManager.AppSettings["EventHubName"];
            string consumerGroup = ConfigurationManager.AppSettings["ConsumerGroupName"] ?? "$Default";

            System.Diagnostics.Trace.TraceInformation("Connecting to {0}/{1}/{2}, storing in {3}", eventHubConnectionString, eventHubName, consumerGroup, storageConnectionString);

            var factory = new ReadingProcessorFactory(item =>
            {
                //Trace.TraceInformation("From EH: {0} @ ({1}, {2})", item.UserID, item.Latitude, item.Longitude);
                this._reading(item);
            });

            await EventHubProcessor.AttachProcessorForHub("control", eventHubConnectionString, storageConnectionString, eventHubName, consumerGroup, factory);
        }

        private Action<IReading> _reading;
    }

}
