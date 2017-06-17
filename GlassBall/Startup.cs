using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(GlassBall.Startup))]
namespace GlassBall
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
            app.MapSignalR();
        }
    }
}
