/**************************************************************
* Application Name:DataHelper
* File Name/Namespace Name:DataHelper.cs/HISDataAccess
* Author Name		:GMSBU
* Date of Creation	:18/05/2003
* File References	: 
* Components Used	:ABDataInfo.Register,AB.ITAM.HISExceptionManager
* Calling Pages		:In all the DAL's in the application 
* Called Pages      :
* Version Number	:1.0 
* Purpose			:To create a common pass-through layer for accessing either SQL Server or Oracle or any other database/csv exposing an Oledb Provider.
*					 and to create public methods by which user does not have to repeat database related specifics.
*                    The component on the whole is based on 'Singleton' pattern, while the intrinsic working utilizes the Abstract-Factory pattern design.
*					
* History:  Created By    Date        Version  Purpose
-----------------------------------------------------------------------------------------------------------------
		  
		      Integrating with exception manager
		      Usage testing pulled up null pointer exception in RunSP method.
		      Integrated with AB's component to fetch connection string from the registry.
		      With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.
		       This is a design of ours purely, and strives to incorporate best practises from SqlHelper/OledbHelper/OracleHelper, provided by MS,
			   as well as using a totally OO approach.	
			   Initially, as the design would support only 2 providers as against a variety of providers,
			   the switch/case(procedural) methodology was used, as type determination happens at compile time,
			   it is slightly faster than runtime-invocation(i.e using CreateInstance()).
			   Now as the no. of providers are increasing, it only makes sense to write a generic class,
			   based on OO techniques, which can be made to work for a variety of providers, if need be,
			   with minimalist tweaking.Though the better offerings of the earlier code are still available,
			   as on the whole the class is still static.For example,the ConnectionString property will be accessible
			   across BAL's,DAL's and the Web Application, as the DataHelper loads in to the same appdomain 
			   as that of the executable, as used to happen before.
**************************************************************/
using System;
using System.Collections;
using System.Data;
using System.Data.OleDb;
using System.Data.OracleClient;
using System.Data.SqlClient;
using System.Reflection;
using HISException;
using System.Configuration;
using System.IO ;
using System.Runtime.InteropServices;
[assembly: ComVisible(true)]



namespace HISDataAccess
{	
	#region Enums
	/// <summary>
	/// Used to indicated which database server type is being used.
	/// </summary>
	/*************************************************************
	* Method Name	: enum/ProviderType
	* Components	: 
	* Tables Used	: N.A.
	* Create Date	: 19/05/2003
	* Author        : Robin 
	* Change Control #    Date       Author         Description 
	* *************************************************************
	*				1)   19/05/2003  Robin       		
	***************************************************************/
	public enum ProviderType 
	{ 		
		/// <summary>Provider Not Set</summary>
		NotSet=0,
		/// <summary>OleDb Provider for generic data access</summary>
		OleDb=1, 
		/// <summary>SQL Server Provider</summary>
		Sql=2,
		/// <summary>Microsoft's Native Provider for Oracle</summary>
		Oracle=3
		
	}	
	#endregion
	
	#region DataHelper Class
	/// <summary>
	/// To create a common pass-through layer for accessing either SQL Server or Oracle or any other database/csv exposing an Oledb Provider,
	///	and to provide an interface, using which the user does not have to repeat database related specifics.
	/// The component on the whole is based on 'Singleton' pattern, while the intrinsic working utilizes the Abstract-Factory pattern design.
	/// All methods and properties are static.
	/// Provides 3 overloads for all public methods:
	/// a)User sets the connectionString in the Global.asax and subsequently uses this cached string.
	/// b)User passes his connection string and provider each time (if there is a need to use something other than the cache).
	/// c)User passes the connection/transaction object directly.
	/// Note: Transaction overloads are not provided for purely data-read operations.Can be added-on.
	/// </summary>
	/*************************************************************
	* Method Name	: class/DataHelper
	* Components	: 
	* Tables Used	: N.A.
	* Create Date	: 18/05/2003
	* Author        : Robin
	* Change Control #    Date       Author         Description 
	* *************************************************************
	*				1)   18/05/2003  Robin      		
	***************************************************************/

	public class DataHelper
		{
        public static string MDBSType;
			#region Sub-classes and Structs

			#region Activator Interface and Sub-classes
			/// <summary>
			/// The interface that defines the implementation of custom early-bound activators.		
			/// </summary>		
			private interface IActivator
			{
				IDbConnection CreateConnection();
				IDbCommand CreateCommand();
				IDbDataAdapter CreateDataAdapter();
				IDbDataParameter CreateDataParameter();
			}
			/// <summary>
			/// A static Activator class that is used solely for creating and returning the appropriate Activator.
			/// </summary>	
			private class Activator
			{
				#region ctors
				private Activator(){}//So that instantiation is not possible.
				#endregion

				#region Public Methods
				/// <summary>
				/// It is a static method used solely for creating and returning the appropriate Activator.
				/// based on the provider.
				/// </summary>
				/*************************************************************
					* Method Name	: CreateInstance
					* Components	: 
					* Tables Used	: N.A.
					* Create Date	: 06/10/2003
					* Author        : Robin 
					* Change Control #    Date       Author         Description 
					* *************************************************************
					*				1)   06/10/2003  Robin       		
					***************************************************************/	
				public static IActivator CreateInstance(ProviderType provider)
				{
					IActivator activator=null;
					switch(provider)
					{
						case ProviderType.OleDb:
							activator=new OledbActivator();
							break;					
						case ProviderType.Oracle:
							activator=new OracleActivator();
							break;
						case ProviderType.Sql:
							activator=new SqlActivator();
							break;
						case ProviderType.NotSet:
							throw new ProviderNotSetException();
						default:
							throw new ProviderNotSetException();
					}
					return activator;
				}
				#endregion
			}		
			/// <summary>
			/// Represents an OledbActivator class that inherits from IActivator.
			/// </summary>	
			private class OledbActivator:IActivator
			{
				#region Public Methods
				/// <summary>
				/// Creates a OleDbConnection.
				/// </summary>
				/*************************************************************
					* Method Name	: CreateConnection
					* Components	: 
					* Tables Used	: N.A.
					* Create Date	: 06/10/2003
					* Author        : Robin 
					* Change Control #    Date       Author         Description 
					* *************************************************************
					*				1)   06/10/2003  Robin       		
					***************************************************************/
				public IDbConnection CreateConnection()
				{
					return new OleDbConnection();
				}
				/// <summary>
				/// Creates a OleDbCommand.
				/// </summary>
				/*************************************************************
					* Method Name	: CreateCommand
					* Components	: 
					* Tables Used	: N.A.
					* Create Date	: 06/10/2003
					* Author        : Robin 
					* Change Control #    Date       Author         Description 
					* *************************************************************
					*				1)   06/10/2003  Robin       		
					***************************************************************/
				public IDbCommand CreateCommand()
				{
					return new OleDbCommand();
				}
				/// <summary>
				/// Creates a OleDbDataAdapter.
				/// </summary>
				/*************************************************************
					* Method Name	: CreateDataAdapter
					* Components	: 
					* Tables Used	: N.A.
					* Create Date	: 06/10/2003
					* Author        : Robin 
					* Change Control #    Date       Author         Description 
					* *************************************************************
					*				1)   06/10/2003  Robin       		
					***************************************************************/
				public IDbDataAdapter CreateDataAdapter()
				{
					return new OleDbDataAdapter();
				}
				/// <summary>
				/// Creates a OleDbParameter.
				/// </summary>
				/*************************************************************
					* Method Name	: CreateDataParameter
					* Components	: 
					* Tables Used	: N.A.
					* Create Date	: 06/10/2003
					* Author        : Robin 
					* Change Control #    Date       Author         Description 
					* *************************************************************
					*				1)   06/10/2003  Robin       		
					***************************************************************/
				public IDbDataParameter CreateDataParameter()
				{
					return new OleDbParameter();
				}
				#endregion
			}
			/// <summary>
			/// Represents an SqlActivator class that inherits from IActivator.
			/// </summary>	
			private class SqlActivator:IActivator
			{
				#region Public Methods
				/// <summary>
				/// Creates a SqlConnection.
				/// </summary>
				/*************************************************************
					* Method Name	: CreateConnection
					* Components	: 
					* Tables Used	: N.A.
					* Create Date	: 06/10/2003
					* Author        : Robin 
					* Change Control #    Date       Author         Description 
					* *************************************************************
					*				1)   06/10/2003  Robin       		
					***************************************************************/
				public IDbConnection CreateConnection()
				{
					return new SqlConnection();
				}
				/// <summary>
				/// Creates a SqlCommand.
				/// </summary>
				/*************************************************************
					* Method Name	: CreateCommand
					* Components	: 
					* Tables Used	: N.A.
					* Create Date	: 06/10/2003
					* Author        : Robin 
					* Change Control #    Date       Author         Description 
					* *************************************************************
					*				1)   06/10/2003  Robin       		
					***************************************************************/
				public IDbCommand CreateCommand()
				{
					return new SqlCommand();
				}
				/// <summary>
				/// Creates a SqlDataAdapter.
				/// </summary>
				/*************************************************************
					* Method Name	: CreateDataAdapter
					* Components	: 
					* Tables Used	: N.A.
					* Create Date	: 06/10/2003
					* Author        : Robin 
					* Change Control #    Date       Author         Description 
					* *************************************************************
					*				1)   06/10/2003  Robin       		
					***************************************************************/
				public IDbDataAdapter CreateDataAdapter()
				{
					return new SqlDataAdapter();
				}
				/// <summary>
				/// Creates a SqlParameter.
				/// </summary>
				/*************************************************************
					* Method Name	: CreateDataParameter
					* Components	: 
					* Tables Used	: N.A.
					* Create Date	: 06/10/2003
					* Author        : Robin 
					* Change Control #    Date       Author         Description 
					* *************************************************************
					*				1)   06/10/2003  Robin       		
					***************************************************************/
				public IDbDataParameter CreateDataParameter()
				{
					return new SqlParameter();
				}
				#endregion
			}		
			/// <summary>
			/// Represents an OracleActivator class that inherits from IActivator.
			/// </summary>		
			private class OracleActivator:IActivator
			{
				#region Public Methods
				/// <summary>
				/// Creates a OracleConnection.
				/// </summary>
				/*************************************************************
					* Method Name	: CreateConnection
					* Components	: 
					* Tables Used	: N.A.
					* Create Date	: 06/10/2003
					* Author        : Robin 
					* Change Control #    Date       Author         Description 
					* *************************************************************
					*				1)   06/10/2003  Robin       		
					***************************************************************/
				public IDbConnection CreateConnection()
				{
					return new OracleConnection();
				}
				/// <summary>
				/// Creates a OracleCommand.
				/// </summary>
				/*************************************************************
					* Method Name	: CreateCommand
					* Components	: 
					* Tables Used	: N.A.
					* Create Date	: 06/10/2003
					* Author        : Robin 
					* Change Control #    Date       Author         Description 
					* *************************************************************
					*				1)   06/10/2003  Robin       		
					***************************************************************/
				public IDbCommand CreateCommand()
				{
					return new OracleCommand();
				}
				/// <summary>
				/// Creates a OracleDataAdapter.
				/// </summary>
				/*************************************************************
					* Method Name	: CreateDataAdapter
					* Components	: 
					* Tables Used	: N.A.
					* Create Date	: 06/10/2003
					* Author        : Robin 
					* Change Control #    Date       Author         Description 
					* *************************************************************
					*				1)   06/10/2003  Robin       		
					***************************************************************/
				public IDbDataAdapter CreateDataAdapter()
				{
					return new OracleDataAdapter();
				}
				/// <summary>
				/// Creates a OracleParameter.
				/// </summary>
				/*************************************************************
					* Method Name	: CreateDataParameter
					* Components	: 
					* Tables Used	: N.A.
					* Create Date	: 06/10/2003
					* Author        : Robin 
					* Change Control #    Date       Author         Description 
					* *************************************************************
					*				1)   06/10/2003  Robin       		
					***************************************************************/
				public IDbDataParameter CreateDataParameter()
				{
					return new OracleParameter();
				}
				#endregion
			}
		#endregion

			#endregion

			#region ctors

            //private DataHelper() // Made private so that instantiation is not possible.
            //{
            //    //
            //    // TODO: Add constructor logic here
            //    //


            //}

            //private DataHelper(MethodBase objMethod)
            //{
            //}
            static DataHelper()
            {
                try
                {
                    //StreamReader str;
                    //str = File.OpenText(@"constr.txt");
                    //connString = str.ReadToEnd();
                    //str.Close();
                    MDBSType = "";
                    connString = "";
                    provider = ProviderType.Sql;
                    intConnectionTimeOut = 30;
                }
                catch (Exception e)
                {
                    throw e;
                }
                
            }

            //public static void SetNewConnectionString()
            //{				
            //    try
            //    {
            //        StreamReader str;
            //        str = File.OpenText(@"constr.txt");				
            //        connString = str.ReadToEnd();
            //        str.Close();				
            //    }
            //    catch(System.IO.FileNotFoundException ex)
            //    {
            //        throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());					
            //    }
            //}
			

			#endregion		
		
			#region Private Methods and Variables			
	
			private static string connString;

            private static ProviderType provider;
            private static int intConnectionTimeOut;
		
