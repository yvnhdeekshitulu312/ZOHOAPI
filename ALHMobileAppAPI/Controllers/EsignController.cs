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
        // Matches your existing convention (SignatureController) of newing up
        // services per-action rather than constructor injection -- no IoC
        // container is wired up in this project's Startup.cs.
        //private IEsignService BuildEsignService()
        //{
        //    var repository = new FileBasedEsignRepository();
        //    var fileStorage = new LocalDiskFileStorageService();
        //    var stamper = new PdfStampingService();
        //    var notifier = new LoggingEsignNotificationService();

        //    return new EsignService(
        //        repository,
        //        fileStorage,
        //        stamper,
        //        notifier,
        //        signingBaseUrl: "http://localhost:4200/esign/sign/"); // TODO: move to Web.config appSetting
        //}

        private IEsignService BuildEsignService()
        {
            var repository = new FileBasedEsignRepository();
            var fileStorage = new LocalDiskFileStorageService();
            var stamper = new PdfStampingService();
            var notifier = new LoggingEsignNotificationService();
            var renderer = BuildPdfRenderingService(); // already exists in the controller

            return new EsignService(repository, fileStorage, stamper, notifier, renderer, signingBaseUrl: "...");
        }

        private IPdfRenderingService BuildPdfRenderingService() => new AsposePdfRenderingService();

        private string GetClientIp()
        {
            var ip = HttpContext.Current?.Request?.UserHostAddress;
            if (ip == "::1") ip = "127.0.0.1";
            return ip;
        }

        // POST API/Esign/UploadDocument
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

                var esignService = BuildEsignService();
                var renderingService = BuildPdfRenderingService();

                // Read the uploaded file into memory exactly once. ReadAsStreamAsync()
                // returns the SAME underlying stream on repeat calls -- disposing it
                // after one use (via `using`) closes it for the second use too, which
                // is what "Cannot access a closed Stream" was coming from.
                byte[] fileBytes;
                using (var sourceStream = await file.ReadAsStreamAsync())
                using (var buffer = new MemoryStream())
                {
                    await sourceStream.CopyToAsync(buffer);
                    fileBytes = buffer.ToArray();
                }

                System.Collections.Generic.List<string> pageImages;
                using (var renderStream = new MemoryStream(fileBytes))
                {
                    pageImages = await renderingService.RenderPagesAsync(renderStream);
                }

                var uploadedBy = User?.Identity?.Name ?? "unknown";
                UploadDocumentResponse uploadResult;
                using (var uploadStream = new MemoryStream(fileBytes))
                {
                    uploadResult = await esignService.UploadDocumentAsync(uploadStream, fileName, contentType, uploadedBy);
                }

                var response = new
                {
                    uploadResult.DocumentId,
                    uploadResult.Name,
                    uploadResult.OriginalGcsPath,
                    PageImages = pageImages
                };

                return OkOrNotFound(response);
            }
            catch (Exception ex)
            {
                SetErrorObject(objBase, ex, "Error in UploadDocument");
            }
            return OkOrNotFound(objBase);
        }

        // POST API/Esign/SendDocument
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
                var esignService = BuildEsignService();
                await esignService.SendDocumentAsync(request, sentBy);
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

        // GET API/Esign/GetDocument/{id}
        [HttpGet]
        [Route("API/Esign/GetDocument/{id}")]
        public async Task<IHttpActionResult> GetDocument(int id)
        {
            try
            {
                var esignService = BuildEsignService();
                var result = await esignService.GetDocumentAsync(id);
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

        // GET API/Esign/GetForSigner/{accessToken}
        [HttpGet]
        [Route("API/Esign/GetForSigner/{accessToken}")]
        public async Task<IHttpActionResult> GetForSigner(Guid accessToken)
        {
            try
            {
                var esignService = BuildEsignService();
                var result = await esignService.GetDocumentForSignerAsync(accessToken);
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

        // POST API/Esign/Sign
        [HttpPost]
        [Route("API/Esign/Sign")]
        public async Task<IHttpActionResult> Sign([FromBody] SignDocumentRequest request)
        {
            try
            {
                var esignService = BuildEsignService();
                await esignService.SignAsync(request, GetClientIp());
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

        // POST API/Esign/Reject
        [HttpPost]
        [Route("API/Esign/Reject")]
        public async Task<IHttpActionResult> Reject([FromBody] RejectDocumentRequest request)
        {
            try
            {
                var esignService = BuildEsignService();
                await esignService.RejectAsync(request, GetClientIp());
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

        //[HttpGet]
        //[Route("API/Esign/MyPending")]
        //public async Task<IHttpActionResult> MyPending()
        //{
        //    try
        //    {
        //        var esignService = BuildEsignService();
        //        var email =  "dotnetsriharisurendra@gmail.com"; //User?.Identity?.Name ??
        //        var result = await esignService.GetMyPendingDocumentsAsync(email);
        //        return OkOrNotFound(result);
        //    }
        //    catch (Exception ex) { SetErrorObject(objBase, ex, "Error in MyPending"); }
        //    return OkOrNotFound(objBase);
        //}

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


        //// EsignController
        //[HttpGet]
        //[Route("API/Esign/GetForLoggedInSigner/{documentId}")]
        //public async Task<IHttpActionResult> GetForLoggedInSigner(int documentId)
        //{
        //    try
        //    {
        //        var result = await BuildEsignService().GetDocumentForLoggedInSignerAsync(documentId, "dotnetsriharisurendra@gmail.com");
        //        return OkOrNotFound(result);
        //    }
        //    catch (InvalidOperationException ex)
        //    {
        //        objBase.Message = ex.Message;
        //        objBase.Code = CommanUtilities.Models.ProcessStatus.Fail;
        //        objBase.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
        //    }
        //    catch (Exception ex) { SetErrorObject(objBase, ex, "Error in GetForLoggedInSigner"); }
        //    return OkOrNotFound(objBase);
        //}

        //[HttpPost]
        //[Route("API/Esign/SignAsUser")]
        //public async Task<IHttpActionResult> SignAsUser([FromBody] SignAsUserRequest request)
        //{
        //    try
        //    {
        //        await BuildEsignService().SignAsLoggedInUserAsync(request.DocumentId, "dotnetsriharisurendra@gmail.com", request.FieldValues, GetClientIp());
        //        return OkWithBoolSuccessStatus(true, "Document signed.");
        //    }
        //    catch (InvalidOperationException ex)
        //    {
        //        objBase.Message = ex.Message;
        //        objBase.Code = CommanUtilities.Models.ProcessStatus.Fail;
        //        objBase.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
        //    }
        //    catch (Exception ex) { SetErrorObject(objBase, ex, "Error in SignAsUser"); }
        //    return OkOrNotFound(objBase);
        //}

        [HttpGet]
        [Route("API/Esign/MyPending")]
        public async Task<IHttpActionResult> MyPending()
        {
            try
            {
                var email = User?.Identity?.Name;
                var result = await BuildEsignService().GetMyPendingDocumentsAsync(email);
                return OkOrNotFound(result);
            }
            catch (Exception ex) { SetErrorObject(objBase, ex, "Error in MyPending"); }
            return OkOrNotFound(objBase);
        }

        [HttpGet]
        [Route("API/Esign/GetForLoggedInSigner/{documentId}")]
        public async Task<IHttpActionResult> GetForLoggedInSigner(int documentId)
        {
            try
            {
                var result = await BuildEsignService().GetDocumentForLoggedInSignerAsync(documentId, User?.Identity?.Name);
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
                await BuildEsignService().SignAsLoggedInUserAsync(request.DocumentId, User?.Identity?.Name, request.FieldValues, GetClientIp());
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
    }
}
