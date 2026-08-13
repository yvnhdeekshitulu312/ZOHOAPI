using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ALHMobileAppAPI.Esign.Models;

namespace ALHMobileAppAPI.Esign.Services
{
    /// <summary>
    /// Thin wrapper over the GCS file operations you already built for scanned
    /// documents (FileController). Point this at that same implementation --
    /// no new storage code needed, just reuse Upload/Download/GetSignedUrl.
    /// </summary>
    public interface IFileStorageService
    {
        Task<string> UploadAsync(Stream fileStream, string fileName, string contentType);
        Task<Stream> DownloadAsync(string gcsPath);
        Task<string> GetViewerUrlAsync(string gcsPath); // same blob-URL pattern used for the PDF iframe viewer
        Task DeleteAsync(string gcsPath);
    }

    /// <summary>
    /// Draws field values (signature images, stamps, text) onto the PDF at the
    /// stored X/Y percentages. Implementation lives in PdfStampingService.
    /// </summary>
    public interface IPdfStampingService
    {
        Task<byte[]> StampAsync(Stream sourcePdf, IList<EsignField> fields, IList<FieldStampInput> values);
    }

    public class FieldStampInput
    {
        public int FieldId { get; set; }
        public FieldType FieldType { get; set; }
        public int PageNumber { get; set; }
        public decimal XPct { get; set; }
        public decimal YPct { get; set; }
        public decimal WidthPct { get; set; }
        public decimal HeightPct { get; set; }
        /// Base64 PNG for Signature/Stamp, plain text for Text/Date/Checkbox
        public string Value { get; set; }
    }

    /// <summary>
    /// Sends the "you have a document to sign" / "reminder" / "completed" pings.
    /// Backed by an n8n webhook, consistent with your existing n8n workflows
    /// (radiology report routing, doctor notes) rather than raw SMTP.
    /// </summary>
    public interface IEsignNotificationService
    {
        Task NotifyRecipientAsync(EsignRecipient recipient, EsignDocument document, string signingLink);
        Task NotifyReminderAsync(EsignRecipient recipient, EsignDocument document, string signingLink);
        Task NotifyDocumentCompletedAsync(EsignDocument document);
        Task NotifyDocumentRejectedAsync(EsignDocument document, EsignRecipient rejectedBy);
    }
}
