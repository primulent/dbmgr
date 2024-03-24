using dbmgr.utilities.common;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;

namespace dbmgr.utilities.data
{
    public class DataContext : IDisposable
    {
        private readonly int _defaultTimeout;
        private readonly int _defaultTransTimeout;
        private readonly Action<DbConnection> _enlistTransaction;

        private string _connectionString { get; set; }
        private DbProviderFactory _dbProviderFactory { get; set; }
        private DbConnection? _connection = null;

        public static string? GetConnectionString(string connectionString, string[]? replacementParameters)
        {
            try
            {
                return string.Format(connectionString, replacementParameters);
            }
            catch (ArgumentNullException)
            {
                Log.Logger.Warning("No connection parameters passed to the connection string.");
            }
            catch (FormatException)
            {
                Log.Logger.Warning("Wrong connection parameters passed to the connection string; they do not match what is expected.");
            }

            return null;
        }

        public DataContext(string connectionProvider, string connectionString, Action<DbConnection> enlistTransaction, int defaultTimeout, int defaultTransTimeout)
        {
            _enlistTransaction = enlistTransaction;
            _defaultTimeout = defaultTimeout;
            _defaultTransTimeout = defaultTransTimeout;

            try
            {
                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    this._connectionString = connectionString;
                    Log.Logger.Verbose(connectionString);
                }
                else
                {
                    throw new ArgumentException("No connection string");
                }
                // Register Providers
                DbProviderFactories.RegisterFactory("System.Data.SqlClient", SqlClientFactory.Instance);

                // Grab provider
                this._dbProviderFactory = SafeProviderName(connectionProvider);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException("A connection string must be specified. Provider: " + connectionProvider + " Connection: " + connectionString);
            }
        }

        /// <summary>
        /// Returns a consistently constructed TransactionScope object
        /// </summary>
        /// <param name="tso">TransactionScopeOption for the transaction</param>
        /// <returns>Returns a TransactionScope object that has it's options set properly and timeout configured from configuration</returns>
        public TransactionScope StartTransaction(TransactionScopeOption tso)
        {
            TransactionOptions to = new TransactionOptions
            {
                IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
                Timeout = new TimeSpan(0, _defaultTransTimeout, 0)
            };

            return new TransactionScope(tso, to);
        }

        /// <summary>
        /// Creates the parameter.
        /// </summary>
        /// <param name="parameterName">Name of the parameter.</param>
        /// <param name="parameterValue">The parameter value.</param>
        /// <returns>The parameter.</returns>
        public IDbDataParameter CreateParameter(string parameterName, object parameterValue)
        {
            // Handle pseudo-NULL values
            if (parameterValue is DateTime && ((DateTime)parameterValue) == DateTime.MinValue)
            {
                parameterValue = DBNull.Value;
            }

            if (parameterValue == null)
            {
                parameterValue = DBNull.Value;
            }

            DbParameter? parameter = this._dbProviderFactory.CreateParameter();
            if (parameter != null)
            {
                parameter.ParameterName = parameterName;
                parameter.Value = parameterValue;
            }

            return parameter;
        }

        private bool isDisposed;

