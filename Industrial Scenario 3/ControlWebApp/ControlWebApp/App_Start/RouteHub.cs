using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ControlWebApp
{
    public class RouteHub : Hub
    {
        public RouteHub()
        {
        }

        public static IHubContext Hub()
        {
            return GlobalHost.ConnectionManager.GetHubContext<RouteHub>();
        }

        public static void Send(IHubContext hub, int temperature, int humidity)
        {
            hub.Clients.All.newReading(temperature, humidity);
        }

        public void Send(int temperature, int humidity)
        {
            Clients.All.newReading(temperature, humidity);
        }
    }
}