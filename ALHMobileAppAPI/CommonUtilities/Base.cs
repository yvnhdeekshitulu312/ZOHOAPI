using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CommanUtilities.Models
{
    public class Base
    {
        ProcessStatus objCode = ProcessStatus.Fail;
        string strStatus = ProcessStatus.Fail.ToString();
        string strMessage = string.Empty;
        string strMessage2L = string.Empty;
        string strMobileNo = string.Empty;

        public ProcessStatus Code { get { return objCode; } set { objCode = value; } }
        public string Status { get { return strStatus; } set { strStatus = value; } }
        public string MobileNo { get { return strMobileNo; } set { strMobileNo = value; } }
        public string Message { get { return strMessage; } set { strMessage = value; } }
        public string Message2L { get { return strMessage2L; } set { strMessage2L = value; } }

        //public override string ToString()
        //{
        //    JavaScriptSerializer js = new JavaScriptSerializer();

        //    return js.Serialize(this);
        //}
        public static List<T> AssembleBECollection<T>(IDataReader dr) where T : new()
        {
            Type businessEntityType = typeof(T);

            List<T> entitys = new List<T>();

            Hashtable hashtable = new Hashtable();

            PropertyInfo[] properties = businessEntityType.GetProperties();

            foreach (PropertyInfo info in properties)
            {
                hashtable[info.Name.ToUpper()] = info;
            }

            while (dr.Read())
            {
                T newObject = new T();
                for (int index = 0; index < dr.FieldCount; index++)
                {
                    PropertyInfo info = (PropertyInfo)hashtable[dr.GetName(index).ToUpper()];
                    if ((info != null) && info.CanWrite)
                    {
                        if (dr.GetValue(index) != DBNull.Value)
                        {
                            if (dr.GetFieldType(index) == typeof(DateTime))
                            {
                                DateTime dt;
                                DateTime.TryParse(dr.GetValue(index).ToString(), out dt);
                                info.SetValue(newObject, dt.ToString("dd-MMM-yyyy H:mm:ss"), null);

                            }
                            else if (dr.GetFieldType(index) == typeof(TimeSpan))
                            {
                                TimeSpan ts;
                                TimeSpan.TryParse(dr.GetValue(index).ToString(), out ts);
                                info.SetValue(newObject, ts.ToString(), null);
                            }
                            else
                                info.SetValue(newObject, dr.GetValue(index), null);
                        }
                    }
                }
                entitys.Add(newObject);
            }
            return entitys;
        }




    }
    public enum ProcessStatus
    {
        Success = 1,
        Fail = 2
    }
    public static class Extension
    {
        public static List<T> AssembleBECollection<T>(this IDataReader dr) where T : new()
        {
            List<T> entitys = new List<T>();

            Hashtable hashtable = GetHashTable<T>();

            while (dr.Read())
            {
                T newObject = new T();
                for (int index = 0; index < dr.FieldCount; index++)
                {
                    PropertyInfo info = (PropertyInfo)hashtable[dr.GetName(index).ToUpper()];
                    if ((info != null) && info.CanWrite)
                    {
                        if (dr.GetValue(index) != DBNull.Value)
                        {
                            if (dr.GetName(index).ToString().Contains("BillDate") || dr.GetName(index).ToString().Contains("AppointmentBookedDate"))
                            {
                                DateTime dt;
                                DateTime.TryParse(dr.GetValue(index).ToString(), out dt);
                                info.SetValue(newObject, dt.ToString("dd-MMM-yyyy H:mm:ss"), null);
                            }
                            else if (dr.GetFieldType(index) == typeof(DateTime))
                            {
                                DateTime dt;
                                DateTime.TryParse(dr.GetValue(index).ToString(), out dt);
                                info.SetValue(newObject, dt.ToString("dd-MMM-yyyy"), null);
                            }
                            else
                                info.SetValue(newObject, dr.GetValue(index), null);
                        }
                    }
                }

                entitys.Add(newObject);
            }
            return entitys;
        }

        public static T AssembleBE<T>(this IDataReader dr) where T : new()
        {
            T newObject = new T();

            Hashtable hashtable = GetHashTable<T>();
            for (int index = 0; index < dr.FieldCount; index++)
            {
                PropertyInfo info = (PropertyInfo)hashtable[dr.GetName(index).ToUpper()];
                if ((info != null) && info.CanWrite)
                {
                    if (dr.GetValue(index) != DBNull.Value)
                    {

                        if (dr.GetFieldType(index) == typeof(DateTime))
                        {
                            if (dr.GetName(index).ToString().Contains("ReportCreatedDate"))
                            {
                                DateTime dt;
                                DateTime.TryParse(dr.GetValue(index).ToString(), out dt);
                                info.SetValue(newObject, dt.ToString("dd-MMM-yyyy H:mm:ss"), null);
                            }
                            else
                            {
                                DateTime dt;
                                DateTime.TryParse(dr.GetValue(index).ToString(), out dt);
                                info.SetValue(newObject, dt.ToString("dd-MMM-yyyy"), null);
                            }
                        }
                        else
                            info.SetValue(newObject, dr.GetValue(index), null);
                    }
                }
            }

            return newObject;
        }

        public static Hashtable GetHashTable<T>() where T : new()
        {
            Type businessEntityType = typeof(T);

            Hashtable hashtable = new Hashtable();

            PropertyInfo[] properties = businessEntityType.GetProperties();

            foreach (PropertyInfo info in properties)
            {
                hashtable[info.Name.ToUpper()] = info;
            }
            return hashtable;
        }
    }


}
