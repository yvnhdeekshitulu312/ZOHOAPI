/**************************************************************
* Application Name:ExceptionManager
* File Name/Namespace Name:ExceptionManager.cs/HISException
* Author Name		:Robin
* Date of Creation	:20/05/2003
* File References	: 
* Components Used	:HISResource.ResourceManager
* Calling Pages		:In all the classes in the application 
* Called Pages      :
* Version Number	:1.0 
* Purpose			:To create a common exception handling component. 
*
* History:  Created By    Date        Version  Purpose
-----------------------------------------------------------------------------------------------------------------

											   
**************************************************************/

using System;
using System.Reflection;
using System.Collections;
using System.Text;
using System.Security;
using System.Security.Principal;
using System.Resources;
using System.Data;
using System.Data.OleDb;
using System.Data.OracleClient;
using System.Data.SqlClient;
using HISResource;

namespace HISException
{	
	#region Enums
	/// <summary>
	/// Used to indicate the type of the error.
	/// </summary>
	/*************************************************************
	* Method Name	: enum/ErrorType 
	* Components	: 
	* Tables Used	: N.A.
	* Create Date	: 04/06/2003
	* Author        : Robin
	* Change Control #    Date       Author         Description 
	* *************************************************************
	*				1)  04/06/2003  Robin      	Added	
	***************************************************************/
	public enum ErrorType 
	{ 
		///<summary>Raised By System: either Database or System Exception</summary>
		System=0,
		///<summary>
		///Raised By User:Custom errors.There will be an AppCode associated
		///with this type of error.
		///</summary>
		Application=1,
		///<summary>If error emanates from within component</summary>
		WithinComponent=2
	}
    #endregion
   
	#region ExceptionManager	
	/// <summary>
	/// Provides an interface that handles exceptions and retrieves custom messages from a resource file.Based on 'Singleton' pattern.
	/// </summary>	
	public class ExceptionManager		
	{
		#region ctors		
		private ExceptionManager(){}//Instantiation should not be possible.				
		#endregion

