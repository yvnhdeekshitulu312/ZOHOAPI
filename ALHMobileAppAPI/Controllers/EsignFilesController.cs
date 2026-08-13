using System.IO;
using System.Web;
using System.Web.Http;
using System.Net.Http;
using System.Net.Http.Headers;

namespace ALHMobileAppAPI.Controllers
{
    public class EsignFilesController : BaseController
    {
        [HttpGet]
        [Route("API/Esign/GetFile/{fileName}")]
        public HttpResponseMessage GetFile(string fileName)
        {
            var folder = HttpContext.Current.Server.MapPath("~/App_Data/EsignFiles");
            var fullPath = Path.Combine(folder, fileName);

            if (!File.Exists(fullPath))
                return Request.CreateResponse(System.Net.HttpStatusCode.NotFound);

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            var response = Request.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Content = new StreamContent(stream);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            return response;
        }
    }
}
