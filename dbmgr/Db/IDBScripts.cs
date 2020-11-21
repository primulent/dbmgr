using dbmgr.utilities.data;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace dbmgr.utilities.db
{
    public interface IDBScripts
    {
        string ShortName { get; }

        string DbConnectionKey { get; }

        string[] ParseProviderConnection(string args);

        string[] ParseStandardConnection(string dbName, string dbServer = null, string dbPort = null, string dbUser = null, string dbPwd = null, string opt1 = null, string opt2 = null);

        void EnlistTransaction(DbConnection connection);

        /// <summary>
        /// Returns the file extension to use for current script filenames; typically ".sql"
        /// </summary>
        string GetFileNameExtension();

        /// <summary>
        /// During extraction of current scripts, will put the types into these directories
        /// </summary>
        /// <returns>Returns the list of directory names that correspond to the types</returns>
        string[] GetScriptDirectoryNames();

        /// <summary>
        /// Returns the types and the order into when to process them; this is used for extraction as a parameter into the GetExtractSQL query for the type
        /// </summary>
        string[] GetScriptTypes();

        /// <summary>
        /// For the given type returned from ScriptTypes, what filename should we use.  During extraction, must also handle the individual type returned from the dependency SQL to determine the filename to link to. 
        /// </summary>
        string GetFileNamePrefix(string type);
        /// <summary>
        /// SQL that will be used to extract for each ScriptType passed in.
        /// </summary>
        string GetExtractSQL(string type);

        List<string> GetExtractSchema(DataContext dataContext, string schema_name);

        /// <summary>
        /// SQL that will be used to determine the dependencies for a given current script
        /// </summary>
        string GetDependenciesSQL(string name);

        /// <summary>
        /// SQL used to test if a SQL connection is valid
        /// </summary>
        string GetTestConnectionSQL();

        /// <summary>
        /// Returns a SQL statement that will generate an error if there is no migration tracking table
        /// </summary>
        /// <returns>Count of rows in the migration tracking table</returns>
        string GetCheckMigrationStructureSQL();
        /// <summary>
        /// Returns a SQL statement that will generate an error if there is no script tracking table
        /// </summary>
        /// <returns>Count of rows in the script tracking table</returns>
        string GetCheckCurrentStructureSQL();

        /// <summary>
        /// Returns a SQL statement that indicates the number of rows in the migration tracking table that have the timestamp indicated.
        /// </summary>
        /// <returns>Count of rows that match the timestamp</returns>
        string GetCheckMigrationRecordSQL(string versionTimestamp);

        string GetInsertMigrationRecordSQL();

        string GetUpdateCurrentRecordSQL();

        string GetInsertCurrentRecordSQL();

        string GetCurrentRecordSQL(int systemId);

        string GetCreateCurrentTrackingSQL();

        string GetCreateMigrationTrackingSQL();

        string GetUpdateSystemRecordSQL();

        string GetParameterName(string input);

        string GetSplitPhrase();
    }
}