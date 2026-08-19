using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ALHMobileAppAPI.Esign.Models;

namespace ALHMobileAppAPI.Esign.Services
{
    public interface IEsignRepository
    {
        Task<int> CreateDocumentAsync(EsignDocument document);
        Task<EsignDocument> GetDocumentAsync(int documentId);
        Task UpdateDocumentAsync(EsignDocument document);
        Task DeleteDocumentAsync(int documentId);
        Task<List<EsignRecipient>> AddRecipientsAsync(int documentId, List<EsignRecipient> recipients);
        Task<List<EsignRecipient>> GetRecipientsAsync(int documentId);
        Task<EsignRecipient> GetRecipientByTokenAsync(Guid accessToken);
        Task UpdateRecipientAsync(EsignRecipient recipient);
        Task AddFieldsAsync(int documentId, List<EsignField> fields);
        Task<List<EsignField>> GetFieldsAsync(int documentId);
        Task<List<EsignField>> GetFieldsForRecipientAsync(int recipientId);
        Task UpdateFieldValueAsync(int fieldId, string value);
        Task LogAuditAsync(EsignAuditLog entry);
        Task<List<EsignDocument>> GetPendingDocumentsForRecipientAsync(string email, string EmpID);
        Task<List<EsignDocument>> GetDocumentsCreatedByAsync(string userEmail,string EmpID);
        Task<EsignRecipient> GetRecipientByDocumentAndEmailAsync(int documentId, string email);

        Task DraftdeleteDocument(int documentId);


    }
}