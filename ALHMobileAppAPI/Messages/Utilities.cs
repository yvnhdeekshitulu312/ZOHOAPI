using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Xml.Serialization;

namespace ALHMobileAppAPI.Messages
{
    public static class Utilities
    {
        public const string DefaultErrorMessage = "Request can't be processed this time, plese try again later.";
        public const string NoRecordsFound = "No Records found";
        public const string SucMessage = "Success";
        public const string FailMessage = "Success";

        public static int GenerateRandomNo()
        {
            int _min = 1000;
            int _max = 9999;
            Random _rdm = new Random();
            return _rdm.Next(_min, _max);
        }
        public static DataSet ToDataSetFromArrayOfObject(this object[] arrCollection)
        {
            DataSet ds = new DataSet();
            try
            {
                XmlSerializer serializer = new XmlSerializer(arrCollection.GetType());
                System.IO.StringWriter sw = new System.IO.StringWriter();
                serializer.Serialize(sw, arrCollection);
                System.IO.StringReader reader = new System.IO.StringReader(sw.ToString());
                ds.ReadXml(reader);
            }
            catch (Exception ex)
            {
                throw (new Exception("Error While Converting Array of Object to Dataset."));
            }
            return ds;
        }
    }
}