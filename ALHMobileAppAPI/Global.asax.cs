using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.SessionState;

namespace ALHMobileAppAPI
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            //PdfiumViewer.PdfiumResolver.Resolve += (sender, e) =>
            //{
            //    var binDir = System.Web.HttpRuntime.BinDirectory;
            //    var x64Path = System.IO.Path.Combine(binDir, "x64", "pdfium.dll");
            //    var x86Path = System.IO.Path.Combine(binDir, "x86", "pdfium.dll");
            //    var chosen = Environment.Is64BitProcess ? x64Path : x86Path;

            //    System.IO.File.AppendAllText(@"C:\pdfium-render-debug.log",
            //        $"{DateTime.Now:O}  Is64BitProcess={Environment.Is64BitProcess}  " +
            //        $"x64 exists={System.IO.File.Exists(x64Path)}  x86 exists={System.IO.File.Exists(x86Path)}  " +
            //        $"chosen={chosen}{Environment.NewLine}");

            //    e.PdfiumFileName = chosen;
            //};
            PdfiumViewer.PdfiumResolver.Resolve += (sender, e) =>
            {
                var binDir = System.Web.HttpRuntime.BinDirectory;
                var arch = Environment.Is64BitProcess ? "x64" : "x86";
                e.PdfiumFileName = System.IO.Path.Combine(binDir, arch, "pdfium.dll");
            };
            //CustomLogging.Initialize(Server.MapPath("~"));
            IPHostEntry iPHostEntry = Dns.GetHostEntry(Dns.GetHostName());
            //CustomLogging.LogMessage(CustomLogging.TracingLevel.INFO, "Customer IP With IPHostEntry: " + Convert.ToString(iPHostEntry.AddressList.FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)));
        }
        public override void Init()
        {
            this.PostAuthenticateRequest += MvcApplication_PostAuthenticateRequest;
            base.Init();
        }
        void MvcApplication_PostAuthenticateRequest(object sender, EventArgs e)
        {
            System.Web.HttpContext.Current.SetSessionStateBehavior(
                SessionStateBehavior.Required);
        }
    }
}
