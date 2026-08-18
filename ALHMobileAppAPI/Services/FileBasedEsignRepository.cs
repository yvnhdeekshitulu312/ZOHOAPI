using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using ALHMobileAppAPI.Esign.Models;

namespace ALHMobileAppAPI.Esign.Services
{
    /// <summary>
    /// Stores ESign data as JSON files under App_Data/EsignData instead of SQL Server.
    /// Same IEsignRepository contract as the SQL version, so switching to SQL later
    /// is purely a Unity registration change -- no controller/service code changes.
    ///
    /// Not meant as a permanent production store for high-concurrency use (file-level
    /// locking serializes all writes), but it's solid for getting the feature working
    /// end-to-end before the DB team runs the schema script.
    /// </summary>
    public class FileBasedEsignRepository : IEsignRepository
    {
        private readonly string _dataFolder;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public FileBasedEsignRepository()
        {
            // App_Data is the conventional "don't serve this over HTTP" folder in
            // a Web API project -- IIS won't serve files from here by default.
            _dataFolder = HttpContext.Current != null
                ? HttpContext.Current.Server.MapPath("~/App_Data/EsignData")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "EsignData");

            if (!Directory.Exists(_dataFolder))
                Directory.CreateDirectory(_dataFolder);
        }

        private string DocumentsPath => Path.Combine(_dataFolder, "documents.json");
        private string RecipientsPath => Path.Combine(_dataFolder, "recipients.json");
        private string FieldsPath => Path.Combine(_dataFolder, "fields.json");
        private string AuditLogPath => Path.Combine(_dataFolder, "auditlog.json");

        // ---------- generic load/save helpers ----------

        private List<T> LoadList<T>(string path)
        {
            if (!File.Exists(path)) return new List<T>();
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return new List<T>();
            return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
        }

