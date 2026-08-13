using ALHMobileAppAPI.App_Start;
using ALHMobileAppAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Cors;
namespace ALHMobileAppAPI
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // to enable Basic Authentication for entire Application
            config.Filters.Add(new BasicAuthenticationAttribute());
            // Web API configuration and services
            EnableCorsAttribute cors = new EnableCorsAttribute("*", "*", "*");
            config.EnableCors(cors);
            // Web API routes
            config.MapHttpAttributeRoutes();
            config.Filters.Add(new ValidateModelStateFilter());
            config.MessageHandlers.Add(new BasicAuthHandler());
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
