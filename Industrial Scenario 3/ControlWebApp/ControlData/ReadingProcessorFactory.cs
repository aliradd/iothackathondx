using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;

namespace ControlData
{
    public class ReadingProcessorFactory : IEventProcessorFactory
    {
        public ReadingProcessorFactory(Action<IReading> item)
        {
            this.ItemCB = item;
        }

        public IEventProcessor CreateEventProcessor(PartitionContext context)
        {
            return new ReadingProcessor(this.ItemCB);
        }

        private Action<IReading> ItemCB;
    }
}
