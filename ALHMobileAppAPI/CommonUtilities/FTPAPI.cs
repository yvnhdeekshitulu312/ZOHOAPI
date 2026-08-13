using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text;
using System.IO;
using System.Net;

namespace CommanUtilities.Models
{
    public class FTPAPI
    {
        public bool CheckIfFileExistsOnServer(string fileName, string strIPAdd, string strRemote, string strUser, string strPwd)
        {
            try
            {
                StringBuilder result = new StringBuilder();
                bool Filevalue = false;
                List<string> listFiles = new List<string>();
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://" + strIPAdd + "/" + strRemote);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(strUser, strPwd);
                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                Stream responseStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(responseStream);
                string names = reader.ReadToEnd();
                reader.Close();
                response.Close();
                listFiles = names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (listFiles.Count > 0)
                {
                    for (int count = 0; count < listFiles.Count; count++)
                    {
                        if (listFiles[count].ToString() == fileName)
                        {
                            Filevalue = true;
                        }
                    }
                }
                return Filevalue;
            }
            catch (WebException ex)
            {
                String status = ((FtpWebResponse)ex.Response).StatusDescription;
                return false;
            }
        }
        public string DownloadFileFromFTP(string strRemoteFileName,string strIPAddd,string strRemotee, string strUser, string strPwd,string strLocalPath)
        {
            string strScanPath = string.Empty;
            int bufferSize = 2048;
            byte[] buffer = new byte[bufferSize];
            FtpWebResponse response = null;
            FtpWebRequest reqFTP;
            try
            {
                if (!Directory.Exists(strLocalPath))
                    Directory.CreateDirectory(strLocalPath);
                if (CheckIfFileExistsOnServer(strRemoteFileName, strIPAddd, strRemotee, strUser, strPwd))
                {
                    strScanPath= strLocalPath+ "\\"+ strRemoteFileName;
                    FileStream outputStream = new FileStream(strScanPath, FileMode.Create);
                    reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri("ftp://" + strIPAddd + "/" + strRemotee + "/" + strRemoteFileName));
                    reqFTP.Method = WebRequestMethods.Ftp.DownloadFile;
                    reqFTP.UseBinary = true;
                    reqFTP.Credentials = new NetworkCredential(strUser, strPwd);
                    response = (FtpWebResponse)reqFTP.GetResponse();
                    Stream ftpStream = response.GetResponseStream();
                    long cl = response.ContentLength;

                    int readCount;
                    readCount = ftpStream.Read(buffer, 0, bufferSize);
                    while (readCount > 0)
                    {
                        outputStream.Write(buffer, 0, readCount);
                        readCount = ftpStream.Read(buffer, 0, bufferSize);
                    }
                    ftpStream.Close();
                    outputStream.Close();
                    response.Close();
                    
                    return strScanPath;
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                throw ex;

            }
            return string.Empty;
        }
        public byte[] ImageToByteConversion(string strScanPath)
        {
            FileStream fs = new FileStream(strScanPath, FileMode.Open, FileAccess.Read);
            //Initialize a byte array with size of stream
            byte[] imgByteArr = new byte[fs.Length];
            //Read data from the file stream and put into the byte array
            fs.Read(imgByteArr, 0, Convert.ToInt32(fs.Length));
            //Close a file stream
            fs.Close();
            return imgByteArr;
        }
        public bool UploadFiletoFTP(string strIPAdd,string strRemote,string LocalFilePath,string strUser,string strPwd)
        {
            FtpWebRequest reqFTP;
            FileInfo fileInf = new FileInfo(LocalFilePath);
            try
            {
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri("ftp://" + strIPAdd + "/" + strRemote + "/" + fileInf.Name));
                reqFTP.Credentials = new NetworkCredential(strUser, strPwd);
                reqFTP.Method = WebRequestMethods.Ftp.GetFileSize;
                reqFTP.Proxy = null;
                reqFTP.KeepAlive = false;
                reqFTP.Method = WebRequestMethods.Ftp.UploadFile;
                Stream ftpStream = reqFTP.GetRequestStream();
                FileStream file = File.OpenRead(LocalFilePath);
                int length = 1024;
                byte[] buffer = new byte[length];
                int byteRead = 0;
                do
                {
                    byteRead = file.Read(buffer, 0, length);
                    ftpStream.Write(buffer, 0, byteRead);
                }
                while (byteRead != 0);
                file.Close();
                ftpStream.Close();
            }
            catch(Exception ex)
            {
                throw ex;
                return false;
                
            }

            return true;
        }
        
    }
}