		#region Private Methods and Variables
		/// <summary>
		/// Sets the SysErrorDesc property of the actual exception or the inner exception(if message of 
		/// actual is empty),as the case may be, or the GetType().FullName of the innermost class if all are empty.
		/// </summary>		
		/*************************************************************
		* Method Name	: SetSysMessage
		* Components	: 
		* Tables Used	: N.A.
		* Create Date	: 23/07/2003
		* Author        : Vikas Rao
		* Change Control #    Date       Author         Description 
		* *************************************************************
		*				1)  23/07/2003  Vikas Rao      	Added	
		***************************************************************/
		private static void SetSysMessage(CustomException customException,System.Exception ex)		
		{	
			//We are not catching for exceptions here as there cannot be any,
			//and there should'nt be any....The exception manager class should be the final frontier for trapping
			//exceptions generated within too..but ofcourse thats kinda unreasonable eh!!
			if(customException==null || ex==null)
				return;
			if((ex.Message!=null) && (ex.Message!=""))
			{										
				customException.SysErrorDesc=ex.Message;											
			}
			else 
			{
				if(ex.InnerException!=null)
				{
					SetSysMessage(customException,ex.InnerException);
				}
				else
				{
					//Get the class name only, if the exception does not have a message
					//and the inner exception is null.
					customException.SysErrorDesc="Could not retrieve an exception message.Retrieving only Exception Class Name,which is:'"+ex.GetType().FullName+"'.";							
				}
			}			
		}
		/// <summary>
		/// Sets the AppErrorDesc property of the actual exception or the inner exception(if message of 
		/// actual is empty),as the case may be, or the GetType().FullName of the innermost class if all are empty.
		/// </summary>		
		/*************************************************************
		* Method Name	: SetAppMessage
		* Components	: 
		* Tables Used	: N.A.
		* Create Date	: 23/07/2003
		* Author        : Vikas Rao
		* Change Control #    Date       Author         Description 
		* *************************************************************
		*				1)  23/07/2003  Vikas Rao      	Added	
		***************************************************************/
		private static void SetAppMessage(CustomException customException,System.Exception ex,string appErrorDesc)		
		{	
			//We are not catching for exceptions here as there cannot be any,
			//and there should'nt be any....The exception manager class should be the final frontier for trapping
			//exceptions generated within too..but ofcourse thats kinda unreasonable eh!!
			if(appErrorDesc==null || appErrorDesc.Equals(""))
			{
				if((ex.Message!=null) && (ex.Message!=""))
				{										
					customException.AppErrorDesc=ex.Message;											
				}
				else 
				{
					if(ex.InnerException!=null)
					{
						SetAppMessage(customException,ex.InnerException,null);
					}
					else
					{
						//Get the class name only, if the exception does not have a message
						//and the inner exception is null.
						customException.AppErrorDesc="Could not retrieve an exception message.Retrieving only Exception Class Name,which is:'"+ex.GetType().FullName+"'.";								
					}
				}
			}
			else
			{
				customException.AppErrorDesc=appErrorDesc;
			}
		}
		/// <summary>
		/// Appends all the error messages in a stack of exceptions and gives it back as a string.
		/// </summary>		
		/*************************************************************
		* Method Name	: GetMessageLog
		* Components	: 
		* Tables Used	: N.A.
		* Create Date	: 23/07/2003
		* Author        : Vikas Rao
		* Change Control #    Date       Author         Description 
		* *************************************************************
		*				1)  23/07/2003  Vikas Rao      	Added	
		***************************************************************/
		private static void GetMessageLog(System.Exception ex,ref StringBuilder messageLog)		
		{				
			//We are not catching for exceptions here as there cannot be any,
			//and there should'nt be any....The exception manager class should be the final frontier for trapping
			//exceptions generated within too..but ofcourse thats kinda unreasonable eh!!
			try
			{
				if (ex==null)
					return;
				if(messageLog==null)
					return;
				if((ex.Message!=null) && (ex.Message!=""))
				{				
					if(messageLog.Length==0)
					{
						messageLog.AppendFormat("{0}",ex.Message);
					}
					else
					{
						messageLog.AppendFormat("{0}Additional Information : {1}",Environment.NewLine,ex.Message);
					}
					//Lets append any other information also, if it exists.
					if(ex.InnerException!=null)
					{
						GetMessageLog(ex.InnerException,ref messageLog);
					}
				}
				else 
				{
					if(ex.InnerException!=null)
					{
						GetMessageLog(ex.InnerException,ref messageLog);
					}
					else
					{
						if(messageLog.Length==0)
						{
							//Get the class name only, if the exception does not have a message
							//and the inner exception is null.
							messageLog.AppendFormat("{0}:{1}","Could not retrieve an exception message.Retrieving only Exception Class Name,which is","'"+ex.GetType().FullName+"'.");			
						}
					}
				}
			}
			catch
			{
				return;
			}

			
		}		
		#endregion

		#region Properties Exposed		
		#endregion
	
