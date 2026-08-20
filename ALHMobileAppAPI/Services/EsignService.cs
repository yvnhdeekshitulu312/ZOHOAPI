using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Web;
using ALHMobileAppAPI.Esign.DTOs;
using ALHMobileAppAPI.Esign.Models;
using ALHMobileAppAPI.CommonUtilities; // EmailHelper (adjust if it lives in another namespace)

namespace ALHMobileAppAPI.Esign.Services
{
    public interface IEsignService
    {
        Task SignAsLoggedInUserAsync(int documentId, string email, List<FieldValueDto> fieldValues, string ipAddress);
        Task<DocumentDetailResponse> GetDocumentForLoggedInSignerAsync(int documentId, string email);
        Task<UploadDocumentResponse> UploadDocumentAsync(Stream fileStream, string fileName, string contentType, string uploadedBy,string EmpID);
        Task SendDocumentAsync(SendDocumentRequest request, string sentBy,string EmpID);
        Task<DocumentDetailResponse> GetDocumentAsync(int documentId);
        Task<DocumentDetailResponse> GetDocumentForSignerAsync(Guid accessToken);
        Task SignAsync(SignDocumentRequest request, string ipAddress);
        Task RejectAsync(RejectDocumentRequest request, string ipAddress);
        Task<List<DocumentDetailResponse>> GetMyPendingDocumentsAsync(string userEmail,string EmpID);
        Task<List<DocumentDetailResponse>> GetMyDocumentsAsync(string userEmail,string EmpID, string FromDate, string ToDate);
        Task DeleteDocumentAsync(int documentId, string requestedBy);

        Task DraftdeleteDocument(int documentId, string deletedBy);
    }

    public class EsignService : IEsignService
    {
        private readonly IEsignRepository _repo;
        private readonly IFileStorageService _storage;
        private readonly IPdfStampingService _stamper;
        private readonly IEsignNotificationService _notifier;
        private readonly IPdfRenderingService _renderer;
        private readonly string _signingBaseUrl;

        // Per-recipient sign lock -- prevents two near-simultaneous SignAsUser/Sign
        // calls for the same recipient from both stamping/completing concurrently.
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _signLocks =
            new ConcurrentDictionary<int, SemaphoreSlim>();

        public EsignService(
            IEsignRepository repo,
            IFileStorageService storage,
            IPdfStampingService stamper,
            IEsignNotificationService notifier,
            IPdfRenderingService renderer,
            string signingBaseUrl)
        {
            _repo = repo;
            _storage = storage;
            _stamper = stamper;
            _notifier = notifier;
            _renderer = renderer;
            _signingBaseUrl = signingBaseUrl;
        }

        private async Task<DocumentDetailResponse> BuildDetailResponseAsync(
            EsignDocument doc, int? restrictToRecipientId = null, bool includePageImages = true)
        {
            var recipients = await _repo.GetRecipientsAsync(doc.Id);
            var fields = await _repo.GetFieldsAsync(doc.Id);

            if (restrictToRecipientId.HasValue)
                fields = fields.Where(f => f.RecipientId == restrictToRecipientId.Value).ToList();

            // Defensive de-dupe -- collapses any accidental duplicate rows
            // (e.g. from a past double-submit) so signers never see doubled fields.
            fields = fields
                .GroupBy(f => new { f.RecipientId, f.PageNumber, f.FieldType, f.XPct, f.YPct })
                .Select(g => g.First())
                .ToList();

            var viewerUrl = await _storage.GetViewerUrlAsync(doc.WorkingGcsPath ?? doc.OriginalGcsPath);

            List<string> pageImages = new List<string>();
            if (includePageImages)
            {
                pageImages = (doc.CachedPageImages != null && doc.CachedPageImages.Count > 0)
                    ? doc.CachedPageImages
                    : await RenderAndCacheFallbackAsync(doc); // only hit for pre-existing docs uploaded before caching existed
            }

            return new DocumentDetailResponse
            {
                Id = doc.Id,
                Name = doc.Name,
                CreatedOn = Convert.ToDateTime(doc.CreatedOn).ToString(),
                CreatedBy = doc.CreatedBy,
                Status = doc.Status.ToString(),
                IsOrdered = doc.IsOrdered,
                ViewerGcsUrl = viewerUrl,
                PageImages = pageImages,
                EmpNo = doc.EmpNo,
                FullName = doc.FullName,
                DepartmentName = doc.DepartmentName,


                Recipients = recipients.Select(r => new RecipientSummaryDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    CreateDate = r.CreatedOn,
                    CreatedBy = r.CreatedBy,
                    Email = r.Email,
                    Role = r.Role.ToString(),
                    Status = r.Status.ToString(),
                    SigningOrder = r.SigningOrder
                }).ToList(),
                Fields = fields.Select(f => new FieldSummaryDto
                {
                    Id = f.Id,
                    RecipientId = f.RecipientId,
                    FieldType = f.FieldType.ToString(),
                    PageNumber = f.PageNumber,
                    XPct = f.XPct,
                    YPct = f.YPct,
                    WidthPct = f.WidthPct,
                    HeightPct = f.HeightPct,
                    Value = f.Value,
                    IsRequired = f.IsRequired
                }).ToList()
            };
        }

