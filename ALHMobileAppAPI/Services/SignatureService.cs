using ALHMobileAppAPI.ALHAppDAL;
using ALHMobileAppAPI.Models;
using CommanUtilities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ALHMobileAppAPI.Services
{
    public class SignatureService 
    {
        public LoginDetails ValidateLoginCredentials(string username, string password)
        {
            SignatureDAL dal = new SignatureDAL();
            LoginDetails obj = dal.ValidateLoginCredentials(username, password);
            return obj;
        }
        public Base SaveSignatureRequests(SignatureModel SigParams)
        {
            SignatureDAL dal = new SignatureDAL();
            return dal.SaveSignatureRequests(SigParams);
        }
        public Base FetchSignatureRequests(string RequestId)
        {
            SignatureDAL dal = new SignatureDAL();
            return dal.FetchSignatureRequests(RequestId);
        }
        public Base FetchSSSignatureReciepientUsers(string name)
        {
            SignatureDAL dal = new SignatureDAL();
            return dal.FetchSSSignatureReciepientUsers(name);
        }
    }
}