		#region Public Methods			
		/// <summary>
		/// Handles the raised exception and generates a new custom exception for the client to catch.
		/// </summary>		
		/*************************************************************
		* Method Name	: HandleException 
		* Components	: 
		* Tables Used	: N.A.
		* Create Date	: 04/06/2003
		* Author        : Vikas Rao
		* Change Control #    Date       Author         Description 
		* *************************************************************
		*				1)  04/06/2003  Vikas Rao      	Modified	
		***************************************************************/
		public static CustomException HandleException(System.Exception ex,string source,MethodBase currentMethod,params object[] listOfParams)
		{
			//Declarations
			const string appError="A";
			const string sysError="S";
			int exceptionCode=0;
			string exceptionMessage="";		
			string shortExceptionMessage="";
			string typeOfError="";
			string userId="";
			StringBuilder messageLog=null;
			CustomException customException=null;

			
			try
			{	
			
				if((ex==null)||(source==null)||currentMethod==null)
					return customException;
			
				customException=ex as CustomException;
				//Get the current identity.
				userId=WindowsIdentity.GetCurrent().Name;
				//If the exception is already of type CustomException then return from the method..
				if(customException!=null)
				{					
					return customException;						
				}
				else 
				{
					#region "For CodeBasedException"
					//Otherwise create a custom exception object with the required attributes.																					
					if((ex as CodeBasedException)!=null)
					{	
						typeOfError=appError;
						exceptionCode=((CodeBasedException)ex).ErrorCode;
						shortExceptionMessage=ex.Message;
						//To get a complete message listing of inner exceptions also passed as part of ITAMException
						messageLog=new StringBuilder(150);
						GetMessageLog(ex,ref messageLog);						
						exceptionMessage=messageLog.ToString();						
					}										
						#endregion "For CodeBasedException"

						#region "For OleDbException and OracleException"
					else if((ex as OleDbException)!=null || (ex as OracleException)!=null)					
					{						
						int indexOfOra=0;						
						int indexForStartSearch=0;
						int indexForEndSearch=0;

						//Fetch the index of the key 'ORA' from the exception message.
						indexOfOra=ex.Message.IndexOf("ORA");
						if(indexOfOra>-1) //Oledb error from Oracle.
						{							
							//Error number: either oracle's system no. or our custom no. raised from oracle.
							//Clip the 'ORA' part of the string and get the -ve or +ve number b/w
							//ORA and first instance of ':'.For ex: in the string='ORA-20001:', the number will be '-20001'.
							
							//Add 3 to the indexOfOra for the length of the string 'ORA'.
							indexForStartSearch=indexOfOra+3;

							//Fetch the exception code from the string.

							//If the char ':' is found then mark it as the position of end of search.
							indexForEndSearch=ex.Message.IndexOf(":",indexForStartSearch);
							if(indexForEndSearch>-1)
							{
								exceptionCode=Convert.ToInt32(ex.Message.Substring(indexForStartSearch,indexForEndSearch-indexForStartSearch));
							}
							
							//Custom error numbers from ORACLE are  b/w -20999 and -20000.
							if(exceptionCode>=-20999 && exceptionCode<=-20000) 
							{
								typeOfError=appError;
								exceptionMessage=ResourceFileManager.GetMessage(exceptionCode);																	
								shortExceptionMessage=exceptionMessage;
							}
							else
							{
								typeOfError=sysError;							
							}
						}
						else //Oledb error other than oracle.
						{
							typeOfError=sysError;						
						}
					}
						#endregion "For OleDbException and OracleException"
					
						#region "For SqlException"
					else if((ex as SqlException)!=null)
					{												
						exceptionCode=((SqlException)ex).Number;						
						//No.s greater than or equal to 50000, are custom errors returned from Sql stored procs or functions
						//and hence their messages are retrieved from the resource file.
						//Custom error numbers from SQL Server are >=50000.
						
						//Sql is raising Unique Key Exception code 2627 and getting Common Code for 2627, So that
						//for other Data Providers also same code can be thrown.Application will be depend on common
						//code.
						//						exceptionCode=50000;
						//						if((exceptionCode>=50000) || (exceptionCode ==2627) )
						//						{
						//							typeOfError=appError;							
						//							exceptionMessage=ResourceFileManager.GetMessage(exceptionCode);	
						//							shortExceptionMessage=exceptionMessage;
						//						}
						//						else
						//						{
						typeOfError=sysError;	
						//						}
					}
						#endregion "For SqlException"
					else
					{
						typeOfError=sysError;					
					}
					


					//Perform common operations based on the exception type.
					switch(typeOfError)
					{
						case appError:
							customException=new CustomException(exceptionMessage,ex.InnerException);
							customException.ErrorType=ErrorType.Application;

							#region Commented Code
							//changed by srikanth because of change in AppErrorcode from int to string
							//customException.AppErrorCode=exceptionCode;	
							#endregion Commented Code

							//When Exception code is 2627 for SQL Unique constraint Change AppErrorCode from 2627 to Message
							//Retrived from Resource file. even Add Same sort of code for Oracle. So that Front End Application will depend on Message from resource file 
							if(exceptionCode==2627)
							{
								customException.AppErrorCode=exceptionMessage;			
								//when exception =2627 i.e unique constraint for SQL the Message will be "Dup" making message of backend to be passed to front for logging
								SetAppMessage(customException,ex,"");				
							}
							else
							{
								customException.AppErrorCode=exceptionCode.ToString() ;						
								SetAppMessage(customException,ex,shortExceptionMessage);				
							}
							
							break;
						case sysError:
							//To get a complete message listing of inner exceptions also passed as part of the system exception.
							messageLog=new StringBuilder(150);
							GetMessageLog(ex,ref messageLog);
							exceptionMessage=messageLog.ToString();
							customException=new CustomException(exceptionMessage,ex);
							customException.ErrorType=ErrorType.System;
							SetSysMessage(customException,ex);											
							break;
					}
					//Assign the objectinfo (if any).
					customException.ObjectInfo=listOfParams;	
					customException.Source=source;
					customException.NTUserID=userId;
					customException.MethodInfo=currentMethod;					
				}
			}
			catch(CustomException exWithinComponent)
			{
				customException=exWithinComponent;
			}
			catch(System.Data.DataException exWithinComponent)
			{
				customException=new CustomException(exWithinComponent.Message,exWithinComponent);
				customException.ErrorType=ErrorType.WithinComponent;
				customException.Source="ExceptionManager";				
				customException.MethodInfo=MethodBase.GetCurrentMethod();
				SetSysMessage(customException,exWithinComponent);				
			}
			catch 
			{
				return customException;
			}
			return customException;	
		}