        // Dispose() calls Dispose(true)
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // The bulk of the clean-up code is implemented in Dispose(bool)
        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed) return;

            if (disposing)
            {
                // free managed resources
                _connection?.Dispose();
                _connection = null;
            }

            isDisposed = true;
        }

        /// <summary>
        /// Executes the non query.
        /// </summary>
        /// <param name="sql">The SQL.</param>
        /// <param name="parameterList">The parameter list.</param>
        /// <param name="timeout">The timeout.</param>
        public virtual int ExecuteNonQuery(string sql, List<IDbDataParameter>? parameterList = null, int? timeout = null)
        {
            return this.Execute<int>(sql, CommandType.Text, parameterList, command => command.ExecuteNonQuery(), timeout ?? _defaultTimeout);
        }

        /// <summary>
        /// Executes the reader.
        /// </summary>
        /// <param name="sql">The SQL.</param>
        /// <param name="parameterList">The parameter list.</param>
        /// <returns>The DataReader object.</returns>
        public IDataReader ExecuteReader(string sql, List<IDbDataParameter>? parameterList = null)
        {
            return this.Execute<IDataReader>(sql, CommandType.Text, parameterList, command => command.ExecuteReader());
        }

        /// <summary>
        /// Executes the scalar.
        /// </summary>
        /// <param name="sql">The SQL.</param>
        /// <param name="parameterList">The parameter list.</param>
        /// <returns>The scalar object.</returns>
        public virtual object ExecuteScalar(string sql, List<IDbDataParameter>? parameterList = null)
        {
            return this.Execute<object>(sql, CommandType.Text, parameterList, command => command.ExecuteScalar());
        }

        /// <summary>
        /// Executes the script.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="splitCharacter">How to split the file into chunks.</param>
        /// <param name="scriptReplacements">Optional token/replacement values.</param>
        public virtual void ExecuteScript(string fileName, string splitCharacter, Dictionary<string, string>? scriptReplacements = null)
        {
            FileInfo fi = new FileInfo(fileName);
            using (StreamReader sr = new StreamReader(fi.Open(FileMode.Open, FileAccess.Read)))
            {
                if (!string.IsNullOrWhiteSpace(splitCharacter))
                {
                    string script = sr.ReadToEnd();

                    // look for script replacements
                    if (scriptReplacements != null)
                    {
                        script = CommonUtilities.ReplaceTokensInContent(script, scriptReplacements);
                    }

                    string[] chunks = Regex.Split(script, splitCharacter, RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase);
                    foreach (string text in chunks)
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(text))
                                continue;

                            ExecuteNonQuery(text);
                        }
                        catch (Exception ex)
                        {
                            throw new NotSupportedException("Failed running script " + fileName + ".  ERROR: " + ex.Message + "\n\nSCRIPT: " + text + "\nLENGTH: " + text.Length, ex);
                        }
                    }
                }
                else
                {
                    ExecuteNonQuery(sr.ReadToEnd());
                }
            }
        }

        /// <summary>
        /// Gets the connection.
        /// </summary>
        /// <returns>The database connection.</returns>
        public DbConnection GetConnection()
        {
            if (_connection != null && _connection.State == ConnectionState.Closed)
            {
                _connection.Dispose();
                _connection = null;
            }

            if (_connection == null)
            {
                DbConnection? conn = this._dbProviderFactory.CreateConnection();
                if (conn != null)
                {
                    conn.ConnectionString = this._connectionString;
                    conn.Open();
                }
                _connection = conn;
            }

            _enlistTransaction?.Invoke(_connection);

            return _connection;
        }

        internal static DbProviderFactory SafeProviderName(string key)
        {
            return DbProviderFactories.GetFactory(string.IsNullOrWhiteSpace(key) ? "System.Data.SqlClient" : key);
        }

        /// <summary>
        /// Executes the specified command text.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="commandText">The command text.</param>
        /// <param name="commandType">Type of the command.</param>
        /// <param name="parameters">The parameters.</param>
        /// <param name="commandExecute">The command execute.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns>
        /// The result of executing the command.
        /// </returns>
        private TResult Execute<TResult>(string commandText, CommandType commandType, IList<IDbDataParameter>? parameters, Func<IDbCommand, TResult> commandExecute, int? timeout = null)
        {
            TResult? result = default;
            try
            {
                IDbConnection connection = this.GetConnection();
                using (IDbCommand command = connection.CreateCommand(commandText, commandType, null, timeout ?? _defaultTimeout))
                {
                    if (parameters != null)
                    {
                        foreach (IDbDataParameter parameter in parameters)
                        {
                            command.Parameters.Add(parameter);
                        }
                    }
                    result = commandExecute(command);
                }
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder();
                if (commandType == CommandType.Text)
                {
                    sb.Append(commandText).Append(";");
                }
                else
                {
                    sb.Append(commandText);
                }

                if (parameters != null)
                {
                    if (commandType == CommandType.Text)
                    {
                        sb.Append(", ");
                    }
                    else
                    {
                        sb.Append(" ");
                    }

                    for (int counter = 0; counter < parameters.Count; counter++)
                    {
                        if (counter > 0)
                        {
                            sb.Append(", ");
                        }

                        if (parameters[counter].Value == null || parameters[counter].Value == DBNull.Value)
                        {
                            sb.AppendFormat("{0} = NULL", parameters[counter].ParameterName);
                        }

                        if (parameters[counter].DbType == DbType.AnsiString || parameters[counter].DbType == DbType.AnsiStringFixedLength || parameters[counter].DbType == DbType.Date || parameters[counter].DbType == DbType.DateTime || parameters[counter].DbType == DbType.DateTime2 || parameters[counter].DbType == DbType.DateTimeOffset || parameters[counter].DbType == DbType.String || parameters[counter].DbType == DbType.StringFixedLength || parameters[counter].DbType == DbType.Time || parameters[counter].DbType == DbType.Xml)
                        {
                            sb.AppendFormat("{0} = '{1}'", parameters[counter].ParameterName, parameters[counter].Value);
                            continue;
                        }

                        sb.AppendFormat("{0} = {1}", parameters[counter].ParameterName, parameters[counter].Value);
                    }
                }

                Log.Logger.Debug("SQL Exception [" + sb.ToString() + "] " + ex.ToString());

                throw;
            }

            return result;
        }
    }
}