using ALHMobileAppAPI.ALHAppDAL;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;


namespace ALHMobileAppAPI.Models
{
    public class BasicAuthenticationAttribute : AuthorizationFilterAttribute
    {

        public override void OnAuthorization(HttpActionContext actionContext)
        {
            try
            {


                if (actionContext.Request.Headers.Authorization == null)
                {
                    actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized);
                }
                else
                {
                    // Gets header parameters  
                    var authToken = actionContext.Request.Headers.Authorization.Parameter;

                    // decoding authToken we get decode value in 'Username:Password' format
                    var decodeauthToken = Encoding.UTF8.GetString(Convert.FromBase64String(authToken));

                    // spliting decodeauthToken using ':'   
                    var arrUserNameandPassword = decodeauthToken.Split(':');
                    // Gets username and password  
                    string UserName = decodeauthToken.Split(':')[0];
                    string Password = decodeauthToken.Split(':')[1];
                    SignatureDAL OakDal = new SignatureDAL();
                    // Validate username and password  
                    if (OakDal.CheckValidateUser(UserName, Password))
                    {
                        // setting current principle  
                        Thread.CurrentPrincipal = new GenericPrincipal(
                               new GenericIdentity(arrUserNameandPassword[0]), null);
                    }
                    else
                    {
                        // returns unauthorized error  
                        actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized);
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }
            base.OnAuthorization(actionContext);
        }

    }
}