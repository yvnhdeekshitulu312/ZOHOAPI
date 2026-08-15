using CommanUtilities.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ALHMobileAppAPI.Models
{
    public class SignatureModel : Base
    {
        public int RequestId { get; set; }
        public int DocumentId { get; set; }
        public int Patientid { get; set; }
        public string RequestDocumentName { get; set; }
        public bool SendInOrder { get; set; }
        public int DaysToComplete { get; set; }
        public int Remainder { get; set; }
        public string Notes { get; set; }
        public string HTMLDocumentName { get; set; }
        public string HTMLStringForSignature { get; set; }
        // public List<ReciepientsXML> ReciepientsXML { get; set; }
        public List<SignatureReciepient> ReciepientsXML { get; set; }
        public int UserId { get; set; }
        public int WorkStationID { get; set; }
        public int HospitalId { get; set; }
    }
    public class ReciepientsXML
    {
        public string EMAIL { get; set; }
        public string NAME { get; set; }
        public string RUSERID { get; set; }        

    }
    public class SignatureReciepient
    {
        public string Email { get; set; }
        public string ReciepientName { get; set; }
        public string Role { get; set; }
        public int? SigningOrder { get; set; }
        public int? ReciepientUserID { get; set; }
        public string DeliveryMethod { get; set; }
    }
    public class ConfigDetails : Base
    {
        public string IOSVersion { get; set; }
        public string AndriodVersion { get; set; }
        public string IOSURL { get; set; }
        public string AndriodURL { get; set; }
        public bool ForceUpdate { get; set; }

    }

    public class SignatureRequests : Base
    {

        List<SignatureRequestsData> SignatureRequestsData = new List<SignatureRequestsData>();
        public List<SignatureRequestsData> SignatureRequestsDataList { get { return SignatureRequestsData; } set { SignatureRequestsData = value; } }

        List<SignatureReciepientData> SignatureReciepientData = new List<SignatureReciepientData>();
        public List<SignatureReciepientData> SignatureReciepientDataList { get { return SignatureReciepientData; } set { SignatureReciepientData = value; } }

        List<SignatureDocumentsData> SignatureDocumentsData = new List<SignatureDocumentsData>();
        public List<SignatureDocumentsData> SignatureDocumentsDataList { get { return SignatureDocumentsData; } set { SignatureDocumentsData = value; } }
    }
    public class SignatureRequestsData
    {
        public string RequestId { get; set; }
        public string DocumentName { get; set; }
        public string SendInOrder { get; set; }
        public string DaysToComplete { get; set; }
        public string Remainder { get; set; }
        public string Notes { get; set; }
        public string Createdate { get; set; }
        public string Moddate { get; set; }
        public string USERID { get; set; }
        public string WorkStationId { get; set; }
        public string RoutID { get; set; }
        public string Blocked { get; set; }
        public string Enddate { get; set; }
        public string Status { get; set; }
    }
    public class SignatureReciepientData
    {
        public string ReciepientId { get; set; }
        public string RequestId { get; set; }
        public string Email { get; set; }
        public string DepartmentName { get; set; }
        public string ReciepientName { get; set; }
        public string ReciepientUserID { get; set; }
        public string Createdate { get; set; }
        public string Moddate { get; set; }
        public string USERID { get; set; }
        public string WorkStationId { get; set; }
        public string RoutID { get; set; }
        public string Blocked { get; set; }
        public string Enddate { get; set; }
        public string Status { get; set; }
    }
    public class SignatureDocumentsData
    {
        public string DocumentId { get; set; }
        public string RequestId { get; set; }
        public string HTMLDocumentName { get; set; }
        public string HTMLStringForSignature { get; set; }
        public string PendingUserID { get; set; }
        public string Createdate { get; set; }
        public string Moddate { get; set; }
        public string USERID { get; set; }
        public string WorkStationId { get; set; }
        public string RoutID { get; set; }
        public string Blocked { get; set; }
        public string Enddate { get; set; }
        public string Status { get; set; }
    }

    public class SignatureEmployees : Base
    {
        List<SignatureEmployeesData> SignatureEmployeesData = new List<SignatureEmployeesData>();
        public List<SignatureEmployeesData> SignatureEmployeesDataList { get { return SignatureEmployeesData; } set { SignatureEmployeesData = value; } }

    }
    public class SignatureEmployeesData
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }


    public class LoginDetails : Base
    {
        List<LoginDetailsOutput> MasterData = new List<LoginDetailsOutput>();
        public List<LoginDetailsOutput> SmartDataList { get { return MasterData; } set { MasterData = value; } }
    }
    public class LoginDetailsOutput
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string UserId { get; set; }
        public string ISLocked { get; set; }
        public string ISPWDExpired { get; set; }
        public string PWDSetDate { get; set; }
        public string LoggedHostIP { get; set; }
        public string LoggedHostName { get; set; }
        public string ISLogged { get; set; }
        public string PWDDays { get; set; }
        public string CredentialsMessage { get; set; }
        public int EmpId { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeName2l { get; set; }
        public string UIUserTimeOut { get; set; }
        public string EmpSpecialisationId { get; set; }
        public bool IsActive { get; set; }
        public string Email { get; set; }


    }

    public class PdfToImageModel
    {

        public string Name { get; set; }
        public IFormFile Image { get; set; }
    }

}