using Microsoft.Azure.Devices;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace ControlWebApp.API
{
    public class IoTHubController : ApiController
    {
        // GET api/<controller>
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/<controller>/5
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<controller>
        public async Task<bool> Post([FromBody]string value)
        {
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(ConfigurationManager.AppSettings["IoTHubConnectionString"]);
            var serviceMessage = new Microsoft.Azure.Devices.Message(Encoding.ASCII.GetBytes(value));
            serviceMessage.Ack = DeliveryAcknowledgement.Full;
            serviceMessage.MessageId = Guid.NewGuid().ToString();
            await serviceClient.SendAsync(ConfigurationManager.AppSettings["deviceId"], serviceMessage);
            System.Threading.Thread.Sleep(1000);
            await serviceClient.CloseAsync();

            return true;
        }

        // PUT api/<controller>/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/<controller>/5
        public void Delete(int id)
        {
        }
    }
}