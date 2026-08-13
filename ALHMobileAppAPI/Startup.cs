using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;
using Microsoft.Owin.Security.OAuth;
using System.Web.Http;

[assembly: OwinStartup(typeof(ALHMobileAppAPI.Startup))]

namespace ALHMobileAppAPI
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {

            //this is very important line cross orgin source(CORS)it is used to enable cross-site HTTP requests 
            //For security reasons, browsers restrict cross-origin HTTP requests
            app.UseCors(Microsoft.Owin.Cors.CorsOptions.AllowAll);
           

        }
    }
}
