using System.Diagnostics;
using System.Threading.Tasks;
using ALHMobileAppAPI.Esign.Models;

namespace ALHMobileAppAPI.Esign.Services
{
    /// <summary>
    /// Logs instead of actually sending notifications. Swap for an SMTP or n8n-webhook
    /// backed implementation once you're ready -- IEsignNotificationService is the
    /// only contract EsignService depends on.
    /// </summary>
    public class LoggingEsignNotificationService : IEsignNotificationService
    {
        public Task NotifyRecipientAsync(EsignRecipient recipient, EsignDocument document, string signingLink)
        {
            Trace.TraceInformation($"[ESign] Would notify {recipient.Email} to sign '{document.Name}': {signingLink}");
            return Task.CompletedTask;
        }

        public Task NotifyReminderAsync(EsignRecipient recipient, EsignDocument document, string signingLink)
        {
            Trace.TraceInformation($"[ESign] Would remind {recipient.Email} about '{document.Name}': {signingLink}");
            return Task.CompletedTask;
        }

        public Task NotifyDocumentCompletedAsync(EsignDocument document)
        {
            Trace.TraceInformation($"[ESign] Document '{document.Name}' completed.");
            return Task.CompletedTask;
        }

        public Task NotifyDocumentRejectedAsync(EsignDocument document, EsignRecipient rejectedBy)
        {
            Trace.TraceInformation($"[ESign] Document '{document.Name}' rejected by {rejectedBy.Email}.");
            return Task.CompletedTask;
        }
    }
}
