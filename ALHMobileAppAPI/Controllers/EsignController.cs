using ALHMobileAppAPI.Esign.DTOs;
using ALHMobileAppAPI.Esign.Services;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace ALHMobileAppAPI.Controllers
{
    public class EsignController : BaseController
    {
        private IEsignService BuildEsignService()
        {
            var repository = new SqlEsignRepository();
            var fileStorage = new GcsFileStorageService();
            var stamper = new PdfStampingService();
            var notifier = new LoggingEsignNotificationService();
            var renderer = BuildPdfRenderingService();

            return new EsignService(
                repository, fileStorage, stamper, notifier, renderer,
                signingBaseUrl: "http://localhost:4200/esign/sign/"); // TODO: move to Web.config appSetting
        }

        private IPdfRenderingService BuildPdfRenderingService() => new AsposePdfRenderingService();

        private string GetClientIp()
        {
            var ip = HttpContext.Current?.Request?.UserHostAddress;
            if (ip == "::1") ip = "127.0.0.1";
            return ip;
        }

        [HttpPost]
        [Route("API/Esign/UploadDocument")]
        public async Task<IHttpActionResult> UploadDocument()
        {
            try
            {
                if (!Request.Content.IsMimeMultipartContent())
                {
                    objBase.Message = "Expected multipart/form-data content.";
                    objBase.Code = CommanUtilities.Models.ProcessStatus.Fail;
                    objBase.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
                    return OkOrNotFound(objBase);
                }

                var provider = await Request.Content.ReadAsMultipartAsync();
                var file = provider.Contents[0];
                var fileName = file.Headers.ContentDisposition.FileName?.Trim('"');
                var contentType = file.Headers.ContentType?.MediaType ?? "application/pdf";

                byte[] fileBytes;
                using (var sourceStream = await file.ReadAsStreamAsync())
                using (var buffer = new MemoryStream())
                {
                    await sourceStream.CopyToAsync(buffer);
                    fileBytes = buffer.ToArray();
                }

                var esignService = BuildEsignService();
                var uploadedBy = User?.Identity?.Name ?? "unknown";

                UploadDocumentResponse uploadResult;
                using (var uploadStream = new MemoryStream(fileBytes))
                {
                    uploadResult = await esignService.UploadDocumentAsync(uploadStream, fileName, contentType, uploadedBy);
                }

                // Re-fetch to return the cached page images generated during upload
                var doc = await esignService.GetDocumentAsync(uploadResult.DocumentId);

                var response = new
                {
                    uploadResult.DocumentId,
                    uploadResult.Name,
                    uploadResult.OriginalGcsPath,
                    PageImages = doc.PageImages
                };

                return OkOrNotFound(response);
            }
            catch (Exception ex)
            {
                SetErrorObject(objBase, ex, "Error in UploadDocument");
            }
            return OkOrNotFound(objBase);
        }

        [HttpPost]
        [Route("API/Esign/SendDocument")]
        public async Task<IHttpActionResult> SendDocument([FromBody] SendDocumentRequest request)
        {
            try
            {
                if (request == null || request.Recipients == null || request.Recipients.Count == 0)
                {
                    objBase.Message = "At least one recipient is required.";
                    objBase.Code = CommanUtilities.Models.ProcessStatus.Fail;
                    objBase.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
                    return OkOrNotFound(objBase);
                }

                var sentBy = User?.Identity?.Name ?? "unknown";
                await BuildEsignService().SendDocumentAsync(request, sentBy);
                return OkWithBoolSuccessStatus(true, "Document sent for signature.");
            }
            catch (InvalidOperationException ex)
            {
                objBase.Message = ex.Message;
                objBase.Code = CommanUtilities.Models.ProcessStatus.Fail;
                objBase.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
            }
            catch (Exception ex)
            {
                SetErrorObject(objBase, ex, "Error in SendDocument");
            }
            return OkOrNotFound(objBase);
        }

        [HttpGet]
        [Route("API/Esign/GetDocument/{id}")]
        public async Task<IHttpActionResult> GetDocument(int id)
        {
            try
            {
                var result = await BuildEsignService().GetDocumentAsync(id);
                return OkOrNotFound(result);
            }
            catch (InvalidOperationException ex)
            {
                objBase.Message = ex.Message;
                objBase.Code = CommanUtilities.Models.ProcessStatus.Fail;
                objBase.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
            }
            catch (Exception ex)
            {
                SetErrorObject(objBase, ex, "Error in GetDocument");
            }
            return OkOrNotFound(objBase);
        }

        [HttpGet]
        [Route("API/Esign/GetForSigner/{accessToken}")]
        public async Task<IHttpActionResult> GetForSigner(Guid accessToken)
        {
            try
            {
                var result = await BuildEsignService().GetDocumentForSignerAsync(accessToken);
                return OkOrNotFound(result);
            }
            catch (InvalidOperationException ex)
            {
                objBase.Message = ex.Message;
                objBase.Code = CommanUtilities.Models.ProcessStatus.Fail;
                objBase.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
            }
            catch (Exception ex)
            {
                SetErrorObject(objBase, ex, "Error in GetForSigner");
            }
            return OkOrNotFound(objBase);
        }

        [HttpPost]
        [Route("API/Esign/Sign")]
        public async Task<IHttpActionResult> Sign([FromBody] SignDocumentRequest request)
        {
            try
            {
                await BuildEsignService().SignAsync(request, GetClientIp());
                return OkWithBoolSuccessStatus(true, "Document signed.");
            }
            catch (InvalidOperationException ex)
            {
                objBase.Message = ex.Message;
                objBase.Code = CommanUtilities.Models.ProcessStatus.Fail;
                objBase.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
            }
            catch (Exception ex)
            {
                SetErrorObject(objBase, ex, "Error in Sign");
            }
            return OkOrNotFound(objBase);
        }

        [HttpPost]
        [Route("API/Esign/Reject")]
        public async Task<IHttpActionResult> Reject([FromBody] RejectDocumentRequest request)
        {
            try
            {
                await BuildEsignService().RejectAsync(request, GetClientIp());
                return OkWithBoolSuccessStatus(true, "Document rejected.");
            }
            catch (InvalidOperationException ex)
            {
                objBase.Message = ex.Message;
                objBase.Code = CommanUtilities.Models.ProcessStatus.Fail;
                objBase.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
            }
            catch (Exception ex)
            {
                SetErrorObject(objBase, ex, "Error in Reject");
            }
            return OkOrNotFound(objBase);
        }

        [HttpGet]
        [Route("API/Esign/MyPending")]
        public async Task<IHttpActionResult> MyPending(string email)
        {
            try
            {
                var emaild = User?.Identity?.Name;
                var result = await BuildEsignService().GetMyPendingDocumentsAsync(email);
                return OkOrNotFound(result);
            }
            catch (Exception ex) { SetErrorObject(objBase, ex, "Error in MyPending"); }
            return OkOrNotFound(objBase);
        }

        [HttpGet]
        [Route("API/Esign/MyDocuments")]
        public async Task<IHttpActionResult> MyDocuments()
        {
            try
            {
                var result = await BuildEsignService().GetMyDocumentsAsync(User?.Identity?.Name);
                return OkOrNotFound(result);
            }
            catch (Exception ex) { SetErrorObject(objBase, ex, "Error in MyDocuments"); }
            return OkOrNotFound(objBase);
        }

        [HttpGet]
        [Route("API/Esign/GetForLoggedInSigner")]
        public async Task<IHttpActionResult> GetForLoggedInSigner(int documentId, string email)
        {
            try
            {
                var result = await BuildEsignService().GetDocumentForLoggedInSignerAsync(documentId, email);
                return OkOrNotFound(result);
            }
            catch (InvalidOperationException ex)
            {
                objBase.Message = ex.Message;
                objBase.Code = CommanUtilities.Models.ProcessStatus.Fail;
                objBase.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
            }
            catch (Exception ex) { SetErrorObject(objBase, ex, "Error in GetForLoggedInSigner"); }
            return OkOrNotFound(objBase);
        }

        [HttpPost]
        [Route("API/Esign/SignAsUser")]
        public async Task<IHttpActionResult> SignAsUser([FromBody] SignAsUserRequest request)
        {
            try
            {
                await BuildEsignService().SignAsLoggedInUserAsync(request.DocumentId, request.email, request.FieldValues, GetClientIp());
                return OkWithBoolSuccessStatus(true, "Document signed.");
            }
            catch (InvalidOperationException ex)
            {
                objBase.Message = ex.Message;
                objBase.Code = CommanUtilities.Models.ProcessStatus.Fail;
                objBase.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
            }
            catch (Exception ex) { SetErrorObject(objBase, ex, "Error in SignAsUser"); }
            return OkOrNotFound(objBase);
        }

        [HttpDelete]
        [Route("API/Esign/DeleteDocument/{id}")]
        public async Task<IHttpActionResult> DeleteDocument(int id)
        {
            try
            {
                await BuildEsignService().DeleteDocumentAsync(id, User?.Identity?.Name);
                return OkWithBoolSuccessStatus(true, "Document deleted.");
            }
            catch (Exception ex) { SetErrorObject(objBase, ex, "Error in DeleteDocument"); }
            return OkOrNotFound(objBase);
        }
    }
}