		#endregion
	}
	#endregion 
				
	#region CustomException Class
	/// <summary>
	/// Custom exception class which is thrown back by the 'ExceptionManager.HandleException' method
	/// </summary>
	/*************************************************************
	* Method Name	: Class/CustomException 
	* Components	: 
	* Tables Used	: N.A.
	* Create Date	: 04/06/2003
	* Author        : Vikas Rao
	* Change Control #    Date       Author         Description 
	* *************************************************************
	*				1)  04/06/2003  Vikas Rao      	Modified	
	***************************************************************/	
	public class CustomException : ApplicationException		
	{
		
		
		
		#region Private Members and Variables
		/// changed by srikanth because of change in AppErrorcode from int to string 
		/// private int appErrorCode;		
		private string _appErrCode;		
		private string _appErrDesc;
		private string _sysErrDesc;
		private string _ntUserId;
		private MethodBase _methodInfo;	
		private object[] _objectInfo;
		private ErrorType _errorType;	
		#endregion

		#region ctors		
		/// <summary>
		/// Takes a message string, and an exception object and sets it as the inner exception object.
		/// </summary>
		CustomException()
		{
		}
		public CustomException(string message)
		{
		}

		 internal CustomException(string message,System.Exception innerException):base(message,innerException){}		
		#endregion

		#region Properties Exposed
		/// <summary>
		/// Gets/Sets the Application error code..
		/// </summary>	
		/// changed by srikanth because of change in AppErrorcode from int to string old -- public int AppErrorCode
		public string AppErrorCode
		{
			get
			{
				return _appErrCode;
			}	
			set
			{
				_appErrCode=value;
			}
		}
		/// <summary>
		/// Gets/Sets the Application error message..
		/// </summary>	
		public string AppErrorDesc
		{
			get
			{
				return _appErrDesc;
			}	
			set
			{
				_appErrDesc=value;
			}
		}
		/// <summary>
		/// Gets/Sets the System error message..
		/// </summary>	
		public string SysErrorDesc
		{			
			get
			{
				return _sysErrDesc;
			}	
			set
			{
				_sysErrDesc=value;
			}
		}		
		/// <summary>
		/// Gets/Sets the NTUserID..
		/// </summary>		
		public string NTUserID
		{
			get
			{
				return _ntUserId;
			}
			set
			{
				_ntUserId=value;
			}
		}
		/// <summary>
		/// Gets/Sets the method info for the exception.
		/// </summary>		
		public MethodBase MethodInfo
		{
			get
			{
				return _methodInfo;
			}
			set
			{				
				_methodInfo=value;
			}
		}	
		/// <summary>
		/// Gets/Sets additional info in the exception object.
		/// </summary>		
		public object[] ObjectInfo
		{
			get
			{
				return _objectInfo;
			}
			set
			{
				_objectInfo=value;
			}
		}
		/// <summary>
		/// Gets/Sets the type error ie either System or Application or WithinComponent.
		/// </summary>		
		public ErrorType ErrorType
		{
			get
			{
				return _errorType;
			}			
			set
			{				
				_errorType=value;
			}
		}			
		#endregion
	}

