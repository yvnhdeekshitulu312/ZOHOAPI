using ALHMobileAppAPI.Models;
using HISDataAccess;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using CommanUtilities.Models;
using HIS.GeneralFormValidations;

namespace ALHMobileAppAPI.ALHAppDAL
{
    public class SignatureDAL
    {

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
        public bool CheckValidateUser(string UserName, string Password)
        {
            DataHelper objDataHelper = new DataHelper(0, 1);
            DataSet dsToken = new DataSet();
            bool UserExists;
            try
            {
                List<IDbDataParameter> objIDbDataParameters = new List<IDbDataParameter>();
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@UserName", UserName, DbType.String, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@Password", Password, DbType.String, ParameterDirection.Input));
                dsToken = objDataHelper.RunSPReturnDS("PR_FetchAPIBASICAUTHORIZATIONUSERS_MAPI", objIDbDataParameters.ToArray());
                if (dsToken.Tables[0].Rows.Count > 0)
                    UserExists = true;
                else
                    UserExists = false;
                return UserExists;
            }
            finally
            { objDataHelper = null; }
        }
        public LoginDetails ValidateLoginCredentials(string userName, string password)
        {
            string SWlicenceStatus = string.Empty;
            LoginDetails obj = new LoginDetails();
            DataSet ds = new DataSet(); LoginDetailsOutput objdata = new LoginDetailsOutput();
            DataHelper objDataHelper = new DataHelper(DEFAULTWORKSTATION, (int)Database.Master, 0);
            try
            {
                IDbDataParameter PrmUserName = objDataHelper.CreateDataParameter();
                PrmUserName.ParameterName = "@UserName";
                PrmUserName.DbType = DbType.String;
                PrmUserName.Value = userName;
                PrmUserName.Direction = ParameterDirection.Input;

                
                ds = objDataHelper.RunSPReturnDS("PR_CheckUserLogins", PrmUserName);
                //end of addition.

                if (ds == null)
                    return null;
                else if (ds.Tables.Count == 0)
                    return null;

                if (ds.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow dr in ds.Tables[0].Rows)
                    {
                        objdata.UserName = dr["UserName"].ToString();
                        objdata.Password = dr["Password"].ToString();
                        objdata.Email = dr["Email"].ToString();
                        objdata.UserId = dr["UserId"].ToString();
                        objdata.IsActive = Convert.ToBoolean(dr["IsActive"]);
                        obj.SmartDataList.Add(objdata);
                    }
                }
                else
                {
                    objdata.CredentialsMessage = "Incorrect Username";
                    obj.SmartDataList.Add(objdata);
                    return obj;
                }
                HIS.TOOLS.Logger.XCryptEngine xc = new HIS.TOOLS.Logger.XCryptEngine(HIS.TOOLS.Logger.XCryptEngine.AlgorithmType.DES);
                string encryptuserpwd = xc.Encrypt(password, userName.ToUpper());
                string desc = xc.Decrypt(objdata.Password, userName.ToUpper());
                if (objdata.Password != encryptuserpwd)
                {
                    objdata = new LoginDetailsOutput();
                    objdata.CredentialsMessage = "Incorrect Password";
                    obj.SmartDataList.Clear();
                    obj.SmartDataList.Add(objdata);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            { objDataHelper = null; }
            return obj;
        }
        public DataSet FetchEmployees(int intUserID, int intWorkStationID, int HospitalId)
        {
            DataHelper objDataHelper = new DataHelper(DEFAULTWORKSTATION, (int)Database.Master, Convert.ToInt32(HospitalId));
            DataSet dsList = new DataSet();
            try
            {
                List<IDbDataParameter> objIDbDataParameters = new List<IDbDataParameter>();
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@Type", null, DbType.String, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@Filter", "SID=" + intUserID, DbType.String, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@USERID", intUserID, DbType.Int32, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@WORKSTATIONID", intWorkStationID, DbType.Int32, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@Error", 0, DbType.Int32, ParameterDirection.Output));
                using (dsList = objDataHelper.RunSPReturnDS("Pr_FetchUsersGroupsAdv", objIDbDataParameters.ToArray()))
                {
                }

                return dsList;
            }
            finally
            {
                objDataHelper = null;
            }
        }
        public DataSet FetchEmployeeSpecialisation(int EmpId, int LangId, int UserId, int intWorkStationID, int HospitalId)
        {
            DataHelper objDataHelper = new DataHelper(DEFAULTWORKSTATION, (int)Database.Master, Convert.ToInt32(HospitalId));
            DataSet dsList = new DataSet();
            try
            {
                List<IDbDataParameter> objIDbDataParameters = new List<IDbDataParameter>();
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@Type", 0, DbType.Int32, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@LanguageId", 0, DbType.String, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@Filter", "Blocked=0 and EmpId=" + EmpId + " and HospitalId=" + HospitalId, DbType.String, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@UserId", UserId, DbType.Int32, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@WorkStationID", intWorkStationID, DbType.Int32, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@Error", 0, DbType.Int32, ParameterDirection.Output));
                using (dsList = objDataHelper.RunSPReturnDS("Pr_FetchEmployeeSpecializationsAdv", objIDbDataParameters.ToArray()))
                {
                }

                return dsList;
            }
            finally
            {
                objDataHelper = null;
            }
        }
        public SignatureModel SaveSignatureRequests(SignatureModel SigParams)
        {
            SignatureModel objSave = new SignatureModel();
            DataHelper objDataHelper = new DataHelper(DEFAULTWORKSTATION, (int)Database.Master, SigParams.HospitalId);
            try
            {
                string strReciepientsXML = string.Empty;
                if (SigParams.ReciepientsXML != null)
                {
                    if (SigParams.ReciepientsXML.ToArray().Length > 0)
                    {
                        DataSet dtReciepientsXML = ALHMobileAppAPI.Messages.Utilities.ToDataSetFromArrayOfObject(SigParams.ReciepientsXML.ToArray());
                        if (dtReciepientsXML != null && dtReciepientsXML.Tables.Count > 0)
                        {
                            strReciepientsXML = Utilities.ConvertDTToXML("SIGN", "ITM", dtReciepientsXML.Tables[0]);
                        }
                    }
                }
                List<IDbDataParameter> objIDbDataParameters = new List<IDbDataParameter>();
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@RequestId", DBNull.Value, DbType.Int32, ParameterDirection.Output));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@DocumentId", DBNull.Value, DbType.Int32, ParameterDirection.Output));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@Patientid", SigParams.Patientid, DbType.Int32, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@RequestDocumentName", SigParams.RequestDocumentName, DbType.String, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@SendInOrder", SigParams.SendInOrder, DbType.Boolean, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@DaysToComplete", SigParams.DaysToComplete, DbType.Int32, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@Remainder", SigParams.Remainder, DbType.Int32, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@Notes", SigParams.Notes, DbType.String, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@HTMLDocumentName", SigParams.HTMLDocumentName, DbType.String, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@HTMLStringForSignature", SigParams.HTMLStringForSignature, DbType.String, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@ReciepientsXML", strReciepientsXML, DbType.String, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@UserId", SigParams.UserId, DbType.Int32, ParameterDirection.Input));
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@WorkStationID", SigParams.WorkStationID, DbType.Int32, ParameterDirection.Input));

                int intRes = objDataHelper.RunSP("Pr_SaveSignatureRequests", objIDbDataParameters.ToArray());

                if (intRes == -1 || intRes > 0)
                {
                    objSave.Code = CommanUtilities.Models.ProcessStatus.Success;
                    objSave.Status = CommanUtilities.Models.ProcessStatus.Success.ToString();
                    objSave.Message = "Signature Request Saved Successfully";

                }
                else
                {
                    objSave.Code = CommanUtilities.Models.ProcessStatus.Fail;
                    objSave.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
                    objSave.Message = "Error occured while Saving";
                }
                return objSave;
            }
            catch (Exception ex)
            {
                objSave.Code = CommanUtilities.Models.ProcessStatus.Fail;
                objSave.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
                objSave.Message = ex.Message;
                HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(ex, MODULE_NAME, "Error in SaveSignatureRequests", "");
            }
            finally
            {
                objDataHelper = null;
            }
            return objSave;
        }

        public SignatureRequests FetchSignatureRequests(string RequestId)
        {
            DataHelper objDataHelper = new DataHelper(DEFAULTWORKSTATION, (int)Database.Master);
            SignatureRequests objGetMasterData = new SignatureRequests();

            try
            {
                List<IDbDataParameter> objIDbDataParameters = new List<IDbDataParameter>();
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@RequestId", Convert.ToInt32(RequestId), DbType.Int32, ParameterDirection.Input));
                using (DataSet dsDocList = objDataHelper.RunSPReturnDS("Pr_FetchSignatureRequests", objIDbDataParameters.ToArray()))
                {
                    if (dsDocList.Tables.Count > 0)
                    {
                        foreach (DataRow dr in dsDocList.Tables[0].Rows)
                        {
                            SignatureRequestsData obj = new SignatureRequestsData();
                            obj.RequestId = dr["RequestId"].ToString();
                            obj.DocumentName = dr["DocumentName"].ToString();
                            obj.SendInOrder = dr["SendInOrder"].ToString();
                            obj.DaysToComplete = dr["DaysToComplete"].ToString();
                            obj.Remainder = dr["Remainder"].ToString();
                            obj.Notes = dr["Notes"].ToString();
                            obj.Createdate = Convert.ToDateTime(dr["Createdate"]).ToString("dd-MMM-yyyy");
                            obj.Moddate = Convert.ToDateTime(dr["Moddate"]).ToString("dd-MMM-yyyy");
                            obj.USERID = dr["USERID"].ToString();
                            obj.WorkStationId = dr["WorkStationId"].ToString();
                            obj.RoutID = dr["RoutID"].ToString();
                            obj.Blocked = dr["Blocked"].ToString();
                            obj.Enddate = dr["Enddate"].ToString();
                            obj.Status = dr["Status"].ToString();
                            objGetMasterData.SignatureRequestsDataList.Add(obj);
                        }
                        foreach (DataRow dr in dsDocList.Tables[1].Rows)
                        {
                            SignatureReciepientData obj1 = new SignatureReciepientData();
                            obj1.ReciepientId = dr["ReciepientId"].ToString();
                            obj1.RequestId = dr["RequestId"].ToString();
                            obj1.Email = dr["Email"].ToString();
                            obj1.DepartmentName = dr["DepartmentName"].ToString();
                            obj1.ReciepientName = dr["ReciepientName"].ToString();
                            obj1.ReciepientUserID = dr["ReciepientUserID"].ToString();
                            obj1.Createdate = Convert.ToDateTime(dr["Createdate"]).ToString("dd-MMM-yyyy");
                            obj1.Moddate = Convert.ToDateTime(dr["Moddate"]).ToString("dd-MMM-yyyy");
                            obj1.USERID = dr["USERID"].ToString();
                            obj1.WorkStationId = dr["WorkStationId"].ToString();
                            obj1.RoutID = dr["RoutID"].ToString();
                            obj1.Blocked = dr["Blocked"].ToString();
                            obj1.Enddate = dr["Enddate"].ToString();
                            obj1.Status = dr["Status"].ToString();
                            objGetMasterData.SignatureReciepientDataList.Add(obj1);
                        }
                        foreach (DataRow dr in dsDocList.Tables[2].Rows)
                        {
                            SignatureDocumentsData obj2 = new SignatureDocumentsData();
                            obj2.DocumentId = dr["DocumentId"].ToString();
                            obj2.RequestId = dr["RequestId"].ToString();
                            obj2.HTMLDocumentName = dr["HTMLDocumentName"].ToString();
                            obj2.HTMLStringForSignature = dr["HTMLStringForSignature"].ToString();
                            obj2.PendingUserID = dr["PendingUserID"].ToString();
                            obj2.Createdate = Convert.ToDateTime(dr["Createdate"]).ToString("dd-MMM-yyyy");
                            obj2.Moddate = Convert.ToDateTime(dr["Moddate"]).ToString("dd-MMM-yyyy");
                            obj2.USERID = dr["USERID"].ToString();
                            obj2.WorkStationId = dr["WorkStationId"].ToString();
                            obj2.RoutID = dr["RoutID"].ToString();
                            obj2.Blocked = dr["Blocked"].ToString();
                            obj2.Enddate = dr["Enddate"].ToString();
                            obj2.Status = dr["Status"].ToString();
                            objGetMasterData.SignatureDocumentsDataList.Add(obj2);
                        }
                    }
                    if (objGetMasterData.SignatureRequestsDataList.Count > 0)
                    {
                        objGetMasterData.Code = CommanUtilities.Models.ProcessStatus.Success;
                        objGetMasterData.Status = CommanUtilities.Models.ProcessStatus.Success.ToString();
                        objGetMasterData.Message = "";
                        objGetMasterData.Message2L = "";
                    }
                    else
                    {
                        objGetMasterData.Code = CommanUtilities.Models.ProcessStatus.Success;
                        objGetMasterData.Status = CommanUtilities.Models.ProcessStatus.Success.ToString();

                    }
                }
                return objGetMasterData;
            }
            catch (Exception ex)
            {
                objGetMasterData.Code = CommanUtilities.Models.ProcessStatus.Fail;
                objGetMasterData.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
                objGetMasterData.Message = ex.Message;
                HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(ex, MODULE_NAME, "Error in FetchEducationMaterials", "");
            }
            finally
            {
                objDataHelper = null;
            }
            return objGetMasterData;
        }
        public SignatureEmployees FetchSSSignatureReciepientUsers(string name)
        {
            DataHelper objDataHelper = new DataHelper(DEFAULTWORKSTATION, (int)Database.Master);
            SignatureEmployees objGetMasterData = new SignatureEmployees();

            try
            {
                List<IDbDataParameter> objIDbDataParameters = new List<IDbDataParameter>();
                objIDbDataParameters.Add(CreateParam(objDataHelper, "@UserName", name, DbType.String, ParameterDirection.Input));
                using (DataSet dsDocList = objDataHelper.RunSPReturnDS("PR_SSUserLogins", objIDbDataParameters.ToArray()))
                {
                    if (dsDocList.Tables.Count > 0)
                    {
                        foreach (DataRow dr in dsDocList.Tables[0].Rows)
                        {
                            SignatureEmployeesData obj = new SignatureEmployeesData();
                            obj.ID = dr["Userid"].ToString();
                            obj.Name = dr["UserName"].ToString();
                            obj.Email = dr["Email"].ToString();
                            objGetMasterData.SignatureEmployeesDataList.Add(obj);
                        }
                    }
                    if (objGetMasterData.SignatureEmployeesDataList.Count > 0)
                    {
                        objGetMasterData.Code = CommanUtilities.Models.ProcessStatus.Success;
                        objGetMasterData.Status = CommanUtilities.Models.ProcessStatus.Success.ToString();
                        objGetMasterData.Message = "";
                        objGetMasterData.Message2L = "";
                    }
                    else
                    {
                        objGetMasterData.Code = CommanUtilities.Models.ProcessStatus.Success;
                        objGetMasterData.Status = CommanUtilities.Models.ProcessStatus.Success.ToString();

                    }
                }
                return objGetMasterData;
            }
            catch (Exception ex)
            {
                objGetMasterData.Code = CommanUtilities.Models.ProcessStatus.Fail;
                objGetMasterData.Status = CommanUtilities.Models.ProcessStatus.Fail.ToString();
                objGetMasterData.Message = ex.Message;
                HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(ex, MODULE_NAME, "Error in FetchEducationMaterials", "");
            }
            finally
            {
                objDataHelper = null;
            }
            return objGetMasterData;
        }
    }
}