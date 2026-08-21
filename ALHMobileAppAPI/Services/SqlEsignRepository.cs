using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ALHMobileAppAPI.Esign.Models;
using HISDataAccess;

namespace ALHMobileAppAPI.Esign.Services
{
    public class SqlEsignRepository : IEsignRepository
    {
        static string MODULE_NAME = "WebAPIDAL";
        const int DEFAULTWORKSTATION = 0;
        static String strConnString = ConfigurationManager.ConnectionStrings["DBConnectionStringMasters"].ConnectionString;
        static String strDefWorkstationId = ConfigurationManager.AppSettings["DefaultWorkstationId"].ToString();
        static String strDefaultUserId = ConfigurationManager.AppSettings["DefaultUserId"].ToString();
        static String strDefaultHospitalId = ConfigurationManager.AppSettings["DefaultHospitalId"].ToString();

        private IDbDataParameter CreateParam(DataHelper objDataHelper, string paramName, object paramVal, DbType paramType, ParameterDirection paramDirection)
        {
            IDbDataParameter objIDbDataParameter = objDataHelper.CreateDataParameter();
            objIDbDataParameter.ParameterName = paramName;
            objIDbDataParameter.Value = paramVal;
            objIDbDataParameter.DbType = paramType;
            objIDbDataParameter.Direction = paramDirection;

            return objIDbDataParameter;
        }

        private readonly string _connStr = ConfigurationManager.ConnectionStrings["DBConnectionStringMasters"].ConnectionString;
        private SqlConnection Conn() => new SqlConnection(_connStr);

        // Every data access method below now calls a stored procedure (see
        // Esign_StoredProcedures.sql) instead of an inline SQL string. This
        // helper just saves repeating "CommandType = CommandType.StoredProcedure"
        // at every call site.
        private static SqlCommand SP(string procName, SqlConnection c) =>
            new SqlCommand(procName, c) { CommandType = CommandType.StoredProcedure };

