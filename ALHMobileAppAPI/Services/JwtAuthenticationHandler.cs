using Microsoft.IdentityModel.Tokens;
using System;
using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ALHMobileAppAPI.Services
{
    public class JwtAuthenticationHandler : DelegatingHandler
    {
        private static readonly string SigningKey = ConfigurationManager.AppSettings["Jwt:Key"];
        private static readonly string Issuer = ConfigurationManager.AppSettings["Jwt:Issuer"];
        private static readonly string Audience = ConfigurationManager.AppSettings["Jwt:Audience"];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var authHeader = request.Headers.Authorization;

            if (authHeader != null &&
                string.Equals(authHeader.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(authHeader.Parameter))
            {
                AttachUserToRequest(request, authHeader.Parameter);
            }

            return await base.SendAsync(request, cancellationToken);
        }

        private void AttachUserToRequest(HttpRequestMessage request, string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(SigningKey);

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidIssuer = Issuer,
                    ValidAudience = Audience,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                }, out SecurityToken validatedToken);

                var jwt = (JwtSecurityToken)validatedToken;
                var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                var userName = jwt.Claims.FirstOrDefault(c => c.Type == "UserName")?.Value;

                if (userIdClaim != null && int.TryParse(userIdClaim, out int userId))
                {
                    request.Properties["UserId"] = userId;
                    request.Properties["UserName"] = userName;
                }
            }
            catch (SecurityTokenException)
            {
                // expired / bad signature / wrong issuer-audience -- leave Properties
                // unset; RequireJwtAuthAttribute rejects requests with no UserId set.
            }
            catch (Exception)
            {
                // malformed token string, etc. -- same handling.
            }
        }
    }
}