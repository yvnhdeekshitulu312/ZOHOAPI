using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ALHMobileAppAPI.Esign.DTOs;
using ALHMobileAppAPI.Esign.Models;

namespace ALHMobileAppAPI.Esign.Services
{
    public interface IEsignService
    {
        Task SignAsLoggedInUserAsync(int documentId, string email, List<FieldValueDto> fieldValues, string ipAddress);
        Task<DocumentDetailResponse> GetDocumentForLoggedInSignerAsync(int documentId, string email);
        Task<UploadDocumentResponse> UploadDocumentAsync(Stream fileStream, string fileName, string contentType, string uploadedBy);
        Task SendDocumentAsync(SendDocumentRequest request, string sentBy);
        Task<DocumentDetailResponse> GetDocumentAsync(int documentId);
        Task<DocumentDetailResponse> GetDocumentForSignerAsync(Guid accessToken);
        Task SignAsync(SignDocumentRequest request, string ipAddress);
        Task RejectAsync(RejectDocumentRequest request, string ipAddress);
        Task<List<DocumentDetailResponse>> GetMyPendingDocumentsAsync(string userEmail);
        Task<List<DocumentDetailResponse>> GetMyDocumentsAsync(string userEmail);
        Task DeleteDocumentAsync(int documentId, string requestedBy);
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
                Status = doc.Status.ToString(),
                IsOrdered = doc.IsOrdered,
                ViewerGcsUrl = viewerUrl,
                PageImages = pageImages,
                Recipients = recipients.Select(r => new RecipientSummaryDto
                {
                    Id = r.Id,
                    Name = r.Name,
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

        public async Task<List<DocumentDetailResponse>> GetMyPendingDocumentsAsync(string userEmail)
        {
            var docs = await _repo.GetPendingDocumentsForRecipientAsync(userEmail);
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

        public async Task<List<DocumentDetailResponse>> GetMyDocumentsAsync(string userEmail)
        {
            var docs = await _repo.GetDocumentsCreatedByAsync(userEmail);
            var result = new List<DocumentDetailResponse>();
            foreach (var doc in docs)
                result.Add(await BuildDetailResponseAsync(doc, includePageImages: false));
            return result;
        }

        public async Task<UploadDocumentResponse> UploadDocumentAsync(
            Stream fileStream, string fileName, string contentType, string uploadedBy)
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
                CreatedOn = DateTime.UtcNow
            };

            doc.Id = await _repo.CreateDocumentAsync(doc);

            await _repo.LogAuditAsync(new EsignAuditLog
            {
                DocumentId = doc.Id,
                Action = "Created",
                Timestamp = DateTime.UtcNow,
                Details = $"Uploaded by {uploadedBy}"
            });

            return new UploadDocumentResponse
            {
                DocumentId = doc.Id,
                Name = doc.Name,
                OriginalGcsPath = doc.OriginalGcsPath
            };
        }

        public async Task SendDocumentAsync(SendDocumentRequest request, string sentBy)
        {
            var doc = await _repo.GetDocumentAsync(request.DocumentId);
            if (doc == null) throw new InvalidOperationException("Document not found.");

            doc.Name = request.DocumentName ?? doc.Name;
            doc.IsOrdered = request.IsOrdered;
            doc.DaysToComplete = request.DaysToComplete;
            doc.ReminderDays = request.ReminderDays;
            doc.Note = request.Note;
            doc.Status = DocumentStatus.Pending;
            doc.SentOn = DateTime.UtcNow;

            var recipientEntities = request.Recipients.Select((r, idx) => new EsignRecipient
            {
                DocumentId = doc.Id,
                Email = r.Email,
                Name = r.Name,
                Role = (RecipientRole)Enum.Parse(typeof(RecipientRole), r.Role),
                SigningOrder = request.IsOrdered ? (r.SigningOrder ?? idx + 1) : (int?)null,
                Status = RecipientStatus.Pending,
                DeliveryMethod = r.DeliveryMethod ?? "Email",
                AccessToken = Guid.NewGuid()
            }).ToList();

            var savedRecipients = await _repo.AddRecipientsAsync(doc.Id, recipientEntities);

            var clientIdToRecipientId = request.Recipients
                .Select((r, idx) => new { r.ClientId, RecipientId = savedRecipients[idx].Id })
                .ToDictionary(x => x.ClientId, x => x.RecipientId);

            var fieldEntities = request.Fields.Select(f => new EsignField
            {
                DocumentId = doc.Id,
                RecipientId = clientIdToRecipientId[f.RecipientClientId],
                FieldType = (FieldType)Enum.Parse(typeof(FieldType), f.FieldType),
                PageNumber = f.PageNumber,
                XPct = f.XPct,
                YPct = f.YPct,
                WidthPct = f.WidthPct,
                HeightPct = f.HeightPct,
                IsRequired = f.IsRequired
            }).ToList();

            await _repo.AddFieldsAsync(doc.Id, fieldEntities);
            await _repo.UpdateDocumentAsync(doc);

            await _repo.LogAuditAsync(new EsignAuditLog
            {
                DocumentId = doc.Id,
                Action = "Sent",
                Timestamp = DateTime.UtcNow,
                Details = $"Sent by {sentBy} to {savedRecipients.Count} recipient(s), ordered={request.IsOrdered}"
            });

            var toNotify = request.IsOrdered
                ? savedRecipients.Where(r => r.SigningOrder == 1)
                : savedRecipients;

            foreach (var recipient in toNotify)
            {
                var link = _signingBaseUrl + recipient.AccessToken;
                await _notifier.NotifyRecipientAsync(recipient, doc, link);
                recipient.Status = RecipientStatus.Sent;
                recipient.SentOn = DateTime.UtcNow;
                await _repo.UpdateRecipientAsync(recipient);
            }
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
                recipient.ViewedOn = DateTime.UtcNow;
                await _repo.UpdateRecipientAsync(recipient);
                await _repo.LogAuditAsync(new EsignAuditLog
                {
                    DocumentId = recipient.DocumentId,
                    RecipientId = recipient.Id,
                    Action = "Viewed",
                    Timestamp = DateTime.UtcNow
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
                recipient.ViewedOn = DateTime.UtcNow;
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
                recipient.SignedOn = DateTime.UtcNow;
                await _repo.UpdateRecipientAsync(recipient);

                await _repo.LogAuditAsync(new EsignAuditLog
                {
                    DocumentId = recipient.DocumentId,
                    RecipientId = recipient.Id,
                    Action = "Signed",
                    Timestamp = DateTime.UtcNow,
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
                    doc.CompletedOn = DateTime.UtcNow;
                    await _repo.UpdateDocumentAsync(doc);

                    await _repo.LogAuditAsync(new EsignAuditLog
                    {
                        DocumentId = doc.Id,
                        Action = "Completed",
                        Timestamp = DateTime.UtcNow
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
                            next.SentOn = DateTime.UtcNow;
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
                Timestamp = DateTime.UtcNow,
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
                Timestamp = DateTime.UtcNow,
                Details = $"Deleted by {requestedBy}"
            });
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