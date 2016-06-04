using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common;
using Microsoft.Azure.Devices.Common.Security;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Twilio;

namespace Scenario1_Webjob
{
    class Program
    {
        const int RANGE = -70;
        const int RANGEINTERVAL = 60;

        public static DateTime previousEvent = DateTime.MinValue;

        static void Main(string[] args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            MainAsync(cts.Token).Wait();
        }

        static async Task MainAsync(CancellationToken token)
        {
            EventHubClient eventHubClient = null;
            EventHubReceiver eventHubReceiver = null;

            eventHubClient = EventHubClient.CreateFromConnectionString(Properties.Settings.Default.IoTHubConnectionString, "messages/events");
            int eventHubPartitionsCount = eventHubClient.GetRuntimeInformation().PartitionCount;
            string partition = EventHubPartitionKeyResolver.ResolveToPartition(Properties.Settings.Default.IoTDeviceId, eventHubPartitionsCount);
            eventHubReceiver = eventHubClient.GetConsumerGroup(Properties.Settings.Default.ConsumerGroupName).CreateReceiver(partition, DateTime.Now);

            while (true)
            {
                try
                {
                    EventData eventData = eventHubReceiver.Receive(TimeSpan.FromSeconds(1));

                    if (eventData != null)
                    {
                        string data = Encoding.UTF8.GetString(eventData.GetBytes());
                        string connectionDeviceId = eventData.SystemProperties["iothub-connection-device-id"].ToString();

                        if (string.CompareOrdinal(Properties.Settings.Default.IoTDeviceId, connectionDeviceId) == 0)
                        {
                            // Get RSSI reading from message
                            int rssi = 0;

                            if (rssi < RANGE)
                            {
                                if ((DateTime.Now - previousEvent).TotalSeconds >= RANGEINTERVAL) 
                                {
                                    previousEvent = DateTime.Now;
                                    string cloudToDeviceMessage = "{\"message\":\"flash\"}";

                                    ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(Properties.Settings.Default.IoTHubConnectionString);
                                    var serviceMessage = new Microsoft.Azure.Devices.Message(Encoding.ASCII.GetBytes(cloudToDeviceMessage));
                                    serviceMessage.Ack = DeliveryAcknowledgement.Full;
                                    serviceMessage.MessageId = Guid.NewGuid().ToString();
                                    await serviceClient.SendAsync(Properties.Settings.Default.IoTDeviceId, serviceMessage);
                                    System.Threading.Thread.Sleep(1000);
                                    await serviceClient.CloseAsync();
                                    Console.WriteLine("Sent flash message");

                                    // Send Twilio message
                                    string AccountSid = Properties.Settings.Default.TwilioAccountSid;
                                    string AuthToken = Properties.Settings.Default.TwilioAuthToken;
                                    var twilio = new TwilioRestClient(AccountSid, AuthToken);

                                    var message = twilio.SendMessage(Properties.Settings.Default.TwilioNumber, Properties.Settings.Default.NurseNumber, "Patient arrived in area");
                                    Console.WriteLine("SMS message sent. Sid: " + message.Sid);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occured. Error was " + ex.Message);
                }
            }
        }
    }
}
