using dbmgr.utilities.data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Transactions;

namespace dbmgr.utilities.db
{
    public class OracleScripts : IDBScripts
    {
        public string ShortName { get { return "oracle"; } }
        public string DbConnectionKey { get { return "OracleData"; } }

        public string[] ParseStandardConnection(string dbName, string? dbServer = null, string? dbPort = null, string? dbUser = null, string? dbPwd = null, string? opt1 = null, string? opt2 = null)
        {
            string[]? parameters = null;
            return parameters;
        }

        public string[] ParseProviderConnection(string args)
        {
            // format: user/password@sid:host/port

            string[]? parameters = null;

            string[] first = args.Split('@');
            if (first.Length == 2)
            {
                List<string> p = new List<string>(5);

                string[] second = first[0].Split('/');
                if (second.Length == 2)
                {
                    p.Add(second[0]); // uid
                    p.Add(second[1]); // pwd
                }

                // parse sid out
                string[] third = first[1].Split('/');
                if (third.Length == 2)
                {
                    p.Insert(0, third[1]); // sid

                    string[] fourth = third[0].Split(':');
                    if (fourth.Length == 2)
                    {
                        p.Add(fourth[0]); // host
                        p.Add(fourth[1]); // port
                    }
                }
                else
                {
                    p.Insert(0, first[1]); // sid
                }

                parameters = p.ToArray();
            }

            return parameters;
        }

        public string GetFileNameExtension()
        {
            return ".sql";
        }

        public string[] GetScriptTypes()
        {
            return new string[] { "FN", "VW", "SP", "PKG", "PKGB", "TR" };
        }

        public string GetFileNamePrefix(string type)
        {
            return GetScriptTypes().Where(n => n == type).FirstOrDefault();
        }

        public bool ValidateFile(string fileName)
        {
            return true;
        }

        public List<string> GetExtractSchema(DataContext dataContext, string schema_name)
        {
            throw new NotImplementedException("Feature not available in Oracle");
        }

        public string GetExtractSQL(string type)
        {
            throw new NotImplementedException("Feature not available in Oracle");
        }


        public string GetDependenciesSQL(string name)
        {
            throw new NotImplementedException("Feature not available in Oracle");
        }

        public string[] GetScriptDirectoryNames()
        {
            throw new NotImplementedException("Feature not available in Oracle");
        }

        public string GetParameterName(string input)
        {
            return string.Concat(":", input);
        }

        public string GetSplitPhrase()
        {
            return "^\\s*/";
        }

        public void EnlistTransaction(DbConnection connection)
        {
            // Handle Oracle connection not automatically enlisting in transaction
            //var oc = connection as OracleConnection;
            //if (oc != null && Transaction.Current != null)
            //{
            //    oc.EnlistTransaction(Transaction.Current);
            //}
        }

        public string GetTestConnectionSQL()
        {
            return "SELECT * FROM DUAL";
        }

        public string GetCheckCurrentStructureSQL()
        {
            return "SELECT COUNT(*) FROM DB_SCRIPT_INFO";
        }

        public string GetCheckMigrationStructureSQL()
        {
            return "SELECT COUNT(*) FROM DB_VERSION";
        }

        public string GetCheckMigrationRecordSQL(string versionTimestamp)
        {
            return string.Format("SELECT COUNT(VERSION) FROM DB_VERSION WHERE VERSION = '{0}'", versionTimestamp);
        }

        public string GetInsertMigrationRecordSQL()
        {
            return "INSERT INTO DB_VERSION (SYSTEM_ID, VERSION) VALUES (1, :version)";        
        }

        public string GetRemoveMigrationRecordSQL()
        {
            return "DELETE FROM DB_VERSION WHERE SYSTEM_ID = 1 AND VERSION = :version";
        }

        public string GetUpdateCurrentRecordSQL()
        {
            return "UPDATE DB_SCRIPT_INFO SET CHECKSUM = :checksum, LENGTH = :length, UPDATE_TIME = :modified WHERE SYSTEM_ID = :sysId AND NAME = :scriptname";
        }

        public string GetInsertCurrentRecordSQL()
        {
            return "INSERT INTO DB_SCRIPT_INFO (SYSTEM_ID, NAME, CHECKSUM, LENGTH, UPDATE_TIME) VALUES (:sysId, :scriptname, :checksum, :length, :modified)";
        }

        public string GetUpdateSystemRecordSQL()
        {
            return "UPDATE SystemInfo SET {0} = :url WHERE SYSTEM_ID = 1";
        }

        public string GetCurrentRecordSQL(int systemId)
        {
            return string.Format("SELECT NAME, CHECKSUM, LENGTH FROM DB_SCRIPT_INFO WHERE SYSTEM_ID = {0}", systemId);
        }

        public string GetCreateCurrentTrackingSQL()
        {
            return @"
BEGIN
    execute immediate 'CREATE TABLE DB_SCRIPT_INFO (
        SYSTEM_ID INTEGER CONSTRAINT NN_DB_SCRIPT_INFO_SYSTEM_ID NOT NULL,
        NAME VARCHAR2(255) CONSTRAINT NN_DB_SCRIPT_INFO_NAME NOT NULL,
        CHECKSUM INTEGER CONSTRAINT NN_DB_SCRIPT_INFO_CHECKSUM NOT NULL,
        LENGTH INTEGER CONSTRAINT NN_DB_SCRIPT_INFO_LENGTH NOT NULL,
        UPDATE_TIME TIMESTAMP DEFAULT SYSTIMESTAMP CONSTRAINT NN_DB_SCRIPT_INFO_UPDATE_TIME NOT NULL,
        CONSTRAINT PK_DB_SCRIPT_INFO PRIMARY KEY (SYSTEM_ID, NAME)
    )';

    execute immediate 'COMMENT ON COLUMN DB_SCRIPT_INFO.SYSTEM_ID IS ''The unique system ID for the schema changes''';
    execute immediate 'COMMENT ON COLUMN DB_SCRIPT_INFO.NAME IS ''The name of this script; corresponds to the filename''';
    execute immediate 'COMMENT ON COLUMN DB_SCRIPT_INFO.CHECKSUM IS ''The checksum of this script at time of update.''';
    execute immediate 'COMMENT ON COLUMN DB_SCRIPT_INFO.LENGTH IS ''The length of this script at time of update.''';
    execute immediate 'COMMENT ON COLUMN DB_SCRIPT_INFO.UPDATE_TIME IS ''The last time this script was run.''';
END;
";
        }

        public string GetCreateMigrationTrackingSQL()
        {
            return @"
BEGIN
    execute immediate 'CREATE TABLE DB_VERSION (
        SYSTEM_ID INTEGER CONSTRAINT NN_DB_VERSION_SYSTEM_ID NOT NULL,
        VERSION VARCHAR2(255) CONSTRAINT NN_DB_VERSION_VERSION NOT NULL,
        CREATE_TIME TIMESTAMP DEFAULT SYSTIMESTAMP CONSTRAINT NN_DB_VERSION_CREATE_TIME NOT NULL,
        CONSTRAINT PK_DB_VERSION PRIMARY KEY (SYSTEM_ID, VERSION)
    )';

    execute immediate 'COMMENT ON COLUMN DB_VERSION.SYSTEM_ID IS ''The unique system ID for the schema changes''';
    execute immediate 'COMMENT ON COLUMN DB_VERSION.VERSION IS ''The version ID for this delta script''';
    execute immediate 'COMMENT ON COLUMN DB_VERSION.CREATE_TIME IS ''The time this script was run.''';
END;
";
        }
    }
}