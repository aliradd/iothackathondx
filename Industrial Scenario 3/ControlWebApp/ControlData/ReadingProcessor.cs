using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;

namespace ControlData
{
    public class ReadingProcessor : IEventProcessor
    {
        public ReadingProcessor(Action<IReading> item)
        {
            this.ItemCB = item;
        }

        public const int MessagesBetweenCheckpoints = 100;

        private int untilCheckpoint = MessagesBetweenCheckpoints;
        private Action<IReading> ItemCB;

        public Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            System.Diagnostics.Trace.TraceInformation("Closing RouteItemProcessor: {0}", reason);
            return Task.FromResult(false);
        }

        public Task OpenAsync(PartitionContext context)
        {
            System.Diagnostics.Trace.TraceInformation("Opening RouteItemProcessor");
            return Task.FromResult(false);
        }

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            foreach (var message in messages)
            {
                try
                {
                    var dataStr = Encoding.UTF8.GetString(message.GetBytes());
                    var readingItem = Utility.DeserializeMessage<ReadingEH>(dataStr);
                    try
                    {
                        this.ItemCB(readingItem);
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Trace.TraceError("Failed to process {0}: {1}", readingItem, e);
                        throw;
                    }
                    this.untilCheckpoint--;
                    if (this.untilCheckpoint == 0)
                    {
                        await context.CheckpointAsync();
                        this.untilCheckpoint = MessagesBetweenCheckpoints;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError(ex.Message);
                }
            }
        }
    }
}