        private async Task<List<string>> RenderAndCacheFallbackAsync(EsignDocument doc)
        {
            using (var pdfStream = await _storage.DownloadAsync(doc.WorkingGcsPath ?? doc.OriginalGcsPath))
            {
                var images = await _renderer.RenderPagesAsync(pdfStream);
                // backfill the cache so this document doesn't re-render every future request
                doc.CachedPageImages = images;
                await _repo.UpdateDocumentAsync(doc);
                return images;
            }
        }

        public async Task<List<DocumentDetailResponse>> GetMyPendingDocumentsAsync(string userEmail,string EmpID)
        {
            var docs = await _repo.GetPendingDocumentsForRecipientAsync(userEmail, EmpID);
            var result = new List<DocumentDetailResponse>();
            foreach (var doc in docs)
            {
                var recipients = await _repo.GetRecipientsAsync(doc.Id);
                var me = recipients.First(r => r.Email.Equals(userEmail, StringComparison.OrdinalIgnoreCase));
                // includePageImages: false -- list view doesn't render pages, keeps payload small
                result.Add(await BuildDetailResponseAsync(doc, restrictToRecipientId: me.Id, includePageImages: false));
            }
            return result;
        }

        public async Task<List<DocumentDetailResponse>> GetMyDocumentsAsync(string userEmail,string EmpID, string FromDate, string ToDate)
        {
            var docs = await _repo.GetDocumentsCreatedByAsync(userEmail, EmpID, FromDate, ToDate);
            var result = new List<DocumentDetailResponse>();
            foreach (var doc in docs)
                result.Add(await BuildDetailResponseAsync(doc, includePageImages: false));
            return result;
        }

        public async Task<UploadDocumentResponse> UploadDocumentAsync(
            Stream fileStream, string fileName, string contentType, string uploadedBy,string EmpID)
        {
            var fileBytes = await ReadAllBytesAsync(fileStream);
            var gcsPath = await _storage.UploadAsync(new MemoryStream(fileBytes), fileName, contentType);

            List<string> pageImages;
            using (var renderStream = new MemoryStream(fileBytes))
                pageImages = await _renderer.RenderPagesAsync(renderStream);

            var doc = new EsignDocument
            {
                Name = fileName,
                OriginalGcsPath = gcsPath,
                WorkingGcsPath = gcsPath,
                CachedPageImages = pageImages,
                Status = DocumentStatus.Draft,
                CreatedBy = uploadedBy,
                EmpID = EmpID,
                CreatedOn = DateTime.Now
            };

            doc.Id = await _repo.CreateDocumentAsync(doc);

            await _repo.LogAuditAsync(new EsignAuditLog
            {
                DocumentId = doc.Id,
                Action = "Created",
                Timestamp = DateTime.Now,
                Details = $"Uploaded by {uploadedBy}"
            });

            return new UploadDocumentResponse
            {
                DocumentId = doc.Id,
                Name = doc.Name,
                OriginalGcsPath = doc.OriginalGcsPath
            };
        }

