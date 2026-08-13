using ALHMobileAppAPI.Models;
using ALHMobileAppAPI.Services;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Configuration;
using CommanUtilities.Models;

namespace ALHMobileAppAPI.Controllers
{
    public class APIVersionController : BaseController
    {
        public IHttpActionResult Post()
        {
            ConfigDetails obj = new ConfigDetails();
            try
            {

                if (ConfigurationManager.AppSettings["IOSVersion"] != null)
                    obj.IOSVersion = ConfigurationManager.AppSettings["IOSVersion"].ToString();
                if (ConfigurationManager.AppSettings["AndriodVersion"] != null)
                    obj.AndriodVersion = ConfigurationManager.AppSettings["AndriodVersion"].ToString();
                if (ConfigurationManager.AppSettings["IOSURL"] != null)
                    obj.IOSURL = ConfigurationManager.AppSettings["IOSURL"].ToString();
                if (ConfigurationManager.AppSettings["AndriodURL"] != null)
                    obj.AndriodURL = ConfigurationManager.AppSettings["AndriodURL"].ToString();
                if (ConfigurationManager.AppSettings["ForceUpdate"] != null)
                    obj.ForceUpdate = Convert.ToBoolean(ConfigurationManager.AppSettings["ForceUpdate"]);
                obj.Code = ProcessStatus.Success;
                obj.Status = ProcessStatus.Success.ToString();
                obj.Message ="";
            }
            catch (Exception ex)
            {

                SetErrorObject(obj, ex, ex.Message );

            }
            return OkOrNotFound(obj);
        }
    }
}
