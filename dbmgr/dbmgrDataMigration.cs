using dbmgr.utilities.common;
using dbmgr.utilities.data;
using dbmgr.utilities.db;
using dbmgr.utilities.Db;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;

namespace dbmgr.utilities
{
    public class dbmgrDataMigration
    {
        private const string DEFAULT_SCRIPT_REPLACEMENTS_FILE = "default.db.token.config";
        private const int SYSTEM_ID = 1;
        private const int MAX_COMMENT_LENGTH = 40;
        private const string DELTA_FILE_FORMAT = "{0}_{1}.{2}";
        private const string DATE_TIME_TOKEN = "{DATETIME}";
        private const string CREATOR_TOKEN = "{CREATOR}";
        private readonly string _migrationScriptLocation = null;
        private readonly string _currentScriptLocation = null;
        private readonly string _postScriptLocation = null;
        private readonly string _baseDeployDirectory = null;
        private readonly string _connectionString = null;
        private readonly string _providerName = null;
        private readonly int _defaultTimeout = 0;
        private readonly int _defaultTransTimeout = 0;
        private readonly IDBScripts _database;
        private readonly Dictionary<string, string> _scriptReplacements = new Dictionary<string, string>();  // Holds tokens and replacement values for scripts

        public dbmgrDataMigration(IDatabaseConfiguration config, string deployDirectory, IDBScripts database, string scriptReplacementsFile, string[] replacementParameters)
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();

            // Setup data context information
            _database = database;
            _connectionString = config.ConnectionString;
            _connectionString = DataContext.GetConnectionString(_connectionString, replacementParameters);
            _providerName = config.ConnectionProviderName;
            _defaultTimeout = config.DefaultCommandTimeoutSecs;
            _defaultTransTimeout = config.DefaultTransactionTimeoutMins;

            // Setup Directories used
            _baseDeployDirectory = Path.Combine(deployDirectory, "Database");
            _migrationScriptLocation = Path.Combine(_baseDeployDirectory, "Deltas");
            _currentScriptLocation = Path.Combine(_baseDeployDirectory, "Current");
            _postScriptLocation = Path.Combine(_baseDeployDirectory, "Post");

            if (String.IsNullOrWhiteSpace(scriptReplacementsFile))
            {
                scriptReplacementsFile = Path.Combine(_baseDeployDirectory, "Database", DEFAULT_SCRIPT_REPLACEMENTS_FILE);
            }

            if (File.Exists(scriptReplacementsFile))
            {
                LoadScriptReplacements(scriptReplacementsFile);
            }
        }

        public bool NoDbUpdates { get; set; }
        public List<string> ScriptHistory { get; set; } = new List<string>();

        public string CreateScriptFile(string friendlyName, bool generateDown = false)
        {
            // Create the directory if it doesn't exist
            if (!Directory.Exists(_migrationScriptLocation))
            {
                Directory.CreateDirectory(_migrationScriptLocation);
                Log.Logger.Debug("Created Directory " + _migrationScriptLocation);
            }

            // Generate a filename for the up and down
            DateTime timeForDelta = CurrentTimestamp;
            string version = timeForDelta.ToString("yyyyMMddHHmmss");
            string dateTimeString = timeForDelta.ToString("MM/dd/yyyy");
            string text = FormattedFileName(friendlyName, MAX_COMMENT_LENGTH).Replace(" ", "_").ToLower();
            string creatorString = Environment.UserName;

            // Create the UP script
            string fileNameUp = string.Format(DELTA_FILE_FORMAT, version, text, "up");
            string physicalFileNameUp = Path.Combine(_migrationScriptLocation, fileNameUp);
            string templateUp = CommonUtilities.GetEmbeddedResourceContent("template.up").Replace(DATE_TIME_TOKEN, dateTimeString).Replace(CREATOR_TOKEN, creatorString);
            WriteTextToFile(physicalFileNameUp, templateUp);
            Log.Logger.Information("Created up script file " + physicalFileNameUp);

            if (generateDown)
            {
                // Create the DOWN script
                string fileNameDown = string.Format(DELTA_FILE_FORMAT, version, text, "down");
                string physicalFileNameDown = Path.Combine(_migrationScriptLocation, fileNameDown);
                string templateDown = CommonUtilities.GetEmbeddedResourceContent("template.down").Replace(DATE_TIME_TOKEN, dateTimeString).Replace(CREATOR_TOKEN, creatorString);
                WriteTextToFile(physicalFileNameDown, templateDown);
                Log.Logger.Information("Created down script file " + physicalFileNameDown);
            }

            // Return the filename we created
            return Path.GetFileNameWithoutExtension(physicalFileNameUp);
        }

