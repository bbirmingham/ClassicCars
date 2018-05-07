using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(ClassicCars.Startup))]
namespace ClassicCars
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
