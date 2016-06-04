using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(ControlWebApp.Startup))]
namespace ControlWebApp
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Disable authentication
            //ConfigureAuth(app);
            // Enable signalR
            app.MapSignalR();
        }
    }
}