        public async Task<int> CreateDocumentAsync(EsignDocument d)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_CreateDocument", c))
                {
                    cmd.Parameters.AddWithValue("@Name", d.Name);
                    cmd.Parameters.AddWithValue("@OriginalGcsPath", d.OriginalGcsPath);
                    cmd.Parameters.AddWithValue("@WorkingGcsPath", (object)d.WorkingGcsPath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", d.Status.ToString());
                    cmd.Parameters.AddWithValue("@CreatedBy", d.CreatedBy);
                    cmd.Parameters.AddWithValue("@CreatedOn", d.CreatedOn);
                    cmd.Parameters.AddWithValue("@IsOrdered", d.IsOrdered);
                    //cmd.Parameters.AddWithValue("@CachedPageImagesJson",
                    //    d.CachedPageImages != null && d.CachedPageImages.Count > 0
                    //        ? (object)JsonConvert.SerializeObject(d.CachedPageImages)
                    //        : DBNull.Value);
                    cmd.Parameters.AddWithValue("@CachedPageImagesJson", DBNull.Value);                    
                    cmd.Parameters.AddWithValue("@EmpID", (object)d.EmpID ?? DBNull.Value);

                    // usp_Esign_CreateDocument now generates the HAMS-prefixed
                    // DocumentNumber server-side and returns it alongside the new
                    // Id (OUTPUT INSERTED.Id, INSERTED.DocumentNumber), so this
                    // reads a single-row result set instead of ExecuteScalar.
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                            throw new InvalidOperationException("usp_Esign_CreateDocument did not return the new document's Id/DocumentNumber.");

                        d.Id = (int)reader["Id"];
                        d.DocumentNumber = reader["DocumentNumber"] as string;
                    }

                    return d.Id;
                }
            }
        }


        public async Task LogAuditAsync(EsignAuditLog e)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_LogAudit", c))
                {
                    cmd.Parameters.AddWithValue("@DocumentId", e.DocumentId);
                    cmd.Parameters.AddWithValue("@RecipientId", (object)e.RecipientId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Action", e.Action);
                    cmd.Parameters.AddWithValue("@Timestamp", e.Timestamp);
                    cmd.Parameters.AddWithValue("@IpAddress", (object)e.IpAddress ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@UserAgent", (object)e.UserAgent ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Details", (object)e.Details ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }



        // NOTE: this used to run "SELECT * FROM EsignDocuments WHERE Id=@id AND
        // IsDeleted=0" directly and hand the reader to MapDocument -- which also
        // reads EmpNo/FullName/DepartmentName. Those three columns only exist via
        // the V_EmployeesDetails join used in GetDocumentAsync below, not on
        // EsignDocuments itself, so this method would throw the moment MapDocument
        // touched them. It isn't part of IEsignRepository (FileBasedEsignRepository
        // has no equivalent) and nothing in the provided code calls it, so rather
        // than port a query guaranteed to fail, this now just delegates to the
        // already-correct query. Safe to delete entirely if nothing references it.
        public async Task<EsignDocument> GetDocumentAsyncOld(int documentId)
        {
            return await GetDocumentAsync(documentId);
        }

        public async Task<EsignDocument> GetDocumentAsync(int documentId)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                EsignDocument doc = null;

                using (var cmd = SP("usp_Esign_GetDocumentById", c))
                {
                    cmd.Parameters.AddWithValue("@Id", documentId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                            doc = MapDocument(reader);
                    }
                }

                if (doc == null) return null;

                doc.Recipients = await GetRecipientsAsync(documentId);
                doc.Fields = await GetFieldsAsync(documentId);
                return doc;
            }
        }

        public async Task UpdateDocumentAsync(EsignDocument d)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_UpdateDocument", c))
                {
                    cmd.Parameters.AddWithValue("@Id", d.Id);
                    cmd.Parameters.AddWithValue("@Name", d.Name);
                    cmd.Parameters.AddWithValue("@WorkingGcsPath", (object)d.WorkingGcsPath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FinalGcsPath", (object)d.FinalGcsPath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", d.Status.ToString());
                    cmd.Parameters.AddWithValue("@SentOn", (object)d.SentOn ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CompletedOn", (object)d.CompletedOn ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DaysToComplete", (object)d.DaysToComplete ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReminderDays", (object)d.ReminderDays ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Note", (object)d.Note ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsOrdered", d.IsOrdered);
                    //cmd.Parameters.AddWithValue("@CachedPageImagesJson",
                    //    d.CachedPageImages != null && d.CachedPageImages.Count > 0
                    //        ? (object)JsonConvert.SerializeObject(d.CachedPageImages)
                    //        : DBNull.Value);
                    cmd.Parameters.AddWithValue("@CachedPageImagesJson", DBNull.Value);
                  
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task DeleteDocumentAsync(int documentId)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_DeleteDocument", c))
                {
                    cmd.Parameters.AddWithValue("@Id", documentId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<List<EsignRecipient>> AddRecipientsAsync(
        int documentId,
        List<EsignRecipient> recipients)
        {
            try
            {
                using (var c = Conn())
                {
                    await c.OpenAsync();

                    foreach (var r in recipients)
                    {
                        using (var cmd = SP("usp_Esign_AddRecipient", c))
                        {
                            cmd.Parameters.AddWithValue("@DocumentId", documentId);
                            cmd.Parameters.AddWithValue("@Email", r.Email);
                            cmd.Parameters.AddWithValue("@Name", r.Name);
                            cmd.Parameters.AddWithValue("@Role", r.Role.ToString());

                            cmd.Parameters.AddWithValue(
                                "@SigningOrder",
                                (object)r.SigningOrder ?? DBNull.Value
                            );

                            cmd.Parameters.AddWithValue("@Status", r.Status.ToString());

                            cmd.Parameters.AddWithValue(
                                "@DeliveryMethod",
                                (object)r.DeliveryMethod ?? DBNull.Value
                            );

                            cmd.Parameters.AddWithValue(
                                "@AccessToken",
                                (object)r.AccessToken ?? DBNull.Value
                            );

                            cmd.Parameters.AddWithValue(
                                "@signRecipientEmpID",
                                (object)r.EmpID ?? DBNull.Value
                            );

                            r.Id = (int)await cmd.ExecuteScalarAsync();
                            r.DocumentId = documentId;
                        }
                    }

                    return recipients;
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<EsignRecipient>> GetRecipientsAsync(int documentId)
        {
            var result = new List<EsignRecipient>();
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_GetRecipientsByDocument", c))
                {
                    cmd.Parameters.AddWithValue("@DocumentId", documentId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                        while (await reader.ReadAsync())
                            result.Add(MapRecipient(reader));
                }
            }
            return result;
        }

        // Batch version of GetRecipientsAsync -- fetches recipients for MANY documents
        // in one round trip instead of one call per document. Use this from any code
        // path that loops over a list of documents (e.g. EsignService.GetMyDocumentsAsync
        // / GetMyPendingDocumentsAsync) to avoid the N+1 pattern where a 20-document list
        // turned into 20 separate "usp_Esign_GetRecipientsByDocument @DocumentId=..." calls.
        public async Task<Dictionary<int, List<EsignRecipient>>> GetRecipientsForDocumentsAsync(IEnumerable<int> documentIds)
        {
            var ids = (documentIds ?? Enumerable.Empty<int>()).Distinct().ToList();
            var result = ids.ToDictionary(id => id, id => new List<EsignRecipient>());
            if (ids.Count == 0) return result;

            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_GetRecipientsByDocumentIds", c))
                {
                    cmd.Parameters.AddWithValue("@DocumentIds", string.Join(",", ids));
                    using (var reader = await cmd.ExecuteReaderAsync())
                        while (await reader.ReadAsync())
                        {
                            var recipient = MapRecipient(reader);
                            if (!result.TryGetValue(recipient.DocumentId, out var list))
                                result[recipient.DocumentId] = list = new List<EsignRecipient>();
                            list.Add(recipient);
                        }
                }
            }
            return result;
        }

        public async Task<EsignRecipient> GetRecipientByTokenAsync(Guid accessToken)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_GetRecipientByToken", c))
                {
                    cmd.Parameters.AddWithValue("@AccessToken", accessToken);
                    using (var reader = await cmd.ExecuteReaderAsync())
                        return await reader.ReadAsync() ? MapRecipient(reader) : null;
                }
            }
        }


        public async Task<EsignRecipient> GetRecipientByDocumentAndEmailAsync(int documentId, string email)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_GetRecipientByDocumentAndEmail", c))
                {
                    cmd.Parameters.AddWithValue("@DocumentId", documentId);
                    cmd.Parameters.AddWithValue("@Email", email);
                    using (var reader = await cmd.ExecuteReaderAsync())
                        return await reader.ReadAsync() ? MapRecipient(reader) : null;
                }
            }
        }

        public async Task UpdateRecipientAsync(EsignRecipient r)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_UpdateRecipient", c))
                {
                    cmd.Parameters.AddWithValue("@Id", r.Id);
                    cmd.Parameters.AddWithValue("@Status", r.Status.ToString());
                    cmd.Parameters.AddWithValue("@SentOn", (object)r.SentOn ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ViewedOn", (object)r.ViewedOn ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SignedOn", (object)r.SignedOn ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@RejectReason", (object)r.RejectReason ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task AddFieldsAsync(int documentId, List<EsignField> fields)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                foreach (var f in fields)
                {
                    using (var cmd = SP("usp_Esign_AddField", c))
                    {
                        cmd.Parameters.AddWithValue("@DocumentId", documentId);
                        cmd.Parameters.AddWithValue("@RecipientId", f.RecipientId);
                        cmd.Parameters.AddWithValue("@FieldType", f.FieldType.ToString());
                        cmd.Parameters.AddWithValue("@PageNumber", f.PageNumber);
                        cmd.Parameters.AddWithValue("@XPct", f.XPct);
                        cmd.Parameters.AddWithValue("@YPct", f.YPct);
                        cmd.Parameters.AddWithValue("@WidthPct", f.WidthPct);
                        cmd.Parameters.AddWithValue("@HeightPct", f.HeightPct);
                        cmd.Parameters.AddWithValue("@IsRequired", f.IsRequired);

                        f.Id = (int)await cmd.ExecuteScalarAsync();
                        f.DocumentId = documentId;
                    }
                }
            }
        }

        public async Task<List<EsignField>> GetFieldsAsync(int documentId)
        {
            var result = new List<EsignField>();
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_GetFieldsByDocument", c))
                {
                    cmd.Parameters.AddWithValue("@DocumentId", documentId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                        while (await reader.ReadAsync())
                            result.Add(MapField(reader));
                }
            }
            return result;
        }

        public async Task<List<EsignField>> GetFieldsForRecipientAsync(int recipientId)
        {
            var result = new List<EsignField>();
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_GetFieldsByRecipient", c))
                {
                    cmd.Parameters.AddWithValue("@RecipientId", recipientId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                        while (await reader.ReadAsync())
                            result.Add(MapField(reader));
                }
            }
            return result;
        }

        // Batch version of GetFieldsAsync -- same idea as GetRecipientsForDocumentsAsync
        // above: one round trip for a whole list of documents instead of one per document.
        public async Task<Dictionary<int, List<EsignField>>> GetFieldsForDocumentsAsync(IEnumerable<int> documentIds)
        {
            var ids = (documentIds ?? Enumerable.Empty<int>()).Distinct().ToList();
            var result = ids.ToDictionary(id => id, id => new List<EsignField>());
            if (ids.Count == 0) return result;

            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_GetFieldsByDocumentIds", c))
                {
                    cmd.Parameters.AddWithValue("@DocumentIds", string.Join(",", ids));
                    using (var reader = await cmd.ExecuteReaderAsync())
                        while (await reader.ReadAsync())
                        {
                            var field = MapField(reader);
                            if (!result.TryGetValue(field.DocumentId, out var list))
                                result[field.DocumentId] = list = new List<EsignField>();
                            list.Add(field);
                        }
                }
            }
            return result;
        }

        public async Task UpdateFieldValueAsync(int fieldId, string value)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_UpdateFieldValue", c))
                {
                    cmd.Parameters.AddWithValue("@Id", fieldId);
                    cmd.Parameters.AddWithValue("@Value", (object)value ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FilledOn", DateTime.Now);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }



        public async Task<List<EsignDocument>> GetPendingDocumentsForRecipientAsync(string email,string EmpID)
        {
            var result = new List<EsignDocument>();
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_GetPendingDocumentsForRecipient", c))
                {
                    cmd.Parameters.Add("@EmpID", SqlDbType.VarChar, 18).Value = (object)EmpID ?? DBNull.Value;

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(MapDocument(reader));
                        }
                    }
                }
            }

            return result;
        }

        public async Task<List<EsignDocument>> GetDocumentsCreatedByAsync(string userEmail,string EmpID,string FromDate,string ToDate)
        {
            var result = new List<EsignDocument>();

            if (!DateTime.TryParse(FromDate, out DateTime parsedFromDate))
            {
                throw new ArgumentException("Invalid FromDate format.", nameof(FromDate));
            }

            if (!DateTime.TryParse(ToDate, out DateTime parsedToDate))
            {
                throw new ArgumentException("Invalid ToDate format.", nameof(ToDate));
            }

            DateTime startDate = parsedFromDate.Date;
            DateTime endDate = parsedToDate.Date.AddDays(1).AddTicks(-1);


            using (var c = Conn())
            {
                await c.OpenAsync();

                using (var cmd = SP("usp_Esign_GetDocumentsCreatedBy", c))
                {
                    // Use explicit type definition instead of AddWithValue for proper SQL parameter typing
                    cmd.Parameters.Add("@EmpID", SqlDbType.VarChar, 18).Value = (object)EmpID ?? DBNull.Value;
                    cmd.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = startDate;
                    cmd.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = endDate;


                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(MapDocument(reader));
                        }
                    }
                }

            }
            return result;
        }

        // ---------- mapping helpers ----------

        private static EsignDocument MapDocument(SqlDataReader r)
        {
            //var cachedJson = r["CachedPageImagesJson"] as string;
            return new EsignDocument
            {
                Id = (int)r["Id"],
                DocumentNumber = r["DocumentNumber"] as string,
                Name = r["Name"] as string,
                OriginalGcsPath = r["OriginalGcsPath"] as string,
                WorkingGcsPath = r["WorkingGcsPath"] as string,
                FinalGcsPath = r["FinalGcsPath"] as string,
                Status = (DocumentStatus)Enum.Parse(typeof(DocumentStatus), r["Status"] as string),
                CreatedBy = r["CreatedBy"] as string,
                CreatedOn = (DateTime)r["CreatedOn"],
                SentOn = r["SentOn"] as DateTime?,
                CompletedOn = r["CompletedOn"] as DateTime?,
                DaysToComplete = r["DaysToComplete"] as int?,
                ReminderDays = r["ReminderDays"] as int?,
                Note = r["Note"] as string,
                IsOrdered = (bool)r["IsOrdered"],
                IsDeleted = (bool)r["IsDeleted"],

                EmpNo = r["EmpNo"] as string,
                FullName = r["FullName"] as string,
                DepartmentName = r["DepartmentName"] as string,


                //CachedPageImages = string.IsNullOrEmpty(cachedJson)
                //    ? new List<string>()
                //    : JsonConvert.DeserializeObject<List<string>>(cachedJson)
            };
        }

        private static EsignRecipient MapRecipient(SqlDataReader r) => new EsignRecipient
        {
            Id = (int)r["Id"],
            DocumentId = (int)r["DocumentId"],
            Email = r["Email"] as string,
            Name = r["Name"] as string,
            Role = (RecipientRole)Enum.Parse(typeof(RecipientRole), r["Role"] as string),
            SigningOrder = r["SigningOrder"] as int?,
            Status = (RecipientStatus)Enum.Parse(typeof(RecipientStatus), r["Status"] as string),
            DeliveryMethod = r["DeliveryMethod"] as string,
            AccessToken = (Guid)r["AccessToken"],
            SentOn = r["SentOn"] as DateTime?,
            ViewedOn = r["ViewedOn"] as DateTime?,
            SignedOn = r["SignedOn"] as DateTime?,
            RejectReason = r["RejectReason"] as string
        };

        private static EsignField MapField(SqlDataReader r) => new EsignField
        {
            Id = (int)r["Id"],
            DocumentId = (int)r["DocumentId"],
            RecipientId = (int)r["RecipientId"],
            FieldType = (FieldType)Enum.Parse(typeof(FieldType), r["FieldType"] as string),
            PageNumber = (int)r["PageNumber"],
            XPct = (decimal)r["XPct"],
            YPct = (decimal)r["YPct"],
            WidthPct = (decimal)r["WidthPct"],
            HeightPct = (decimal)r["HeightPct"],
            Value = r["Value"] as string,
            IsRequired = (bool)r["IsRequired"],
            FilledOn = r["FilledOn"] as DateTime?
        };


        public async Task DraftdeleteDocument(int documentId)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = SP("usp_Esign_DraftDeleteDocument", c))
                {
                    cmd.Parameters.AddWithValue("@Id", documentId);
                    try
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch (SqlException ex) when (ex.Message.IndexOf("was not found", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // usp_Esign_DraftDeleteDocument RAISERRORs this exact message when the
                        // document doesn't exist (mirrors the old inline-SQL/transaction check).
                        // Translate it back to the same exception type callers already handle.
                        throw new InvalidOperationException($"Document {documentId} was not found.");
                    }
                    // Any other SqlException (deadlock, FK violation, etc.) propagates as-is,
                    // same as before -- the procedure's own TRY/CATCH already rolled back.
                }
            }
        }

    }
}