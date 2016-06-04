using Microsoft.ServiceBus.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlData
{
    public class ReadingSourceFactory
    { 
        public static async Task StartAsync(string readingSourceName, Action<IReading> reading)
        {
            if (string.IsNullOrWhiteSpace(readingSourceName)) throw new ArgumentNullException("readingSourceName");
            switch (readingSourceName.ToLowerInvariant())
            {
                case "eventhub":
                    {
                        await new EventHubReadingSource(reading).StartAsync();
                    }
                    break;
                default:
                    throw new ArgumentException("Unknown reading source: " + readingSourceName);
            }
        }
    }
}
