using Serilog;
using Serilog.Core;
using System.Collections.Generic;
using System.Data.Common;

namespace dbmgr.utilities.db
{
    public class SQLServerScripts : IDBScripts
    {
        public string ShortName { get { return "mssql"; } }

        public string DbConnectionKey { get { return "SqlServerData"; } }

        public string[] ParseStandardConnection(string dbName, string dbServer = null, string dbPort = null, string dbUser = null, string dbPwd = null, string opt1 = null, string opt2 = null)
        {
            bool.TryParse(opt1, out bool integratedSecurity);
            string[] parameters = new string[5];
            parameters[0] = dbName;
            parameters[1] = dbServer;
            parameters[2] = dbUser;
            parameters[3] = dbPwd;
            parameters[4] = integratedSecurity.ToString();
            if (!string.IsNullOrEmpty(dbUser))
            {
                parameters[4] = "True";
            }

            return parameters;
        }

        public string[] ParseProviderConnection(string args)
        {
            // format: [user:password@]myServer\instanceName

            bool integratedSecurity = false;
            string[] parameters = new string[5];

            string serverAndDatabase = null;

            string[] first = args.Split('@');
            if (first.Length == 1)
            {
                serverAndDatabase = first[0];

                // Windows Authentication
                integratedSecurity = true;
            }
            else if (first.Length == 2)
            {
                serverAndDatabase = first[1];

                // SQL Server Authentication
                string[] auth = first[0].Split(':');
                if (auth.Length == 2)
                {
                    parameters[2] = auth[0]; // user
                    parameters[3] = auth[1]; // pwd
                }
                else
                {
                    Log.Logger.Error("Found the wrong number of :'s in the connection information: {0}", first[0]);
                    return null;
                }
            }
            else
            {
                Log.Logger.Error("Found too many @'s in the connection information: {0}", args);
                return null;
            }

            // Process Server and Database            
            string[] second = serverAndDatabase.Split('\\');
            if (second.Length == 2)
            {
                parameters[0] = second[1]; // database
                parameters[1] = second[0]; // server          
                parameters[4] = integratedSecurity.ToString();
            }
            else
            {
                Log.Logger.Error("Found the wrong number of \\'s in the connection information: {0}", serverAndDatabase);
                return null;
            }

            return parameters;
        }

        public string GetFileNameExtension()
        {
            return ".sql";
        }

        public string[] GetScriptDirectoryNames()
        {
            return new string[] { "Other", "Synonyms", "Functions", "Views", "StoredProcedures", "Triggers" };
        }

        public string[] GetScriptTypes()
        {
            return new string[] { "'SO'", "'SN'", "'FN','TF','AF'", "'V'", "'P'", "'TR'" };
        }

        /// <summary>
        /// This needs to handle both the script types defined; plus, for dependency, it must evaluate to each possible type passed back in
        /// </summary>
        public string GetFileNamePrefix(string type)
        {
            string prefix = null;

            switch (type.Trim().ToUpper())
            {
                case "'SO'":
                case "SO":
                    prefix = "o_";
                    break;
                case "'SQ'":
                case "SQ":
                    prefix = "sq_";
                    break;
                case "'SN'":
                case "SN":
                    prefix = "sn_";
                    break;
                case "'FN','TF','AF'":
                case "AF":
                case "FN":
                case "TF":
                    prefix = "fn_";
                    break;
                case "'V'":
                case "V":
                    prefix = "vw_";
                    break;
                case "'P'":
                case "P":
                    prefix = "sp_";
                    break;
                case "'TR'":
                case "TR":
                    prefix = "tr_";
                    break;
            }

            return prefix;
        }


