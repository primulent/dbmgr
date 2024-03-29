﻿using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;

namespace dbmgr.utilities
{
    public class dbmgrCommandLineOptions
    {
        // Connectivity Options
        [Option('d', "database_type", HelpText = "Set the database type; mssql = SQL Server", Default = "mssql")]
        public string DatabaseType { get; set; }

        [Option("ci", HelpText = "Connection info for the database type", SetName = "Connectivity")]
        public string ConnectInfo { get; set; }
        [Option("cif", HelpText = "File location for connection information", SetName = "Connectivity")]
        public string ConnectInfoFile { get; set; }

        [Option("cs", HelpText = "Connection string for the database type", SetName = "Connectivity")]
        public string ConnectString { get; set; }
        [Option("csf", HelpText = "Connection string file for the database type", SetName = "Connectivity")]
        public string ConnectStringFile { get; set; }

        [Option("db", HelpText = "Collect the database name", SetName = "Connectivity")]
        public string DbName { get; set; }
        [Option("host", HelpText = "Collect the server host name for the database")]
        public string DbServer { get; set; }
        [Option("port", HelpText = "Collect the server port number for the database")]
        public string DbPort { get; set; }
        [Option("user", HelpText = "Collect the user name for the database")]
        public string DbUser { get; set; }
        [Option("pwd", HelpText = "Collect the password for the database")]
        public string DbPwd { get; set; }
        [Option("opt1", HelpText = "Collect the first optional parameter for the database provider")]
        public string DbOpt1 { get; set; }
        [Option("opt2", HelpText = "Collect the second optional parameter for the database provider")]
        public string DbOpt2 { get; set; }
        [Option("dbf", HelpText = "Standard database settings file for the database type", SetName = "Connectivity")]
        public string DbFile { get; set; }


        // Action Options
        [Option('r', "replacement_file", HelpText = "Token replacement file location")]
        public string ReplacementFile { get; set; }

        [Option("direct", HelpText = "Execute Direct SQL File")]
        public string ExecuteDirect { get; set; }

        [Option("dry", HelpText = "Perform a dry-run; make no updates to the database")]
        public bool DryRun { get; set; }

        [Option('v', "validate", HelpText = "Validate the scripts for improper syntax")]
        public bool RunValidations { get; set; }

        [Option("blue", HelpText = "Perform the blue part of a blue-green deployment; this will run the blue delta scripts and the current scripts")]
        public bool Blue { get; set; }

        [Option("green", HelpText = "Perform the green part of a blue-green deployment; this will run the green delta scripts and the post scripts")]
        public bool Green { get; set; }

        [Option("nocreate", HelpText = "Fail the deployment if the tracking tables do not exist")]
        public bool NoCreate { get; set; }

        

        // System Commands
        [Option('s', "setup", HelpText = "Setup the database structure; create a \\Database directory with the appropriate subdirectories in the current folder")]
        public bool SetupFolders { get; set; }

        [Option('g', "generate", HelpText = "Generate delta script file with the associated comment in the .\\Database\\Deltas subdirectory")]
        public string? GenerateDelta { get; set; }

        // Database Commands
        [Option('t', "test", HelpText = "Test connectivity to the database")]
        public bool TestConnectivity { get; set; }

        [Option('i', "initialize", HelpText = "Create the initial migration version tracking tables only against the specified database.")]
        public bool CreateSchema { get; set; }

        [Option('m', "migrate", HelpText = "Run the current migration against the specified database.")]
        public bool Migrate { get; set; }

        [Option('b', "rollback", HelpText = "Rollback the migration scripts against the specified database.")]
        public int Rollback { get; set; }

        [Option("xc", HelpText = "Extracts the current script files from the selected database")]
        public bool ExtractCurrent { get; set; }

        [Option("xs", HelpText = "Extracts the schema from the selected database")]
        public string ExtractSchema { get; set; }


        [Usage(ApplicationAlias = "dbmgr")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                UnParserSettings longSettings = new UnParserSettings();
                UnParserSettings shortSettings = new UnParserSettings
                {
                    PreferShortName = true
                };
                UnParserSettings[] settings = new UnParserSettings[] { longSettings, shortSettings };

                yield return new Example($"Set the database type to use", settings, new dbmgrCommandLineOptions { DatabaseType = "mssql" });

                yield return new Example($"{Environment.NewLine}String used to connect to the database; varies per database type", settings, new dbmgrCommandLineOptions { ConnectInfo = "database connection" });

                yield return new Example($"{Environment.NewLine}Location of vault file containing information to connect to the database", settings, new dbmgrCommandLineOptions { ConnectInfoFile = "vault.txt" });

                yield return new Example($"{Environment.NewLine}.NET Connection String used to connect to the database; varies per database type", settings, new dbmgrCommandLineOptions { ConnectString = "database connection string" });

                yield return new Example($"{Environment.NewLine}Location of vault file containing .NET Connection string to connect to the database", settings, new dbmgrCommandLineOptions { ConnectStringFile = "vault.txt" });

                yield return new Example($"{Environment.NewLine}Location of token file containing information to replace in the database scripts", settings, new dbmgrCommandLineOptions { ReplacementFile = "tokens.txt" });

                yield return new Example($"{Environment.NewLine}Do not actually update the database during update operations", settings, new dbmgrCommandLineOptions { DryRun = true });

                yield return new Example($"{Environment.NewLine}Error the deployment on script validation errors", settings, new dbmgrCommandLineOptions { RunValidations = true });

                yield return new Example($"{Environment.NewLine}Test basic connectivity to the database", settings, new dbmgrCommandLineOptions { TestConnectivity = true });

                yield return new Example($"{Environment.NewLine}Create initial folder structure in this directory", settings, new dbmgrCommandLineOptions { SetupFolders = true });

                yield return new Example($"{Environment.NewLine}Create initial database migration tracking tables", settings, new dbmgrCommandLineOptions { CreateSchema = true });

                yield return new Example($"{Environment.NewLine}Run the migrations (with specified tokens config file)", settings, new dbmgrCommandLineOptions { Migrate = true });

                yield return new Example($"{Environment.NewLine}Generate delta script file", settings, new dbmgrCommandLineOptions { GenerateDelta = "create admin table" });

                yield return new Example($"{Environment.NewLine}Database name", settings, new dbmgrCommandLineOptions { DbName = "database" });

                yield return new Example($"{Environment.NewLine}Database server", settings, new dbmgrCommandLineOptions { DbServer = "server" });

                yield return new Example($"{Environment.NewLine}Database port", settings, new dbmgrCommandLineOptions { DbPort = "0000" });

                yield return new Example($"{Environment.NewLine}Database user name", settings, new dbmgrCommandLineOptions { DbUser = "user" });

                yield return new Example($"{Environment.NewLine}Database password", settings, new dbmgrCommandLineOptions { DbPwd = "password" });
            }
        }
    }
}