			/// <summary>
			/// This enum is used to indicate whether the connection was provided by the caller, or created by DataHelper.	
			/// </summary>
			/*************************************************************
			* Method Name	: enum/ConnectionOwnership
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 22/09/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   22/09/2003  Robin      		
			***************************************************************/
			private enum ConnectionOwnership
			{
				/// <summary>Connection is owned and managed by DataHelper</summary>
				Internal, 
				/// <summary>Connection is owned and managed by the caller</summary>
				External
			}
			/// <summary>
			/// This enum is used to set the CommandBehaviour of the DataReader.	
			/// </summary>
			/*************************************************************
			* Method Name	: enum/CommandState
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 22/09/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   22/09/2003  Robin      		
			***************************************************************/
			private enum CommandState
			{
				/// <summary>Connection is to be left open even after the DataReader is closed</summary>
				LeaveOpen, 
				/// <summary>Connection is to be closed as soon as the DataReader is closed</summary>
				Close
			}
			/// <summary>
			/// This method is used to attach array of Parameters to a Command.		
			/// This method will assign a value of DbNull to any parameter with a direction of
			/// InputOutput and a value of null.  		/// 
			/// This behavior will prevent default values from being used, but
			/// this will be the less common case than an intended pure output parameter (derived as InputOutput)
			/// where the user provided no input value.
			/// </summary>		
			private static void AttachParameters(IDbCommand command, IDbDataParameter[] commandParameters)
			{			
				if(commandParameters!=null )
				{
					foreach(IDbDataParameter p in commandParameters)
					{
						if(p!=null )
						{
							// Check for derived output value with no value assigned
							if(p.Direction==ParameterDirection.InputOutput||p.Direction==ParameterDirection.Input)							
							{
								if(p.Value==null)
								{
									p.Value = DBNull.Value;
								}
								else
								{
									//If datatype is String,then replace spaces with DBNull.Value.
									if(p.Value.GetType().FullName=="System.String")	
									{					
										if(p.Value.ToString()=="")
										{
											#region "Commented Code by srikanth for Empty string"
										 //Commented on 2-feb-2005 by srikanth because when front end is passing 
									    //string as empty codes datalayer is assigning it as dbnull
										 //p.Value=DBNull.Value;
											#endregion "Commented Code by srikanth for Empty string"
										}
									}
								}
							}						
							command.Parameters.Add(p);
						}
					}
				}
			}
			/// <summary>
			/// This method opens (if necessary) and assigns a connection, transaction, command type and parameters 
			/// to the provided command
			/// </summary>		
			private static void PrepareCommand(IDbCommand command,IDbConnection connection,IDbTransaction transaction,CommandType commandType,string commandText,IDbDataParameter[] commandParameters,out bool mustCloseConnection)
			{
				//If command or commandText object are null, do not proceed.
				if(command==null) throw ExceptionManager.HandleException(new NullCommandException(),"DataHelper",MethodInfo.GetCurrentMethod());
				if(commandText==null || commandText.Trim().Equals("")) throw ExceptionManager.HandleException(new NullCommandTextException(),"DataHelper",MethodInfo.GetCurrentMethod());
               
				if(File.Exists("d:\\parameterss.txt")) 
				{
					FileStream fs=new FileStream("d:\\parameterss.txt",FileMode.Append);
					StreamWriter Fw=new StreamWriter(fs);

					string str="";
					str = commandText.ToString();
					for(int i=0;i<commandParameters.Length;i++)
					{
						str +=  "    "  + commandParameters[i].ParameterName + "  = " + commandParameters[i].Value + ",";
					}

					Fw.WriteLine(str);
					Fw.Flush();
					Fw.Flush();
					Fw.WriteLine("-----------------------------------------------");
					Fw.Close();
					fs.Close();
				}
//				

				// Attach the command parameters if they are provided
				if(commandParameters!=null)
				{
					AttachParameters(command,commandParameters);
				}

				// If the provided connection is not open, we will open it
				if(connection.State!=ConnectionState.Open)
				{
					mustCloseConnection=true;
					connection.Open();
				}
				else
				{
					mustCloseConnection=false;
				}

				// Associate the connection with the command
				command.Connection=connection;

				// Set the command text (stored procedure name or SQL statement)
				command.CommandText=commandText;

				// If we were provided a transaction, assign it
				if(transaction!=null)
				{	
					if(transaction.Connection==null)
					{
						throw ExceptionManager.HandleException(new TransactionExpiredException(),"DataHelper",MethodInfo.GetCurrentMethod());					
					}
					command.Transaction=transaction;
				} 
				// Set the command type
				command.CommandType=commandType;
				return;
			}

			/// <summary>
			/// Execute a Command (that returns no resultset) against the specified Connection 
			/// using the provided parameters.
			/// </summary>		
			private static int ExecuteNonQuery(IActivator activator,IDbConnection connection,CommandType commandType,string commandText,ConnectionOwnership connectionOwnership,params IDbDataParameter[] commandParameters)
			{
				//If connection object is null then do not proceed.
				if(connection==null) throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());

				IDbCommand cmd=null;
				int retval=0;
				bool mustCloseConnection = false;			
				try
				{
					//Create a command object.(Using default ctor as other ctors take more time.)		
					cmd=activator.CreateCommand();
					cmd.CommandTimeout = intConnectionTimeOut;
					//Prepare the command object by adding the parameters.	
					PrepareCommand(cmd, connection,null,commandType,commandText,commandParameters,out mustCloseConnection);    		
					// Finally, execute the command
					retval=cmd.ExecuteNonQuery();    		
					// Detach the SqlParameters from the command object, so they can be used again
					cmd.Parameters.Clear();
				}
				catch(CustomException ex)
				{
					throw ex;
				}				
				catch(System.Data.DataException ex)
				{
					throw ex;
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ex;
				}
				finally
				{
					if(mustCloseConnection && (connectionOwnership==ConnectionOwnership.Internal))
					{
						connection.Close();
					}
				}
				return retval;
			}
			/// <summary>
			/// Execute a Command (that returns no resultset) against the specified Transaction
			/// using the provided parameters.
			/// </summary>		
			private static int ExecuteNonQuery(IActivator activator,IDbTransaction transaction,CommandType commandType,string commandText,ConnectionOwnership connectionOwnership,params IDbDataParameter[] commandParameters)
			{
				//If transaction object is null then do not proceed.
				if(transaction==null) throw ExceptionManager.HandleException(new NullTransactionException(),"DataHelper",MethodInfo.GetCurrentMethod());

				IDbCommand cmd=null;
				int retval=0;
				bool mustCloseConnection = false;			
				try
				{
					//Create a command object.(Using default ctor as other ctors take more time.)
					cmd=activator.CreateCommand();
					cmd.CommandTimeout = intConnectionTimeOut;
					//Prepare the command object by adding the parameters.	
					PrepareCommand(cmd,transaction.Connection,transaction,commandType,commandText,commandParameters,out mustCloseConnection);    			
					// Finally, execute the command
					retval=cmd.ExecuteNonQuery();	    			
					// Detach the SqlParameters from the command object, so they can be used again
					cmd.Parameters.Clear();
				}
				catch(CustomException ex)
				{
					throw ex;
				}		
				catch(System.Data.DataException ex)
				{
					throw ex;
				}
				finally
				{
					if(mustCloseConnection && (connectionOwnership==ConnectionOwnership.Internal))
					{
						transaction.Connection.Close();
					}
				}
				return retval;
			}		
			/// <summary>
			/// Execute a Command (that returns a resultset) against the specified Connection 
			/// using the provided parameters.
			/// </summary>	
			private static DataSet ExecuteDataset(IActivator activator,IDbConnection connection,CommandType commandType,string commandText,ConnectionOwnership connectionOwnership,params IDbDataParameter[] commandParameters)
			{
				//If connection object is null then do not proceed.
				if(connection==null) throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());