        public async Task SendDocumentAsync(SendDocumentRequest request, string sentBy,string EmpID)
        {
            var doc = await _repo.GetDocumentAsync(request.DocumentId);
            if (doc == null) throw new InvalidOperationException("Document not found.");

            doc.Name = request.DocumentName ?? doc.Name;
            doc.IsOrdered = request.IsOrdered;
            doc.DaysToComplete = request.DaysToComplete;
            doc.ReminderDays = request.ReminderDays;
            doc.Note = request.Note;
            doc.Status = DocumentStatus.Pending;
            //doc.SentOn = DateTime.UtcNow;
            doc.SentOn = DateTime.Now;

            var recipientEntities = request.Recipients.Select((r, idx) => new EsignRecipient
            {
                DocumentId = doc.Id,
                Email = r.Email,
                EmpID = r.EmpID,
                Name = r.Name,
                Role = (RecipientRole)Enum.Parse(typeof(RecipientRole), r.Role),
                SigningOrder = request.IsOrdered ? (r.SigningOrder ?? idx + 1) : (int?)null,
                Status = RecipientStatus.Pending,
                DeliveryMethod = r.DeliveryMethod ?? "Email",
                AccessToken = Guid.NewGuid()
            }).ToList();

            var savedRecipients = await _repo.AddRecipientsAsync(doc.Id, recipientEntities);

            // Map each client-side ClientId -> the DB id of its saved recipient.
            // Correlate by a stable natural key (email), NOT by list position:
            // AddRecipientsAsync does not guarantee it returns rows in the order they
            // were passed, so an index-based pairing silently binds fields to the wrong
            // recipient the moment there is more than one recipient.
            var reqRecipients = request.Recipients.ToList();
            var clientIdToRecipientId = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < reqRecipients.Count; i++)
            {
                var req = reqRecipients[i];
                if (string.IsNullOrWhiteSpace(req.ClientId)) continue;

                var saved = savedRecipients.FirstOrDefault(s =>
                                !string.IsNullOrEmpty(s.Email) &&
                                s.Email.Equals(req.Email, StringComparison.OrdinalIgnoreCase))
                            ?? (i < savedRecipients.Count ? savedRecipients[i] : null);

                if (saved != null)
                    clientIdToRecipientId[req.ClientId.Trim()] = saved.Id;
            }

            //var fieldEntities = request.Fields.Select(f => new EsignField
            //{
            //    DocumentId = doc.Id,
            //    RecipientId = clientIdToRecipientId[f.RecipientClientId],
            //    FieldType = (FieldType)Enum.Parse(typeof(FieldType), f.FieldType),
            //    PageNumber = f.PageNumber,
            //    XPct = f.XPct,
            //    YPct = f.YPct,
            //    WidthPct = f.WidthPct,
            //    HeightPct = f.HeightPct,
            //    IsRequired = f.IsRequired
            //}).ToList();


            // Only used to rescue single-recipient docs whose fields arrive unlabelled.
            var singleRecipientId = savedRecipients.Count == 1 ? savedRecipients[0].Id : (int?)null;

            var fieldEntities = request.Fields.Select(f =>
            {
                var clientId = f.RecipientClientId?.Trim();
                int recipientId = 0;
                bool resolved = false;

                // 1) Exact match on the client id the recipient was actually sent with.
                if (!string.IsNullOrEmpty(clientId) &&
                    clientIdToRecipientId.TryGetValue(clientId, out recipientId))
                {
                    resolved = true;
                }

                // 2) Prefix fallback: "r1_..." -> 1st recipient, "r2_..." -> 2nd, etc.
                if (!resolved && !string.IsNullOrEmpty(clientId))
                {
                    var prefix = clientId.Split('_')[0];              // e.g. "r1"
                    if (int.TryParse(prefix.TrimStart('r', 'R'), out var oneBased))
                    {
                        var index = oneBased - 1;                     // 1-based -> 0-based
                        if (index >= 0 && index < savedRecipients.Count)
                        {
                            recipientId = savedRecipients[index].Id;
                            resolved = true;
                        }
                    }
                }

                // 3) Single-recipient document: an unlabelled field can only belong to
                //    that lone recipient, so bind it rather than dropping it to id 0.
                if (!resolved && singleRecipientId.HasValue)
                {
                    recipientId = singleRecipientId.Value;
                    resolved = true;
                }

                // 4) Genuinely ambiguous -> fail loudly. Writing RecipientId = 0 (the old
                //    behaviour) persists an invalid FK and silently breaks signing/stamping.
                if (!resolved)
                {
                    throw new InvalidOperationException(
                        $"Could not bind a field to a recipient. RecipientClientId='{f.RecipientClientId}'. " +
                        $"Known client ids: [{string.Join(", ", clientIdToRecipientId.Keys)}]. " +
                        "Check that the client sends each field's RecipientClientId matching a recipient's ClientId.");
                }

                return new EsignField
                {
                    DocumentId = doc.Id,
                    RecipientId = recipientId,
                    FieldType = Enum.TryParse<FieldType>(f.FieldType, true, out var fieldType) ? fieldType : default,
                    PageNumber = f.PageNumber,
                    XPct = f.XPct,
                    YPct = f.YPct,
                    WidthPct = f.WidthPct,
                    HeightPct = f.HeightPct,
                    IsRequired = f.IsRequired
                };
            }).ToList();