        public bool HaveConnectivity()
        {
            using (DataContext dataContext = GetDataContext())
            {
                try
                {
                    dataContext.ExecuteScalar(_database.GetTestConnectionSQL());
                    return true;
                }
                catch
                {
                    // ignore
                }
            }

            return false;
        }

        public bool Extract()
        {
            // Validate the provider is set up correctly.
            string[] directoryNames = _database.GetScriptDirectoryNames();
            string[] extractTypes = _database.GetScriptTypes();

            if (directoryNames == null || extractTypes == null ||
                directoryNames.Length != extractTypes.Length)
            {
                throw new ArgumentException("Database provider is setup incorrectly; types and names must match.");
            }

            // Configure the standard directories
            CreateStandardDirectories();

            // loop through names and perform extractions
            for (int i = 0; i < directoryNames.Length; i++)
            {
                // Ensure directory exists
                string path = Path.Combine(_currentScriptLocation, directoryNames[i]);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    Log.Logger.Debug("Creating directory {0}", path);
                }

                // Extract the names from the database
                using (DataContext dataContext = GetDataContext())
                {
                    string loadSql = _database.GetExtractSQL(extractTypes[i]);
                    Log.Logger.Debug("Extracting type {0}", extractTypes[i]);
                    Log.Logger.Debug("Running SQL {0}", loadSql);
                    using (IDataReader idr = dataContext.ExecuteReader(loadSql))
                    {
                        while (idr.Read())
                        {
                            // Grab the name and content, and write the file
                            string name = idr.GetStringSafe(0);
                            string contents = idr.GetStringSafe(1);
                            Log.Logger.Debug("Found database object {0} ({1} bytes)", name, contents?.Length);

                            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(contents))
                            {
                                StringBuilder injectDependencies = new StringBuilder();
                                string dependencySql = _database.GetDependenciesSQL(name);
                                using (IDataReader ddr = dataContext.ExecuteReader(dependencySql))
                                {
                                    while (ddr.Read())
                                    {
                                        string dependency = ddr.GetStringSafe(2);
                                        string dependency_type = ddr.GetStringSafe(0);
                                        string prefix = _database.GetFileNamePrefix(dependency_type);
                                        if (prefix == null)
                                        {
                                            Log.Logger.Warning("Unsupported dependency type detected! Please manually review dependency for {0} of type {1}", dependency, dependency_type);
                                            prefix = "";
                                        }

                                        injectDependencies.Append("--{{");
                                        injectDependencies.Append(prefix + dependency);
                                        injectDependencies.Append("}}");
                                        injectDependencies.Append(Environment.NewLine);
                                        Log.Logger.Information("Found dependency to {0}", dependency);
                                    }
                                }
                                if (injectDependencies.Length > 0)
                                {
                                    injectDependencies.Append(contents);
                                    contents = injectDependencies.ToString();
                                }

                                string file = String.Concat(_database.GetFileNamePrefix(extractTypes[i]), name, _database.GetFileNameExtension());
                                string filename = Path.Combine(path, file);
                                WriteTextToFile(filename, contents.Trim());
                                Log.Logger.Information("Writing database object to {0}", filename);
                            }
                        }
                    }
                }
            }

            return false;
        }

        public bool ValidateSchema()
        {
            using (DataContext dataContext = GetDataContext())
            {
                try
                {
                    object ret;
                    ret = dataContext.ExecuteScalar(_database.GetCheckMigrationStructureSQL());
                    ret = dataContext.ExecuteScalar(_database.GetCheckCurrentStructureSQL());
                }
                catch (Exception)
                {
                    Log.Logger.Warning("Found invalid migration schema.");
                    return false;
                }
            }

            return true;
        }

        public bool CreateSchema()
        {
            using (DataContext dataContext = GetDataContext())
            {
                Log.Logger.Information("Creating Tracking Schema");
                try
                {
                    using (TransactionScope ts = dataContext.StartTransaction(TransactionScopeOption.Required))
                    {
                        if (!NoDbUpdates)
                        {
                            string updateSql = _database.GetCreateMigrationTrackingSQL();
                            dataContext.ExecuteNonQuery(updateSql);

                            updateSql = _database.GetCreateCurrentTrackingSQL();
                            dataContext.ExecuteNonQuery(updateSql);

                            ts.Complete();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Logger.Error("Unable to create migration schema! {0} {1}", ex.Message, ex.StackTrace);
                    return false;
                }
            }

            return ValidateSchema();
        }

        public bool DeployDeltas(string directoryPrefix = null)
        {
            bool database_updated = false;
            string deltaScriptLocation = Path.Combine(_migrationScriptLocation, directoryPrefix ?? "");

            if (!Directory.Exists(deltaScriptLocation))
            {
                Log.Logger.Warning("DELTAS not found at {0}; skipping DELTAS.", deltaScriptLocation);
                return database_updated;
            }

            Log.Logger.Information("Reading delta scripts from {0}", deltaScriptLocation);
            IEnumerable<string> deltas = Directory.EnumerateFiles(deltaScriptLocation, "*.up", SearchOption.AllDirectories).OrderBy(n => Path.GetFileName(n));
            if (deltas.Count() > 0)
            {
                using (DataContext dataContext = GetDataContext())
                {
                    // Build a list of the deltas without the paths in the front; so we get the timestamp ordering
                    foreach (string deltaFileName in deltas)
                    {
                        // Since the file could be in a subdirectory, we need to get the real location of the file.
                        string rawFileName = Path.GetFileName(deltaFileName);

                        string v = FormattedFileName(rawFileName, rawFileName.IndexOf("_"));
                        int i = Convert.ToInt32(dataContext.ExecuteScalar(_database.GetCheckMigrationRecordSQL(v)));
                        if (i == 0)
                        {
                            Stopwatch elapsedTime = Stopwatch.StartNew();

                            Log.Logger.Information("Running delta script: {0}", rawFileName);
                            using (TransactionScope ts = dataContext.StartTransaction(TransactionScopeOption.Required))
                            {
                                if (!NoDbUpdates)
                                {
                                    // Run the referenced script
                                    try
                                    {
                                        dataContext.ExecuteScript(deltaFileName, _database.GetSplitPhrase(), _scriptReplacements);
                                    }
                                    catch (Exception ex)
                                    {
                                        throw new SystemException($"Error running script {deltaFileName}: {ex.Message}", ex);
                                    }

                                    // Indicate the version had been run
                                    IDbDataParameter param = dataContext.CreateParameter(_database.GetParameterName("version"), v);
                                    dataContext.ExecuteNonQuery(_database.GetInsertMigrationRecordSQL(), new List<IDbDataParameter>() { param });

                                    // Commit the transaction
                                    ts.Complete();

                                    // Record the script in the history
                                    ScriptHistory.Add(rawFileName);
                                    database_updated = true;
                                }

                                Log.Logger.Information("Finished running delta script {0} in {1}", deltaFileName, elapsedTime.ShowElapsedTime());
                            }
                        }
                    }
                }
            }

            return database_updated;
        }

        public bool DeployCurrent()
        {
            bool database_updated = false;

            if (!Directory.Exists(_currentScriptLocation))
            {
                Log.Logger.Warning("CURRENT not found at {0}; skipping CURRENT.", _currentScriptLocation);
                return database_updated;
            }

            Log.Logger.Information("Reading current scripts from {0}", _currentScriptLocation);

            // Get all current scripts to run
            List<string> currentScripts = new List<string>();
            
            string[] extensionOrder = _database.GetScriptTypes();
            string[] prefix_order = new string[extensionOrder.Length];
            int i = 0;
            foreach (string script_type in extensionOrder)
            {
                prefix_order[i] = _database.GetFileNamePrefix(script_type);

                string ext = String.Concat(prefix_order[i], "*", _database.GetFileNameExtension());
                currentScripts.AddRange(Directory.EnumerateFiles(_currentScriptLocation, ext, SearchOption.AllDirectories).ToList());
                
                i++;
            }

            if (currentScripts.Count() > 0)
            {
                // Establish order of the scripts
                Dictionary<string, List<string>> dependencies = ResolveAllDependencies(currentScripts);
                Dictionary<string, int> orderedScripts = CommonUtilities.TopSort(dependencies);
                List<string> scriptsToRun = currentScripts
                    .OrderByDescending(fn => orderedScripts[Path.GetFileNameWithoutExtension(fn).ToLower()])  // by dependency order
                    .ThenBy(fn2 => { 
                        for (int j = 0; j < prefix_order.Length; j++)
                        {
                            if (Path.GetFileNameWithoutExtension(fn2).StartsWith(prefix_order[j], StringComparison.InvariantCultureIgnoreCase))
                                return j;
                        }
                        throw new NotSupportedException($"Invalid prefix detected in script list {fn2}");
                    }) // then by database provider order defined
                    .Select(n => n).ToList();

                // Start the deployment process
                DateTime deploymentTime = DateTime.UtcNow;
                using (DataContext dataContext = GetDataContext())
                {
                    // Retrieve the existing script information we have in the database
                    Dictionary<string, Tuple<int, long>> existingScriptInfo = GetExistingScriptInfo(dataContext);

                    // Now, process each file we have on the disk
                    foreach (string currentFileName in scriptsToRun)
                    {
                        // Get the CRC and length of the file
                        (uint crc, ulong length) = CommonUtilities.ComputeAdlerCRC(currentFileName);

                        // Determine if we need to hit the database
                        bool isUpdate = false;
                        string key = Path.GetFileNameWithoutExtension(currentFileName).ToLower();                        
                        if (existingScriptInfo.ContainsKey(key))
                        {
                            // The script is found in the database, but lets see if the adler and length match
                            if (existingScriptInfo[key].Item1 == (int)crc && existingScriptInfo[key].Item2 == (long)length)
                            {
                                // Skip this script
                                continue;
                            }

                            isUpdate = true;
                        }

                        // Run the script against the database
                        using (TransactionScope ts = dataContext.StartTransaction(TransactionScopeOption.Required))
                        {
                            Stopwatch elapsedTime = Stopwatch.StartNew();

                            Log.Logger.Information("Running current script: {0}", currentFileName);
                            if (!NoDbUpdates)
                            {
                                // Run the referenced script
                                try
                                {
                                    dataContext.ExecuteScript(currentFileName, _database.GetSplitPhrase(), _scriptReplacements);
                                }
                                catch (Exception ex)
                                {
                                    throw new SystemException($"Error running script {currentFileName}: {ex.Message}", ex);
                                }

                                // Update the script tracking table with CRC and length
                                IDbDataParameter systemid = dataContext.CreateParameter(_database.GetParameterName("sysId"), SYSTEM_ID);
                                IDbDataParameter scriptname = dataContext.CreateParameter(_database.GetParameterName("scriptname"), Path.GetFileNameWithoutExtension(currentFileName));
                                IDbDataParameter checksum = dataContext.CreateParameter(_database.GetParameterName("checksum"), (int)crc);
                                IDbDataParameter len = dataContext.CreateParameter(_database.GetParameterName("length"), (long)length);
                                IDbDataParameter modified = dataContext.CreateParameter(_database.GetParameterName("modified"), deploymentTime);

                                // Some providers require parameter order to match; separate insert versus update
                                if (isUpdate)
                                {
                                    dataContext.ExecuteNonQuery(_database.GetUpdateCurrentRecordSQL(), new List<IDbDataParameter>() { checksum, len, modified, systemid, scriptname });
                                }
                                else
                                {
                                    dataContext.ExecuteNonQuery(_database.GetInsertCurrentRecordSQL(), new List<IDbDataParameter>() { systemid, scriptname, checksum, len, modified });
                                }

                                ts.Complete();

                                // Record the script in the history
                                ScriptHistory.Add(currentFileName);
                                database_updated = true;
                            }

                            Log.Logger.Information("Finished running current script {0} in {1}", currentFileName, elapsedTime.ShowElapsedTime());
                        }
                    }
                }
            }

            return database_updated;
        }

        public bool DeployPost()
        {
            bool database_updated = false;

            if (!Directory.Exists(_postScriptLocation))
            {
                Log.Logger.Warning("POST not found at {0}; skipping POST.", _postScriptLocation);
                return database_updated;
            }

            Log.Logger.Information("Reading post scripts from {0}", _postScriptLocation);

            // Get all post scripts to run
            string ext = "*" + _database.GetFileNameExtension();
            List<string> postScripts = Directory.EnumerateFiles(_postScriptLocation, ext, SearchOption.AllDirectories).ToList();
            if (postScripts.Count() > 0)
            {
                // Establish order of the scripts
                Dictionary<string, List<string>> dependencies = ResolveAllDependencies(postScripts);
                Dictionary<string, int> orderedScripts = CommonUtilities.TopSort(dependencies);

                // Sort the scripts based on the dependencies
                List<string> scriptsToRun = postScripts.OrderByDescending(fn => orderedScripts[Path.GetFileNameWithoutExtension(fn).ToLower()]).Select(n => n).ToList();
                foreach (string rawFileName in scriptsToRun)
                {
                    Stopwatch elapsedTime = Stopwatch.StartNew();
                    using (DataContext dataContext = GetDataContext())
                    {
                        using (TransactionScope ts = dataContext.StartTransaction(TransactionScopeOption.Required))
                        {
                            Log.Logger.Information("Running post script: {0}", rawFileName);
                            if (!NoDbUpdates)
                            {
                                try
                                {
                                    dataContext.ExecuteScript(rawFileName, _database.GetSplitPhrase(), _scriptReplacements);
                                }
                                catch (Exception ex)
                                {
                                    throw new SystemException($"Error running script {rawFileName}: {ex.Message}", ex);
                                }

                                ts.Complete();

                                // Record the script in the history
                                ScriptHistory.Add(rawFileName);
                                database_updated = true;
                            }

                            Log.Logger.Information("Finished running post script {0} in {1}", rawFileName, elapsedTime.ShowElapsedTime());
                        }
                    }
                }
            }

            return database_updated;
        }

        private static Dictionary<string, List<string>> ResolveAllDependencies(List<string> scriptFiles)
        {
            Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>();
            foreach (string fn in scriptFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(fn).ToLower();
                if (!dependencies.ContainsKey(fileName))
                {
                    dependencies.Add(fileName, new List<string>());
                }

                string script = File.ReadAllText(fn);
                MatchCollection mc = Regex.Matches(script, @"{{(\w+)}}");
                foreach (Match m in mc)
                {
                    if (m.Success)
                    {
                        foreach (Capture c in m.Groups[1].Captures)
                        {
                            // We found a dependency
                            string dependentFile = c.Value.ToLower();
                            if (!dependencies.ContainsKey(dependentFile))
                            {
                                dependencies.Add(dependentFile, new List<string>());
                            }
                            dependencies[dependentFile].Add(fileName);
                        }
                    }
                }
            }

            // Find Missing Depedencies
            List<string> allFiles = scriptFiles.Select(fn => Path.GetFileNameWithoutExtension(fn).ToLower()).ToList();
            foreach (string key in dependencies.Keys)
            {
                if (!allFiles.Contains(key))
                {
                    Log.Logger.Error("Unable to find a dependency - " + key + " - referenced in " + string.Join(", ", dependencies[key].ToArray()));
                    throw new NotSupportedException("There is an invalid dependency in the scripts.  Review the log for details.  Please correct before continuing.");
                }
            }

            return dependencies;
        }

        // DI calls
        public virtual Dictionary<string, Tuple<int, long>> GetExistingScriptInfo(DataContext dataContext)
        {
            Dictionary<string, Tuple<int, long>> existingScriptInfo = new Dictionary<string, Tuple<int, long>>();
            using (IDataReader idr = dataContext.ExecuteReader(_database.GetCurrentRecordSQL(SYSTEM_ID)))
            {
                while (idr.Read())
                {
                    existingScriptInfo.Add(idr.GetString(0).ToLower(), new Tuple<int, long>(idr.GetInt32(1), idr.GetInt64(2)));
                }
            }

            return existingScriptInfo;
        }

        public virtual void WriteTextToFile(string filename, string content)
        {
            File.WriteAllText(filename, content);
        }
        public virtual DataContext GetDataContext()
        {
            DataContext dc = new DataContext(_providerName, _connectionString, _database.EnlistTransaction, _defaultTimeout, _defaultTransTimeout);
            return dc;                                                                                      
        }
        public virtual DateTime CurrentTimestamp
        {
            get
            {
                return DateTime.UtcNow;
            }
        }

        // Private helper functions
        private static string FormattedFileName(string value, int length)
        {
            if (value == null || length > value.Length || length < 0)
            {
                return value;
            }

            return value.Substring(0, Math.Min(value.Length, length));
        }

        private void LoadScriptReplacements(string scriptReplacementsFile)
        {
            Log.Logger.Information($"Loading db token config file {scriptReplacementsFile}...");

            using (StreamReader reader = new StreamReader(scriptReplacementsFile))
            {
                while (reader.Peek() != -1)
                {
                    String line = reader.ReadLine();

                    if (!line.StartsWith("#"))
                    {
                        string[] parts = line.Split('=');
                        _scriptReplacements.Add(parts[0], parts.Length > 1 && parts[1] != null ? parts[1] : "");
                    }
                }
            }
        }

        public string CreateStandardDirectories(string[] deltaDirectories = null, string[] currentDirectories = null)
        {
            if (!Directory.Exists(_migrationScriptLocation))
            {
                Directory.CreateDirectory(_migrationScriptLocation);
                Log.Logger.Debug("Creating directory {0}", _migrationScriptLocation);
            }
            if (Directory.Exists(_migrationScriptLocation))
            {
                if (deltaDirectories != null)
                {
                    foreach (string d in deltaDirectories)
                    {
                        string n = Path.Combine(_migrationScriptLocation, d);
                        if (!Directory.Exists(n))
                        {
                            Directory.CreateDirectory(n);
                            Log.Logger.Debug("Creating directory {0}", n);
                        }
                    }
                }
            }

            if (!Directory.Exists(_currentScriptLocation))
            {
                Directory.CreateDirectory(_currentScriptLocation);
                Log.Logger.Debug("Creating directory {0}", _currentScriptLocation);
            }
            if (Directory.Exists(_currentScriptLocation))
            {
                if (currentDirectories != null)
                {
                    foreach (string d in currentDirectories)
                    {
                        string n = Path.Combine(_currentScriptLocation, d);
                        if (!Directory.Exists(n))
                        {
                            Directory.CreateDirectory(n);
                            Log.Logger.Debug("Creating directory {0}", n);
                        }                            
                    }
                }
            }

            if (!Directory.Exists(_postScriptLocation))
            {
                Directory.CreateDirectory(_postScriptLocation);
                Log.Logger.Debug("Creating directory {0}", _postScriptLocation);
            }

            return _baseDeployDirectory;
        }

        public void DeleteStandardDirectories()
        {
            if (Directory.Exists(_migrationScriptLocation))
            {
                Directory.Delete(_migrationScriptLocation, true);
                Log.Logger.Debug("Deleted directory {0}", _migrationScriptLocation);
            }

            if (Directory.Exists(_currentScriptLocation))
            {
                Directory.Delete(_currentScriptLocation, true);
                Log.Logger.Debug("Deleted directory {0}", _currentScriptLocation);
            }

            if (Directory.Exists(_postScriptLocation))
            {
                Directory.Delete(_postScriptLocation, true);
                Log.Logger.Debug("Deleted directory {0}", _postScriptLocation);
            }
        }

    }
}