	#endregion

	#region CodeBasedException Class
	/// <summary>
	/// All application errors derive from or use this class.
	/// It forces the users to store their exception messages based on a code-value
	/// pair, in the ResourceCodes.resx file.
	/// Protected constructors provide a way to circumvent the above kind of usage, in dire cases.
	/// </summary>
	/*************************************************************
	* Method Name	: Class/CodeBasedException 
	* Components	: 
	* Tables Used	: N.A.
	* Create Date	: 04/06/2003
	* Author        : Vikas Rao
	* Change Control #    Date       Author         Description 
	* *************************************************************
	*				1)  04/06/2003  Vikas Rao      	Added	
	***************************************************************/	
	public class CodeBasedException:ApplicationException
	{
		
		CodeBasedException()
		{
		}
		#region Private Members and Variables
		private readonly int errCode=0;	
		
		/// <summary>
		/// To make all exceptions, dynamic in nature, picking up values from the ResourceCodes.resx file 
		/// and replacing place holders with place holder values.	
		/// </summary>		
		private static string GetExceptionMessage(int erCode,string[] placeHolderValues)
		{	
			StringBuilder messageBuilder=new StringBuilder(150);
			string returnMessage="";			
			int messageListCounter=0;
			try
			{				
				messageBuilder.Append(ResourceFileManager.GetMessage(erCode));				
			}
			catch
			{
				returnMessage="Error trying to fetch exception message from 'ResourceCodes.dll' for the code='"+erCode.ToString()+"'.";				
				return returnMessage;
			}
			if(placeHolderValues!=null)
			{
				foreach(string placeHolderValue in placeHolderValues)
				{						
					messageBuilder.Replace("[PlaceHolder"+messageListCounter+"]",placeHolderValue);					
					messageListCounter++;
				}
			}
			return messageBuilder.ToString();
		}
		#endregion

		#region ctors	
		/// <summary>
		/// Takes in an error code and a variable list of place-holder values.
		/// </summary>		
		public CodeBasedException(int erCode,params string[] placeHolderValues):base(GetExceptionMessage(erCode,placeHolderValues))
		{	
			this.errCode=erCode;
		}
		/// <summary>
		/// Takes in an error code, an inner exception and a variable list of place-holder values.
		/// </summary>		
		public CodeBasedException(int erCode,System.Exception innerException,params string[] placeHolderValues):base(GetExceptionMessage(erCode,placeHolderValues),innerException)
		{
			this.errCode=erCode;
		}
		/// <summary>
		/// Takes in a string and sets it as the message of the exception.
		/// </summary>		
		protected CodeBasedException(string message):base(message)
		{			
		}	
		/// <summary>
		/// Takes in a string and exception object and sets it as the message/inner exception of the exception.
		/// </summary>		
		protected CodeBasedException(string message,System.Exception innerException):base(message,innerException)
		{			
		}	
		#endregion

		#region Properties Exposed
		/// <summary>
		/// Gets the ErrorCode.
		/// </summary>
		public int ErrorCode
		{
			get
			{
				return errCode;
			}
		}
		#endregion
	}
	#endregion
}
