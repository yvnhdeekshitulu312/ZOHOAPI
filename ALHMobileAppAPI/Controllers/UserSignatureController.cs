using HISDataAccess;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Http;
using static ALHMobileAppAPI.Esign.Controllers.UserSignatureRepository;

namespace ALHMobileAppAPI.Esign.Controllers
{


    /// <summary>Self-signature profile payload (base64 data URLs for the images).</summary>
    public class UserSignatureDto
    {
        public int UserId { get; set; }
        public string SignatureBase64 { get; set; }
        public string InitialBase64 { get; set; }
        public string StampBase64 { get; set; }
        public string DateFormat { get; set; }
        public string TimeZone { get; set; }
    }

    /// <summary>Thin ADO.NET data layer over the two stored procedures.</summary>
    public class UserSignatureRepository
    {
        // TODO: point this at your real connection-string name in Web.config
        // <connectionStrings>. Falls back to an AppSetting of the same name.

        static string MODULE_NAME = "WebAPIDAL";
        const int DEFAULTWORKSTATION = 0;
        static String strConnString = ConfigurationManager.ConnectionStrings["DBConnectionStringMasters"].ConnectionString;
        static String strDefWorkstationId = ConfigurationManager.AppSettings["DefaultWorkstationId"].ToString();
        static String strDefaultUserId = ConfigurationManager.AppSettings["DefaultUserId"].ToString();
        static String strDefaultHospitalId = ConfigurationManager.AppSettings["DefaultHospitalId"].ToString();
        SqlConnection conn = null;// new SqlConnection(strConnString);    

        internal enum Database
        {
            Master = 1,
            Transaction = 2
        }
        private IDbDataParameter CreateParam(DataHelper objDataHelper, string paramName, object paramVal, DbType paramType, ParameterDirection paramDirection)
        {
            IDbDataParameter objIDbDataParameter = objDataHelper.CreateDataParameter();
            objIDbDataParameter.ParameterName = paramName;
            objIDbDataParameter.Value = paramVal;
            objIDbDataParameter.DbType = paramType;
            objIDbDataParameter.Direction = paramDirection;

            return objIDbDataParameter;
        }

