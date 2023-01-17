using CommandLine;
using dbmgr.utilities.common;
using dbmgr.utilities.db;
using dbmgr.utilities.Db;
using Microsoft.Extensions.Configuration;
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

        private static IConfigurationRoot _configuration;

        public static int Main(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (string.IsNullOrWhiteSpace(environment)) { environment = "Production"; }

            // Set working directory to where the binaries are for the configuration settings
            string baseDirectory = Directory.GetCurrentDirectory();
            string? cwd = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
            Directory.SetCurrentDirectory(cwd ?? ".");

            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(_configuration)
                .CreateLogger();

            Directory.SetCurrentDirectory(baseDirectory);

            var parser = Parser.Default;
            ParserResult<dbmgrCommandLineOptions> pr = parser.ParseArguments<dbmgrCommandLineOptions>(args);
            return pr.MapResult(
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
                string netConnectionString = null;

                // Do we have standard parameters?
                if (!string.IsNullOrWhiteSpace(options.DbName))
                {
                    // Handle our individual parameters
                    replacementParameters = _database.ParseStandardConnection(options.DbName,
                                                                                options.DbServer,
                                                                                options.DbPort,
                                                                                options.DbUser,
                                                                                options.DbPwd,
                                                                                options.DbOpt1,
                                                                                options.DbOpt2);
                }
                else if (!string.IsNullOrWhiteSpace(options.DbFile))
                {
                    // Handle our database file
                    try
                    {
                        Log.Logger.Information("Loading standard parameters from file");
                        string[] fileparams = File.ReadAllLines(options.DbFile);
                        if (fileparams.Length >= 1) options.DbName = fileparams[0]; else options.DbName = null;
                        if (fileparams.Length >= 2) options.DbServer = fileparams[1]; else options.DbServer = null;
                        if (fileparams.Length >= 3) options.DbPort = fileparams[2]; else options.DbPort = null;
                        if (fileparams.Length >= 4) options.DbUser = fileparams[3]; else options.DbUser = null;
                        if (fileparams.Length >= 5) options.DbPwd = fileparams[4]; else options.DbPwd = null;
                        if (fileparams.Length >= 6) options.DbOpt1 = fileparams[5]; else options.DbOpt1 = null;
                        if (fileparams.Length >= 7) options.DbOpt2 = fileparams[6]; else options.DbOpt2 = null;
                        if (string.IsNullOrWhiteSpace(options.DbName))
                        {
                            Log.Logger.Error("Format of standard parameter file is incorrect");
                            return EXIT_CODE_ARGUMENT_ERROR;
                        }

                        replacementParameters = _database.ParseStandardConnection(options.DbName,
                                                                                  options.DbServer,
                                                                                  options.DbPort,
                                                                                  options.DbUser,
                                                                                  options.DbPwd,
                                                                                  options.DbOpt1,
                                                                                  options.DbOpt2);
                    }
                    catch (FileNotFoundException)
                    {
                        Log.Logger.Error("Unable to find standard parameter file: {0}.", options.DbFile);
                        return EXIT_CODE_GENERAL_ERROR;
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error("Error loading standard parameter file", ex);
                        return EXIT_CODE_GENERAL_ERROR;
                    }
                }

                // if we didn't have standard parameters, let's try provider-specific
                if (replacementParameters == null)
                {
                    // Do we have provider-specific connection info?
                    if (!string.IsNullOrWhiteSpace(options.ConnectInfo))
                    {
                        // Handle the connect info
                        Log.Logger.Information("Replacement parameters parsed from command line");
                        replacementParameters = _database.ParseProviderConnection(options.ConnectInfo);
                        if (replacementParameters == null || replacementParameters.Length == 0)
                        {
                            Log.Logger.Error("Unable to parse database replacement parameters");
                            return EXIT_CODE_ARGUMENT_ERROR;
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(options.ConnectInfoFile))
                    {
                        // We have the database connection in the vault file
                        try
                        {
                            string input = File.ReadAllText(options.ConnectInfoFile);
                            Log.Logger.Information("Connection info read from connection info file");
                            replacementParameters = _database.ParseProviderConnection(input);
                            if (replacementParameters == null)
                            {
                                // Try another file format where each provider specific item is on one line
                                Log.Logger.Information("Replacement parameters loaded from vault file");
                                replacementParameters = File.ReadAllLines(options.ConnectInfoFile);
                                int l = 1;
                                foreach (string s in replacementParameters)
                                {
                                    Log.Logger.Debug("Replacement parameter #{0}: {1}", l++, s);
                                }
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            Log.Logger.Error("Unable to find connection info file: {0}.", options.ConnectInfoFile);
                            return EXIT_CODE_GENERAL_ERROR;
                        }
                        catch (Exception ex)
                        {
                            Log.Logger.Error("Error loading connection info file", ex);
                            return EXIT_CODE_GENERAL_ERROR;
                        }
                    }
                }

                // Do we have a connection string already?                
                if (!string.IsNullOrWhiteSpace(options.ConnectString))
                {
                    netConnectionString = options.ConnectString;
                    Log.Logger.Information("Connection string override from command line");
                }
                else if (!string.IsNullOrWhiteSpace(options.ConnectStringFile))
                {
                    string input = File.ReadAllText(options.ConnectStringFile);
                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        Log.Logger.Information("Connection string override from file");
                        netConnectionString = input;
                    }
                }

                // Set up the migrator
                IDatabaseConfiguration config = new NetDatabaseConfiguration(_database.DbConnectionKey, _configuration);
                dbmgrDataMigration m = new(config, _destinationBase, _database, options.ReplacementFile, replacementParameters, netConnectionString);
                if (options.DryRun)
                {
                    Log.Logger.Information("Executing in DRY RUN mode.  No updates to database will take place.");
                    m.NoDbUpdates = options.DryRun;
                }
                if (options.RunValidations)
                {
                    Log.Logger.Information("Executing validations on script files.");
                    m.ValidateFiles = options.RunValidations;
                }
                if (!string.IsNullOrWhiteSpace(options.ExecuteDirect))
                {
                    Log.Logger.Information("Executing Direct SQL {0}.", options.ExecuteDirect);
                    try
                    {
                        m.GetDataContext().ExecuteScript(options.ExecuteDirect, _database.GetSplitPhrase(), null);
                    }
                    catch(Exception e)
                    {
                        Log.Logger.Error("Failed executing SQL {0}.", e.Message);
                        return EXIT_CODE_GENERAL_ERROR;
                    }
                    return EXIT_CODE_SUCCESS;
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

                if (options.ExtractCurrent)
                {
                    return ProcessExtractCurrentCommand(m) ? EXIT_CODE_SUCCESS : EXIT_CODE_GENERAL_ERROR;
                }

                if (!string.IsNullOrWhiteSpace(options.ExtractSchema))
                {
                    return ProcessExtractSchemaCommand(m, options.ExtractSchema) ? EXIT_CODE_SUCCESS : EXIT_CODE_GENERAL_ERROR;
                }

                // Run the migration
                if (options.Migrate || options.CreateSchema)
                {
                    try
                    {
                        // Validate and/or create the schema
                        bool exists = ValidateAndCreateSchema(m, options.NoCreate);
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
            catch (Exception e)
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
            Stopwatch rt = new ();
            Log.Logger.Information("Processing delta directories");
            m.DeployDeltas(directoryPrefix);
            Log.Logger.Information("Completed delta directories in {0}", rt.ShowElapsedTime());
        }
        private static void DeployCurrent(dbmgrDataMigration m)
        {
            Stopwatch rt = new ();
            Log.Logger.Information("Processing current directories");
            m.DeployCurrent();
            Log.Logger.Information("Completed current directories in {0}", rt.ShowElapsedTime());
        }
        private static void DeployPost(dbmgrDataMigration m)
        {
            Stopwatch rt = new ();
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

        private static bool ProcessExtractCurrentCommand(dbmgrDataMigration m)
        {
            return m.ExtractCurrent();
        }
        private static bool ProcessExtractSchemaCommand(dbmgrDataMigration m, string schema_name)
        {
            return m.ExtractSchema(schema_name);
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

        private static bool ValidateAndCreateSchema(dbmgrDataMigration m, bool noCreate)
        {
            Log.Logger.Information("Validating schema");
            bool exists = m.ValidateSchema();
            if (!exists)
            {
                Stopwatch rt = new ();
                if (!noCreate)
                {
                    Log.Logger.Information("Creating schema");

                    // This must succeed
                    exists = m.CreateSchema();
                }
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