				IDbCommand cmd=null;	
				IDbDataAdapter da=null;
				DataSet ds=null;
				bool mustCloseConnection = false;			
				try
				{
					//Create a command object.(Using default ctor as other ctors take more time.)
					cmd=activator.CreateCommand();
					cmd.CommandTimeout = intConnectionTimeOut;
					//Prepare the command object by adding the parameters.	
					PrepareCommand(cmd,connection,null,commandType,commandText,commandParameters,out mustCloseConnection);
					// Create the DataAdapter(Using default ctor as other ctors take more time.)
					da=activator.CreateDataAdapter();
					da.SelectCommand=cmd;
					//Create a new dataset.
					ds=new DataSet();
					// Fill the DataSet using default values for DataTable names, etc
					da.Fill(ds);	
					// Detach the Parameters from the command object, so they can be used again
					cmd.Parameters.Clear();		
				}	
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ex;
				}
				finally
				{
					if(mustCloseConnection && (connectionOwnership==ConnectionOwnership.Internal))
					{
						connection.Close();
					}
				}			
				// Return the dataset
				return ds;			
			}
			/// <summary>
			/// Execute a Command (that returns a 1x1 resultset) against the specified Connection 
			/// using the provided parameters.
			/// </summary>		
			private static object ExecuteScalar(IActivator activator,IDbConnection connection,CommandType commandType,string commandText,ConnectionOwnership connectionOwnership,params IDbDataParameter[] commandParameters)
			{
				//If connection object is null then do not proceed.
				if(connection==null) throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());

				IDbCommand cmd=null;	
				object retval=null;
				bool mustCloseConnection = false;			
				try
				{
					//Create a command object.(Using default ctor as other ctors take more time.)
					cmd=activator.CreateCommand();
					cmd.CommandTimeout = intConnectionTimeOut;
					//Prepare the command object by adding the parameters.	
					PrepareCommand(cmd,connection,null,commandType,commandText,commandParameters,out mustCloseConnection );    			
					// Execute the command and return the results
					retval=cmd.ExecuteScalar();    			
					// Detach the SqlParameters from the command object, so they can be used again
					cmd.Parameters.Clear();
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ex;
				}
				finally
				{
					if(mustCloseConnection && (connectionOwnership==ConnectionOwnership.Internal))
					{
						connection.Close();
					}
				}			
				return retval;
			}
			/// <summary>
			/// Create and prepare a Command, and call ExecuteReader with the appropriate CommandBehavior.
			/// </summary>		
			private static IDataReader ExecuteReader(IActivator activator,IDbConnection connection,CommandType commandType,string commandText,ConnectionOwnership connectionOwnership,CommandState commandState, IDbDataParameter[] commandParameters)
			{	
				//If connection object is null then do not proceed.
				if(connection==null) throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());

				IDbCommand cmd=null;
				IDataReader dataReader=null;
				//Commented out due to error described below(above its usage).
				//bool canClear = true;
				bool mustCloseConnection = false;			
				try
				{
					//Create a command object.(Using default ctor as other ctors take more time.)
					cmd=activator.CreateCommand();
					cmd.CommandTimeout = intConnectionTimeOut;
					//Prepare the command object by adding the parameters.	
					PrepareCommand(cmd,connection,null,commandType,commandText,commandParameters,out mustCloseConnection);						
					// Call ExecuteReader	
					if(commandState==CommandState.Close)
					{
						dataReader=cmd.ExecuteReader(CommandBehavior.CloseConnection);		
					}
					else
					{
						dataReader=cmd.ExecuteReader();
					}
					/* The following does not seem to be behaving consistently with the OLEDB provider,
					 * as it raises the error 'The OLEDB Command is currently busy open,fetching.',
					 * though the check is made for not clearing the parameter colelction if any InputOutput,Output
					 * or ReturnValue parameter exists.
					 * This performs consistently with the SQL as well as ORACLE provider.				 				
					foreach(IDbDataParameter commandParameter in cmd.Parameters)
					{
						if(commandParameter.Direction!=ParameterDirection.Input)
						{
							canClear = false;
						}
					}            
					if(canClear)
					{
						cmd.Parameters.Clear();
					}
					*/								
				}
				catch(CustomException ex)
				{
					//We should try to close the connection only in case of exception..
					//Hence no finally{} block
					if(mustCloseConnection && (connectionOwnership==ConnectionOwnership.Internal))
					{			
						connection.Close();					
					}
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					//We should try to close the connection only in case of exception..
					//Hence no finally{} block
					if(mustCloseConnection && (connectionOwnership==ConnectionOwnership.Internal))
					{			
						connection.Close();					
					}
					throw ex;
				}			
				return dataReader;
			}
			/// <summary>
			/// Returns true if provider is set to either OleDb,SQL or Oracle.
			/// </summary>
			/*************************************************************
			* Method Name	: IsProviderSet
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 19/05/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   19/05/2003  Robin      		
			***************************************************************/
			private static bool IsProviderSet()
			{			
				if(provider==ProviderType.NotSet)
				{
					return false;
				}			
				return true;
			}		
		
			/// <summary>
			/// Returns an arraylist whose contents are items of datatype 'object'.
			/// Row wise data can be accessed this way:foreach(object[] obj in al){}
			/// </summary>
			/*************************************************************
			* Method Name	: GetRows
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 19/05/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   19/05/2003  Robin      		
			***************************************************************/
			private static ArrayList GetRows(IDataReader dr)
			{
				ArrayList Rows = new ArrayList();
				object[] OneRow;
				while (dr.Read())
				{
					OneRow = (object[])Array.CreateInstance(typeof(object),dr.FieldCount);
					dr.GetValues(OneRow);
					Rows.Add(OneRow);
				}	
				return Rows;
			}
			/// <summary>
			/// Overload of 'GetRows' provides faster performance as field count
			/// is specified hence object[] can be intialized without using 
			/// reflection for runtime invocation.
			/// Returns an arraylist whose contents are items of datatype 'object'.
			/// Row wise data can be accessed this way:foreach(object[] obj in al){}
			/// </summary>
			/*************************************************************
			* Method Name	: GetRows overload
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 19/05/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   19/05/2003  Robin      		
			***************************************************************/
			private static ArrayList GetRows(IDataReader dr,int fieldCount)
			{			
				ArrayList Rows = new ArrayList();
				object[] OneRow;
				while(dr.Read())
				{
					OneRow = new object[fieldCount];
					dr.GetValues(OneRow);
					Rows.Add(OneRow);
				}	
				return Rows;
			}
		
			/// <summary>
			/// Retrieves only first and second columns in a select sql, and builds a hashtable.
			/// They should be typically used with sql's that return key-value pair.
			/// The first column is used as the key, while the second is used as the value.	
			/// </summary>
			/*************************************************************
			* Method Name	: GetHashTable
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 06/10/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   06/10/2003  Robin      		
			***************************************************************/
			private static Hashtable GetHashTable(IDataReader dr)
			{
				Hashtable hashTable = new Hashtable();			
				while(dr.Read())
				{				
					hashTable.Add(dr.GetValue(0),dr.GetValue(1));				
				}	
				return hashTable;
			}		
			#endregion		
			
			#region Properties Exposed

			/// <summary>
			/// Static property, used to get and set the connection string.		
			/// </summary>
			/*************************************************************
			* Method Name	: Property/ConnectionString 
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 19/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   19/05/2003  Robin       		
			***************************************************************/
			public static string ConnectionString
			{
				get
				{
					return connString;
				}
				//			set
				//			{	
				//				if(value==null || value.Trim().Equals(""))
				//				{
				//					throw ExceptionManager.HandleException(new SetConnectionToNullException(),"DataHelper",MethodBase.GetCurrentMethod());
				//				}
				//				else
				//				{
				//					if(!IsConnStringCached())
				//					{
				//						connString=value;
				//					}
				//					else
				//					{
				//						throw ExceptionManager.HandleException(new ConnStrSetOnceException(),"DataHelper",MethodBase.GetCurrentMethod());					
				//					}
				//				}
				//			}
			}
			/// <summary>
			/// Static property, used to get and set the provider type.		
			/// </summary>
			/*************************************************************
			* Method Name	: Property/Provider 
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 19/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   19/05/2003  Robin       		
			***************************************************************/
			public static ProviderType Provider
			{
				get
				{
					return provider;
				}
				set
				{
					provider=value;
				}
			}

		/// <summary>
		/// This property is set to connection time out period for command object. 
		/// By default it is 30. 
		/// </summary>
		public static int ConnectionTimeOut
		{
			get { return intConnectionTimeOut; }
			set { intConnectionTimeOut = (int)value; }
		}

			#endregion Properties Exposed
		
			#region Public Methods

			#region IDbConnection methods
			/// <summary>
			/// Used for Creating and returning a connection object based on the provider specified.	
			/// </summary>
			/*************************************************************
			* Method Name	: CreateConnection
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 24/09/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   24/09/2003  Robin       	
			***************************************************************/
			public static IDbConnection CreateConnection(int WorkStationID, string DataBaseType)
			{			
				IDbConnection connection=null;
				try
				{
                    GetConnectionString(WorkStationID,DataBaseType);
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}

				
					connection=Activator.CreateInstance(provider).CreateConnection();
					connection.ConnectionString=connString;
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}		
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				return connection;
			}
			/// <summary>
			/// Overloaded;Used for Creating and returning a connection object based on the provider
			/// and connection string specified.	
			/// </summary>
			/*************************************************************
			* Method Name	: CreateConnection
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 24/09/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   24/09/2003  Robin       	
			***************************************************************/
			public static IDbConnection CreateConnection(ProviderType provider,int WorkStationID, string DataBaseType)
			{			
				//If caller has not set the provider.
				IDbConnection connection=null;
				try
				{
                     GetConnectionString(WorkStationID,DataBaseType);
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
					connection=Activator.CreateInstance(provider).CreateConnection();
					connection.ConnectionString=connString;
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());	
				}
				return connection;
			}
	
			//		public static IDbConnection CreateConnection(ProviderType provider,string connectionString)
			//		{			
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//Connection String cannot be null or spaces.
			//			if(connectionString==null || connectionString.Trim().Equals("")) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
			//			}
			//
			//			IDbConnection connection = null;
			//			try
			//			{
			//				connection=Activator.CreateInstance(provider).CreateConnection();
			//				connection.ConnectionString=connectionString;
			//			}
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
			//			}			
			//			return connection;
			//		}
			#endregion
		
			#region IDbCommand methods
			/// <summary>
			/// Used for Creating and returning a command object based on the provider specified.	
			/// </summary>
			/*************************************************************
			* Method Name	: CreateCommand
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 24/09/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   24/09/2003  Robin       	
			***************************************************************/
			public static IDbCommand CreateCommand()
			{			
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}

				IDbCommand command=null;
				try
				{
					command=Activator.CreateInstance(provider).CreateCommand();
					command.Connection = Activator.CreateInstance(provider).CreateConnection();
					command.Connection.ConnectionString=connString;
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}			
				return command;
			}
		/// <summary>
		/// Used for Creating and returning a command object based on the provider specified.	
		/// </summary>
			public static IDbCommand CreateCommand(ProviderType provider)
			{			
				IDbCommand command;
				try
				{
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
					command=Activator.CreateInstance(provider).CreateCommand();
					command.Connection = Activator.CreateInstance(provider).CreateConnection();
					command.Connection.ConnectionString=connString;
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}			
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				return command;
			}
			/// <summary>
			/// Overloaded;Used for Creating and returning a command object based on the provider
			/// and commandText string specified.	
			/// </summary>
			/*************************************************************
			* Method Name	: CreateCommand
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 24/09/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   24/09/2003  Robin       	
			***************************************************************/
			public static IDbCommand CreateCommand(ProviderType provider,string commandText)
			{
				IDbCommand	command ;
				try
				{
					command = null;
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				//commandText String cannot be null or spaces.
				if(commandText==null || commandText.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullCommandTextException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
			
				
					command=Activator.CreateInstance(provider).CreateCommand();
					command.CommandText=commandText;
					command.Connection = Activator.CreateInstance(provider).CreateConnection();
					command.Connection.ConnectionString=connString;
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}		
				return command;
			}


			public static IDbCommand CreateCommand(ProviderType provider,string commandText,IDbTransaction trans)
			{
				IDbCommand	command = null;
				try
				{
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				//commandText String cannot be null or spaces.
				if(commandText==null || commandText.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullCommandTextException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				
					command=Activator.CreateInstance(provider).CreateCommand();
					command.CommandText=commandText;
					command.Connection = Activator.CreateInstance(provider).CreateConnection();
					command.Transaction=trans;
					command.Connection.ConnectionString=connString;
				
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}			
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				return command;
			}

			#region "Commented Method"
			//		/// <summary>
			//		/// Overloaded;Used for Creating and returning a command object based on the provider,
			//		/// and commandText string and connection object specified.	
			//		/// </summary>
			//		/*************************************************************
			//		* Method Name	: CreateCommand
			//		* Components	: AB.ITAM.Infrastructure.ExceptionManager
			//		* Tables Used	: N.A.
			//		* Create Date	: 24/09/2003
			//		* Author        : Robin 
			//		* Change Control #    Date       Author         Description 
			//		* *************************************************************
			//		*				1)   24/09/2003  Robin       	
			//		***************************************************************/
			//		
			//		public static IDbCommand CreateCommand(ProviderType provider,string commandText,IDbConnection connection)
			//		{
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//commandText String cannot be null or spaces.
			//			if(commandText==null || commandText.Trim().Equals("")) 
			//			{
			//				throw ExceptionManager.HandleException(new NullCommandTextException(),"DataHelper",MethodBase.GetCurrentMethod());
			//			}			
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//
			//			IDbCommand command=null;
			//			try
			//			{
			//				command=Activator.CreateInstance(provider).CreateCommand();
			//				command.CommandText=commandText;
			//				command.Connection=connection;
			//			}
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
			//			}			
			//			return command;
			//		}
			//		/// <summary>
			//		/// Overloaded;Used for Creating and returning a command object based on the provider,
			//		/// commandText string, connection object and transaction object specified.	
			//		/// </summary>
			//		/*************************************************************
			//		* Method Name	: CreateCommand
			//		* Components	: AB.ITAM.Infrastructure.ExceptionManager
			//		* Tables Used	: N.A.
			//		* Create Date	: 24/09/2003
			//		* Author        : Robin 
			//		* Change Control #    Date       Author         Description 
			//		* *************************************************************
			//		*				1)   24/09/2003  Robin       	
			//		***************************************************************/
			//		public static IDbCommand CreateCommand(ProviderType provider,string commandText,IDbConnection connection,IDbTransaction transaction)
			//		{
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//commandText String cannot be null or spaces.
			//			if(commandText==null || commandText.Trim().Equals("")) 
			//			{
			//				throw ExceptionManager.HandleException(new NullCommandTextException(),"DataHelper",MethodBase.GetCurrentMethod());
			//			}			
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not sent a valid transaction.
			//			if(transaction==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullTransactionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//
			//			IDbCommand command=null;			
			//			try
			//			{
			//				command=Activator.CreateInstance(provider).CreateCommand();
			//				command.CommandText=commandText;
			//				command.Connection=connection;
			//				command.Transaction=transaction;
			//			}			
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
			//			}		
			//			return command;
			//		}
			#endregion "Commented Method"
			#endregion
		
			#region IDbDataAdapter methods
			/// <summary>
			/// Used for Creating and returning a data adapter object based on the provider,specified.	
			/// </summary>
			/*************************************************************
			* Method Name	: CreateDataAdapter
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 24/09/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   24/09/2003  Robin       	
			***************************************************************/
			public static IDbDataAdapter CreateDataAdapter()
			{
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}

				IDbDataAdapter dataAdapter=null;
				try
				{
					dataAdapter=Activator.CreateInstance(provider).CreateDataAdapter();
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				return dataAdapter;
			}
			public static IDbDataAdapter CreateDataAdapter(ProviderType provider)
			{
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}

				IDbDataAdapter dataAdapter=null;
				try
				{
					dataAdapter=Activator.CreateInstance(provider).CreateDataAdapter();
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());	
				}
				
				return dataAdapter;
			}
			/// <summary>
			/// Overloaded;Used for Creating and returning a data adapter object based on the provider and
			/// command object specified.	
			/// </summary>
			/*************************************************************
			* Method Name	: CreateDataAdapter
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 24/09/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   24/09/2003  Robin       	
			***************************************************************/
			public static IDbDataAdapter CreateDataAdapter(ProviderType provider,IDbCommand command)
			{
				IDbDataAdapter dataAdapter=null;
				try
				{
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				//command object cannot be null.
				if(command==null) 
				{
					throw ExceptionManager.HandleException(new NullCommandException(),"DataHelper",MethodBase.GetCurrentMethod());
				}

				
					dataAdapter=Activator.CreateInstance(provider).CreateDataAdapter();
					dataAdapter.SelectCommand=command;
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				return dataAdapter;
			}
	
			public static IDbDataAdapter CreateDataAdapter(ProviderType provider,string commandText)
			{
				IDbDataAdapter dataAdapter=null;	
				IActivator activator=null;
				try
				{
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				//commandText String cannot be null or spaces.
				if(commandText==null || commandText.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullCommandTextException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}

				
					activator=Activator.CreateInstance(provider);
					dataAdapter=activator.CreateDataAdapter();
					dataAdapter.SelectCommand=activator.CreateCommand();
					dataAdapter.SelectCommand.CommandText=commandText;
					dataAdapter.SelectCommand.Connection=activator.CreateConnection();
					dataAdapter.SelectCommand.Connection.ConnectionString=connString;
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				return dataAdapter;
			}
			#region "Commented Code"
			//		/// <summary>
			//		/// Overloaded;Used for Creating and returning a data adapter object based on the provider,
			//		/// commandText string and connection object specified.	
			//		/// </summary>
			//		/*************************************************************
			//		* Method Name	: CreateDataAdapter
			//		* Components	: AB.ITAM.Infrastructure.ExceptionManager
			//		* Tables Used	: N.A.
			//		* Create Date	: 24/09/2003
			//		* Author        : Robin 
			//		* Change Control #    Date       Author         Description 
			//		* *************************************************************
			//		*				1)   24/09/2003  Robin       	
			//		***************************************************************/
			//		public static IDbDataAdapter CreateDataAdapter(ProviderType provider,string commandText,IDbConnection connection)
			//		{
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//commandText String cannot be null or spaces.
			//			if(commandText==null || commandText.Trim().Equals("")) 
			//			{
			//				throw ExceptionManager.HandleException(new NullCommandTextException(),"DataHelper",MethodBase.GetCurrentMethod());
			//			}
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//
			//			IDbDataAdapter dataAdapter=null;
			//			IActivator activator=null;
			//			try
			//			{
			//				activator=Activator.CreateInstance(provider);
			//				dataAdapter=activator.CreateDataAdapter();
			//				dataAdapter.SelectCommand=activator.CreateCommand();
			//				dataAdapter.SelectCommand.CommandText=commandText;
			//				dataAdapter.SelectCommand.Connection=connection;
			//			}
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			return dataAdapter;
			//		}
			//		/// <summary>
			//		/// Overloaded;Used for Creating and returning a data adapter object based on the provider,
			//		/// commandText string and connection string specified.	
			//		/// </summary>
			//		/*************************************************************
			//		* Method Name	: CreateDataAdapter
			//		* Components	: AB.ITAM.Infrastructure.ExceptionManager
			//		* Tables Used	: N.A.
			//		* Create Date	: 24/09/2003
			//		* Author        : Robin 
			//		* Change Control #    Date       Author         Description 
			//		* *************************************************************
			//		*				1)   24/09/2003  Robin       	
			//		***************************************************************/
			//		public static IDbDataAdapter CreateDataAdapter(ProviderType provider,string commandText, string connectionString)
			//		{
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//commandText String cannot be null or spaces.
			//			if(commandText==null || commandText.Trim().Equals("")) 
			//			{
			//				throw ExceptionManager.HandleException(new NullCommandTextException(),"DataHelper",MethodBase.GetCurrentMethod());
			//			}
			//			//Connection String cannot be null or spaces.
			//			if(connectionString==null || connectionString.Trim().Equals("")) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
			//			}
			//
			//			IDbDataAdapter dataAdapter=null;	
			//			IActivator activator=null;
			//			try
			//			{
			//				activator=Activator.CreateInstance(provider);
			//				dataAdapter=activator.CreateDataAdapter();
			//				dataAdapter.SelectCommand=activator.CreateCommand();
			//				dataAdapter.SelectCommand.CommandText=commandText;
			//				dataAdapter.SelectCommand.Connection=activator.CreateConnection();
			//				dataAdapter.SelectCommand.Connection.ConnectionString=connectionString;
			//			}
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			return dataAdapter;
			//		}
			#endregion "Commented Code"
			#endregion

			#region IDbDataParameter methods
			/// <summary>
			/// Used for Creating and returning a data parameter object based on the provider specified.		
			/// </summary>
			/*************************************************************
			* Method Name	: CreateDataParameter
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 24/09/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   24/09/2003  Robin       	
			***************************************************************/
			public static IDbDataParameter CreateDataParameter()
			{
				IDbDataParameter param=null;
				try
				{
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
					param=Activator.CreateInstance(provider).CreateDataParameter();
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
				return param;
			}
			public static IDbDataParameter CreateDataParameter(ProviderType provider)
			{
				IDbDataParameter param=null;
				try
				{
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
					param=Activator.CreateInstance(provider).CreateDataParameter();
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				return param;
			}
			/// <summary>
			/// Overloaded;Used for Creating and returning a data parameter object based on the provider,
			/// parameter name and parameter value specified.Using default activator ctors for performance.	
			/// </summary>
			/*************************************************************
			* Method Name	: CreateDataParameter
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 24/09/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   24/09/2003  Robin       	
			***************************************************************/
			public static IDbDataParameter CreateDataParameter(ProviderType provider,string parameterName,object valueOf)
			{
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				if(parameterName==null || parameterName.Trim().Equals(""))
				{
					throw ExceptionManager.HandleException(new NullParameterException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}

				IDbDataParameter param=null;
				try
				{
					param=Activator.CreateInstance(provider).CreateDataParameter();
					if(param!=null)
					{
						param.ParameterName=parameterName;
						param.Value=valueOf;
					}
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
				return param;
			}		
			/// <summary>
			/// Overloaded;Used for Creating and returning a data parameter object based on the provider,
			/// parameter name and parameter data type specified.Using default activator ctors for performance.
			/// </summary>
			/*************************************************************
			* Method Name	: CreateDataParameter
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 24/09/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   24/09/2003  Robin       	
			***************************************************************/
			public static IDbDataParameter CreateDataParameter(ProviderType provider,string parameterName,DbType dataType)
			{
				IDbDataParameter param=null;
				try
				{
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				if(parameterName==null || parameterName.Trim().Equals(""))
				{
					throw ExceptionManager.HandleException(new NullParameterException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}

					param=Activator.CreateInstance(provider).CreateDataParameter();
					if (param!=null)
					{
						param.ParameterName=parameterName;
						param.DbType=dataType;
					}
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				catch(System.IO.FileNotFoundException ex)				
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
				return param;
			}
			/// <summary>
			/// Overloaded;Used for Creating and returning a data parameter object based on the provider,
			/// parameter name, parameter data type and parameter size specified.Using default activator ctors for performance.		
			/// </summary>
			/*************************************************************
			* Method Name	: CreateDataParameter
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 24/09/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   24/09/2003  Robin       	
			***************************************************************/
			public static IDbDataParameter CreateDataParameter(ProviderType provider,string parameterName,DbType dataType,int size)
			{
				IDbDataParameter param=null;
				try
				{
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				if(parameterName==null || parameterName.Trim().Equals(""))
				{
					throw ExceptionManager.HandleException(new NullParameterException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}

					param=Activator.CreateInstance(provider).CreateDataParameter();
					if (param!=null)
					{
						param.ParameterName=parameterName;
						param.DbType=dataType;
						param.Size=size;
					}
				}
				catch(CustomException ex)
				{
					throw ex;
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				
				return param;
			}		
			#endregion

			#region RunSQL
			/// <summary>
			/// Used for Inserts,Deletes and Updates.
			/// This Overload of RunSQL uses cached conn string.Returns no. of records affected as 'int'.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSQL
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 20/05/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   20/05/2003  Robin       
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  				
			***************************************************************/
			#region "Commented Method"
			//		public static int RunSQL(string sqlText,params IDbDataParameter[] listOfParams)
			//		{				
			//			//Declarations
			//			int noOfRecordsAffected=0;
			//
			//			//Check if connstring is cached.
			//			if(!IsConnStringCached()){throw ExceptionManager.HandleException(new ConnStrNotCachedException(),"DataHelper",MethodInfo.GetCurrentMethod());}
			//			//Check if Provider is cached.
			//			if(!IsProviderSet()){throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodBase.GetCurrentMethod());}	
			//
			//			//Run overload of RunSQLReturnDS
			//			try
			//			{
			//				noOfRecordsAffected=RunSQL(connString,provider,sqlText,listOfParams);
			//			}
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			return noOfRecordsAffected;
			//		}
			#endregion "Commented Method"
			/// <summary>
			/// Used for Inserts,Deletes and Updates.
			/// This Overload of RunSQL uses user defined conn string and provider.Returns no. of records affected as 'int'.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSQL (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 20/05/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   20/05/2003  Robin     
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  		
			***************************************************************/
			public static int RunSQL(string sqlText,params IDbDataParameter[] listOfParams)
			{
				// Declarations.	
				int noOfRecordsAffected=0;
				IDbConnection connection=null;		
				IActivator activator=null;

				try
				{
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
				//Execute relevant Execute method of the  helper class.
				
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;
					//Call ExecuteNonQuery
					noOfRecordsAffected=ExecuteNonQuery(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.Internal,listOfParams);
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				finally
				{				
					if(connection!=null)connection.Dispose();	
				}
				return noOfRecordsAffected;
			}
			public static int RunSQL(ProviderType provider,string sqlText,params IDbDataParameter[] listOfParams)
			{
				// Declarations.	
				int noOfRecordsAffected=0;
				IDbConnection connection=null;		
				IActivator activator=null;
				//Execute relevant Execute method of the  helper class.
				try
				{
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;
					//Call ExecuteNonQuery
					noOfRecordsAffected=ExecuteNonQuery(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.Internal,listOfParams);
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());	
				}
				finally
				{				
					if(connection!=null)connection.Dispose();	
				}
				return noOfRecordsAffected;
			}
		
			//#region "Commented Code"
			/// <summary>
			/// Used for Inserts,Deletes and Updates.
			/// This Overload of RunSQL uses user passed connection object.Returns no. of records affected as 'int'.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSQL (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 20/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   20/05/2003  Robin       
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  		
			***************************************************************/
			public static int RunSQL(IDbConnection connection,string sqlText,params IDbDataParameter[] listOfParams)
			{
				// Declarations.	
				int noOfRecordsAffected=0;
				IActivator activator=null;
				//Execute relevant method of the helper class.
				try
				{		
					//If caller has not sent a valid connection.
					if(connection==null) 
					{
						throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
					}
					//If caller has not set the provider.
					if(provider==ProviderType.NotSet)
					{
						throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
					}			
					
					//Call ExecuteNonQuery
					activator=Activator.CreateInstance(provider);
					noOfRecordsAffected=ExecuteNonQuery(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.External,listOfParams);				
				}	
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}		
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}

				return noOfRecordsAffected;   
			}

			public static int RunSQL(IDbConnection connection,ProviderType provider,string sqlText,params IDbDataParameter[] listOfParams)
			{
				//If caller has not sent a valid connection.
				if(connection==null) 
				{
					throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}			
				// Declarations.	
				int noOfRecordsAffected=0;
				IActivator activator=null;
				//Execute relevant method of the helper class.
				try
				{		
					//Call ExecuteNonQuery
					activator=Activator.CreateInstance(provider);
					noOfRecordsAffected=ExecuteNonQuery(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.External,listOfParams);				
				}	
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				return noOfRecordsAffected;   
			}

			/// <summary>
			/// Used for Inserts,Deletes and Updates.
			/// This Overload of RunSQL uses user passed Transaction Context.Returns no. of records affected as 'int'.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSQL (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 20/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   20/05/2003  Robin      
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  		 		
			***************************************************************/
			public static int RunSQL(IDbTransaction transaction,string sqlText,params IDbDataParameter[] listOfParams)
			{
				// Declarations.	
				int noOfRecordsAffected=0;
				IActivator activator=null;
				//Execute relevant method of the helper class.
				try
				{	
				//If caller has not sent a valid transaction.
				if(transaction==null) 
				{
					throw ExceptionManager.HandleException(new NullTransactionException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
						
					//Call ExecuteNonQuery
					activator=Activator.CreateInstance(provider);
					noOfRecordsAffected=ExecuteNonQuery(activator,transaction,CommandType.Text,sqlText,ConnectionOwnership.External,listOfParams);												
				}	
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}		
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());	
				}
				return noOfRecordsAffected;   
			}
			public static int RunSQL(IDbTransaction transaction,ProviderType provider,string sqlText,params IDbDataParameter[] listOfParams)
			{
				int noOfRecordsAffected=0;
				try
				{	
				//If caller has not sent a valid transaction.
				if(transaction==null) 
				{
					throw ExceptionManager.HandleException(new NullTransactionException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				// Declarations.	
				
				IActivator activator=null;
				//Execute relevant method of the helper class.
						
					//Call ExecuteNonQuery
					activator=Activator.CreateInstance(provider);
					noOfRecordsAffected=ExecuteNonQuery(activator,transaction,CommandType.Text,sqlText,ConnectionOwnership.External,listOfParams);												
				}	
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}		
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}

				return noOfRecordsAffected;   
			}
		
			//#endregion "Commented Code"
			#endregion

			#region RunSQLReturnDS
			/// <summary>
			/// This Overload of RunSQLReturnDS uses cached conn string.Returns a Dataset.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSQLReturnDS 
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 20/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   20/05/2003 Robin       	
			*               2)   24/09/2003 Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  			
			***************************************************************/
			#region "Commented Method"
			//		public static DataSet RunSQLReturnDS(string sqlText,params IDbDataParameter[] listOfParams)
			//		{
			//			// Declarations.
			//			DataSet ds=null;			
			//			
			//			//Check if connstring is cached.
			//			if(!IsConnStringCached()){throw ExceptionManager.HandleException(new ConnStrNotCachedException(),"DataHelper",MethodInfo.GetCurrentMethod());}
			//			//Check if Provider is cached.
			//			if(!IsProviderSet()){throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodBase.GetCurrentMethod());}	
			//
			//			//Run overload of RunSQLReturnDS
			//			try
			//			{
			//				ds=RunSQLReturnDS(connString,provider,sqlText,listOfParams);
			//			}								
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			return ds;   
			//		}

			#endregion "Commented Method"
			/// <summary>
			/// This Overload of RunSQLReturnDS uses user defined conn string and provider.Returns a Dataset.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSQLReturnDS (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 20/05/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   20/05/2003 Robin       
			*               2)   24/09/2003 Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  				
			***************************************************************/
			public static DataSet RunSQLReturnDS(string strSql,params IDbDataParameter[] listOfParams)
			{
				// Declarations.
				DataSet ds=new DataSet();
				IDbConnection connection=null;	
				IActivator activator=null;
				//Execute relevant method of the helper class.
				try
				{				
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
							
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;			
					//Call ExecuteDataset..
					ds=ExecuteDataset(activator,connection,CommandType.Text,strSql,ConnectionOwnership.Internal,listOfParams);				
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					//throw ex;
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				
				finally
				{				
					if(connection!=null)connection.Dispose();
				}
				return ds;   
			}

			public static DataSet RunSQLReturnDS(ProviderType provider,string sqlText,params IDbDataParameter[] listOfParams)
			{
				
				// Declarations.
				DataSet ds=new DataSet();;
				IDbConnection connection=null;	
				IActivator activator=null;
				try
				{
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
				//Execute relevant method of the helper class.
											
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;			
					//Call ExecuteDataset..
					ds=ExecuteDataset(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.Internal,listOfParams);				
				}
//				catch(CustomException ex)
//				{
//					throw ex;
//				}			
				catch(System.Data.DataException ex)
				{
					throw ex;
					//throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());	
				}
				finally
				{				
					if(connection!=null)connection.Dispose();
				}
				return ds;   
			}

			#region "Commented Code"
			//		/// <summary>
			//		/// This Overload of RunSQLReturnDS uses user passed connection object.Returns a Dataset.
			//		/// </summary>
			//		/*************************************************************
			//		* Method Name	: RunSQLReturnDS (Overload)
			//		* Components	: AB.ITAM.Infrastructure.ExceptionManager
			//		* Tables Used	: N.A.
			//		* Create Date	: 20/05/2003
			//		* Author        : Robin 
			//		* Change Control #    Date       Author         Description 
			//		* *************************************************************
			//		*				1)   20/05/2003  Robin       
			//		*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  						
			//		***************************************************************/
			//		public static DataSet RunSQLReturnDS(IDbConnection connection,string strSql,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}	
			//			// Declarations.
			//			DataSet ds=null;
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{				
			//				//Call ExecuteDataset.
			//				activator=Activator.CreateInstance(provider);
			//				ds=ExecuteDataset(activator,connection,CommandType.Text,strSql,ConnectionOwnership.External,listOfParams);								
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}			
			//			return ds;   
			//		}
			//
			//		public static DataSet RunSQLReturnDS(IDbConnection connection,ProviderType provider,string   sqlText,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}	
			//			// Declarations.
			//			DataSet ds=null;
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{				
			//				//Call ExecuteDataset.
			//				activator=Activator.CreateInstance(provider);
			//				ds=ExecuteDataset(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.External,listOfParams);								
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}			
			//			return ds;   
			//		}
			//
			#endregion "Commented Code"
			#endregion		
			
			#region RunSQLReturnDT
		/// <summary>
		/// This Overload of RunSQLReturnDT uses cached conn string.Returns a DataTable.
		/// </summary>
		/*************************************************************
			* Method Name	: RunSQLReturnDT 
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 20/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   20/05/2003 Robin       	
			*               2)   24/09/2003 Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  			
			***************************************************************/
		
		/// <summary>
		/// This Overload of RunSQLReturnDS uses user defined conn string and provider.Returns a Dataset.
		/// </summary>
		/*************************************************************
			* Method Name	: RunSQLReturnDT (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 20/05/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   20/05/2003 Robin       
			*               2)   24/09/2003 Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  				
			***************************************************************/
		public static DataTable RunSQLReturnDT(string strSql,params IDbDataParameter[] listOfParams)
		{
			// Declarations.
			DataSet ds=null;
			IDbConnection connection=null;	
			IActivator activator=null;
			//Execute relevant method of the helper class.
			try
			{	
			//Connection String cannot be null or spaces.
			if(connString==null || connString.Trim().Equals("")) 
			{
				throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
			}
			//If caller has not set the provider.
			if(provider==ProviderType.NotSet)
			{
				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			}
				//Create the connection(Using default ctor for activator as other ctors take more time.)
				activator=Activator.CreateInstance(provider);
				connection=activator.CreateConnection();				
				connection.ConnectionString=connString;			
				//Call ExecuteDataset..
				ds=ExecuteDataset(activator,connection,CommandType.Text,strSql,ConnectionOwnership.Internal,listOfParams);				
			}
			catch(CustomException ex)
			{
				throw ex;
			}			
			catch(System.Data.DataException ex)
			{
				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			}
			catch(System.IO.FileNotFoundException ex)
			{
				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			}
			finally
			{				
				if(connection!=null)connection.Dispose();
			}
			return ds.Tables[0];   
		}

		public static DataTable RunSQLReturnDT(ProviderType provider,string sqlText,params IDbDataParameter[] listOfParams)
		{
			
			// Declarations.
			DataSet ds=new DataSet();
			IDbConnection connection=null;	
			IActivator activator=null;
			//Execute relevant method of the helper class.
			try
			{		
			//Connection String cannot be null or spaces.
			if(connString==null || connString.Trim().Equals("")) 
			{
				throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
			}
			//If caller has not set the provider.
			if(provider==ProviderType.NotSet)
			{
				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			}
								
				//Create the connection(Using default ctor for activator as other ctors take more time.)
				activator=Activator.CreateInstance(provider);
				connection=activator.CreateConnection();				
				connection.ConnectionString=connString;			
				//Call ExecuteDataset..
				ds=ExecuteDataset(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.Internal,listOfParams);				
			}
			catch(CustomException ex)
			{
				throw ex;
			}			
			catch(System.Data.DataException ex)
			{
				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			}
			catch(System.IO.FileNotFoundException ex)
			{
				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			}
			finally
			{				
				if(connection!=null)connection.Dispose();
			}
			return ds.Tables[0];      
		}

		
		#endregion		

			#region RunSQLReturnScalar
			/// <summary>
			/// This Overload of RunSQLReturnScalar uses cached conn string.Returns data of type 'Object'.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSQLReturnScalar
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 20/05/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   20/05/2003  Robin     	
			*               2)   24/09/2003  Robin      With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  							
			***************************************************************/
			#region "Commented Method"
			//		public static object RunSQLReturnScalar(string sqlText,params IDbDataParameter[] listOfParams)
			//		{
			//			// Declarations.
			//			object retVal=null;			
			//			
			//			//Check if connstring is cached.
			//			if(!IsConnStringCached()){throw ExceptionManager.HandleException(new ConnStrNotCachedException(),"DataHelper",MethodInfo.GetCurrentMethod());}
			//			//Check if Provider is cached.
			//			if(!IsProviderSet()){throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodBase.GetCurrentMethod());}	
			//
			//			//Run overload of RunSQLReturnScalar
			//			try
			//			{
			//				retVal=RunSQLReturnScalar(connString,provider,sqlText,listOfParams);
			//			}								
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			return retVal;   
			//		}
			#endregion "Commented Method"
			/// <summary>
			/// This Overload of RunSQLReturnScalar uses user defined conn string and provider.
			/// Returns data of type 'Object'.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSQLReturnScalar (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 20/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   20/05/2003  Robin       		
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  						
			***************************************************************/
			public static object RunSQLReturnScalar(string sqlText,params IDbDataParameter[] listOfParams)
			{
				
				// Declarations.
				object retVal=null;
				IDbConnection connection=null;				
				IActivator activator=null;
				//Execute relevant method of the helper class.
				try
				{
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
										
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;		
					//Call ExecuteScalar..										
					retVal=ExecuteScalar(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.Internal,listOfParams);				
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
						throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				finally
				{				
					if(connection!=null)connection.Dispose();
				}
				return retVal;   
			}
			public static object RunSQLReturnScalar(ProviderType provider,string sqlText,params IDbDataParameter[] listOfParams)
			{
				// Declarations.
				object retVal=null;
				IDbConnection connection=null;				
				IActivator activator=null;
				//Execute relevant method of the helper class.
				try
				{	
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
											
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;		
					//Call ExecuteScalar..										
					retVal=ExecuteScalar(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.Internal,listOfParams);				
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				finally
				{				
					if(connection!=null)connection.Dispose();
				}
				return retVal;   
			}
		
			/// <summary>
			/// This Overload of RunSQLReturnScalar uses user passed connection object.
			/// Returns data of type 'Object'.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSQLReturnScalar (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 20/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   20/05/2003  Robin       
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  								
			***************************************************************/
			#region "Commented Method"
			//		public static object RunSQLReturnScalar(IDbConnection connection,string sqlText,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			object retVal=null;
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{				
			//				//Call ExecuteScalar.
			//				activator=Activator.CreateInstance(provider);
			//				retVal=ExecuteScalar(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.External,listOfParams);				
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}		
			//			return retVal;
			//		}
			//
			//		public static object RunSQLReturnScalar(IDbConnection connection,ProviderType provider,string sqlText,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			object retVal=null;
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{				
			//				//Call ExecuteScalar.
			//				activator=Activator.CreateInstance(provider);
			//				retVal=ExecuteScalar(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.External,listOfParams);				
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}		
			//			return retVal;
			//		}

			#endregion "Commented Method"
			#endregion		

			#region RunSQLReturnArrayList
			#region "Commented Code"
			/// <summary>
			/// This Overload of RunSQLReturnArrayList uses cached conn string.Returns an ArrayList.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSQLReturnArrayList 
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 21/05/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   21/05/2003  Robin      	
			*               2)   24/09/2003  Robin      With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  									
			***************************************************************/
		

		
			//		public static ArrayList RunSQLReturnArrayList(string sqlText,params IDbDataParameter[] listOfParams)
			//		{
			//			// Declarations.			
			//			ArrayList al=null;
			//			
			//			//Check if connstring is cached.
			//			if(!IsConnStringCached()){throw ExceptionManager.HandleException(new ConnStrNotCachedException(),"DataHelper",MethodInfo.GetCurrentMethod());}
			//			//Check if Provider is cached.
			//			if(!IsProviderSet()){throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodBase.GetCurrentMethod());}	
			//
			//			//Run overload of RunSQLReturnDS
			//			try
			//			{
			//				al=RunSQLReturnArrayList(connString,provider,sqlText,listOfParams);				
			//			}								
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			return al;   
			//		}
			#endregion "Commented Code"
			/// <summary>
			/// This Overload of RunSQLReturnArrayList uses user defined conn string and provider.Returns an ArrayList.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSQLReturnArrayList (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 21/05/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   21/05/2003  Robin      
			*               2)   24/09/2003  Robin      With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  										
			***************************************************************/
			public static ArrayList RunSQLReturnArrayList(string sqlText,params IDbDataParameter[] listOfParams)
			{
				// Declarations.
				ArrayList al=null;
				IDataReader dr=null;
				IDbConnection connection=null;		
				IActivator activator=null;
				try
				{
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
				
				//Excecute relevant method of the helper class.
												
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;			
					//Call ExecuteReader..						
					dr=ExecuteReader(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.Internal,CommandState.LeaveOpen,listOfParams);				
					al=GetRows(dr,dr.FieldCount);						
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());	
				}
				finally
				{
					if(dr!=null)
					{
						if(!dr.IsClosed)
						{
							dr.Close();		
						}
						dr.Dispose();
					}			
					if(connection!=null)
					{	
						if(connection.State!=ConnectionState.Closed)
						{
							connection.Close();
						}
						connection.Dispose();
					}				
				}
				return al;   
			}

			public static ArrayList RunSQLReturnArrayList(ProviderType provider,string sqlText,params IDbDataParameter[] listOfParams)
			{
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				// Declarations.
				ArrayList al=null;
				IDataReader dr=null;
				IDbConnection connection=null;		
				IActivator activator=null;
				//Excecute relevant method of the helper class.
				try
				{								
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;			
					//Call ExecuteReader..						
					dr=ExecuteReader(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.Internal,CommandState.LeaveOpen,listOfParams);				
					al=GetRows(dr,dr.FieldCount);						
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				
				finally
				{
					if(dr!=null)
					{
						if(!dr.IsClosed)
						{
							dr.Close();		
						}
						dr.Dispose();
					}			
					if(connection!=null)
					{	
						if(connection.State!=ConnectionState.Closed)
						{
							connection.Close();
						}
						connection.Dispose();
					}				
				}
				return al;   
			}

			#region "Commented Method"
			/// <summary>
			/// This Overload of RunSQLReturnArrayList uses user passed connection object.Returns an ArrayList.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSQLReturnArrayList (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 21/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   21/05/2003  Robin       
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  										
			***************************************************************/
		
		
			//		public static ArrayList RunSQLReturnArrayList(IDbConnection connection,string sqlText,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			ArrayList al=null;
			//			IDataReader dr=null;
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{	
			//				activator=Activator.CreateInstance(provider);
			//				dr=ExecuteReader(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.External,CommandState.LeaveOpen,listOfParams);				
			//				al=GetRows(dr,dr.FieldCount);				
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			finally
			//			{
			//				if(dr!=null)
			//				{
			//					if(!dr.IsClosed)
			//					{
			//						dr.Close();		
			//					}
			//					dr.Dispose();
			//				}	
			//			}
			//			return al;   
			//		}
			//
			//		public static ArrayList RunSQLReturnArrayList(IDbConnection connection,ProviderType provider,string sqlText,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			ArrayList al=null;
			//			IDataReader dr=null;
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{	
			//				activator=Activator.CreateInstance(provider);
			//				dr=ExecuteReader(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.External,CommandState.LeaveOpen,listOfParams);				
			//				al=GetRows(dr,dr.FieldCount);				
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			finally
			//			{
			//				if(dr!=null)
			//				{
			//					if(!dr.IsClosed)
			//					{
			//						dr.Close();		
			//					}
			//					dr.Dispose();
			//				}	
			//			}
			//			return al;   
			//		}

			#endregion "Commented Method"
			#endregion				

			#region RunSQLReturnHashTable
			/// <summary>
			/// This Overload of RunSQLReturnHashTable uses cached conn string.Returns a Hashtable.
			/// They should be typically used with sql's that return key-value pair.
			/// The first column is used as the key, while the second is used as the value.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSQLReturnHashTable 
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 06/10/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   06/10/2003  Robin       		
			***************************************************************/
		
			#region "Commented Method"
			//		public static Hashtable RunSQLReturnHashTable(string sqlText,params IDbDataParameter[] listOfParams)
			//		{
			//			// Declarations.			
			//			Hashtable ht=null;
			//			
			//			//Check if connstring is cached.
			//			if(!IsConnStringCached()){throw ExceptionManager.HandleException(new ConnStrNotCachedException(),"DataHelper",MethodInfo.GetCurrentMethod());}
			//			//Check if Provider is cached.
			//			if(!IsProviderSet()){throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodBase.GetCurrentMethod());}	
			//
			//			//Run overload of RunSQLReturnHashTable
			//			try
			//			{
			//				ht=RunSQLReturnHashTable(connString,provider,sqlText,listOfParams);				
			//			}								
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			return ht;   
			//		}
			#endregion "Commented Method"
			/// <summary>
			/// This Overload of RunSQLReturnHashTable uses user defined conn string and provider.Returns a Hashtable.
			/// They should be typically used with sql's that return key-value pair.
			/// The first column is used as the key, while the second is used as the value.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSQLReturnHashTable 
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 06/10/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   06/10/2003  Robin     		
			***************************************************************/
			public static Hashtable RunSQLReturnHashTable(string sqlText,params IDbDataParameter[] listOfParams)
			{
				// Declarations.
				Hashtable ht=new Hashtable();
				IDataReader dr=null;
				IDbConnection connection=null;				
				IActivator activator=null;
				//Excecute relevant method of the helper class.
				try
				{		
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
										
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;			
					//Call ExecuteReader..						
					dr=ExecuteReader(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.Internal,CommandState.LeaveOpen,listOfParams);				
					ht=GetHashTable(dr);						
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				finally
				{
					if(dr!=null)
					{
						if(!dr.IsClosed)
						{
							dr.Close();		
						}
						dr.Dispose();
					}			
					if(connection!=null)
					{	
						if(connection.State!=ConnectionState.Closed)
						{
							connection.Close();
						}
						connection.Dispose();
					}				
				}
				return ht;   
			}

			public static Hashtable RunSQLReturnHashTable(ProviderType provider,string sqlText,params IDbDataParameter[] listOfParams)
			{
				// Declarations.
				Hashtable ht=null;
				IDataReader dr=null;
				IDbConnection connection=null;				
				IActivator activator=null;

				try
				{	
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
				//Excecute relevant method of the helper class.
											
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;			
					//Call ExecuteReader..						
					dr=ExecuteReader(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.Internal,CommandState.LeaveOpen,listOfParams);				
					ht=GetHashTable(dr);						
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}		
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}		
				finally
				{
					if(dr!=null)
					{
						if(!dr.IsClosed)
						{
							dr.Close();		
						}
						dr.Dispose();
					}			
					if(connection!=null)
					{	
						if(connection.State!=ConnectionState.Closed)
						{
							connection.Close();
						}
						connection.Dispose();
					}				
				}
				return ht;   
			}

			#region "Commented Method"
			//		/// <summary>
			//		/// This Overload of RunSQLReturnHashTable uses user passed connection object.Returns a Hashtable.
			//		/// They should be typically used with sql's that return key-value pair.
			//		/// The first column is used as the key, while the second is used as the value.
			//		/// </summary>
			//		/*************************************************************
			//		* Method Name	: RunSQLReturnHashTable 
			//		* Components	: AB.ITAM.Infrastructure.ExceptionManager
			//		* Tables Used	: N.A.
			//		* Create Date	: 06/10/2003
			//		* Author        : Robin 
			//		* Change Control #    Date       Author         Description 
			//		* *************************************************************
			//		*				1)   06/10/2003  Robin       		
			//		***************************************************************/
			//		public static Hashtable RunSQLReturnHashTable(IDbConnection connection,string sqlText,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			Hashtable ht=null;
			//			IDataReader dr=null;
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{	
			//				activator=Activator.CreateInstance(provider);
			//				dr=ExecuteReader(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.External,CommandState.LeaveOpen,listOfParams);				
			//				ht=GetHashTable(dr);				
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			finally
			//			{
			//				if(dr!=null)
			//				{
			//					if(!dr.IsClosed)
			//					{
			//						dr.Close();		
			//					}
			//					dr.Dispose();
			//				}	
			//			}
			//			return ht;   
			//		}
			//
			//		public static Hashtable RunSQLReturnHashTable(IDbConnection connection,ProviderType provider,string sqlText,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			Hashtable ht=null;
			//			IDataReader dr=null;
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{	
			//				activator=Activator.CreateInstance(provider);
			//				dr=ExecuteReader(activator,connection,CommandType.Text,sqlText,ConnectionOwnership.External,CommandState.LeaveOpen,listOfParams);				
			//				ht=GetHashTable(dr);				
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			finally
			//			{
			//				if(dr!=null)
			//				{
			//					if(!dr.IsClosed)
			//					{
			//						dr.Close();		
			//					}
			//					dr.Dispose();
			//				}	
			//			}
			//			return ht;   
			//		}

			#endregion "Commented Method"
			#endregion				

			#region RunSP
			/// <summary>
			/// Used for Inserts,Deletes and Updates.
			/// This Overload of RunSP uses cached conn string.Returns no. of records affected as 'int'.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSP 
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 21/05/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   21/05/2003  Robin     
			*               2)   24/09/2003  Robin      With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  									 		
			***************************************************************/
		
			#region "Commented Method"
			//		public static int RunSP(string procName,params IDbDataParameter[] listOfParams)
			//		{	
			//			//Declarations
			//			int noOfRecordsAffected=0;
			//
			//			//Check if connstring is cached.
			//			if(!IsConnStringCached()){throw ExceptionManager.HandleException(new ConnStrNotCachedException(),"DataHelper",MethodInfo.GetCurrentMethod());}
			//			//Check if Provider is cached.
			//			if(!IsProviderSet()){throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodBase.GetCurrentMethod());}	
			//
			//			//Run overload of RunSQLReturnDS
			//			try
			//			{
			//				noOfRecordsAffected=RunSP(connString,provider,procName,listOfParams);
			//			}								
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			return noOfRecordsAffected;
			//		}
			#endregion "Commented Method"
			/// <summary>
			/// Used for Inserts,Deletes and Updates.
			/// This Overload of RunSP uses user defined conn string and provider.Returns no. of records affected as 'int'.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSP (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 21/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   21/05/2003  Robin       
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  									 				
			***************************************************************/
			public static int RunSP(string procName,params IDbDataParameter[] listOfParams)
			{
				// Declarations.	
				int noOfRecordsAffected=0;
				IDbConnection connection=null;			
				IActivator activator=null;
				//Execute relevant method of the helper class.
				try
				{		
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
										
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;				
					//Call ExecuteNonQuery..	
					noOfRecordsAffected=ExecuteNonQuery(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,listOfParams);				
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				finally
				{				
					if(connection!=null)connection.Dispose();	
				}
				return noOfRecordsAffected;
			}
			public static int RunSP(ProviderType provider,string procName,params IDbDataParameter[] listOfParams)
			{
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				// Declarations.	
				int noOfRecordsAffected=0;
				IDbConnection connection=null;			
				IActivator activator=null;
				//Execute relevant method of the helper class.
				try
				{								
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;				
					//Call ExecuteNonQuery..	
					noOfRecordsAffected=ExecuteNonQuery(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,listOfParams);				
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				
				finally
				{				
					if(connection!=null)connection.Dispose();	
				}
				return noOfRecordsAffected;
			}
			#region "Commented Method"
			//		/// <summary>
			//		/// Used for Inserts,Deletes and Updates.
			//		/// This Overload of RunSP uses user passed connection object.Returns no. of records affected as 'int'.
			//		/// </summary>
			//		/*************************************************************
			//		* Method Name	: RunSP (Overload)
			//		* Components	: AB.ITAM.Infrastructure.ExceptionManager
			//		* Tables Used	: N.A.
			//		* Create Date	: 21/05/2003
			//		* Author        : robin
			//		* Change Control #    Date       Author         Description 
			//		* *************************************************************
			//		*				1)   21/05/2003  robin      		
			//		*               2)   24/09/2003  robin      With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  									 		
			//		***************************************************************/
			//
			//	
			//		
			//		public static int RunSP(IDbConnection connection,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			int noOfRecordsAffected=0;				
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{				
			//				//Call ExecuteNonQuery
			//				activator=Activator.CreateInstance(provider);
			//				noOfRecordsAffected=ExecuteNonQuery(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.External,listOfParams);
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}		
			//			return noOfRecordsAffected;
			//		}
			//
			//		public static int RunSP(IDbConnection connection,ProviderType provider,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			int noOfRecordsAffected=0;				
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{				
			//				//Call ExecuteNonQuery
			//				activator=Activator.CreateInstance(provider);
			//				noOfRecordsAffected=ExecuteNonQuery(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.External,listOfParams);
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}		
			//			return noOfRecordsAffected;
			//		}
			//
			#endregion "Commented Method"
			/// <summary>
			/// Used for Inserts,Deletes and Updates.
			/// This Overload of RunSP uses user passed Transaction Context.Returns no. of records affected as 'int'.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSP (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 21/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   21/05/2003  Robin       		
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  									 		
			***************************************************************/
			public static int RunSP(IDbTransaction transaction,string procName,params IDbDataParameter[] listOfParams)
			{
				// Declarations.
				int noOfRecordsAffected=0;			
				IActivator activator=null;
				try
				{	
				//If caller has not sent a valid transaction.
				if(transaction==null) 
				{
					throw ExceptionManager.HandleException(new NullTransactionException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
				//Execute method of the relevant helper class.
				
					activator=Activator.CreateInstance(provider);
					noOfRecordsAffected=ExecuteNonQuery(activator,transaction,CommandType.StoredProcedure,procName,ConnectionOwnership.External,listOfParams);				
				}	
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}		
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}	
				return noOfRecordsAffected;
			}

			public static int RunSP(IDbTransaction transaction,ProviderType provider,string procName,params IDbDataParameter[] listOfParams)
			{
				
				int noOfRecordsAffected;
				if(listOfParams==null)
					return noOfRecordsAffected=0;
				
				try
				{	
				//If caller has not sent a valid transaction.
				if(transaction==null) 
				{
					throw ExceptionManager.HandleException(new NullTransactionException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				// Declarations.
							
				IActivator activator=null;
				//Execute method of the relevant helper class.
				
					activator=Activator.CreateInstance(provider);
					noOfRecordsAffected=ExecuteNonQuery(activator,transaction,CommandType.StoredProcedure,procName,ConnectionOwnership.External,listOfParams);				
				}	
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}		
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}	
				
				return noOfRecordsAffected;
			}

			#endregion		

			#region RunSPReturnDS
			/// <summary>
			/// This Overload of RunSPReturnDS uses cached conn string.Returns a dataset.	
			/// </summary>
			/*************************************************************
			* Method Name	: RunSPReturnDS 
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 21/05/2003
			* Author        : Robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   21/05/2003  Robin     
			*               2)   24/09/2003  Robin      With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  									 				
			***************************************************************/

			#region "Commented Method"
			////		public static DataSet RunSPReturnDS(string procName,params IDbDataParameter[] listOfParams)
			////		{
			////			// Declarations.
			////			DataSet ds=null;			
			////			
			////			//Check if connstring is cached.
			////			if(!IsConnStringCached()){throw ExceptionManager.HandleException(new ConnStrNotCachedException(),"DataHelper",MethodInfo.GetCurrentMethod());}
			////			//Check if Provider is cached.
			////			if(!IsProviderSet()){throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodBase.GetCurrentMethod());}	
			////
			////			//Run overload of RunSQLReturnDS
			////			try
			////			{
			////				ds=RunSPReturnDS(connString,provider,procName,listOfParams);
			////			}								
			////			catch(CustomException ex)
			////			{
			////				throw ex;
			////			}			
			////			catch(System.Exception ex)
			////			{
			////				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			////			}		
			////			return ds;   
			////		}
			////	
			#endregion "Commented Method"
			/// <summary>
			/// This Overload of RunSPReturnDS uses user defined conn string and provider.Returns a dataset.		
			/// </summary>
			/*************************************************************
			* Method Name	: RunSPReturnDS (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 21/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   21/05/2003  Robin 
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  									 		      		
			***************************************************************/
			public static DataSet RunSPReturnDS(ProviderType provider,string procName,int WorkStationID, string DbType,params IDbDataParameter[] listOfParams)
			{
				// Declarations.
				DataSet ds=new DataSet();
				IDbConnection connection;		
				IActivator activator;
				//Execute relevant method of the helper class.
				try
				{
                    GetConnectionString(WorkStationID, DbType);
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;		
					//Call ExecuteDataset..	
					ds=ExecuteDataset(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,listOfParams);								
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}	
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}	
				finally
				{				
					
				}
				return ds;   
			}				
			public static DataSet RunSPReturnDS(string procName,int WorkStationID, string DbType,params IDbDataParameter[] listOfParams)
			{
				// Declarations.
				DataSet ds=null;
				IDbConnection connection=null;		
				IActivator activator=null;
                try
				{
                    GetConnectionString(WorkStationID, DbType);
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
				//Execute relevant method of the helper class.
				
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;		
					//Call ExecuteDataset..	
					ds=ExecuteDataset(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,listOfParams);								
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());	
				}
				
				finally
				{				
					if(connection!=null)connection.Dispose();
				}
				return ds;   
			}				
		
			#region "Commented Method"
			/// <summary>
			/// This Overload of RunSPReturnDS uses user passed connection object.Returns a dataset.		
			/// </summary>
			/*************************************************************
			* Method Name	: RunSPReturnDS (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 21/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   21/05/2003  Robin     
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  									 		  		
			***************************************************************/
			//		public static DataSet RunSPReturnDS(IDbConnection connection,ProviderType provider,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			DataSet ds=null;		
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{
			//				//Call ExecuteDataset
			//				activator=Activator.CreateInstance(provider);
			//				ds=ExecuteDataset(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.External,listOfParams);
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}		
			//			return ds;   
			//		}
			//			
			//		public static DataSet RunSPReturnDS(IDbConnection connection,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			DataSet ds=null;		
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{
			//				//Call ExecuteDataset
			//				activator=Activator.CreateInstance(provider);
			//				ds=ExecuteDataset(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.External,listOfParams);
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}		
			//			return ds;   
			//		}
			//			
			#endregion "Commented Method"
			#endregion
		
			#region "Commented Method"
			#region RunSPReturnDSAndParams  --KEPT ONLY FOR BACKWARD COMPATIBILITY(Can use RunSPReturnDS instead.)
			//		/// <summary>
			//		/// This Overload of RunSPReturnDSAndParams uses cached conn string and provider and uses supplied IDbDataParameter array.		
			//		/// Use this only if the return is just a dataset or a dataset plus o/p parameters.		
			//		/// For only getting o/p parameters use "RunSPReturnParams".
			//		/// NOTE: KEPT ONLY FOR BACKWARD COMPATIBILITY(Can use RunSPReturnDS instead).
			//		/// </summary>
			//		/*************************************************************
			//		* Method Name	: RunSPReturnDSAndParams 
			//		* Components	: AB.ITAM.Infrastructure.ExceptionManager
			//		* Tables Used	: N.A.
			//		* Create Date	: 21/05/2003
			//		* Author        : Robin 
			//		* Change Control #    Date       Author         Description 
			//		* *************************************************************
			//		*				1)   21/05/2003  Robin       	
			//		*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  									 		      			
			//		***************************************************************/
			//		#region "Commented Method"
			////		public static DataSet RunSPReturnDSAndParams(string procName,params IDbDataParameter[] listOfParams)
			////		{
			////			// Declarations.
			////			DataSet ds=null;			
			////			
			////			//Check if connstring is cached.
			////			if(!IsConnStringCached()){throw ExceptionManager.HandleException(new ConnStrNotCachedException(),"DataHelper",MethodInfo.GetCurrentMethod());}
			////			//Check if Provider is cached.
			////			if(!IsProviderSet()){throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodBase.GetCurrentMethod());}	
			////
			////			//Run overload of RunSPReturnDSAndParams
			////			try
			////			{
			////				ds=RunSPReturnDSAndParams(connString,provider,procName,listOfParams);
			////			}								
			////			catch(CustomException ex)
			////			{
			////				throw ex;
			////			}			
			////			catch(System.Exception ex)
			////			{
			////				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			////			}		
			////			return ds;   
			////		}
			//		#endregion "Commented Method"
			//		/// <summary>
			//		/// This Overload of RunSPReturnDSAndParams uses user defined conn string and provider and uses supplied IDbDataParameter array.
			//		/// Use this only if the return is just a dataset or a dataset plus o/p parameters.		
			//		/// For only getting o/p parameters use "RunSPReturnParams".
			//		/// NOTE: KEPT ONLY FOR BACKWARD COMPATIBILITY(Can use RunSPReturnDS instead).
			//		/// </summary>
			//		/*************************************************************
			//		* Method Name	: RunSPReturnDSAndParams (Overload)
			//		* Components	: AB.ITAM.Infrastructure.ExceptionManager
			//		* Tables Used	: N.A.
			//		* Create Date	: 21/05/2003
			//		* Author        : Robin 
			//		* Change Control #    Date       Author         Description 
			//		* *************************************************************
			//		*				1)   21/05/2003  Robin      
			//		*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  									 		      		 		
			//		***************************************************************/
			//		public static DataSet RunSPReturnDSAndParams(string connectionString,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			//Connection String cannot be null or spaces.
			//			if(connectionString==null || connectionString.Trim().Equals("")) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			DataSet ds=null;
			//			IDbConnection connection=null;				
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.			
			//			try
			//			{
			//				//Create the connection(Using default ctor for activator as other ctors take more time.)
			//				activator=Activator.CreateInstance(provider);
			//				connection=activator.CreateConnection();				
			//				connection.ConnectionString=connectionString;			
			//				//Call ExecuteDataset..	
			//				ds=ExecuteDataset(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,listOfParams);								
			//			}
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			finally
			//			{				
			//				if(connection!=null)connection.Dispose();
			//			}
			//			return ds;   
			//		}
			//		public static DataSet RunSPReturnDSAndParams(string connectionString,ProviderType provider,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			//Connection String cannot be null or spaces.
			//			if(connectionString==null || connectionString.Trim().Equals("")) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			DataSet ds=null;
			//			IDbConnection connection=null;				
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.			
			//			try
			//			{
			//				//Create the connection(Using default ctor for activator as other ctors take more time.)
			//				activator=Activator.CreateInstance(provider);
			//				connection=activator.CreateConnection();				
			//				connection.ConnectionString=connectionString;			
			//				//Call ExecuteDataset..	
			//				ds=ExecuteDataset(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,listOfParams);								
			//			}
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			finally
			//			{				
			//				if(connection!=null)connection.Dispose();
			//			}
			//			return ds;   
			//		}
			//		/// <summary>
			//		/// This Overload of RunSPReturnDSAndParams uses user passed connection object and uses supplied IDbDataParameter array.	
			//		/// Use this only if the return is just a dataset or a dataset plus o/p parameters.		
			//		/// For only getting o/p parameters use "RunSPReturnParams".
			//		/// NOTE: KEPT ONLY FOR BACKWARD COMPATIBILITY(Can use RunSPReturnDS instead).
			//		/// </summary>
			//		/*************************************************************
			//		* Method Name	: RunSPReturnDSAndParams (Overload)
			//		* Components	: AB.ITAM.Infrastructure.ExceptionManager
			//		* Tables Used	: N.A.
			//		* Create Date	: 21/05/2003
			//		* Author        : Robin 
			//		* Change Control #    Date       Author         Description 
			//		* *************************************************************
			//		*				1)   21/05/2003  Robin       
			//		*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  									 		      				
			//		***************************************************************/
			//		public static DataSet RunSPReturnDSAndParams(IDbConnection connection,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			DataSet ds=null;
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{
			//				//Call ExecuteDataset
			//				activator=Activator.CreateInstance(provider);
			//				ds=ExecuteDataset(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.External,listOfParams);
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}		
			//			return ds;   
			//		}	
			//		public static DataSet RunSPReturnDSAndParams(IDbConnection connection,ProviderType provider,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			DataSet ds=null;
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{
			//				//Call ExecuteDataset
			//				activator=Activator.CreateInstance(provider);
			//				ds=ExecuteDataset(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.External,listOfParams);
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}		
			//			return ds;   
			//		}	
			#endregion
			#endregion "Commented Method"

			#region RunSPReturnScalar
			/// <summary>
			/// This Overload of RunSPReturnScalar uses cached conn string.
			/// Returns data of type 'Object'.
			/// Always returns null when used against an Oracle database
			/// (as every output in Oracle is via an out parameter).
			/// WorkAround is to call this method with a parameter with direction=ReturnValue/Out
			/// (based on whether a Function is called or a SP is called), that gets filled with the output,
			/// and call the function as a void function without bothering about assigning the return value.		
			/// </summary>
			/*************************************************************
			* Method Name	: RunSPReturnScalar 
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 21/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   21/05/2003  Robin      
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  									 		      				 		
			***************************************************************/
			#region "Commented Method"
			//		public static object RunSPReturnScalar(string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			/*
			//			 **************************************CAUTION:**********************************************
			//			 * Method does not have much meaning in ORACLE through OLEDB/OracleClient Provider as ExecuteScalar always 
			//			 * returns null from a stored proc or function.			 
			//			 **************************************CAUTION:**********************************************
			//			*/
			//
			//			// Declarations.
			//			object retVal=null;			
			//			
			//			//Check if connstring is cached.
			//			if(!IsConnStringCached()){throw ExceptionManager.HandleException(new ConnStrNotCachedException(),"DataHelper",MethodInfo.GetCurrentMethod());}
			//			//Check if Provider is cached.
			//			if(!IsProviderSet()){throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodBase.GetCurrentMethod());}	
			//
			//			//Run overload of RunSPReturnScalar
			//			try
			//			{
			//				retVal=RunSPReturnScalar(connString,provider,procName,listOfParams);
			//			}								
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			return retVal;   
			//		}
			#endregion "Commented Method"
			/// <summary>
			/// This Overload of RunSPReturnScalar uses user defined conn string and provider.
			/// Returns data of type 'Object'.
			/// Always returns null when used against an Oracle database
			/// (as every output in Oracle is via an out parameter).
			/// WorkAround is to call this method with a parameter with direction=ReturnValue/Out
			/// (based on whether a Function is called or a SP is called), that gets filled with the output,
			/// and call the function as a void function without bothering about assigning the return value.		
			/// </summary>
			/*************************************************************
			* Method Name	: RunSPReturnScalar (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 21/05/2003
			* Author        : robin
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   21/05/2003  robin      		
			*               2)   24/09/2003  robin      With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  									 		      				
			***************************************************************/
			public static object RunSPReturnScalar(string procName,params IDbDataParameter[] listOfParams)
			{
				// Declarations.			
				object retVal=null;
				IDbConnection connection=null;		
				IActivator activator=null;
				try
				{
				/*
				 **************************************CAUTION:**********************************************
				 * Method does not have much meaning in ORACLE through OLEDB/OracleClient Provider as ExecuteScalar always 
				 * returns null from a stored proc or function.			 
				 **************************************CAUTION:**********************************************
				*/

				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;	
					//Call ExecuteScalar..	
					retVal=ExecuteScalar(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,listOfParams);			
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				finally
				{				
					if(connection!=null)connection.Dispose();
				}
				return retVal;   
			}
			public static object RunSPReturnScalar(ProviderType provider,string procName,params IDbDataParameter[] listOfParams)
			{
				/*
				 **************************************CAUTION:**********************************************
				 * Method does not have much meaning in ORACLE through OLEDB/OracleClient Provider as ExecuteScalar always 
				 * returns null from a stored proc or function.			 
				 **************************************CAUTION:**********************************************
				*/
				// Declarations.			
				object retVal=null;
				IDbConnection connection=null;		
				IActivator activator=null;
				try
				{

				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;	
					//Call ExecuteScalar..	
					retVal=ExecuteScalar(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,listOfParams);			
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				finally
				{				
					if(connection!=null)connection.Dispose();
				}
				return retVal;   
			}

			#region "Commented Code"
			/// <summary>
			/// This Overload of RunSPReturnScalar uses user passed connection object.
			/// Returns data of type 'Object'.
			/// Always returns null when used against an Oracle database
			/// (as every output in Oracle is via an out parameter).
			/// WorkAround is to call this method with a parameter with direction=ReturnValue/Out
			/// (based on whether a Function is called or a SP is called), that gets filled with the output,
			/// and call the function as a void function without bothering about assigning the return value.		
			/// </summary>
			/*************************************************************
			* Method Name	: RunSPReturnScalar (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 21/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   21/05/2003  Robin       		
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.		  									 		      				
			***************************************************************/
			//		public static object RunSPReturnScalar(IDbConnection connection,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			/*
			//			 **************************************CAUTION:**********************************************
			//			 * Method does not have much meaning in ORACLE through OLEDB/OracleClient Provider as ExecuteScalar always 
			//			 * returns null from a stored proc or function.			 
			//			 **************************************CAUTION:**********************************************
			//			*/
			//
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			object retVal=null;
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{
			//				//Call ExecuteScalar
			//				activator=Activator.CreateInstance(provider);
			//				retVal=ExecuteScalar(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.External,listOfParams);
			//			}
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}		
			//			return retVal;
			//		}
			//
			//		public static object RunSPReturnScalar(IDbConnection connection,ProviderType provider,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			/*
			//			 **************************************CAUTION:**********************************************
			//			 * Method does not have much meaning in ORACLE through OLEDB/OracleClient Provider as ExecuteScalar always 
			//			 * returns null from a stored proc or function.			 
			//			 **************************************CAUTION:**********************************************
			//			*/
			//
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			object retVal=null;
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{
			//				//Call ExecuteScalar
			//				activator=Activator.CreateInstance(provider);
			//				retVal=ExecuteScalar(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.External,listOfParams);
			//			}
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}		
			//			return retVal;
			//		}

			#endregion "Commented Code"
			#endregion		

			#region RunSPReturnParams
			/// <summary>
			/// This Overload of RunSPReturnParams uses cached conn string to return a Dataset.
			/// Use it if you only require o/p parameters as it returns void.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSPReturnParams 
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 21/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   21/05/2003  Robin       	
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.	
			***************************************************************/
			#region "Commented Method"
			//		public static void RunSPReturnParams(string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			// Declarations.		
			//			
			//			//Check if connstring is cached.
			//			if(!IsConnStringCached()){throw ExceptionManager.HandleException(new ConnStrNotCachedException(),"DataHelper",MethodInfo.GetCurrentMethod());}
			//			//Check if Provider is cached.
			//			if(!IsProviderSet()){throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodBase.GetCurrentMethod());}	
			//
			//			//Run overload of RunSPReturnParams
			//			try
			//			{
			//				RunSPReturnParams(connString,provider,procName,listOfParams);
			//			}								
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}	
			//		}
			/// <summary>
			/// This Overload of RunSPReturnParams uses user defined conn string and provider.
			/// Use it if you only require o/p parameters as it returns void.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSPReturnParams (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 21/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   21/05/2003  Robin       		
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.
			***************************************************************/
			#endregion "Commented Method"
			public static void RunSPReturnParams(string procName,params IDbDataParameter[] listOfParams)
			{
				// Declarations.			
				IDbConnection connection;
				IActivator activator;

				try
				{
					 activator=null;
					 connection=null;
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
				//Execute method of the relevant helper class.
				
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;			
					//Call ExecuteNonQuery..	
					ExecuteNonQuery(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,listOfParams);							
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}	
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());	
				}
				
				finally
				{				
					//if(connection!=null)connection.Dispose();	
				}
			}
			public static void RunSPReturnParams(ProviderType provider,string procName,params IDbDataParameter[] listOfParams)
			{
				// Declarations.			
				IDbConnection connection=null;
				IActivator activator=null;
				//Execute method of the relevant helper class.
				try
				{
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;			
					//Call ExecuteNonQuery..	
					ExecuteNonQuery(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,listOfParams);							
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}	
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}	
				finally
				{				
					if(connection!=null)connection.Dispose();	
				}
			}
		
			public static void RunSPReturnParams(IDbTransaction transaction, string procName,params IDbDataParameter[] listOfParams)
			{
				// Declarations.			
				IDbConnection connection=null;
				IActivator activator=null;
				//Execute method of the relevant helper class.
				try
				{
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;			
					//Call ExecuteNonQuery..	
					ExecuteNonQuery(activator,transaction,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,listOfParams);							
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}	
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}	
				finally
				{				
					if(connection!=null)connection.Dispose();	
				}
			}
			#region "Commented Code"
			//		/// <summary>
			//		/// This Overload of RunSPReturnParams uses user connection object.
			//		/// Use it if you only require o/p parameters as it returns void.
			//		/// </summary>
			//		/*************************************************************
			//		* Method Name	: RunSPReturnParams (Overload)
			//		* Components	: AB.ITAM.Infrastructure.ExceptionManager
			//		* Tables Used	: N.A.
			//		* Create Date	: 21/05/2003
			//		* Author        : Robin 
			//		* Change Control #    Date       Author         Description 
			//		* *************************************************************
			//		*				1)   21/05/2003  Robin       		
			//		*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.
			//		***************************************************************/
			//		public static void RunSPReturnParams(IDbConnection connection,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.		
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{
			//				//Call ExecuteNonQuery..	
			//				activator=Activator.CreateInstance(provider);
			//				ExecuteNonQuery(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,listOfParams);							
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}				
			//		}	
			//		public static void RunSPReturnParams(IDbConnection connection,ProviderType provider,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.		
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{
			//				//Call ExecuteNonQuery..	
			//				activator=Activator.CreateInstance(provider);
			//				ExecuteNonQuery(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,listOfParams);							
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}				
			//		}	
			#endregion "Commented Code"
			#endregion	

			#region RunSPReturnArrayList
			/// <summary>
			/// This Overload of RunSPReturnArrayList uses cached conn string.Returns an ArrayList.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSPReturnArrayList
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 22/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   22/05/2003  Robin    
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.   		
			***************************************************************/
			#region"Commented Method"
			//		public static ArrayList RunSPReturnArrayList(string procName,params IDbDataParameter[] listOfParams)
			//		{	
			//			//Declarations
			//			ArrayList al=null;
			//
			//			//Check if connstring is cached.
			//			if(!IsConnStringCached()){throw ExceptionManager.HandleException(new ConnStrNotCachedException(),"DataHelper",MethodInfo.GetCurrentMethod());}
			//			//Check if Provider is cached.
			//			if(!IsProviderSet()){throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodBase.GetCurrentMethod());}	
			//
			//			//Run overload of RunSPReturnArrayList
			//			try
			//			{									
			//				al=RunSPReturnArrayList(connString,provider,procName,listOfParams);
			//			}								
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			return al;
			//		}
			#endregion"Commented Method"
			/// <summary>
			/// This Overload of RunSPReturnArrayList uses user defined conn string and provider.Returns an ArrayList.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSPReturnArrayList (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 22/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   22/05/2003  Robin       		
			*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.
			***************************************************************/
			public static ArrayList RunSPReturnArrayList(string procName,params IDbDataParameter[] listOfParams)
			{
				// Declarations.	
				ArrayList al=new ArrayList();
				IDataReader dr=null;
				IDbConnection connection=null;			
				IActivator activator=null;
				//Execute relevant method of the helper class.
				try
				{
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;			
					//Call ExecuteReader..						
					dr=ExecuteReader(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,CommandState.LeaveOpen,listOfParams);				
					al=GetRows(dr,dr.FieldCount);		
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				finally
				{
					if(dr!=null)
					{
						if(!dr.IsClosed)
						{
							dr.Close();		
						}
						dr.Dispose();
					}			
					if(connection!=null)
					{	
						if(connection.State!=ConnectionState.Closed)
						{
							connection.Close();
						}
						connection.Dispose();
					}	
				}
				return al;
			}
			public static ArrayList RunSPReturnArrayList(ProviderType provider,string procName,params IDbDataParameter[] listOfParams)
			{
				// Declarations.	
				ArrayList al=null;
				IDataReader dr=null;
				IDbConnection connection=null;			
				IActivator activator=null;
				try
				{
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
				//Execute relevant method of the helper class.
				
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;			
					//Call ExecuteReader..						
					dr=ExecuteReader(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,CommandState.LeaveOpen,listOfParams);				
					al=GetRows(dr,dr.FieldCount);		
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				finally
				{
					if(dr!=null)
					{
						if(!dr.IsClosed)
						{
							dr.Close();		
						}
						dr.Dispose();
					}			
					if(connection!=null)
					{	
						if(connection.State!=ConnectionState.Closed)
						{
							connection.Close();
						}
						connection.Dispose();
					}	
				}
				return al;
			}
			#region "Commented Method"
			//		/// <summary>
			//		/// This Overload of RunSPReturnArrayList uses user passed connection object.Returns an ArrayList.
			//		/// </summary>
			//		/*************************************************************
			//		* Method Name	: RunSPReturnArrayList (Overload)
			//		* Components	: AB.ITAM.Infrastructure.ExceptionManager
			//		* Tables Used	: N.A.
			//		* Create Date	: 22/05/2003
			//		* Author        : Robin 
			//		* Change Control #    Date       Author         Description 
			//		* *************************************************************
			//		*				1)   22/05/2003  Robin       		
			//		*               2)   24/09/2003  Robin       With the inclusion of Oracle .NET provider,I decided, to change the design towards OO.
			//		***************************************************************/
			//		public static ArrayList RunSPReturnArrayList(IDbConnection connection,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			ArrayList al=null;
			//			IDataReader dr=null;			
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{
			//				activator=Activator.CreateInstance(provider);
			//				dr=ExecuteReader(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.External,CommandState.LeaveOpen,listOfParams);				
			//				al=GetRows(dr,dr.FieldCount);
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}	
			//			finally
			//			{
			//				if(dr!=null)
			//				{
			//					if(!dr.IsClosed)
			//					{
			//						dr.Close();		
			//					}
			//					dr.Dispose();
			//				}	
			//			}
			//			return al;
			//		}
			//		public static ArrayList RunSPReturnArrayList(IDbConnection connection,ProviderType provider,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			ArrayList al=null;
			//			IDataReader dr=null;			
			//			IActivator activator=null;
			//		    //Execute relevant method of the helper class.
			//			try
			//			{
			//				activator=Activator.CreateInstance(provider);
			//				dr=ExecuteReader(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.External,CommandState.LeaveOpen,listOfParams);				
			//				al=GetRows(dr,dr.FieldCount);
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}	
			//			finally
			//			{
			//				if(dr!=null)
			//				{
			//					if(!dr.IsClosed)
			//					{
			//						dr.Close();		
			//					}
			//					dr.Dispose();
			//				}	
			//			}
			//			return al;
			//		}
			#endregion "Commented Method"
			#endregion		

			#region RunSPReturnHashTable
			/// <summary>
			/// This Overload of RunSPReturnHashTable uses cached conn string.Returns a Hashtable.
			/// They should be typically used with sp's that have sql's that return key-value pair.
			/// The first column is used as the key, while the second is used as the value.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSPReturnHashTable 
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 06/10/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   06/10/2003  Robin       		
			***************************************************************/
			#region "Commented Method"
			//		public static Hashtable RunSPReturnHashTable(string procName,params IDbDataParameter[] listOfParams)
			//		{	
			//			//Declarations
			//			Hashtable ht=null;
			//
			//			//Check if connstring is cached.
			//			if(!IsConnStringCached()){throw ExceptionManager.HandleException(new ConnStrNotCachedException(),"DataHelper",MethodInfo.GetCurrentMethod());}
			//			//Check if Provider is cached.
			//			if(!IsProviderSet()){throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodBase.GetCurrentMethod());}	
			//
			//			//Run overload of RunSPReturnArrayList
			//			try
			//			{									
			//				ht=RunSPReturnHashTable(connString,provider,procName,listOfParams);
			//			}								
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}
			//			return ht;
			//		}

			#endregion "Commented Method"
			/// <summary>
			/// This Overload of RunSPReturnHashTable uses user defined conn string and provider.Returns a Hashtable.
			/// They should be typically used with sp's that have sql's that return key-value pair.
			/// The first column is used as the key, while the second is used as the value.
			/// </summary>
			/*************************************************************
			* Method Name	: RunSPReturnHashTable 
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 06/10/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   06/10/2003  Robin       		
			***************************************************************/
			public static Hashtable RunSPReturnHashTable(string procName,params IDbDataParameter[] listOfParams)
			{
				// Declarations.	
				Hashtable ht= new Hashtable();
				IDataReader dr=null;
				IDbConnection connection=null;			
				IActivator activator=null;
				try
				{
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
				
				//Execute relevant method of the helper class.
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;			
					//Call ExecuteReader..						
					dr=ExecuteReader(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,CommandState.LeaveOpen,listOfParams);				
					ht=GetHashTable(dr);		
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				finally
				{
					if(dr!=null)
					{
						if(!dr.IsClosed)
						{
							dr.Close();		
						}
						dr.Dispose();
					}			
					if(connection!=null)
					{	
						if(connection.State!=ConnectionState.Closed)
						{
							connection.Close();
						}
						connection.Dispose();
					}	
				}
				return ht;
			}
			public static Hashtable RunSPReturnHashTable(ProviderType provider,string procName,params IDbDataParameter[] listOfParams)
			{
				
				// Declarations.	
				Hashtable ht= new Hashtable();
				IDataReader dr=null;
				IDbConnection connection=null;			
				IActivator activator=null;
				//Execute relevant method of the helper class.
				try
				{
					//Connection String cannot be null or spaces.
					if(connString==null || connString.Trim().Equals("")) 
					{
						throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
					}
					//If caller has not set the provider.
					if(provider==ProviderType.NotSet)
					{
						throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
					}
				
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					connection=activator.CreateConnection();				
					connection.ConnectionString=connString;			
					//Call ExecuteReader..						
					dr=ExecuteReader(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,CommandState.LeaveOpen,listOfParams);				
					ht=GetHashTable(dr);		
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());	
				}
				
				finally
				{
					if(dr!=null)
					{
						if(!dr.IsClosed)
						{
							dr.Close();		
						}
						dr.Dispose();
					}			
					if(connection!=null)
					{	
						if(connection.State!=ConnectionState.Closed)
						{
							connection.Close();
						}
						connection.Dispose();
					}	
				}
				return ht;
			}
			#region "Commented Code"
			//		/// <summary>
			//		/// This Overload of RunSPReturnHashTable uses user passed connection object.Returns a Hashtable.
			//		/// They should be typically used with sp's that have sql's that return key-value pair.
			//		/// The first column is used as the key, while the second is used as the value.
			//		/// </summary>
			//		/*************************************************************
			//		* Method Name	: RunSPReturnHashTable 
			//		* Components	: AB.ITAM.Infrastructure.ExceptionManager
			//		* Tables Used	: N.A.
			//		* Create Date	: 06/10/2003
			//		* Author        : Robin 
			//		* Change Control #    Date       Author         Description 
			//		* *************************************************************
			//		*				1)   06/10/2003  Robin       		
			//		***************************************************************/
			//		public static Hashtable RunSPReturnHashTable(IDbConnection connection,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			Hashtable ht=null;
			//			IDataReader dr=null;			
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{
			//				activator=Activator.CreateInstance(provider);
			//				dr=ExecuteReader(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.External,CommandState.LeaveOpen,listOfParams);				
			//				ht=GetHashTable(dr);
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}	
			//			finally
			//			{
			//				if(dr!=null)
			//				{
			//					if(!dr.IsClosed)
			//					{
			//						dr.Close();		
			//					}
			//					dr.Dispose();
			//				}	
			//			}
			//			return ht;
			//		}
			//		public static Hashtable RunSPReturnHashTable(IDbConnection connection,ProviderType provider,string procName,params IDbDataParameter[] listOfParams)
			//		{
			//			//If caller has not sent a valid connection.
			//			if(connection==null) 
			//			{
			//				throw ExceptionManager.HandleException(new NullConnectionException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			//If caller has not set the provider.
			//			if(provider==ProviderType.NotSet)
			//			{
			//				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			//			}
			//			// Declarations.
			//			Hashtable ht=null;
			//			IDataReader dr=null;			
			//			IActivator activator=null;
			//			//Execute relevant method of the helper class.
			//			try
			//			{
			//				activator=Activator.CreateInstance(provider);
			//				dr=ExecuteReader(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.External,CommandState.LeaveOpen,listOfParams);				
			//				ht=GetHashTable(dr);
			//			}	
			//			catch(CustomException ex)
			//			{
			//				throw ex;
			//			}			
			//			catch(System.Exception ex)
			//			{
			//				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			//			}	
			//			finally
			//			{
			//				if(dr!=null)
			//				{
			//					if(!dr.IsClosed)
			//					{
			//						dr.Close();		
			//					}
			//					dr.Dispose();
			//				}	
			//			}
			//			return ht;
			//		}
			#endregion "Commented Code"
			#endregion		

			#region IsConnectionString Cached?
			/// <summary>
			/// Returns true if connection string is cached.
			/// </summary>
			/*************************************************************
			* Method Name	: IsConnStringCached 
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 23/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   23/05/2003  Robin       		
			***************************************************************/
			public static bool IsConnStringCached()
			{
				try
				{
					if(connString==null || connString.Trim().Equals(""))
					{
						return false;
					}
					else
					{
						return true;
					}
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());			
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());			
				}
			}
			/// <summary>
			/// Returns true if user provided conn string and cached conn string match.
			/// Can be used by user before deciding to use functions with explicit conn string and
			/// provider as parameters or using functions which use cache settings.
			/// </summary>
			/*************************************************************
			* Method Name	: IsConnStringCached (Overload)
			* Components	: AB.ITAM.Infrastructure.ExceptionManager
			* Tables Used	: N.A.
			* Create Date	: 23/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   23/05/2003  Robin       		
			***************************************************************/
			public static bool IsConnStringCached (string connectionString)
			{
				try
				{
					if(connectionString.Equals(connString))
					{
						return true;
					}
					else
					{
						return false;
					}
				}
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());			
				}
				catch(System.IO.FileNotFoundException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodInfo.GetCurrentMethod());			
				}
			}			
			#endregion	
			
		 
			public static IDataReader RunSPReturnDataReader(IDbConnection connection, string procName,params IDbDataParameter[] listOfParams)
		{
			
				// Declarations.
				IDataReader dr=null;
				//IDbConnection connection=null;		
				IActivator activator=null;
				//Execute relevant method of the helper class.
				try
				{	
				//Connection String cannot be null or spaces.
			if(connString==null || connString.Trim().Equals("")) 
			{
				throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
			}
			//If caller has not set the provider.
			if(provider==ProviderType.NotSet)
			{
				throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
			}
			
				//Create the connection(Using default ctor for activator as other ctors take more time.)
				activator=Activator.CreateInstance(provider);
				//connection=activator.CreateConnection();				
				//connection.ConnectionString=connString;		
				//Call ExecuteDataset..	
				dr=ExecuteReader(activator,connection,CommandType.StoredProcedure,procName,ConnectionOwnership.Internal,CommandState.LeaveOpen,listOfParams);								
			}
			catch(CustomException ex)
			{
				throw ex;
			}			
			catch(System.Data.DataException ex)			{
				throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
			}	
			
			finally
			{				
				//if(connection!=null)connection.Dispose();
			}
			return dr;   
		}				
		
			public static IDataReader RunSQLReturnDataReader(IDbConnection connection, string strSql,params IDbDataParameter[] listOfParams)
			{
				// Declarations.
				IDataReader dr=null;
				//IDbConnection connection=null;	
				IActivator activator=null;
				//Execute relevant method of the helper class.
				try
				{
				//Connection String cannot be null or spaces.
				if(connString==null || connString.Trim().Equals("")) 
				{
					throw ExceptionManager.HandleException(new NullConnectionStringException(),"DataHelper",MethodBase.GetCurrentMethod());
				}
				//If caller has not set the provider.
				if(provider==ProviderType.NotSet)
				{
					throw ExceptionManager.HandleException(new ProviderNotSetException(),"DataHelper",MethodInfo.GetCurrentMethod());
				}
											
					//Create the connection(Using default ctor for activator as other ctors take more time.)
					activator=Activator.CreateInstance(provider);
					//connection=activator.CreateConnection();				
					//connection.ConnectionString=connString;			
					//Call ExecuteDataset..
					dr=ExecuteReader(activator,connection,CommandType.Text,strSql,ConnectionOwnership.Internal,CommandState.LeaveOpen ,listOfParams);				
				}
				catch(CustomException ex)
				{
					throw ex;
				}			
				catch(System.Data.DataException ex)
				{
					throw ExceptionManager.HandleException(ex,"DataHelper",MethodBase.GetCurrentMethod());					
				}
				finally
				{				
					if(connection!=null)connection.Dispose();
				}
				return dr;   
			}

			#endregion

			#region Custom Exception Classes
			/// <summary>
			/// ConnStrNotCachedException Exception Class gets raised when Connection String
			/// has not been cached, and a method is being tried to excute without the 
			/// connection string explicitly specified.
			/// </summary>
			/*************************************************************
			* Method Name	: class/ConnStrNotCachedException 
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 30/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   30/05/2003  Robin       		
			***************************************************************/
			private class ConnStrNotCachedException:CodeBasedException
			{
				/// <summary>
				/// Message=Connection String not cached. 101
				/// </summary>		
				public ConnStrNotCachedException():base(101)
				{
				}			
			}  
			/// <summary>
			/// ConnStrSetOnceException Exception Class gets raised when Connection String
			/// has been cached and an attempt is made to cache it again.
			/// </summary>
			/*************************************************************
			* Method Name	: class/ConnStrSetOnceException 
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 30/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   30/05/2003  Robin       		
			***************************************************************/
			private class ConnStrSetOnceException:CodeBasedException
			{
				/// <summary>
				/// Message=Connection String has been cached once already and cannot be cached again. 102		 
				/// </summary>	
				public ConnStrSetOnceException():base(102)
				{
				}			
			}  
			/// <summary>
			/// ProviderNotSetException Exception Class gets raised when provider enum
			/// has not been set and a method is being tried to excute.
			/// </summary>
			/*************************************************************
			* Method Name	: class/ProviderNotSetException 
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 30/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   30/05/2003  Robin       		
			***************************************************************/
			private class ProviderNotSetException:CodeBasedException
			{
				/// <summary>
				/// Message=Provider Type not set. 103
				/// </summary>		
				public ProviderNotSetException():base(103)
				{
				}			
			}		
			/// <summary>
			/// NullConnectionException Exception Class gets raised when the connection 
			/// object passed as an arguement is null.
			/// </summary>
			/*************************************************************
			* Method Name	: class/NullConnectionException 
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 30/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   30/05/2003  Robin       		
			***************************************************************/
			private class NullConnectionException:CodeBasedException
			{
				/// <summary>
				/// Message=The connection object parameter that is passed equates to null. 104 
				/// </summary>		
				public NullConnectionException():base(104)
				{
				}			
			}
			/// <summary>
			/// NullTransactionException Exception Class gets raised when the transaction 
			/// object passed as an arguement is null.
			/// </summary>
			/*************************************************************
			* Method Name	: class/NullTransactionException 
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 30/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   30/05/2003  Robin       		
			***************************************************************/
			private class NullTransactionException:CodeBasedException
			{
				/// <summary>
				/// Message=The transaction object parameter that is passed, equates to null. 105
				/// </summary>	
				public NullTransactionException():base(105)
				{
				}			
			}
			/// <summary>
			/// NullCommandException Exception Class gets raised when the command 
			/// object passed as an arguement is null.
			/// </summary>
			/*************************************************************
			* Method Name	: class/NullCommandException 
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 30/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   30/05/2003  Robin       		
			***************************************************************/
			private class NullCommandException:CodeBasedException
			{
				/// <summary>
				/// Message=The command object parameter that is passed equates to null. 106
				/// </summary>		
				public NullCommandException():base(106)
				{			
				}			
			}
			/// <summary>
			/// NullConnectionStringException Exception Class gets raised when the connection 
			/// string passed as an arguement equates to either null or spaces.
			/// </summary>
			/*************************************************************
			* Method Name	: class/NullConnectionStringException 
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 30/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   30/05/2003  Robin       		
			***************************************************************/
			private class NullConnectionStringException:CodeBasedException
			{
				/// <summary>
				/// Message=The connection string parameter that is passed equates to either null or spaces. 107
				/// </summary>		
				public NullConnectionStringException():base(107)
				{
				}			
			}
			/// <summary>
			/// NullCommandTextException Exception Class gets raised when the command text 
			/// string passed as an arguement equates to either null or spaces.
			/// </summary>
			/*************************************************************
			* Method Name	: class/NullCommandTextException 
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 30/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   30/05/2003  Robin       		
			***************************************************************/
			private class NullCommandTextException:CodeBasedException
			{
				/// <summary>
				/// Message=The command text parameter that is passed equates to either null or spaces. 108
				/// </summary>		
				public NullCommandTextException():base(108)
				{
				}			
			}	
			/// <summary>
			/// SetConnectionToNullException Exception Class gets raised when an attempt is 
			/// made to set the connection string to null or spaces.
			/// </summary>
			/*************************************************************
			* Method Name	: class/SetConnectionToNullException 
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 30/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   30/05/2003  Robin       		
			***************************************************************/
			private class SetConnectionToNullException:CodeBasedException
			{
				/// <summary>
				/// Message=The connection string cannot be set to either null or spaces. 109
				/// </summary>		
				public SetConnectionToNullException():base(109)
				{
				}			
			}
			/// <summary>
			/// TransactionExpiredException Exception Class gets raised when an attempt is 
			/// made to commit or rollback a transaction whose connection equates to null.
			/// </summary>
			/*************************************************************
			* Method Name	: class/TransactionExpiredException 
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 30/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   30/05/2003  Robin       		
			***************************************************************/
			private class TransactionExpiredException:CodeBasedException
			{
				/// <summary>
				/// Message=The connection string cannot be set to either null or spaces. 110
				/// </summary>		
				public TransactionExpiredException():base(110)
				{
				}			
			}
			/// <summary>
			/// NullParameterException Exception Class gets raised when the 'ParameterName' 
			/// string passed as an arguement equates to either null or spaces.
			/// </summary>
			/*************************************************************
			* Method Name	: class/NullParameterException 
			* Components	: 
			* Tables Used	: N.A.
			* Create Date	: 30/05/2003
			* Author        : Robin 
			* Change Control #    Date       Author         Description 
			* *************************************************************
			*				1)   30/05/2003  Robin       		
			***************************************************************/
			private class NullParameterException:CodeBasedException
			{
				/// <summary>
				/// Message=The parameter name that is passed for the command parameter, equates to either null or spaces. 111
				/// </summary>
				public NullParameterException():base(111)
				{
				}			
			}
			#endregion	

            #region Connection String

          

            public static void  GetConnectionString(int WorkStationID, string DbType)
           {
               try
                   
               {
                   connString = ConfigurationManager.ConnectionStrings[MDBSType].ConnectionString;
                   int HospitalId = GetWorkLocationId(WorkStationID);
                   if (MDBSType == DbType || HospitalId == 0)
                   {
                       //Master Database;
                       connString = ConfigurationManager.ConnectionStrings[MDBSType].ConnectionString;

                   }
                   else
                   {

                       String TransDbtype = ConfigurationManager.AppSettings.Get(HospitalId).ToString();
                       connString = ConfigurationManager.ConnectionStrings[TransDbtype].ConnectionString;

                   }
               }
               catch (Exception ex)
               {
                   throw ex;
               }
              
            
              
           }
           public static int  GetWorkLocationId(int WorkStationID)
           {
               string sqlText="select dbo.Fn_GetHospitalid("+ WorkStationID + ",1)";
               object objWorkLocationId= RunSQLReturnScalar(sqlText);
               int intWorkLocationId = Int32.Parse(objWorkLocationId.ToString ());
               return intWorkLocationId;
           }             
           

        #endregion Connection String
		


		}	

	#endregion

	
}
