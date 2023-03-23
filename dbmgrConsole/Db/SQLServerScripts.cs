using dbmgr.utilities.common;
using dbmgr.utilities.data;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

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
            parameters[0] = dbServer;
            parameters[1] = dbName;
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
            return new string[] { "Other", "Sequences", "Synonyms", "Functions", "Views", "StoredProcedures", "Triggers" };
        }

        public string[] GetScriptTypes()
        {
            return new string[] { "'SO'", "'SQ'", "'SN'", "'FN','TF','AF','IF'", "'V'", "'P'", "'TR'" };
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
                case "'FN','TF','AF','IF'":
                case "AF":
                case "IF":
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

        public bool ValidateFile(string fileName)
        {
            bool validation = true;

            // Load file and see if we find "USE "
            int lineNo = 1;
            foreach (string s in File.ReadAllLines(fileName))
            {
                if (s.Trim().StartsWith("USE", StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.Logger.Warning("Script {0} may not contain USE statement on line {1}.", fileName, lineNo);
                    validation = false;
                }
                lineNo++;
            }

            return validation;
        }

        public List<string> GetExtractSchema(DataContext dataContext, string schema_name)
        {
            StringBuilder tb_sql = new StringBuilder(10240);
            StringBuilder fk_sql = new StringBuilder(10240);
            StringBuilder idx_sql = new StringBuilder(10240);
            StringBuilder desc_sql = new StringBuilder(10240);

            List<string> ext_dependencies = new List<string>();

            // Grab the list of tables
            Dictionary<string, int> sorted_tables_with_dependencies = GetTablesInOrder(dataContext, schema_name);

            // Loop through each table
            foreach (string table_name in sorted_tables_with_dependencies.OrderByDescending(n => n.Value).ThenBy(n => n.Key).Select(n => n.Key))
            {
                Log.Logger.Information("Processing table {0} ...", table_name);
                ScriptCreateTable(dataContext, table_name, tb_sql, ext_dependencies);
                FetchExpressionDefaults(dataContext, table_name, ext_dependencies);
                ScriptForeignKeys(dataContext, table_name, fk_sql);
                ScriptIndexes(dataContext, table_name, idx_sql);
                ScriptDescriptions(dataContext, table_name, desc_sql);
            }

            Log.Logger.Information("Done External Dependencies...");
            StringBuilder ext_sql = ScriptExternalDependencies(dataContext, ext_dependencies);
            Log.Logger.Information("Done Processing All Tables");

            // Build the full script
            StringBuilder sql = new StringBuilder();
            if (ext_sql.Length > 0)
            {
                sql.AppendLine("-----------------------------------------------");
                sql.AppendLine("------------ External Dependencies ------------");
                sql.AppendLine("-----------------------------------------------");
                sql.Append(ext_sql);
            }
            if (tb_sql.Length > 0)
            {
                sql.AppendLine("------------------------------------");
                sql.AppendLine("------------ Add Tables ------------");
                sql.AppendLine("------------------------------------");
                sql.Append(tb_sql);
            }
            if (desc_sql.Length > 0)
            {
                sql.AppendLine("-----------------------------------------------------------");
                sql.AppendLine("------------ Add Table and Column Descriptions ------------");
                sql.AppendLine("-----------------------------------------------------------");
                sql.Append(desc_sql);
            }
            if (fk_sql.Length > 0)
            {
                sql.AppendLine("------------------------------------------");
                sql.AppendLine("------------ Add Foreign Keys ------------");
                sql.AppendLine("------------------------------------------");
                sql.Append(fk_sql);
            }
            if (idx_sql.Length > 0)
            {
                sql.AppendLine("-------------------------------------");
                sql.AppendLine("------------ Add Indexes ------------");
                sql.AppendLine("-------------------------------------");
                sql.Append(idx_sql);
            }

            return new List<string>() { sql.ToString() };
        }

        public string GetExtractSQL(string type)
        {
            return string.Format(@"select o.name, 'IF EXISTS ( SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'''+s.name+'.'+o.name+''')) BEGIN'+CHAR(10)+'DROP '+ CASE WHEN o.type = 'P' THEN 'PROCEDURE' WHEN o.type = 'V' THEN 'VIEW' WHEN o.type = 'TR' THEN 'TRIGGER' WHEN o.type = 'SN' THEN 'SYNONYM' WHEN o.type in ('FN', 'TF', 'AF','IF') THEN 'FUNCTION' ELSE '<object type not supported>' END +' [' + s.name + '].[' + o.name + '] END' + CHAR(10) + 'GO' + CHAR(10) + m.definition from sys.objects o inner join sys.sql_modules m on o.object_id = m.object_id inner join sys.schemas s on s.schema_id = o.schema_id where o.is_ms_shipped = 0 AND o.type in ({0})
union all select o.name, '-- CLR AGGREGATE FUNCTIONS NOT SUPPORTED' from sys.objects o inner join sys.assembly_modules a on o.object_id = a.object_id where o.is_ms_shipped = 0 AND o.type in ({0})
union all select o.name, '-- SERVICE BROKER QUEUES NOT SUPPORTED' from sys.objects o inner join sys.service_queues q on o.object_id = q.object_id where o.is_ms_shipped = 0 AND o.type in ({0})
union all select o.name, 'IF (SELECT OBJECT_ID('''+s.name+'.'+o.name+''')) IS NULL BEGIN CREATE SYNONYM [' + s.name + '].[' + o.name + '] FOR ' + x.base_object_name + ' END' from sys.objects o inner join sys.synonyms x on o.object_id = x.object_id inner join sys.schemas s on s.schema_id = o.schema_id where o.is_ms_shipped = 0 AND o.type in ({0})
union all select o.name, 'IF EXISTS ( SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'''+s.name+''+o.name+''')) BEGIN'+CHAR(10)+'DROP SEQUENCE [' + s.name + '].[' + o.name + '] END' + CHAR(10) + 'GO' + CHAR(10) + 'CREATE SEQUENCE [' + s.name + '].[' + o.name + '] AS [' + CAST(TYPE_NAME(e.user_type_id) AS VARCHAR(255)) + '] START WITH '+ CAST(e.current_value AS VARCHAR(255)) + ' INCREMENT BY ' + CAST(e.increment AS VARCHAR(255)) 
+ ' MINVALUE ' + CAST(e.minimum_value AS VARCHAR(255)) + ' MAXVALUE ' + CAST(e.maximum_value AS VARCHAR(255)) + CASE WHEN e.is_cached = 1 THEN ' CACHE' ELSE '' END + CASE WHEN e.is_cycling = 1 THEN ' CYCLE' ELSE '' END 
from sys.objects o inner join sys.sequences e on o.object_id = e.object_id inner join sys.schemas s on s.schema_id = o.schema_id where o.is_ms_shipped = 0 AND o.type in ({0})", type);
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




        private static StringBuilder ScriptExternalDependencies(DataContext dataContext, List<string> ext_dependencies)
        {
            StringBuilder ext_sql = new StringBuilder(10240);

            // Place any dependencies for the table scripts here
            Dictionary<string, string> set = new Dictionary<string, string>();
            for (int i = 0; i < ext_dependencies.Count; i++)
            {
                string dep_name = ext_dependencies[i];
                string expdepSql = @"select so.name, sm.definition
from 
sys.sql_expression_dependencies l1
inner join sys.objects so on so.object_id = l1.referenced_id and l1.referencing_id <> so.object_id
inner join sys.sql_modules sm on so.object_id = sm.object_id
where l1.referencing_id = OBJECT_ID(N'{0}')";

                // Keep looping over this until there are no results, meaning no further dependencies
                using (IDataReader cidr = dataContext.ExecuteReader(string.Format(expdepSql, dep_name)))
                {
                    while (cidr.Read())
                    {
                        string obj_name = cidr.GetStringSafe(0);
                        string obj_def = cidr.GetStringSafe(1);
                        if (!set.ContainsKey(obj_name))
                        {
                            set.Add(obj_name, obj_def);
                            ext_dependencies.Add(obj_name);
                        }
                    }
                }
            }
            foreach (string key in set.Keys.Reverse())
            {
                ext_sql.AppendLine(set[key]);
                ext_sql.AppendLine("GO");
                ext_sql.AppendLine("");
            }

            return ext_sql;
        }

        private static void ScriptDescriptions(DataContext dataContext, string table_name, StringBuilder desc_sql)
        {
            // Add the DESCRIPTIONS
            Log.Logger.Debug("Adding Descriptions for table {0} ...", table_name);
            string commentSQL = @"select ep.value as ddescription, SCHEMA_NAME(so.schema_id) as dschema, so.name as dtable, COL_NAME(so.object_id, ep.minor_id) as dcolumn 
from sys.extended_properties ep 
left join sys.objects so on ep.major_id = so.object_id
where ep.name = 'MS_Description' and ep.major_id = OBJECT_ID(N'{0}')";
            using (IDataReader cidr = dataContext.ExecuteReader(string.Format(commentSQL, table_name)))
            {
                while (cidr.Read())
                {
                    string description = cidr.GetStringSafe(0);
                    string schema = cidr.GetStringSafe(1);
                    string table = cidr.GetStringSafe(2);
                    string column = cidr.GetStringSafe(3);

                    desc_sql.Append("EXECUTE sp_addextendedproperty N'MS_Description', N'");
                    desc_sql.Append(description.Replace("'", "''"));
                    desc_sql.Append("', 'SCHEMA', N'");
                    desc_sql.Append(schema);
                    desc_sql.Append("', 'TABLE', N'");
                    desc_sql.Append(table);
                    if (!string.IsNullOrWhiteSpace(column))
                    {
                        desc_sql.Append("', 'COLUMN', N'");
                        desc_sql.Append(column);
                        desc_sql.AppendLine("'");
                    }
                    else
                    {
                        desc_sql.AppendLine("', NULL, NULL");
                    }
                    desc_sql.AppendLine("GO");
                    desc_sql.AppendLine("");
                }
            }
        }

        private static void ScriptIndexes(DataContext dataContext, string table_name, StringBuilder idx_sql)
        {
            // Add the INDEXES
            Log.Logger.Debug("Adding Indexes for table {0} ...", table_name);
            string indexSql = @"select i.name, object_name(ic.object_id), COL_NAME(ic.object_id, ic.column_id), i.filter_definition, CASE when i.is_unique = 1 THEN 'UNIQUE ' ELSE '' END + i.type_desc + ' INDEX', ic.is_descending_key, ic.is_included_column
from sys.tables t
inner join sys.indexes i on t.object_id = i.object_id and i.is_primary_key = 0 and i.is_unique_constraint = 0
inner join sys.index_columns ic on i.object_id = ic.object_id and i.index_id = ic.index_id
where t.name = '{0}'
order by ic.index_id, ic.key_ordinal";
            using (IDataReader cidr = dataContext.ExecuteReader(string.Format(indexSql, table_name)))
            {
                string old_idx_name = null;

                string idx_name = null;
                List<string> key_columns = new List<string>();
                List<string> include_columns = new List<string>();
                string references_table = null;
                string filter_def = null;
                string type_name = null;
                bool is_included = false;
                bool is_descending = false;

                while (cidr.Read())
                {
                    idx_name = cidr.GetStringSafe(0);
                    if (old_idx_name != null && old_idx_name != idx_name)
                    {
                        // new table, so we should write the prior alter table
                        idx_sql.Append("CREATE ");
                        idx_sql.Append(type_name);
                        idx_sql.Append(" [");
                        idx_sql.Append(old_idx_name);
                        idx_sql.Append("]");
                        idx_sql.Append(" ON [");
                        idx_sql.Append(references_table);
                        idx_sql.Append("] (");
                        idx_sql.Append(string.Join(",", key_columns));

                        if (!string.IsNullOrWhiteSpace(filter_def))
                        {
                            idx_sql.Append(") WHERE (");
                            idx_sql.Append(filter_def);
                        }
                        if (include_columns.Count > 0)
                        {
                            idx_sql.Append(") INCLUDE (");
                            idx_sql.Append(string.Join(",", include_columns));
                        }
                        // End the prior statement
                        idx_sql.AppendLine(")");
                        idx_sql.AppendLine("GO");
                        idx_sql.AppendLine("");

                        key_columns = new List<string>();
                        include_columns = new List<string>();
                    }

                    references_table = cidr.GetStringSafe(1);
                    filter_def = cidr.GetStringSafe(3);
                    type_name = cidr.GetStringSafe(4);
                    is_descending = cidr.GetBoolean(5);
                    is_included = cidr.GetBoolean(6);

                    string references_contents = string.Concat("[", cidr.GetStringSafe(2), "]");
                    if (is_descending)
                    {
                        references_contents += " DESC";
                    }

                    if (is_included)
                    {
                        include_columns.Add(references_contents);
                    }
                    else
                    {
                        key_columns.Add(references_contents);
                    }

                    old_idx_name = idx_name;
                }

                if (old_idx_name != null)
                {
                    // new table, so we should write the prior alter table
                    idx_sql.Append("CREATE ");
                    idx_sql.Append(type_name);
                    idx_sql.Append(" [");
                    idx_sql.Append(old_idx_name);
                    idx_sql.Append("]");
                    idx_sql.Append(" ON [");
                    idx_sql.Append(references_table);
                    idx_sql.Append("] (");
                    idx_sql.Append(string.Join(",", key_columns));

                    if (!string.IsNullOrWhiteSpace(filter_def))
                    {
                        idx_sql.Append(") WHERE (");
                        idx_sql.Append(filter_def);
                    }
                    if (include_columns.Count > 0)
                    {
                        idx_sql.Append(") INCLUDE (");
                        idx_sql.Append(string.Join(",", include_columns));
                    }
                    // End the prior statement
                    idx_sql.AppendLine(")");
                    idx_sql.AppendLine("GO");
                    idx_sql.AppendLine("");
                }
            }
        }

        private static void ScriptForeignKeys(DataContext dataContext, string table_name, StringBuilder fk_sql)
        {
            // Add the FOREIGN KEY constraints
            Log.Logger.Debug("Adding Foreign Keys for table {0} ...", table_name);
            string foreignKeySql = @"select si.name as constraint_name, si.is_not_trusted as with_nocheck,
COL_NAME(sic.parent_object_id, sic.parent_column_id) as fk_contents, 
object_name(sic.referenced_object_id) as references_table,
COL_NAME(sic.[referenced_object_id], sic.referenced_column_id) as references_contents,
CASE WHEN delete_referential_action > 0 THEN ' ON DELETE ' + REPLACE(delete_referential_action_desc, '_', ' ') ELSE '' END +
CASE WHEN update_referential_action > 0 THEN ' ON UPDATE ' + REPLACE(update_referential_action_desc, '_', ' ') ELSE '' END as cascade_desc
from sys.foreign_keys si 
inner join sys.foreign_key_columns sic on si.object_id = sic.constraint_object_id
where si.parent_object_id = OBJECT_ID(N'{0}')
order by constraint_object_id";
            using (IDataReader cidr = dataContext.ExecuteReader(string.Format(foreignKeySql, table_name)))
            {
                string old_fk_name = null;

                string fk_name = null;
                bool with_nocheck = false;
                string fk_contents = null;
                string references_table = null;
                string cascade_desc = null;
                List<string> ref_columns = new List<string>();
                List<string> key_columns = new List<string>();

                while (cidr.Read())
                {
                    fk_name = cidr.GetStringSafe(0);
                    with_nocheck = cidr.GetBoolean(1);
                    if (old_fk_name != null && old_fk_name != fk_name)
                    {
                        // new table, so we should write the prior alter table
                        fk_sql.Append("ALTER TABLE [");
                        fk_sql.Append(table_name);
                        fk_sql.Append("] ADD CONSTRAINT [");
                        fk_sql.Append(old_fk_name);
                        fk_sql.AppendLine("]");
                        fk_sql.Append("\tFOREIGN KEY ([");
                        fk_sql.Append(string.Join("],[", ref_columns));
                        fk_sql.Append("]) REFERENCES [");
                        fk_sql.Append(references_table);
                        fk_sql.Append("] ([");
                        fk_sql.Append(string.Join("],[", key_columns));
                        // End the prior statement
                        fk_sql.Append("]) ");
                        fk_sql.AppendLine(cascade_desc);
                        fk_sql.AppendLine("GO");
                        fk_sql.AppendLine("");

                        key_columns = new List<string>();
                        ref_columns = new List<string>();
                    }

                    string references_contents = cidr.GetStringSafe(4);
                    cascade_desc = cidr.GetStringSafe(5);

                    fk_contents = cidr.GetStringSafe(2);
                    references_table = cidr.GetStringSafe(3);

                    ref_columns.Add(fk_contents);
                    key_columns.Add(references_contents);

                    old_fk_name = fk_name;
                }

                if (old_fk_name != null)
                {
                    // new table, so we should write the prior alter table
                    fk_sql.Append("ALTER TABLE [");
                    fk_sql.Append(table_name);
                    fk_sql.Append("] ADD CONSTRAINT [");
                    fk_sql.Append(old_fk_name);
                    fk_sql.AppendLine("]");
                    fk_sql.Append("\tFOREIGN KEY ([");
                    fk_sql.Append(string.Join("],[", ref_columns));
                    fk_sql.Append("]) REFERENCES [");
                    fk_sql.Append(references_table);
                    fk_sql.Append("] ([");
                    fk_sql.Append(string.Join("],[", key_columns));
                    // End the prior statement
                    fk_sql.Append("]) ");
                    fk_sql.AppendLine(cascade_desc);
                    fk_sql.AppendLine("GO");
                    fk_sql.AppendLine("");
                }
            }
        }

        private static void FetchExpressionDefaults(DataContext dataContext, string table_name, List<string> ext_dependencies)
        {
            // Add the DEFAULTS dependent on expressions
            Log.Logger.Debug("Adding Defaults for table {0} ...", table_name);
            string depfunSql = @"select OBJECT_DEFINITION(sc.default_object_id), sc.name, sm.name
from sys.objects so
inner join sys.columns sc on sc.object_id = so.object_id
inner join sys.types st on st.user_type_id = sc.user_type_id
INNER JOIN sys.default_constraints sm ON sm.object_id = sc.default_object_id
INNER JOIN sys.sql_expression_dependencies sed ON sm.object_id = sed.referencing_id
LEFT JOIN sys.check_constraints cc ON sc.object_id = cc.parent_object_id AND cc.parent_column_id = sc.column_id 
WHERE so.type = 'U'
and so.object_id = OBJECT_ID(N'{0}')
order by so.name, sc.column_id";
            using (IDataReader cidr = dataContext.ExecuteReader(string.Format(depfunSql, table_name)))
            {
                while (cidr.Read())
                {
                    string constraint_name = cidr.GetStringSafe(2);
                    ext_dependencies.Add(constraint_name);
                }
            }
        }

        private static void ScriptCreateTable(DataContext dataContext, string table_name, StringBuilder tb_sql, List<string> ext_dependencies)
        {
            // Create the table
            tb_sql.Append("CREATE TABLE [");
            tb_sql.Append(table_name);
            tb_sql.AppendLine("] (");

            // Grab columns
            string columnsSql = @"select sc.Name, 
case when scc.definition IS NULL THEN st.name + ' ' ELSE '' END + 
case when scc.definition IS NULL AND st.Name in ('varchar', 'char', 'varbinary', 'binary') then '(' + CASE WHEN sc.max_length = -1 THEN 'MAX' else cast(sc.max_length as varchar) END + ') ' else '' end +
case when scc.definition IS NULL AND st.Name in ('nvarchar', 'nchar') then '(' + CASE WHEN sc.max_length = -1 THEN 'MAX' else cast(sc.max_length/2 as varchar) END + ') ' else '' end +
case when scc.definition IS NULL AND st.Name in ('numeric', 'decimal', 'float') then '(' + cast(sc.precision as varchar(20)) + CASE WHEN sc.scale > 0 THEN ',' + cast(sc.scale as varchar(20)) + ')' else ')' END else '' end +
case when scc.definition IS NOT NULL THEN ' AS ' + scc.definition ELSE '' END +
CASE WHEN sc.collation_name IS NOT NULL THEN ' COLLATE ' + sc.collation_name ELSE ''  END +
case when sc.is_rowguidcol = 1 THEN ' ROWGUIDCOL' ELSE '' END +
case when scc.definition IS NULL AND sc.is_nullable = 0 then ' NOT NULL' else '' end + 
case when sm.definition IS NOT NULL then ' CONSTRAINT [' + OBJECT_NAME(sc.default_object_id) + '] DEFAULT ' +  OBJECT_DEFINITION(sc.default_object_id) else '' END +
case when scc.definition IS NULL AND cc.definition IS NOT NULL then ' CONSTRAINT [' + cc.name + '] CHECK ' + cc.definition ELSE '' END +
CASE WHEN sc.is_identity = 1 THEN ' IDENTITY(' + CAST(IDENTITYPROPERTY(sc.object_id, 'SeedValue') AS VARCHAR(20)) + ',' + CAST(IDENTITYPROPERTY(sc.object_id, 'IncrementValue') AS VARCHAR(20)) + ')'   ELSE ''   END + 
'' as script, sc.is_computed
from sys.objects so
inner join sys.columns sc on sc.object_id = so.object_id
inner join sys.types st on st.user_type_id = sc.user_type_id
LEFT JOIN sys.default_constraints sm ON sm.object_id = sc.default_object_id
LEFT JOIN sys.sql_expression_dependencies sed ON sm.object_id = sed.referencing_id
LEFT JOIN sys.check_constraints cc ON sc.object_id = cc.parent_object_id AND cc.parent_column_id = sc.column_id 
LEFT JOIN sys.computed_columns scc on sc.object_id = sc.object_id and sc.column_id = scc.column_id and sc.is_computed = 1
WHERE so.type = 'U'
and so.object_id = OBJECT_ID(N'{0}')
order by sc.column_id";
            using (IDataReader cidr = dataContext.ExecuteReader(string.Format(columnsSql, table_name)))
            {
                int counter = 0;
                while (cidr.Read())
                {
                    string column_name = cidr.GetStringSafe(0);
                    string column_def = cidr.GetStringSafe(1);
                    bool is_computed = cidr.GetBoolean(2);

                    if (counter++ > 0)
                    {
                        tb_sql.AppendLine(",");
                    }
                    tb_sql.Append("\t[");
                    tb_sql.Append(column_name);
                    tb_sql.Append("] ");
                    tb_sql.Append(column_def);

                    if (is_computed)
                    {
                        ext_dependencies.Add(table_name);
                    }
                }
            }

            // Add the primary key and unique constraints
            Log.Logger.Debug("Adding Primary Key for table {0} ...", table_name);
            string primaryKeySql = @"select si.name, case when is_primary_key = 1 then 'PRIMARY KEY ' + si.type_desc else 'UNIQUE' end, COL_NAME(sic.[object_id], sic.column_id)  -- 'CONSTRAINT [' + si.name + '] PRIMARY KEY ' + si.type_desc + ' ([BudgetTemplateId])'
from sys.indexes si 
inner join sys.index_columns sic on si.object_id = sic.object_id and si.index_id = sic.index_id
where (is_primary_key = 1 or is_unique_constraint = 1) and si.object_id = OBJECT_ID(N'{0}')
order by key_ordinal";
            using (IDataReader cidr = dataContext.ExecuteReader(string.Format(primaryKeySql, table_name)))
            {
                string old_name = null;

                string pk_name = null;
                string pk_type = null;
                List<string> key_columns = new List<string>();

                while (cidr.Read())
                {
                    pk_name = cidr.GetStringSafe(0);
                    if (old_name != null && old_name != pk_name)
                    {
                        tb_sql.AppendLine(",");
                        tb_sql.Append("\tCONSTRAINT [");
                        tb_sql.Append(old_name);
                        tb_sql.Append("] ");
                        tb_sql.Append(pk_type);
                        tb_sql.Append(" ([");
                        tb_sql.Append(string.Join("],[", key_columns));
                        tb_sql.Append("])");

                        key_columns = new List<string>();
                    }

                    pk_type = cidr.GetStringSafe(1);
                    string col_name = cidr.GetStringSafe(2);

                    key_columns.Add(col_name);

                    old_name = pk_name;
                }

                if (old_name != null)
                {
                    tb_sql.AppendLine(",");
                    tb_sql.Append("\tCONSTRAINT [");
                    tb_sql.Append(old_name);
                    tb_sql.Append("] ");
                    tb_sql.Append(pk_type);
                    tb_sql.Append(" ([");
                    tb_sql.Append(string.Join("],[", key_columns));
                    tb_sql.Append("])");
                }
            }

            tb_sql.AppendLine("");
            tb_sql.AppendLine(")");
            tb_sql.AppendLine("GO");
            tb_sql.AppendLine("");
        }

        private static Dictionary<string, int> GetTablesInOrder(DataContext dataContext, string schema)
        {
            Log.Logger.Information("Grabbing the tables and dependencies...");
            string depSql = @"select DISTINCT OBJECT_NAME(t.object_id) as tablename, object_name(sic.referenced_object_id) as references_table
from sys.schemas s
inner join sys.tables t on s.schema_id = t.schema_id and s.name = '{0}' and t.type = 'U'
left join sys.foreign_keys si  on t.object_id = si.parent_object_id
left join sys.foreign_key_columns sic on si.object_id = sic.constraint_object_id
order by tablename";
            Dictionary<string, List<string>> table_with_dependencies = new Dictionary<string, List<string>>();
            using (IDataReader idr = dataContext.ExecuteReader(string.Format(depSql, schema)))
            {
                string table_n = null;
                List<string> dependencies = null;
                while (idr.Read())
                {
                    string t_name = idr.GetStringSafe(0);
                    string d_name = idr.GetStringSafe(1);
                    if (table_n == null || table_n != t_name)
                    {
                        dependencies = new List<string>();
                        table_with_dependencies.Add(t_name, dependencies);
                        table_n = t_name;
                    }

                    if (d_name != null)
                    {
                        dependencies?.Add(d_name);
                    }
                }
            }

            // Order the tables
            Log.Logger.Information("Ordering the {0} table dependencies...", table_with_dependencies.Count);

            Dictionary<string, int> sorted_tables_with_dependencies = CommonUtilities.TopSort(table_with_dependencies, true);

            return sorted_tables_with_dependencies;
        }

    }
}
