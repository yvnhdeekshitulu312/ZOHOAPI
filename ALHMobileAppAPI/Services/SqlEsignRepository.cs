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

        public async Task<int> CreateDocumentAsync(EsignDocument d)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = new SqlCommand(@"
                    INSERT INTO EsignDocuments (Name, OriginalGcsPath, WorkingGcsPath, Status, CreatedBy, CreatedOn, IsOrdered, CachedPageImagesJson,SavedEmpID)
                    OUTPUT INSERTED.Id
                    VALUES (@Name, @OriginalGcsPath, @WorkingGcsPath, @Status, @CreatedBy, @CreatedOn, @IsOrdered, @CachedPageImagesJson,@EmpID)", c))
                {
                    cmd.Parameters.AddWithValue("@Name", d.Name);
                    cmd.Parameters.AddWithValue("@OriginalGcsPath", d.OriginalGcsPath);
                    cmd.Parameters.AddWithValue("@WorkingGcsPath", (object)d.WorkingGcsPath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", d.Status.ToString());
                    cmd.Parameters.AddWithValue("@CreatedBy", d.CreatedBy);
                    cmd.Parameters.AddWithValue("@CreatedOn", d.CreatedOn);
                    cmd.Parameters.AddWithValue("@IsOrdered", d.IsOrdered);
                    cmd.Parameters.AddWithValue("@CachedPageImagesJson",
                        d.CachedPageImages != null && d.CachedPageImages.Count > 0
                            ? (object)JsonConvert.SerializeObject(d.CachedPageImages)
                            : DBNull.Value);
                    cmd.Parameters.AddWithValue("@EmpID", d.EmpID);

                    var id = (int)await cmd.ExecuteScalarAsync();
                    d.Id = id;
                    return id;
                }
            }
        }

        public async Task<EsignDocument> GetDocumentAsync(int documentId)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                EsignDocument doc = null;
                using (var cmd = new SqlCommand("SELECT * FROM EsignDocuments WHERE Id=@id AND IsDeleted=0", c))
                {
                    cmd.Parameters.AddWithValue("@id", documentId);
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
                using (var cmd = new SqlCommand(@"
                    UPDATE EsignDocuments SET
                        Name=@Name, WorkingGcsPath=@WorkingGcsPath, FinalGcsPath=@FinalGcsPath,
                        Status=@Status, SentOn=@SentOn, CompletedOn=@CompletedOn,
                        DaysToComplete=@DaysToComplete, ReminderDays=@ReminderDays, Note=@Note,
                        IsOrdered=@IsOrdered, CachedPageImagesJson=@CachedPageImagesJson
                    WHERE Id=@Id", c))
                {
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
                    cmd.Parameters.AddWithValue("@CachedPageImagesJson",
                        d.CachedPageImages != null && d.CachedPageImages.Count > 0
                            ? (object)JsonConvert.SerializeObject(d.CachedPageImages)
                            : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Id", d.Id);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task DeleteDocumentAsync(int documentId)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = new SqlCommand("UPDATE EsignDocuments SET IsDeleted=1 WHERE Id=@id", c))
                {
                    cmd.Parameters.AddWithValue("@id", documentId);
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
                        using (var cmd = new SqlCommand(@"
                    INSERT INTO EsignRecipients
                    (
                        DocumentId,
                        Email,
                        Name,
                        Role,
                        SigningOrder,
                        Status,
                        DeliveryMethod,
                        AccessToken,
                        signRecipientEmpID
                    )
                    OUTPUT INSERTED.Id
                    VALUES
                    (
                        @DocumentId,
                        @Email,
                        @Name,
                        @Role,
                        @SigningOrder,
                        @Status,
                        @DeliveryMethod,
                        @AccessToken,
                        @signRecipientEmpID
                    )", c))
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
                using (var cmd = new SqlCommand("SELECT * FROM EsignRecipients WHERE DocumentId=@id", c))
                {
                    cmd.Parameters.AddWithValue("@id", documentId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                        while (await reader.ReadAsync())
                            result.Add(MapRecipient(reader));
                }
            }
            return result;
        }

        public async Task<EsignRecipient> GetRecipientByTokenAsync(Guid accessToken)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = new SqlCommand("SELECT * FROM EsignRecipients WHERE AccessToken=@token", c))
                {
                    cmd.Parameters.AddWithValue("@token", accessToken);
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
                using (var cmd = new SqlCommand("SELECT * FROM EsignRecipients WHERE DocumentId=@docId AND Email=@email", c))
                {
                    cmd.Parameters.AddWithValue("@docId", documentId);
                    cmd.Parameters.AddWithValue("@email", email);
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
                using (var cmd = new SqlCommand(@"
                    UPDATE EsignRecipients SET Status=@Status, SentOn=@SentOn, ViewedOn=@ViewedOn,
                        SignedOn=@SignedOn, RejectReason=@RejectReason WHERE Id=@Id", c))
                {
                    cmd.Parameters.AddWithValue("@Status", r.Status.ToString());
                    cmd.Parameters.AddWithValue("@SentOn", (object)r.SentOn ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ViewedOn", (object)r.ViewedOn ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SignedOn", (object)r.SignedOn ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@RejectReason", (object)r.RejectReason ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Id", r.Id);
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
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO EsignFields (DocumentId, RecipientId, FieldType, PageNumber, XPct, YPct, WidthPct, HeightPct, IsRequired)
                        OUTPUT INSERTED.Id
                        VALUES (@DocumentId, @RecipientId, @FieldType, @PageNumber, @XPct, @YPct, @WidthPct, @HeightPct, @IsRequired)", c))
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
                using (var cmd = new SqlCommand("SELECT * FROM EsignFields WHERE DocumentId=@id", c))
                {
                    cmd.Parameters.AddWithValue("@id", documentId);
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
                using (var cmd = new SqlCommand("SELECT * FROM EsignFields WHERE RecipientId=@id", c))
                {
                    cmd.Parameters.AddWithValue("@id", recipientId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                        while (await reader.ReadAsync())
                            result.Add(MapField(reader));
                }
            }
            return result;
        }

        public async Task UpdateFieldValueAsync(int fieldId, string value)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = new SqlCommand("UPDATE EsignFields SET Value=@value, FilledOn=@filledOn WHERE Id=@id", c))
                {
                    cmd.Parameters.AddWithValue("@value", (object)value ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@filledOn", DateTime.Now);
                    cmd.Parameters.AddWithValue("@id", fieldId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task LogAuditAsync(EsignAuditLog e)
        {
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = new SqlCommand(@"
                    INSERT INTO EsignAuditLog (DocumentId, RecipientId, Action, Timestamp, IpAddress, UserAgent, Details)
                    VALUES (@DocumentId, @RecipientId, @Action, @Timestamp, @IpAddress, @UserAgent, @Details)", c))
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

        public async Task<List<EsignDocument>> GetPendingDocumentsForRecipientAsync(string email,string EmpID)
        {
            var result = new List<EsignDocument>();
            //using (var c = Conn())
            //{
            //    await c.OpenAsync();
            //    using (var cmd = new SqlCommand(@"
            //        SELECT DISTINCT d.* FROM EsignDocuments d
            //        JOIN EsignRecipients r ON r.DocumentId = d.Id
            //        WHERE d.IsDeleted=0 AND r.Email=@email AND r.Status IN ('Sent','Viewed')", c))
            //    {
            //        cmd.Parameters.AddWithValue("@email", email);
            //        using (var reader = await cmd.ExecuteReaderAsync())
            //            while (await reader.ReadAsync())
            //                result.Add(MapDocument(reader));
            //    }
            //}
            using (var c = Conn())
            {
                await c.OpenAsync();
                using (var cmd = new SqlCommand(@"
        SELECT DISTINCT 
d.Id, d.Name, d.OriginalGcsPath, d.WorkingGcsPath, d.FinalGcsPath, 
                        d.Status, d.CreatedBy, d.CreatedOn, d.SentOn, d.CompletedOn, 
                        d.DaysToComplete, d.ReminderDays, d.Note, d.IsOrdered, d.IsDeleted, d.SavedEmpID 
        FROM EsignDocuments d
        JOIN EsignRecipients r ON r.DocumentId = d.Id
        WHERE d.IsDeleted = 0 
          AND r.signRecipientEmpID = @empId 
          AND r.Status IN ('Sent', 'Viewed')", c))
                {
                    cmd.Parameters.Add("@empId", SqlDbType.VarChar, 18).Value = (object)EmpID ?? DBNull.Value;

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

        public async Task<List<EsignDocument>> GetDocumentsCreatedByAsync(string userEmail,string EmpID)
        {
            var result = new List<EsignDocument>();
            using (var c = Conn())
            {
                await c.OpenAsync();
               

                string query = @"SELECT Id, Name, OriginalGcsPath, WorkingGcsPath, FinalGcsPath, 
                        Status, CreatedBy, CreatedOn, SentOn, CompletedOn, 
                        DaysToComplete, ReminderDays, Note, IsOrdered, IsDeleted, SavedEmpID 
                        FROM EsignDocuments 
                        WHERE IsDeleted = 0 
                        AND SavedEmpID = @EmpID
                        AND CreatedOn >= @FromDate 
                        AND CreatedOn <= @ToDate";


                using (var cmd = new SqlCommand(query, c))
                {
                    // Use explicit type definition instead of AddWithValue for proper SQL parameter typing
                    cmd.Parameters.Add("@EmpID", SqlDbType.VarChar, 18).Value = EmpID;

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
                IsDeleted = (bool)r["IsDeleted"]
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
                using (var tx = c.BeginTransaction())
                {
                    try
                    {
                        using (var check = new SqlCommand("SELECT COUNT(1) FROM EsignDocuments WHERE Id=@id", c, tx))
                        {
                            check.Parameters.AddWithValue("@id", documentId);
                            var exists = (int)await check.ExecuteScalarAsync();
                            if (exists == 0)
                            {
                                tx.Rollback();
                                throw new InvalidOperationException($"Document {documentId} was not found.");
                            }
                        }

                        // Child rows first (FK dependency) ...
                        using (var delAudit = new SqlCommand("DELETE FROM EsignAuditLog WHERE DocumentId=@id", c, tx))
                        {
                            delAudit.Parameters.AddWithValue("@id", documentId);
                            await delAudit.ExecuteNonQueryAsync();
                        }

                        // ... then the parent row.
                        using (var delDoc = new SqlCommand("DELETE FROM EsignDocuments WHERE Id=@id", c, tx))
                        {
                            delDoc.Parameters.AddWithValue("@id", documentId);
                            await delDoc.ExecuteNonQueryAsync();
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { /* connection already broken -- nothing left to roll back */ }
                        throw;
                    }
                }
            }
        }

    }
}