using ALHMobileAppAPI.CommonUtilities;
using ALHMobileAppAPI.Extensions;
using ALHMobileAppAPI.Models;
using ALHMobileAppAPI.Services;
using Aspose.Pdf;
using Aspose.Pdf.Devices;
using CommanUtilities.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace ALHMobileAppAPI.Controllers
{
    public class SignatureController : BaseController
    {
        // GET: Signature

        [HttpGet]
        [Route("API/Esign/ValidateLogin/{username}/{password}")]
        public IHttpActionResult ValidateLogin(string username, string password)
        {
            try
            {
                SignatureService svcObj = new SignatureService();
                var result = svcObj.ValidateLoginCredentials(username, password);
                return OkOrNotFound(result.SmartDataList);
            }
            catch (SqlException ex)
            {
                objBase.Message = "SqlException";
                SetErrorObject(objBase, ex, "SqlException");
            }
            catch (Exception ex)
            {
                SetErrorObject(objBase, ex, "Error in saving FetchPatientData");
            }
            return OkOrNotFound(objBase);
        }


        [HttpPost]
        [Route("API/Esign/SaveSignatureRequests")]
        public async Task<IHttpActionResult> Post([FromBody] SignatureModel DocParams)
        {
            try
            {
                SignatureService svcObj = new SignatureService();
                Base result =  await svcObj.SaveSignatureRequestsAsync(DocParams);
                return OkOrNotFound(result);
            }
            catch (SqlException ex)
            {
                objBase.Message = "SqlException";
                SetErrorObject(objBase, ex, "SqlException");
            }
            catch (Exception ex)
            {
                SetErrorObject(objBase, ex, "Error in saving SaveSignatureRequests");
            }
            return OkOrNotFound(objBase);
        }

        [HttpGet]
        [Route("API/Esign/FetchSignatureRequests")]
        public IHttpActionResult FetchSignatureRequests(string RequestId)
        {
            try
            {
                SignatureService svcObj = new SignatureService();
                var result = svcObj.FetchSignatureRequests(RequestId);
                return OkOrNotFound(result);

            }
            catch (SqlException ex)
            {
                objBase.Message = "File download error";
                SetErrorObject(objBase, ex, "FTPException");
            }
            catch (Exception ex)
            {
                SetErrorObject(objBase, ex, "Error in downloading File");
            }
            return OkOrNotFound(objBase);
        }
        [HttpGet]
        [Route("API/Esign/FetchSSSignatureReciepientUsers")]
        public IHttpActionResult FetchSSSignatureReciepientUsers(string name)
        {
            try
            {
                SignatureService svcObj = new SignatureService();
                var result = svcObj.FetchSSSignatureReciepientUsers(name);
                return OkOrNotFound(result);

            }
            catch (SqlException ex)
            {
                objBase.Message = "File download error";
                SetErrorObject(objBase, ex, "FTPException");
            }
            catch (Exception ex)
            {
                SetErrorObject(objBase, ex, "Error in downloading File");
            }
            return OkOrNotFound(objBase);
        }

        [HttpPost]
        [Route("API/Esign/PdfToImage")]
        public async Task<HttpResponseMessage> PdfToImage()
        {
            List<string> base64String = new List<string>();

            try
            {
                var provider = new MultipartMemoryStreamProvider();
                await Request.Content.ReadAsMultipartAsync(provider);
                

                foreach(var fileContent in provider.Contents)
                {
                    Stream sRead = await fileContent.ReadAsStreamAsync();
                    var document = new Document(sRead);

                    //
                    for (int i = 1; i < document.Pages.Count + 1; i++)
                    {
                        if (i < 5)
                        {
                            MemoryStream memoryStream = new MemoryStream();
                            var renderer = new JpegDevice();
                            renderer.Process(document.Pages[i], memoryStream);
                            byte[] bytes = memoryStream.ToArray();
                            base64String.Add(Convert.ToBase64String(bytes));
                        }
                    }

                    // var doc = new PdfDocument(new PdfReader(sRead));

                    //for (int i = 1; i <= doc.GetNumberOfPages(); i++)
                    //{
                    //    PdfPage pdfPage = doc.GetPage(i);
                    //    PdfWriter writer = new PdfWriter("C:\\Users\\Srihari Surendra\\source\\repos\\ZOHOAPI\\ALHMobileAppAPI\\Assets\\" + i + ".png", new WriterProperties().SetFullCompressionMode(true));
                    //    PdfDocument pdfDocument = new PdfDocument(writer);
                    //    PdfFormXObject pageCopy = pdfPage.CopyAsFormXObject(pdfDocument);
                    //    iText.Layout.Element.Image image = new iText.Layout.Element.Image(pageCopy);
                    //}



                    //var streams = doc.ConvertToJpgStreams();

                    //foreach (var stream in streams)
                    //{
                    //    StreamReader reader = new StreamReader(stream);
                    //    MemoryStream memoryStream = new MemoryStream();
                    //    reader.BaseStream.CopyTo(memoryStream);
                    //    Byte[] bytes = memoryStream.ToArray();

                    //}


                    //Byte[] bytes = ReadFully(sRead);
                    //String file = Convert.ToBase64String(bytes);
                    //PdfReader pdfReader = new PdfReader(sRead);

                    //using (PdfDocument pdfDocument = new PdfDocument(pdfReader))
                    //{
                    //    
                    //}
                }


                return Request.CreateResponse(System.Net.HttpStatusCode.OK, base64String);
            }
            catch (SqlException ex)
            {
                objBase.Message = "SqlException";
                SetErrorObject(objBase, ex, "SqlException");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(System.Net.HttpStatusCode.BadRequest, base64String);
                //SetErrorObject(objBase, ex, "Error in saving SaveSignatureRequests");
            }
            return Request.CreateResponse(System.Net.HttpStatusCode.OK);
        }

        public static byte[] ReadFully(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

    }

}