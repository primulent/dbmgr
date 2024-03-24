using System;
using System.Data;
using System.Data.Common;

namespace dbmgr.utilities.data
{
    public static class DataExtensions
    {
        /// <summary>
        /// Creates the command.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="commandText">The command text.</param>
        /// <param name="commandType">Type of the command.</param>
        /// <param name="transaction">The transaction.</param>
        /// <param name="commandTimeout">The command timeout.</param>
        /// <returns>
        /// An IDbCommand object.
        /// </returns>
        /// <exception cref="System.NullReferenceException">The IDbConnection is null.</exception>
        /// <exception cref="System.ArgumentException">commandText;The command text cannot be null, empty, or contain only whitespace.</exception>
        public static IDbCommand CreateCommand(this IDbConnection connection, string commandText, CommandType commandType = CommandType.Text, IDbTransaction? transaction = null, int commandTimeout = 30)
        {
            if (connection == null)
            {
                throw new NullReferenceException();
            }

            if (string.IsNullOrWhiteSpace(commandText))
            {
                throw new ArgumentException("The command text cannot be null, empty, or contain only whitespace.", "Argument");
            }

            IDbCommand command = connection.CreateCommand();
            command.CommandText = commandText;
            command.CommandType = commandType;
            command.Transaction = transaction;
            command.CommandTimeout = commandTimeout;
            return command;
        }

        public static string GetStringSafe(this IDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
                return null;

            return reader.GetString(index);
        }
    }
}