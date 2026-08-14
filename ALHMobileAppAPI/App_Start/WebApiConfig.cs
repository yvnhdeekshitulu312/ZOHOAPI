using ALHMobileAppAPI.App_Start;
using System.Web.Http;
using System.Web.Http.Cors;

namespace ALHMobileAppAPI
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // BasicAuthenticationAttribute removed: it duplicated BasicAuthHandler's
            // job with a live SQL call on every request, and silently swallowed
            // exceptions in a way that let unauthenticated requests through on error.
            // BasicAuthHandler (message handler, below) is now the single auth path.

            EnableCorsAttribute cors = new EnableCorsAttribute("*", "*", "*");
            config.EnableCors(cors);

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