        public string GetExtractSQL(string type)
        {
            return string.Format(@"select o.name, m.definition from sys.objects o inner join sys.sql_modules m on o.object_id = m.object_id where o.is_ms_shipped = 0 AND o.type in ({0})
union all select o.name, '-- CLR AGGREGATE FUNCTIONS NOT SUPPORTED' from sys.objects o inner join sys.assembly_modules a on o.object_id = a.object_id where o.is_ms_shipped = 0 AND o.type in ({0})
union all select o.name, '-- SERVICE BROKER QUEUES NOT SUPPORTED' from sys.objects o inner join sys.service_queues q on o.object_id = q.object_id where o.is_ms_shipped = 0 AND o.type in ({0})
union all select o.name, 'IF (SELECT OBJECT_ID(''' + s.name + ''')) IS NULL BEGIN CREATE SYNONYM ' + s.name + ' FOR ' + s.base_object_name + ' END' from sys.objects o inner join sys.synonyms s on o.object_id = s.object_id where o.is_ms_shipped = 0 AND o.type in ({0})
union all select o.name, 'CREATE SEQUENCE [' + o.name + '] AS [' + CAST(TYPE_NAME(e.user_type_id) AS VARCHAR(255)) + '] START WITH '+ CAST(e.current_value AS VARCHAR(255)) + ' INCREMENT BY ' + CAST(e.increment AS VARCHAR(255)) 
+ ' MINVALUE ' + CAST(e.minimum_value AS VARCHAR(255)) + ' MAXVALUE ' + CAST(e.maximum_value AS VARCHAR(255)) + CASE WHEN e.is_cached = 1 THEN ' CACHE' ELSE '' END + CASE WHEN e.is_cycling = 1 THEN ' CYCLE' ELSE '' END 
from sys.objects o inner join sys.sequences e on o.object_id = e.object_id where o.is_ms_shipped = 0 AND o.type in ({0})", type);
        }

        public string GetDependenciesSQL(string name)
        {
            return string.Format($@"
select o.type, o.type_desc, o.name from sys.dm_sql_referenced_entities('{name}', 'OBJECT') a inner join sys.objects o on o.name = a.referenced_entity_name and o.type <> 'U' and o.name <> '{name}'
union
select o.type, o.type_desc, o.name from sys.sql_expression_dependencies a inner join sys.objects o on o.name = a.referenced_entity_name and o.type <> 'U' where a.referencing_id = OBJECT_ID('{name}') and o.name <> '{name}'");
        }

        public string GetParameterName(string input)
        {
            return string.Concat("@", input);
        }

        public string GetSplitPhrase()
        {
            return "^\\s*GO\\b";
        }

        public void EnlistTransaction(DbConnection connection)
        {

        }

        public string GetTestConnectionSQL()
        {
            return "SELECT 'TEST'";
        }

        public string GetCheckCurrentStructureSQL()
        {
            return "SELECT COUNT(*) FROM SystemInfoScript";
        }

        public string GetCheckMigrationStructureSQL()
        {
            return "SELECT COUNT(*) FROM DatabaseVersion";
        }

        public string GetCheckMigrationRecordSQL(string versionTimestamp)
        {
            return string.Format("SELECT COUNT(Version) FROM DatabaseVersion WHERE Version = '{0}'", versionTimestamp);
        }

        public string GetInsertMigrationRecordSQL()
        {
            return "INSERT INTO DatabaseVersion (SystemId, Version) VALUES (1, @version)";
        }

        public string GetUpdateCurrentRecordSQL()
        {
            return "UPDATE SystemInfoScript SET Checksum = @checksum, Length = @length, ModifiedTime = @modified WHERE SystemId = @sysId AND ScriptName = @scriptname";
        }

        public string GetInsertCurrentRecordSQL()
        {
            return "INSERT INTO SystemInfoScript (SystemId, ScriptName, Checksum, Length, ModifiedTime) VALUES (@sysId, @scriptname, @checksum, @length, @modified)";
        }

        public string GetUpdateSystemRecordSQL()
        {
            return "UPDATE SystemInfo SET {0} = @url WHERE SystemId = 1";
        }

        public string GetCurrentRecordSQL(int systemId)
        {
            return string.Format("SELECT ScriptName, Checksum, Length FROM SystemInfoScript WHERE SystemId = {0}", systemId);
        }

        public string GetCreateCurrentTrackingSQL()
        {
            return @"CREATE TABLE [SystemInfoScript] (
    [SystemId] INTEGER NOT NULL,
    [ScriptName] VARCHAR(512) NOT NULL,
    [Checksum] INTEGER NOT NULL,
    [Length] BIGINT NOT NULL,
    [CreateTime] DATETIME2 CONSTRAINT[DEF_SystemInfoScript_CreateTime] DEFAULT SYSUTCDATETIME() NOT NULL,
    [ModifiedTime] DATETIME2 NOT NULL,
    CONSTRAINT[PK_SystemInfoScript] PRIMARY KEY([SystemId], [ScriptName])
)";
        }

        public string GetCreateMigrationTrackingSQL()
        {
            return @"CREATE TABLE [DatabaseVersion] (
    [SystemId] INTEGER NOT NULL,
    [Version] NVARCHAR(40) NOT NULL,
    [CreateTime] DATETIME2 CONSTRAINT [DEF_DatabaseVersion_CreateTime] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_DatabaseVersion] PRIMARY KEY ([SystemId], [Version])
)";
        }
    }
}