            await _repo.AddFieldsAsync(doc.Id, fieldEntities);
            await _repo.UpdateDocumentAsync(doc);

            await _repo.LogAuditAsync(new EsignAuditLog
            {
                DocumentId = doc.Id,
                Action = "Sent",
                Timestamp = DateTime.Now,
                Details = $"Sent by {sentBy} to {savedRecipients.Count} recipient(s), ordered={request.IsOrdered}"
            });

            var toNotify = (request.IsOrdered
                ? savedRecipients.Where(r => r.SigningOrder == 1)
                : savedRecipients).ToList();

            foreach (var recipient in toNotify)
            {
                // Email is now sent by SendSignatureEmailsAsync (branded HTML via EmailHelper)
                // below, so the notifier's email call is disabled here to avoid sending twice.
                // Re-enable these two lines if IEsignNotificationService also drives other
                // channels (Slack / Teams / internal) that you still want fired at this point.
                // var link = _signingBaseUrl + recipient.AccessToken;
                // await _notifier.NotifyRecipientAsync(recipient, doc, link);
                recipient.Status = RecipientStatus.Sent;
                recipient.SentOn = DateTime.Now;
                await _repo.UpdateRecipientAsync(recipient);
            }

            // Send branded signature-request emails AFTER a successful save. Mirrors
            // SignatureService.SendSignatureEmailsAsync: resilient per recipient, and an
            // email failure must NOT turn a successful send into a failure. Ordered-vs-all
            // is already decided by `toNotify`.
            try
            {
                await SendSignatureEmailsAsync(doc, toNotify, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(
                    ex, "EsignService", "Error sending signature emails", "");
            }
        }