        private void SaveList<T>(string path, List<T> items)
        {
            var json = JsonConvert.SerializeObject(items, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        private static int NextId<T>(List<T> items, Func<T, int> idSelector) =>
            items.Count == 0 ? 1 : items.Max(idSelector) + 1;

        // ---------- Documents ----------

        public async Task<int> CreateDocumentAsync(EsignDocument document)
        {
            await _lock.WaitAsync();
            try
            {
                var docs = LoadList<EsignDocument>(DocumentsPath);
                document.Id = NextId(docs, d => d.Id);
                docs.Add(document);
                SaveList(DocumentsPath, docs);
                return document.Id;
            }
            finally { _lock.Release(); }
        }

        public async Task<EsignDocument> GetDocumentAsync(int documentId)
        {
            await _lock.WaitAsync();
            try
            {
                var docs = LoadList<EsignDocument>(DocumentsPath);
                var doc = docs.FirstOrDefault(d => d.Id == documentId && !d.IsDeleted);
                if (doc == null) return null;

                doc.Recipients = LoadList<EsignRecipient>(RecipientsPath)
                    .Where(r => r.DocumentId == documentId).ToList();
                doc.Fields = LoadList<EsignField>(FieldsPath)
                    .Where(f => f.DocumentId == documentId).ToList();
                return doc;
            }
            finally { _lock.Release(); }
        }

        public async Task UpdateDocumentAsync(EsignDocument document)
        {
            await _lock.WaitAsync();
            try
            {
                var docs = LoadList<EsignDocument>(DocumentsPath);
                var idx = docs.FindIndex(d => d.Id == document.Id);
                if (idx < 0) throw new InvalidOperationException($"Document {document.Id} not found.");
                docs[idx] = document;
                SaveList(DocumentsPath, docs);
            }
            finally { _lock.Release(); }
        }

        // ---------- Recipients ----------

        public async Task<List<EsignRecipient>> AddRecipientsAsync(int documentId, List<EsignRecipient> recipients)
        {
            await _lock.WaitAsync();
            try
            {
                var all = LoadList<EsignRecipient>(RecipientsPath);
                var nextId = NextId(all, r => r.Id);
                foreach (var r in recipients)
                {
                    r.Id = nextId++;
                    r.DocumentId = documentId;
                    all.Add(r);
                }
                SaveList(RecipientsPath, all);
                return recipients; // preserves insertion order -- required by EsignService
            }
            finally { _lock.Release(); }
        }

        public async Task<List<EsignRecipient>> GetRecipientsAsync(int documentId)
        {
            await _lock.WaitAsync();
            try
            {
                return LoadList<EsignRecipient>(RecipientsPath)
                    .Where(r => r.DocumentId == documentId).ToList();
            }
            finally { _lock.Release(); }
        }

        public async Task<EsignRecipient> GetRecipientByTokenAsync(Guid accessToken)
        {
            await _lock.WaitAsync();
            try
            {
                return LoadList<EsignRecipient>(RecipientsPath)
                    .FirstOrDefault(r => r.AccessToken == accessToken);
            }
            finally { _lock.Release(); }
        }

        public async Task UpdateRecipientAsync(EsignRecipient recipient)
        {
            await _lock.WaitAsync();
            try
            {
                var all = LoadList<EsignRecipient>(RecipientsPath);
                var idx = all.FindIndex(r => r.Id == recipient.Id);
                if (idx < 0) throw new InvalidOperationException($"Recipient {recipient.Id} not found.");
                all[idx] = recipient;
                SaveList(RecipientsPath, all);
            }
            finally { _lock.Release(); }
        }

        // ---------- Fields ----------

        public async Task AddFieldsAsync(int documentId, List<EsignField> fields)
        {
            await _lock.WaitAsync();
            try
            {
                var all = LoadList<EsignField>(FieldsPath);
                var nextId = NextId(all, f => f.Id);
                foreach (var f in fields)
                {
                    f.Id = nextId++;
                    f.DocumentId = documentId;
                    all.Add(f);
                }
                SaveList(FieldsPath, all);
            }
            finally { _lock.Release(); }
        }

        public async Task<List<EsignField>> GetFieldsAsync(int documentId)
        {
            await _lock.WaitAsync();
            try
            {
                return LoadList<EsignField>(FieldsPath)
                    .Where(f => f.DocumentId == documentId).ToList();
            }
            finally { _lock.Release(); }
        }

        public async Task<List<EsignField>> GetFieldsForRecipientAsync(int recipientId)
        {
            await _lock.WaitAsync();
            try
            {
                return LoadList<EsignField>(FieldsPath)
                    .Where(f => f.RecipientId == recipientId).ToList();
            }
            finally { _lock.Release(); }
        }

        public async Task UpdateFieldValueAsync(int fieldId, string value)
        {
            await _lock.WaitAsync();
            try
            {
                var all = LoadList<EsignField>(FieldsPath);
                var field = all.FirstOrDefault(f => f.Id == fieldId);
                if (field == null) throw new InvalidOperationException($"Field {fieldId} not found.");
                field.Value = value;
                field.FilledOn = DateTime.Now;
                SaveList(FieldsPath, all);
            }
            finally { _lock.Release(); }
        }

        // ---------- Audit log ----------

        public async Task LogAuditAsync(EsignAuditLog entry)
        {
            await _lock.WaitAsync();
            try
            {
                var all = LoadList<EsignAuditLog>(AuditLogPath);
                entry.Id = all.Count == 0 ? 1 : all.Max(a => a.Id) + 1;
                all.Add(entry);
                SaveList(AuditLogPath, all);
            }
            finally { _lock.Release(); }
        }

        public async Task<List<EsignDocument>> GetPendingDocumentsForRecipientAsync(string email, string EmpID)
        {
            List<EsignDocument> docs;
            List<EsignRecipient> recipients;
            await _lock.WaitAsync();
            try
            {
                docs = LoadList<EsignDocument>(DocumentsPath).Where(d => !d.IsDeleted).ToList();
                recipients = LoadList<EsignRecipient>(RecipientsPath);
            }
            finally { _lock.Release(); }

            var myPendingDocIds = new HashSet<int>(recipients
             .Where(r => r.Email.Equals(email, StringComparison.OrdinalIgnoreCase)
                      && (r.Status == RecipientStatus.Sent || r.Status == RecipientStatus.Viewed))
             .Select(r => r.DocumentId));

            return docs.Where(d => myPendingDocIds.Contains(d.Id)).ToList();
        }

        public async Task<List<EsignDocument>> GetDocumentsCreatedByAsync(string email,string EmpID)
        {
            await _lock.WaitAsync();
            try
            {
                return LoadList<EsignDocument>(DocumentsPath)
                    .Where(d => !d.IsDeleted && d.CreatedBy.Equals(email, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            finally { _lock.Release(); }
        }

        public async Task<EsignRecipient> GetRecipientByDocumentAndEmailAsync(int documentId, string email)
        {
            await _lock.WaitAsync();
            try
            {
                return LoadList<EsignRecipient>(RecipientsPath)
                    .FirstOrDefault(r => r.DocumentId == documentId
                        && r.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            }
            finally { _lock.Release(); }
        }

        public async Task DeleteDocumentAsync(int documentId)
        {
            await _lock.WaitAsync();
            try
            {
                var docs = LoadList<EsignDocument>(DocumentsPath);
                var doc = docs.FirstOrDefault(d => d.Id == documentId);
                if (doc == null) throw new InvalidOperationException($"Document {documentId} not found.");
                doc.IsDeleted = true;
                SaveList(DocumentsPath, docs);
            }
            finally { _lock.Release(); }
        }
    }
}
