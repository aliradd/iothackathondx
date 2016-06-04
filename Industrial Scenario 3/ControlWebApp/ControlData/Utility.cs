using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlData
{
    public class Utility
    {
        public static T DeserializeMessage<T>(string data, JsonSerializerSettings settings = null)
        {
            System.Diagnostics.Trace.TraceInformation("DeserializeMessage");
            System.Diagnostics.Trace.TraceInformation("Attempting to deserialize '{0}'", data);
            return settings == null
                ? JsonConvert.DeserializeObject<T>(data)
                : JsonConvert.DeserializeObject<T>(data, settings);
        }
    }
}
