using System;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

public class BasicAuthHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authHeader = request.Headers.Authorization;
        if (authHeader != null && authHeader.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase))
        {
            var creds = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Parameter)).Split(':');
            if (creds.Length == 2)
            {
                var username = creds[0];
                var password = creds[1];

               
                if (password == "12345")
                {
                    var identity = new GenericIdentity(username);
                    var principal = new GenericPrincipal(identity, new string[0]);
                    Thread.CurrentPrincipal = principal;
                    if (HttpContext.Current != null) HttpContext.Current.User = principal;
                }
            }
        }
        return await base.SendAsync(request, cancellationToken);
    }
}