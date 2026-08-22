using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Controllers;

namespace ALHMobileAppAPI.Services
{
    public class RequireJwtAuthAttribute : AuthorizeAttribute
    {
        protected override bool IsAuthorized(HttpActionContext actionContext)
            => actionContext.Request.Properties.ContainsKey("UserId");

        protected override void HandleUnauthorizedRequest(HttpActionContext actionContext)
        {
            actionContext.Response = actionContext.Request.CreateResponse(
                HttpStatusCode.Unauthorized,
                new { Message = "Invalid or expired session. Please log in again." });
        }

        public static int CurrentUserId(HttpRequestMessage request)
            => (int)request.Properties["UserId"];

        public static string CurrentUserName(HttpRequestMessage request)
            => request.Properties.TryGetValue("UserName", out var v) ? (string)v : null;

        public static string CurrentEmail(HttpRequestMessage request)
            => request.Properties.TryGetValue("Email", out var v) ? (string)v : null;

        public static string CurrentEmpId(HttpRequestMessage request)
            => request.Properties.TryGetValue("EmpID", out var v) ? (string)v : null;
    }
}