        private static string ConnString
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings["ALHDB"];
                return cs != null ? cs.ConnectionString : ConfigurationManager.AppSettings["DBConnectionStringMasters"];
            }
        }

        //public void Save(UserSignatureDto dto)
        //{
        //    using (var cn = new SqlConnection(ConnString))
        //    using (var cmd = new SqlCommand("dbo.usp_Esign_SaveUserSignature", cn))
        //    {
        //        cmd.CommandType = CommandType.StoredProcedure;
        //        cmd.Parameters.AddWithValue("@UserId", dto.UserId);
        //        cmd.Parameters.AddWithValue("@SignatureImage", (object)dto.SignatureBase64 ?? DBNull.Value);
        //        cmd.Parameters.AddWithValue("@InitialImage",   (object)dto.InitialBase64  ?? DBNull.Value);
        //        cmd.Parameters.AddWithValue("@StampImage",     (object)dto.StampBase64    ?? DBNull.Value);
        //        cmd.Parameters.AddWithValue("@DateFormat",     (object)dto.DateFormat     ?? DBNull.Value);
        //        cmd.Parameters.AddWithValue("@TimeZone",       (object)dto.TimeZone       ?? DBNull.Value);
        //        cn.Open();
        //        cmd.ExecuteNonQuery();
        //    }
        //}
        public class UserSignatureResponseDto
        {
            public CommanUtilities.Models.ProcessStatus Code { get; set; }
            public string Status { get; set; }
            public string Message { get; set; }
            public string Message2L { get; set; }

            // Property required to fix CS1061
            public UserSignatureDto Data { get; set; }
        }

        public UserSignatureResponseDto Save(UserSignatureDto dto)
        {
            UserSignatureResponseDto objSave = new UserSignatureResponseDto();
            DataHelper objDataHelper = new DataHelper(DEFAULTWORKSTATION, (int)Database.Master);

            try
            {
                List<IDbDataParameter> objIDbDataParameters = new List<IDbDataParameter>();

                objIDbDataParameters.Add(CreateParam(objDataHelper, "@UserId", dto.UserId, DbType.Int32, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@SignatureImage", (object)dto.SignatureBase64 ?? DBNull.Value, DbType.String, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@InitialImage", (object)dto.InitialBase64 ?? DBNull.Value, DbType.String, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@StampImage", (object)dto.StampBase64 ?? DBNull.Value, DbType.String, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@DateFormat", (object)dto.DateFormat ?? DBNull.Value, DbType.String, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@TimeZone", (object)dto.TimeZone ?? DBNull.Value, DbType.String, ParameterDirection.Input));

                int intRes = objDataHelper.RunSP("dbo.usp_Esign_SaveUserSignature", objIDbDataParameters.ToArray());

                if (intRes == -1 || intRes > 0)
                {
                    objSave.Code = CommanUtilities.Models.ProcessStatus.Success;
                    objSave.Status = CommanUtilities.Models.ProcessStatus.Success.ToString();
                    objSave.Message = "User Signature Saved Successfully";
                }
                else
                {
                    objSave.Code = CommanUtilities.Models.ProcessStatus.Fail;
                    objSave.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
                    objSave.Message = "Error occurred while saving signature";
                }

                return objSave;
            }
            catch (Exception ex)
            {
                objSave.Code = CommanUtilities.Models.ProcessStatus.Fail;
                objSave.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
                objSave.Message = ex.Message;
                HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(ex, MODULE_NAME, "Error in Save", "");
            }
            finally
            {
                objDataHelper = null;
            }

            return objSave;
        }

        public UserSignatureResponseDto Get(int userId, int hospitalId = 0)
        {
            DataHelper objDataHelper = new DataHelper(DEFAULTWORKSTATION, (int)Database.Master, hospitalId);
            UserSignatureResponseDto objResponse = new UserSignatureResponseDto();

            try
            {
                List<IDbDataParameter> objIDbDataParameters = new List<IDbDataParameter>();
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@UserId", userId, DbType.Int32, ParameterDirection.Input));

                using (DataSet dsSignature = objDataHelper.RunSPReturnDS("dbo.usp_Esign_GetUserSignature", objIDbDataParameters.ToArray()))
                {
                    if (dsSignature != null && dsSignature.Tables.Count > 0 && dsSignature.Tables[0].Rows.Count > 0)
                    {
                        DataRow dr = dsSignature.Tables[0].Rows[0];

                        objResponse.Data = new UserSignatureDto
                        {
                            UserId = userId,
                            SignatureBase64 = dr["SignatureImage"] != DBNull.Value ? dr["SignatureImage"].ToString() : null,
                            InitialBase64 = dr["InitialImage"] != DBNull.Value ? dr["InitialImage"].ToString() : null,
                            StampBase64 = dr["StampImage"] != DBNull.Value ? dr["StampImage"].ToString() : null,
                            DateFormat = dr["DateFormat"] != DBNull.Value ? dr["DateFormat"].ToString() : null,
                            TimeZone = dr["TimeZone"] != DBNull.Value ? dr["TimeZone"].ToString() : null
                        };

                        objResponse.Code = CommanUtilities.Models.ProcessStatus.Success;
                        objResponse.Status = CommanUtilities.Models.ProcessStatus.Success.ToString();
                        objResponse.Message = "User signature fetched successfully.";
                    }
                    else
                    {
                        objResponse.Code = CommanUtilities.Models.ProcessStatus.Success;
                        objResponse.Status = CommanUtilities.Models.ProcessStatus.Success.ToString();
                        objResponse.Message = "No signature record found.";
                    }
                }

                return objResponse;
            }
            catch (Exception ex)
            {
                objResponse.Code = CommanUtilities.Models.ProcessStatus.Fail;
                objResponse.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
                objResponse.Message = ex.Message;
                HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(ex, MODULE_NAME, "Error in Get User Signature", userId.ToString());
            }
            finally
            {
                objDataHelper = null;
            }

            return objResponse;
        }

        //public UserSignatureDto Get(int userId)
        //{
        //    using (var cn = new SqlConnection(ConnString))
        //    using (var cmd = new SqlCommand("dbo.usp_Esign_GetUserSignature", cn))
        //    {
        //        cmd.CommandType = CommandType.StoredProcedure;
        //        cmd.Parameters.AddWithValue("@UserId", userId);
        //        cn.Open();
        //        using (var r = cmd.ExecuteReader())
        //        {
        //            if (!r.Read()) { return null; }
        //            return new UserSignatureDto
        //            {
        //                UserId = userId,
        //                SignatureBase64 = r["SignatureImage"] as string,
        //                InitialBase64 = r["InitialImage"] as string,
        //                StampBase64 = r["StampImage"] as string,
        //                DateFormat = r["DateFormat"] as string,
        //                TimeZone = r["TimeZone"] as string
        //            };
        //        }
        //    }
        //}
    }

    [RoutePrefix("API/Esign")]
    public class UserSignatureController : ApiController
    {
        private readonly UserSignatureRepository _repo = new UserSignatureRepository();

        // GET /API/Esign/GetUserSignature?userId=123
        //[HttpGet, Route("GetUserSignature")]
        [HttpGet]
        [Route("GetUserSignature")]
        public IHttpActionResult GetUserSignature(int userId)
        {
            if (userId <= 0) { return BadRequest("userId is required."); }
            try
            {
                var dto = _repo.Get(userId);

                // If repo returned null or no Data payload was populated, construct a fallback response wrapper
                if (dto == null || dto.Data == null)
                {
                    dto = new UserSignatureResponseDto
                    {
                        Code = CommanUtilities.Models.ProcessStatus.Success,
                        Status = CommanUtilities.Models.ProcessStatus.Success.ToString(),
                        Message = "No signature record found.",
                        Data = new UserSignatureDto { UserId = userId }
                    };
                }

                return Ok(dto);
            }
            catch (Exception ex)
            {
                HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(ex, "UserSignatureController", "GetUserSignature", userId.ToString());
                return InternalServerError();
            }
        }
        //public IHttpActionResult GetUserSignature(int userId)
        //{
        //    if (userId <= 0) { return BadRequest("userId is required."); }
        //    try
        //    {
        //        var dto = _repo.Get(userId);
        //        // Return an empty shell (not 404) so the profile page binds cleanly first time.
        //        return Ok(dto ?? new UserSignatureDto { UserId = userId });
        //    }
        //    catch (Exception ex)
        //    {
        //        HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(ex, "UserSignatureController", "GetUserSignature", userId.ToString());
        //        return InternalServerError();
        //    }
        //}

        // POST /API/Esign/SaveUserSignature   body: UserSignatureDto
        //[HttpPost, Route("SaveUserSignature")]
        [HttpPost]
        [Route("SaveUserSignature")]
        public IHttpActionResult SaveUserSignature([FromBody] UserSignatureDto dto)
        {
            if (dto == null || dto.UserId <= 0) { return BadRequest("A valid UserId is required."); }

            try
            {
                UserSignatureResponseDto result = _repo.Save(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(ex, "UserSignatureController", "SaveUserSignature", dto.UserId.ToString());
                return InternalServerError();
            }
        }
        //public IHttpActionResult SaveUserSignature([FromBody] UserSignatureDto dto)
        //{
        //    if (dto == null || dto.UserId <= 0) { return BadRequest("A valid UserId is required."); }

        //    // NOTE: for production, prefer deriving UserId from the authenticated
        //    // token/principal rather than trusting the request body, so one user
        //    // cannot overwrite another user's signature.
        //    try
        //    {
        //        _repo.Save(dto);
        //        return Ok(new { Status = "Success" });
        //    }
        //    catch (Exception ex)
        //    {
        //        HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(ex, "UserSignatureController", "SaveUserSignature", dto.UserId.ToString());
        //        return InternalServerError();
        //    }
        //}
    }
}
