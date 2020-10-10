using CommandLine;
using dbmgr.utilities.common;
using dbmgr.utilities.db;
using dbmgr.utilities.Db;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace dbmgr.utilities
{
    /// <summary>
    /// dbmgrConsole tool
    /// </summary>
    public static class dbmgrConsole
    {
        public const int EXIT_CODE_SUCCESS = 0;
        public const int EXIT_CODE_GENERAL_ERROR = -1;
        public const int EXIT_CODE_ARGUMENT_ERROR = -2;

        private const string _destinationBase = @".";
        private static IDBScripts _database;

        public static IDBScripts GetDatabaseType()
        {
            return _database;
        }

        public static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.AppSettings()
                .WriteTo.Console()
                .CreateLogger();

            var parser = Parser.Default;
            return parser.ParseArguments<dbmgrCommandLineOptions>(args).MapResult(
                (dbmgrCommandLineOptions options) => ExecuteCommand(options),
                errs => EXIT_CODE_GENERAL_ERROR
            );
        }

        private static int ExecuteCommand(dbmgrCommandLineOptions options)
        {
            Stopwatch elapsedTime = Stopwatch.StartNew();
            try
            {
                // Connect to the database
                // Step #1 - figure out the provider
                SetDatabaseType(options);

                // Step #2 - get the connection parameters
                string[] replacementParameters = null;
                // Step #2a - from the vault file
                if (!string.IsNullOrWhiteSpace(options.VaultFile))
                {
                    // We have the database connection in the vault file
                    try
                    {
                        string input = File.ReadAllText(options.VaultFile);
                        Log.Logger.Information("Replacement parameters parsed from vault file");
                        replacementParameters = _database.ParseConnection(input);
                        if (replacementParameters == null)
                        {
                            Log.Logger.Information("Replacement parameters loaded from vault file");
                            replacementParameters = File.ReadAllLines(options.VaultFile);
                            int l = 1;
                            foreach(string s in replacementParameters)
                            {
                                Log.Logger.Debug("Replacement parameter #{0}: {1}", l++, s);
                            }
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        Log.Logger.Error("Unable to find vault file: {0}.", options.VaultFile);
                        return EXIT_CODE_GENERAL_ERROR;
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error("Error loading vault file", ex);
                        return EXIT_CODE_GENERAL_ERROR;
                    }
                }

                // Step #2b - from the command line - will override vault file
                if (!string.IsNullOrWhiteSpace(options.ConnectInfo))
                {
                    Log.Logger.Information("Replacement parameters parsed from command line");
                    replacementParameters = _database.ParseConnection(options.ConnectInfo, options.DbName, options.DbServer, options.DbPort, options.DbUser, options.DbPwd);
                    if (replacementParameters == null || replacementParameters.Count() == 0)
                    {
                        Log.Logger.Error("Unable to parse database replacement parameters");
                        return EXIT_CODE_ARGUMENT_ERROR;
                    }
                }

                // Step #3b - passed in connectionstring overrides everything
                string netConnectionString = null;
                if (!string.IsNullOrWhiteSpace(options.ConnectString))
                {
                    netConnectionString = options.ConnectString;
                    Log.Logger.Information("Connection string override from command line");
                } else if (!string.IsNullOrWhiteSpace(options.ConnectStringFile))
                {
                    string input = File.ReadAllText(options.ConnectStringFile);
                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        Log.Logger.Information("Connection string override from file");
                        netConnectionString = input;
                    }
                }

                // Set up the migrator
                IDatabaseConfiguration config = new NetDatabaseConfiguration(_database.DbConnectionKey);
                dbmgrDataMigration m = new dbmgrDataMigration(config, _destinationBase, _database, options.ReplacementFile, replacementParameters, netConnectionString);
                if (options.DryRun)
                {
                    Log.Logger.Information("Executing in DRY RUN mode.  No updates to database will take place.");
                    m.NoDbUpdates = options.DryRun;
                }


                // Process the commands that don't need the database
                if (options.SetupFolders)
                {
                    return ProcessSetupCommand(m, options.Blue || options.Green) ? EXIT_CODE_SUCCESS : EXIT_CODE_GENERAL_ERROR;
                }

                if (!String.IsNullOrWhiteSpace(options.GenerateDelta))
                {
                    return ProcessGenerateCommand(m, options.GenerateDelta, null);
                }

                // These commands require a connection to the database                
                if (options.TestConnectivity)
                {
                    return ProcessConnectCommand(m) ? EXIT_CODE_SUCCESS : EXIT_CODE_GENERAL_ERROR;
                }

                if (options.Extract)
                {
                    return ProcessExtractCommand(m) ? EXIT_CODE_SUCCESS : EXIT_CODE_GENERAL_ERROR;
                }

                // Run the migration
                if (options.Migrate || options.CreateSchema)
                {
                    try
                    {
                        // Validate and/or create the schema
                        bool exists = ValidateAndCreateSchema(m);
                        if (!exists)
                        {
                            Log.Logger.Error("Cannot continue - unable to validate migration schema!");
                            return EXIT_CODE_GENERAL_ERROR;
                        }

                        // Start the migration
                        if (options.Migrate)
                        {
                            if (options.Blue || options.Green)
                            {
                                ProcessBlueGreenMigrateCommand(m, options.Blue);
                            }
                            else
                            {
                                ProcessMigrateCommand(m);
                            }
                        }

                        return EXIT_CODE_SUCCESS;
                    }
                    catch (Exception nse)
                    {
                        Log.Logger.Error("ERROR: " + nse.Message);
                        return EXIT_CODE_GENERAL_ERROR;
                    }
                }

                Log.Logger.Error($"Unknown command{Environment.NewLine}");
                GetUsage();
                return EXIT_CODE_ARGUMENT_ERROR;
            }
            catch(Exception e)
            {
                Log.Logger.Error(e, "Exiting with an exception");
                return EXIT_CODE_GENERAL_ERROR;
            }
            finally
            {
                Log.Logger.Information("Finished running in " + elapsedTime.ShowElapsedTime());
            }
        }

        private static void GetUsage()
        {
            // hack to get the usage/help to display on demand
            Parser.Default.ParseArguments<dbmgrCommandLineOptions>("--help".Split());
        }

        private static void SetDatabaseType(dbmgrCommandLineOptions options)
        {
            // Specify the database type of either SQL Server or Oracle
            if (options.DatabaseType != null)
            {
                if (options.DatabaseType.Equals(new SQLServerScripts().ShortName, StringComparison.InvariantCultureIgnoreCase))
                {
                    _database = new SQLServerScripts();
                    Log.Logger.Debug("Database Type SQL Server specified on command line.");
                }
                else if (options.DatabaseType.Equals(new OracleScripts().ShortName, StringComparison.InvariantCultureIgnoreCase))                     
                {
                    _database = new OracleScripts();
                    Log.Logger.Debug("Database Type Oracle specified on command line.");
                }
                else
                {
                    throw new NotSupportedException($"Invalid database type option selected {options.DatabaseType}");
                }
            }

            if (_database == null)
            {
                Log.Logger.Information("Database Type not specified on command line; defaulting Database Type to SQL Server.");
                _database = new SQLServerScripts();
            }
        }

        private static void DeployDeltas(dbmgrDataMigration m, string directoryPrefix = null)
        {
            Stopwatch rt = new Stopwatch();
            Log.Logger.Information("Processing delta directories");
            m.DeployDeltas(directoryPrefix);
            Log.Logger.Information("Completed delta directories in {0}", rt.ShowElapsedTime());
        }
        private static void DeployCurrent(dbmgrDataMigration m)
        {
            Stopwatch rt = new Stopwatch();
            Log.Logger.Information("Processing current directories");
            m.DeployCurrent();
            Log.Logger.Information("Completed current directories in {0}", rt.ShowElapsedTime());
        }
        private static void DeployPost(dbmgrDataMigration m)
        {
            Stopwatch rt = new Stopwatch();
            Log.Logger.Information("Processing post directories");
            m.DeployPost();
            Log.Logger.Information("Completed post directories in {0}", rt.ShowElapsedTime());
        }

        private static void ProcessMigrateCommand(dbmgrDataMigration m)
        {
            DeployDeltas(m);
            DeployCurrent(m);
            DeployPost(m);
        }

        private static void ProcessBlueGreenMigrateCommand(dbmgrDataMigration m, bool isBlue)
        {
            if (isBlue)
            {
                // Blue
                Log.Logger.Information("Starting BLUE migration");
                DeployDeltas(m, "Blue");
                DeployCurrent(m);
            }
            else
            {
                // Green
                Log.Logger.Information("Starting GREEN migration");
                DeployDeltas(m, "Green");
                DeployPost(m);
            }
        }

        private static bool ProcessConnectCommand(dbmgrDataMigration m)
        {
            return m.HaveConnectivity();
        }

        private static bool ProcessExtractCommand(dbmgrDataMigration m)
        {
            return m.Extract();
        }

        private static bool ProcessSetupCommand(dbmgrDataMigration m, bool isBlueGreen)
        {
            string[] deltaDirectories = null;
            if (isBlueGreen)
            {
                deltaDirectories = new[] { "Blue", "Green" };
            }
            string[] currentDirectories = _database.GetScriptDirectoryNames();
            return m.CreateStandardDirectories(deltaDirectories, currentDirectories) != null;
        }

        private static int ProcessGenerateCommand(dbmgrDataMigration m, string deltaName, Action<string, string, string> postProcess)
        {
            try
            {
                string comment = deltaName.ToLower().Replace("-", "_");

                string fileBase = m.CreateScriptFile(comment, true);

                // if a post generation action is defined, let's execute it - for possible extensibility with Visual Studio
                postProcess?.Invoke(_destinationBase, deltaName, fileBase);

                return EXIT_CODE_SUCCESS;
            }
            catch (NotSupportedException)
            {
                return EXIT_CODE_GENERAL_ERROR;
            }
        }

        private static bool ValidateAndCreateSchema(dbmgrDataMigration m)
        {
            Log.Logger.Information("Validating schema");
            bool exists = m.ValidateSchema();
            if (!exists)
            {
                Stopwatch rt = new Stopwatch();
                Log.Logger.Information("Creating schema");

                // This must succeed
                exists = m.CreateSchema();
                if (!exists)
                {
                    Log.Logger.Error("Schema is invalid");
                    return false;
                }
                else
                {
                    Log.Logger.Information("Schema created in {0}", rt.ShowElapsedTime());
                }
            }
            else
            {
                Log.Logger.Information("Schema is valid");
            }

            return true;
        }
    }
}