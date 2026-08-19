using Microsoft.AspNetCore.Mvc;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace YourApp.Esign
{
    // ---- Repository ---------------------------------------------------------
    public interface IEsignRepository
    {
        Task<bool> DeleteDocumentAsync(int documentId, string deletedBy);
        // ...your existing methods (UploadDocument, etc.) stay as they are.
    }

    public class EsignRepository : IEsignRepository
    {
        //private readonly string _connectionString;

        //public EsignRepository(IConfiguration configuration)
        //{
        //    _connectionString = configuration.GetConnectionString("Default")
        //        ?? throw new InvalidOperationException("Missing 'Default' connection string.");
        //}

        private readonly string _connStr = ConfigurationManager.ConnectionStrings["DBConnectionStringMasters"].ConnectionString;
        private SqlConnection Conn() => new SqlConnection(_connStr);

        //public async Task<bool> DeleteDocumentAsync(int documentId, string deletedBy)
        //{
        //    using var connection = new SqlConnection(_connectionString);

        //    var parameters = new DynamicParameters();
        //    parameters.Add("@DocumentId", documentId, DbType.Int32);
        //    parameters.Add("@DeletedBy", deletedBy, DbType.String);

         
        //    var deleted = await connection.ExecuteScalarAsync<bool>(
        //        "dbo.USP_DeleteEsignDocument",
        //        parameters,
        //        commandType: CommandType.StoredProcedure);

        //    return deleted;
        //}
    }

    // ---- Controller -----------------------------------------------------------
    [ApiController]
    [Route("api/[controller]")]
    public class EsignController : ControllerBase
    {
        private readonly IEsignRepository _esignRepository;

        public EsignController(IEsignRepository esignRepository)
        {
            _esignRepository = esignRepository;
        }       
        [HttpDelete("documents/{documentId:int}")]
        public async Task<IActionResult> DeleteDocument(int documentId)
        {
           
            var deletedBy = User?.Identity?.Name ?? "unknown";
            var deleted = await _esignRepository.DeleteDocumentAsync(documentId, deletedBy);
            if (!deleted)
            {
                return NotFound(new { message = $"Document {documentId} was not found." });
            }
            return NoContent(); 
        }     
    }
}
