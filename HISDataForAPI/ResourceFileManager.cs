/**************************************************************
* Application Name:ResourceManager
* File Name/Namespace Name:ResourceManager.cs/HISResource
* Author Name		:Robin
* Date of Creation	:20/05/2003
* File References	: 
* Components Used	:HISResource.ResourceCodes
* Calling Pages		:In all the classes in the application 
* Called Pages      :
* Version Number	:1.0 
* Purpose			:To parse messages from the Resource file.
*
* History:  Created By    Date        Version  Purpose
-----------------------------------------------------------------------------------------------------------------

											   
**************************************************************/
using System;
using System.Text;
using System.Resources;
using System.Reflection;
using HISException;
using System.Threading;
using System.Globalization;



namespace HISResource
{
	/// <summary>
	/// Parses messages from the Resource file (ResourceCodes.resx).
	/// Based on 'Singleton' pattern.
	/// </summary>
	public class ResourceFileManager
	{
		#region ctors
		private ResourceFileManager()//So that instantiation is not possible.
		{			
		}
		
		#endregion

		#region Public Methods
		/// <summary>
		/// Gets the message from the .resx file based on the message code.		
		/// </summary>		
		/*************************************************************
		* Method Name	: GetMessage 
		* Components	: HIS.Resource.ResourceCodes 
		* Tables Used	: N.A.
		* Create Date	: 14/07/2003
		* Author        : Robin
		* Change Control #    Date       Author         Description 
		* *************************************************************
		*				1)  14/07/2003  Robin      	Modified	
		***************************************************************/
		public static string GetMessage(int messageCode,params string[] placeHolderValues)		
		{			
			StringBuilder messageBuilder=new StringBuilder(150);
			string returnMessage="";			
			int messageListCounter=0;			
			System.Exception innerException=null;
			try
			{
				try
				{				
					Assembly resFile=Assembly.Load("HISData");
					ResourceManager resMgr = new ResourceManager("HISData.ResourceCodes",Assembly.GetExecutingAssembly());
					CultureInfo ci = Thread.CurrentThread.CurrentCulture;
					String strMessageCode = messageCode.ToString();
					messageBuilder.Append(resMgr.GetString(strMessageCode,ci));	
			
					//					System.Reflection.Assembly resFile = this.GetType().Assembly;										
					//					ResourceManager resMgr = new ResourceManager("ResourceCodes",resFile);
					//					messageBuilder.Append(resMgr.GetString(messageCode.ToString()));				
				}
				catch(System.Threading.ThreadInterruptedException exWithinComponent)
				{
					returnMessage="Error trying to fetch exception message from 'ResourceCodes.dll' for the code='"+messageCode.ToString()+"'.";								
					innerException=exWithinComponent;
				}
				catch(System.Threading.ThreadAbortException exWithinComponent)
				{
					returnMessage="Error trying to fetch exception message from 'ResourceCodes.dll' for the code='"+messageCode.ToString()+"'.";								
					innerException=exWithinComponent;
				}
				catch(System.Threading.ThreadStateException exWithinComponent)
				{
					returnMessage="Error trying to fetch exception message from 'ResourceCodes.dll' for the code='"+messageCode.ToString()+"'.";								
					innerException=exWithinComponent;
				}
				if(placeHolderValues!=null && innerException==null)
				{
					foreach(string placeHolderValue in placeHolderValues)
					{						
						messageBuilder.Replace("[PlaceHolder"+messageListCounter+"]",placeHolderValue);
						messageListCounter++;
					}
				}
				if(!returnMessage.Equals(""))
				{					
					throw ExceptionManager.HandleException(new ResourceParsingException(returnMessage,innerException),"ExceptionManager",MethodBase.GetCurrentMethod());					
				}
			}
			catch(CustomException ex)
			{
				throw ex;
			}

			catch(System.Threading.ThreadInterruptedException exWithinComponent)
			{
				throw ExceptionManager.HandleException(exWithinComponent,"ExceptionManager",MethodBase.GetCurrentMethod());					
			}
			catch(System.Threading.ThreadAbortException exWithinComponent)
			{
				throw ExceptionManager.HandleException(exWithinComponent,"ExceptionManager",MethodBase.GetCurrentMethod());					
			}
			catch(System.Threading.ThreadStateException exWithinComponent)
			{
				throw ExceptionManager.HandleException(exWithinComponent,"ExceptionManager",MethodBase.GetCurrentMethod());					
			}
//			catch(System.Exception exWithinComponent)
//			{
//				throw ExceptionManager.HandleException(exWithinComponent,"ExceptionManager",MethodBase.GetCurrentMethod());					
//			}
			return messageBuilder.ToString();			
		}		
		#endregion

		#region Custom Exceptions
		/// <summary>
		/// ResourceParsingException Exception Class gets raised when the parsing of a code
		/// from the resource file causes an exception.		
		/// </summary>
		/*************************************************************
		* Method Name	: class/ResourceParsingException 
		* Components	: 
		* Tables Used	: N.A.
		* Create Date	: 30/05/2003
		* Author        : Robin
		* Change Control #    Date       Author         Description 
		* *************************************************************
		*				1)   30/05/2003  Robin      		
		***************************************************************/
		private class ResourceParsingException:CodeBasedException
		{
			/// <summary>
			/// Message='Dynamic'
			/// </summary>		
			public ResourceParsingException(string message,System.Exception innerException):base(message,innerException)
			{
			}			
		}  
		#endregion
	}
}