        // =========================================================
        // Send branded signature-request emails
        // Ported from SignatureService.SendSignatureEmailsAsync, adapted to the typed
        // EsignRecipient model (no reflection GetProp/ParseInt needed here).
        //
        // Requester       -> doc.CreatedBy (the original uploader of the document).
        // Expires on      -> doc.SentOn + doc.DaysToComplete, when both are set
        //                    (both are nullable, so the row/line is simply omitted
        //                    when either is missing).
        // Message to all  -> doc.Note (shown as an em dash when blank).
        // Private message -> not modeled on EsignDocument/EsignRecipient yet, so this
        //                    always shows as an em dash until a field is added -- see
        //                    the TODO below.
        // =========================================================
        private async Task SendSignatureEmailsAsync(
            EsignDocument doc,
            IEnumerable<EsignRecipient> recipients,
            CancellationToken cancellationToken)
        {
            if (doc == null || recipients == null) return;

            var targets = recipients
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Email))
                .ToList();
            if (targets.Count == 0) return;

            string baseUrl = ConfigurationManager.AppSettings["EsignAppBaseUrl"] ?? string.Empty;
            string contactEmail = ConfigurationManager.AppSettings["EsignContactEmail"] ?? string.Empty;
            string organizationName = ConfigurationManager.AppSettings["EsignOrganizationName"] ?? "Al Hammadi Hospitals";
            string docName = string.IsNullOrWhiteSpace(doc.Name) ? "a document" : doc.Name;

            string requestedBy =
                string.IsNullOrWhiteSpace(doc.CreatedBy)
                    ? "A colleague at " + organizationName
                    : doc.CreatedBy;

            string expiresOnText = null;

            if (doc.SentOn.HasValue && doc.DaysToComplete.HasValue)
            {
                expiresOnText =
                    doc.SentOn.Value
                        .AddDays(doc.DaysToComplete.Value)
                        .ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
            }

            string messageToAll = doc.Note;

            // TODO: once a per-document "private message" field exists (e.g. on
            // EsignDocument or per-recipient on EsignRecipient), read it here.
            // Until then this always renders as an em dash.
            string privateMessage = null;

            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string subject = "Signature requested: " + docName;
                string body = BuildEmailBody(
                    target.Name,
                    docName,
                    baseUrl,
                    requestedBy,
                    organizationName,
                    expiresOnText,
                    contactEmail,
                    messageToAll,
                    privateMessage);

                try
                {
                    bool sent = await EmailHelper
                        .SendEmailAsync(target.Email, target.Name, subject, body, cancellationToken)
                        .ConfigureAwait(false);

                    if (!sent)
                    {
                        HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(
                            new Exception("EmailHelper returned false."),
                            "EsignService", "Email was not sent", target.Email);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Bubble cancellation up so the caller logs it once.
                    throw;
                }
                catch (Exception ex)
                {
                    // One recipient failing must not prevent the next from being processed.
                    HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(
                        ex, "EsignService", "Error sending email to " + target.Email, "");
                }
            }
        }

        // =========================================================
        // Email body -- "Brand Card" design
        //
        // Table-based HTML (works in Outlook desktop, Outlook Web,
        // Gmail, Apple Mail) matching the approved mockup's
        // "S · Brand Card" variation. Kept identical to
        // SignatureService.BuildEmailBody so both send paths
        // produce the same branded email. Uses only approved brand
        // colors and Noto Kufi Arabic (falls back to Arial where
        // the font isn't available on the recipient's device, since
        // custom fonts are not reliably loaded by all email
        // clients). The header-band gradient degrades gracefully to
        // a flat Brand Blue background on clients (e.g. Outlook
        // desktop) that ignore CSS background-image.
        //
        // The logo is referenced as cid:{EmailHelper.LogoContentId}
        // -- EmailHelper.SendEmailAsync attaches the actual image
        // file as an inline resource with that Content-Id.
        // =========================================================

        private static string BuildEmailBody(
            string name,
            string docName,
            string baseUrl,
            string requestedBy,
            string organizationName,
            string expiresOnText,
            string contactEmail,
            string messageToAll,
            string privateMessage)
        {
            string safeDocName =
                HttpUtility.HtmlEncode(docName);

            string safeRequestedBy =
                HttpUtility.HtmlEncode(requestedBy);

            string safeOrganization =
                HttpUtility.HtmlEncode(organizationName);

            string signLink =
                string.IsNullOrEmpty(baseUrl)
                    ? "#"
                    : HttpUtility.HtmlAttributeEncode(baseUrl);

            // -----------------------------------------------------
            // EXPIRES ON row (only rendered when we have a value --
            // unlike Message to all / Private message, which always
            // render with an em dash when empty, this one is a
            // genuinely-not-always-set field)
            // -----------------------------------------------------

            string expiresRow =
                string.IsNullOrWhiteSpace(expiresOnText)
                    ? string.Empty
                    : DetailRow("&#9201;", "EXPIRES ON", HttpUtility.HtmlEncode(expiresOnText), false);

            string expiresFooterLine =
                string.IsNullOrWhiteSpace(expiresOnText)
                    ? string.Empty
                    :
                    "<div style=\"font-size:12px;color:#969696;margin-top:10px;\">" +
                    "This link expires on " +
                    HttpUtility.HtmlEncode(expiresOnText) +
                    "</div>";

            // -----------------------------------------------------
            // Footer contact line
            // -----------------------------------------------------

            string contactLine =
                string.IsNullOrWhiteSpace(contactEmail)
                    ?
                    "This is an automated email from the " +
                    safeOrganization +
                    " Signature Portal."
                    :
                    "This is an automated email from the " +
                    safeOrganization +
                    " Signature Portal. For queries, contact " +
                    "<a href=\"mailto:" +
                    HttpUtility.HtmlAttributeEncode(contactEmail) +
                    "\" style=\"color:#1855A4;text-decoration:none;\">" +
                    HttpUtility.HtmlEncode(contactEmail) +
                    "</a> directly.";

            return
$@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#E5E5E5;padding:24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""560"" cellpadding=""0"" cellspacing=""0"" style=""background:#FFFFFF;border-radius:16px;overflow:hidden;font-family:'Noto Kufi Arabic',Arial,sans-serif;"">

        <!-- Header / logo -->
        <tr>
          <td style=""padding:26px 32px;border-bottom:1px solid #E5E5E5;"">
            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"">
              <tr>
                <td style=""width:40px;height:40px;background:#1855A4;border-radius:10px;text-align:center;vertical-align:middle;"">
                  <img src=""cid:{EmailHelper.LogoContentId}"" width=""40"" height=""40"" alt=""{safeOrganization}"" style=""display:block;border-radius:10px;border:0;"" />
                </td>
                <td style=""padding-left:12px;"">
                  <div style=""font-weight:800;font-size:15px;color:#002654;"">{safeOrganization}</div>
                  <div style=""font-size:10.5px;color:#969696;font-weight:600;"">Internal Signature Portal</div>
                </td>
              </tr>
            </table>
          </td>
        </tr>

        <!-- Banner (gradient with flat-color fallback for clients that ignore background-image) -->
        <tr>
          <td style=""background-color:#1855A4;background-image:linear-gradient(135deg,#002654,#1855A4);padding:30px 32px;"">
            <span style=""display:inline-block;background:#CDFCFB;color:#002654;font-size:10.5px;font-weight:800;letter-spacing:.06em;text-transform:uppercase;padding:5px 12px;border-radius:100px;"">Action required</span>
            <div style=""color:#FFFFFF;font-size:23px;font-weight:800;margin-top:12px;font-family:'Noto Kufi Arabic',Arial,sans-serif;"">Digital Signature Request</div>
          </td>
        </tr>

        <!-- Request line + detail rows -->
        <tr>
          <td style=""padding:30px 32px 6px 32px;"">
            <div style=""font-size:14px;color:#002654;line-height:1.7;font-family:'Noto Kufi Arabic',Arial,sans-serif;margin-bottom:10px;"">
              {(string.IsNullOrWhiteSpace(name) ? "Hello," : "Dear " + HttpUtility.HtmlEncode(name) + ",")}<br/>
              <b style=""color:#1855A4;"">{safeRequestedBy}</b> has requested you to review and sign <b style=""color:#1855A4;"">{safeDocName}</b>.
            </div>

            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
              {DetailRow("&#9998;", "SENDER", safeRequestedBy, false)}
              {DetailRow("&#127970;", "ORGANIZATION", safeOrganization, false)}
              {expiresRow}
              {DetailRow("&#128172;", "MESSAGE TO ALL", EmptyOrDash(messageToAll), false)}
              {DetailRow("&#128274;", "PRIVATE MESSAGE", EmptyOrDash(privateMessage), true)}
            </table>
          </td>
        </tr>

        <!-- Start Signing button -->
        <tr>
          <td align=""center"" style=""padding:8px 32px 30px 32px;"">
            <a href=""{signLink}"" style=""background:#1855A4;color:#FFFFFF;text-decoration:none;padding:15px 38px;border-radius:11px;font-weight:800;font-size:14.5px;font-family:'Noto Kufi Arabic',Arial,sans-serif;display:inline-block;"">Start Signing</a>
            {expiresFooterLine}
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style=""background:#E5E5E5;padding:22px 32px;font-size:11px;color:#969696;line-height:1.7;font-family:'Noto Kufi Arabic',Arial,sans-serif;"">
            {contactLine}
          </td>
        </tr>

      </table>
    </td>
  </tr>
</table>";
        }

        // =========================================================
        // Single detail row (icon + label + value), matching the
        // Sender / Organization / Expires on / Message to all /
        // Private message rows in the approved mockup. `noBorder`
        // is set for the last row in the stack.
        // =========================================================

        private static string DetailRow(
            string iconHtmlEntity,
            string label,
            string value,
            bool noBorder)
        {
            string borderStyle =
                noBorder
                    ? string.Empty
                    : "border-bottom:1px solid #E5E5E5;";

            return
                "<tr><td style=\"padding:13px 0;" + borderStyle + "\">" +
                "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tr>" +
                "<td style=\"width:32px;height:32px;background:#CDFCFB;color:#002654;border-radius:9px;text-align:center;vertical-align:middle;font-size:14px;line-height:32px;\">" +
                iconHtmlEntity +
                "</td>" +
                "<td style=\"padding-left:14px;\">" +
                "<div style=\"font-size:11px;font-weight:700;color:#969696;text-transform:uppercase;letter-spacing:.04em;font-family:'Noto Kufi Arabic',Arial,sans-serif;\">" +
                HttpUtility.HtmlEncode(label) +
                "</div>" +
                "<div style=\"font-size:13.5px;font-weight:600;color:#002654;font-family:'Noto Kufi Arabic',Arial,sans-serif;\">" +
                value +
                "</div>" +
                "</td>" +
                "</tr></table>" +
                "</td></tr>";
        }

        // =========================================================
        // Renders an em dash for a missing value, HTML-encodes a
        // real one. Used for Message to all / Private message,
        // which the design always shows (never omits the row).
        // =========================================================

        private static string EmptyOrDash(string value)
        {
            return
                string.IsNullOrWhiteSpace(value)
                    ? "&mdash;"
                    : HttpUtility.HtmlEncode(value);
        }

        public async Task<DocumentDetailResponse> GetDocumentAsync(int documentId)
        {
            var doc = await _repo.GetDocumentAsync(documentId);
            if (doc == null) throw new InvalidOperationException("Document not found.");
            return await BuildDetailResponseAsync(doc);
        }

        public async Task<DocumentDetailResponse> GetDocumentForSignerAsync(Guid accessToken)
        {
            var recipient = await _repo.GetRecipientByTokenAsync(accessToken);
            if (recipient == null) throw new InvalidOperationException("Invalid or expired signing link.");

            if (recipient.Status == RecipientStatus.Sent)
            {
                recipient.Status = RecipientStatus.Viewed;
                recipient.ViewedOn = DateTime.Now;
                await _repo.UpdateRecipientAsync(recipient);
                await _repo.LogAuditAsync(new EsignAuditLog
                {
                    DocumentId = recipient.DocumentId,
                    RecipientId = recipient.Id,
                    Action = "Viewed",
                    Timestamp = DateTime.Now
                });
            }

            var doc = await _repo.GetDocumentAsync(recipient.DocumentId);
            return await BuildDetailResponseAsync(doc, restrictToRecipientId: recipient.Id);
        }

        public async Task<DocumentDetailResponse> GetDocumentForLoggedInSignerAsync(int documentId, string email)
        {
            var recipient = await _repo.GetRecipientByDocumentAndEmailAsync(documentId, email);
            if (recipient == null) throw new InvalidOperationException("You are not a recipient on this document.");

            if (recipient.Status == RecipientStatus.Sent)
            {
                recipient.Status = RecipientStatus.Viewed;
                recipient.ViewedOn = DateTime.Now;
                await _repo.UpdateRecipientAsync(recipient);
            }

            var doc = await _repo.GetDocumentAsync(documentId);
            return await BuildDetailResponseAsync(doc, restrictToRecipientId: recipient.Id);
        }

        public async Task SignAsync(SignDocumentRequest request, string ipAddress)
        {
            var recipient = await _repo.GetRecipientByTokenAsync(request.AccessToken);
            if (recipient == null) throw new InvalidOperationException("Invalid or expired signing link.");
            await SignInternalAsync(recipient, request.FieldValues, ipAddress);
        }

        public async Task SignAsLoggedInUserAsync(int documentId, string email, List<FieldValueDto> fieldValues, string ipAddress)
        {
            var recipient = await _repo.GetRecipientByDocumentAndEmailAsync(documentId, email);
            if (recipient == null) throw new InvalidOperationException("You are not a recipient on this document.");
            await SignInternalAsync(recipient, fieldValues, ipAddress);
        }

        private async Task SignInternalAsync(EsignRecipient recipient, List<FieldValueDto> fieldValues, string ipAddress)
        {
            var gate = _signLocks.GetOrAdd(recipient.Id, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync();
            try
            {
                // Re-check status inside the lock in case a concurrent call already signed
                var current = await _repo.GetRecipientByDocumentAndEmailAsync(recipient.DocumentId, recipient.Email);
                if (current != null && current.Status == RecipientStatus.Signed)
                    throw new InvalidOperationException("This recipient has already signed.");

                foreach (var fv in fieldValues)
                    await _repo.UpdateFieldValueAsync(fv.FieldId, fv.Value);

                recipient.Status = RecipientStatus.Signed;
                recipient.SignedOn = DateTime.Now;
                await _repo.UpdateRecipientAsync(recipient);

                await _repo.LogAuditAsync(new EsignAuditLog
                {
                    DocumentId = recipient.DocumentId,
                    RecipientId = recipient.Id,
                    Action = "Signed",
                    Timestamp = DateTime.Now,
                    IpAddress = ipAddress
                });

                var doc = await _repo.GetDocumentAsync(recipient.DocumentId);
                var allRecipients = await _repo.GetRecipientsAsync(doc.Id);
                var allSigners = allRecipients.Where(r => r.Role == RecipientRole.Sign || r.Role == RecipientRole.Approve).ToList();
                var stillPending = allSigners.Where(r => r.Status != RecipientStatus.Signed).ToList();

                if (!stillPending.Any())
                {
                    // Everyone's done -- stamp ONCE, from the ORIGINAL clean PDF, using every field's saved value
                    var allFields = await _repo.GetFieldsAsync(doc.Id);
                    var stampInputs = allFields.Select(f => new FieldStampInput
                    {
                        FieldId = f.Id,
                        FieldType = f.FieldType,
                        PageNumber = f.PageNumber,
                        XPct = f.XPct,
                        YPct = f.YPct,
                        WidthPct = f.WidthPct,
                        HeightPct = f.HeightPct,
                        Value = f.Value
                    }).ToList();

                    using (var sourceStream = await _storage.DownloadAsync(doc.OriginalGcsPath))
                    {
                        var stampedBytes = await _stamper.StampAsync(sourceStream, allFields, stampInputs);
                        using (var stampedStream = new MemoryStream(stampedBytes))
                            doc.FinalGcsPath = doc.WorkingGcsPath =
                                await _storage.UploadAsync(stampedStream, $"{doc.Id}_final.pdf", "application/pdf");
                    }

                    doc.Status = DocumentStatus.Completed;
                    doc.CompletedOn = DateTime.Now;
                    await _repo.UpdateDocumentAsync(doc);

                    await _repo.LogAuditAsync(new EsignAuditLog
                    {
                        DocumentId = doc.Id,
                        Action = "Completed",
                        Timestamp = DateTime.Now
                    });

                    await _notifier.NotifyDocumentCompletedAsync(doc);
                }
                else
                {
                    doc.Status = DocumentStatus.PartiallySigned;
                    await _repo.UpdateDocumentAsync(doc);

                    if (doc.IsOrdered)
                    {
                        var next = stillPending.OrderBy(r => r.SigningOrder).First();
                        if (next.SigningOrder == recipient.SigningOrder + 1)
                        {
                            await _notifier.NotifyRecipientAsync(next, doc, _signingBaseUrl + next.AccessToken);
                            next.Status = RecipientStatus.Sent;
                            next.SentOn = DateTime.Now;
                            await _repo.UpdateRecipientAsync(next);
                        }
                    }
                }
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task RejectAsync(RejectDocumentRequest request, string ipAddress)
        {
            var recipient = await _repo.GetRecipientByTokenAsync(request.AccessToken);
            if (recipient == null) throw new InvalidOperationException("Invalid or expired signing link.");

            recipient.Status = RecipientStatus.Rejected;
            recipient.RejectReason = request.Reason;
            await _repo.UpdateRecipientAsync(recipient);

            var doc = await _repo.GetDocumentAsync(recipient.DocumentId);
            doc.Status = DocumentStatus.Rejected;
            await _repo.UpdateDocumentAsync(doc);

            await _repo.LogAuditAsync(new EsignAuditLog
            {
                DocumentId = doc.Id,
                RecipientId = recipient.Id,
                Action = "Rejected",
                Timestamp = DateTime.Now,
                IpAddress = ipAddress,
                Details = request.Reason
            });

            await _notifier.NotifyDocumentRejectedAsync(doc, recipient);
        }

        public async Task DeleteDocumentAsync(int documentId, string requestedBy)
        {
            await _repo.DeleteDocumentAsync(documentId);
            await _repo.LogAuditAsync(new EsignAuditLog
            {
                DocumentId = documentId,
                Action = "Deleted",
                Timestamp = DateTime.Now,
                Details = $"Deleted by {requestedBy}"
            });
        }

        public async Task DraftdeleteDocument(int documentId, string requestedBy)
        {
            var doc = await _repo.GetDocumentAsync(documentId);
            if (doc == null)
                throw new InvalidOperationException($"Document {documentId} was not found (or already deleted).");

            if (doc.Status != DocumentStatus.Draft)
                throw new InvalidOperationException(
                    $"Document {documentId} is '{doc.Status}', not Draft. Use DeleteDocumentAsync (soft delete) " +
                    "for documents that have already been sent, so their audit trail is preserved.");

            await _repo.DraftdeleteDocument(documentId);

            HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(
                null, "EsignService", $"Draft document {documentId} permanently deleted by {requestedBy}", "");
        }




        private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
        {
            using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer);
                return buffer.ToArray();
            }
        }
    }
}