# Overview

dbmc is an opinionated database migration and management framework for Microsoft .NET Core that utilizes convention over configuration.  
It enables deployment pipelines to manage database schema, programmability and data changes using direct SQL.

dbmc currently supports Microsoft SQL Server databases.

## Benefits
dbmc makes database deployments consistent and reliable by ensuring your database always matches your application.  Database and code are in one package, versioned using standard SCM tooling, making it easier to detect and debug database problems.

It is especially suited for legacy applications that involve procedures and business logic still in the database.  dbmc facilitates getting your database under control, and thus will assist in modernization of legacy applications.

## Convention

A folder structure will be added to your .NET project.
- Database
  - Deltas
  - Current
  - Post

Each child folder of Database has a defined purpose.  Additional subdirectories will be used underneath the directories listed above which may vary due to installation scenario and/or database provider.

To ensure the database is versioned properly, each database will become aware of its state using system tables that are created using dbmc.  These tables are also used to ensure good runtime performance of dbmc.

### Deltas
- Used to incrementally change the database _structure_ and _data_ over time in a linear fashion.
- Each change to the database is recorded in a SQL script.
- Script files must follow proper naming convention.
- SCM keeps track of these changes in your codebase.

Warning: This style of database change management requires following additional development practices.  For example, you should not change committed delta scripts once they have been deployed to any environment.  Doing so may yield non-equivalent environments.


### Current
- Used to reflect the "current" set of database _code_ (views, functions, stored procedures, etc.) as normal code files.
- Provides an up-to-date catalogue of all views, functions, stored procedures, etc. for your application in the codebase.
- Runs in the order defined by the database provider.
  - For SQL Server:
    1. Other
    1. Synonyms
    1. Functions
    1. Views
    1. StoredProcedures
    1. Triggers
- Script files should follow proper naming convention for the database provider in order to maintain the order of execution.
- Supports dependency tracking and resolution between code files, to explicitly override the default order of the provider.
- SCM keeps track of these changes in your codebase.

### Post
- Generally, used to perform maintenance tasks after each deployment.
- Independent scripts that will run at the end of *every* deployment.
- No environment tracking of the post scripts.
- Run in "alphanumeric" order, based on filename.

## Order of Execution
The standard order of operations for the committed scripts is as follows:
1. Deltas that have not been run will execute.
1. Current scripts that have been altered will execute
1. Post scripts will execute.

---

# Getting Started

## Installation

Extract the binaries, dbmc into a directory.  Ensure the system PATH is set to this directory.

## Help

Help for the available commands, with examples, is available using the help command.

```c#
dbmc --help
```

## Development

### Initializing the basic project folder structure for a new project

To get started, run the following command in the project directory to initialize the folder structure.

```c#
dbmc -s
```

### Creating a Delta Script
Because the naming convention is more challenging to generate for delta scripts, dbmc includes a command to help with this.
To use the tool, navigate to the base directory of your project that contains the Database folder.

```c#
dbmc -g "new database script"
```

will yield files similar to:

```c#
<timestamp>_new_database_script.up
<timestamp>_new_database_script.down
```
for example:
```c#
20200711191228_new_database_script.up
20200711191228_new_database_script.down
```

TODO: One-way migration

### Creating a Current Script
Created like any SQL code file, with the following additional requirements:
- The file must be placed in the Current folder or a sub-directory of Current.
- The file must follow the naming convention for the database provider.
  - For SQL Server:
    - Triggers must start with _tr__
    - Stored Procedures must start with _sp__
    - Views must start with _vw__
    - Functions must start with _fn__
    - Synonyms must start with _sn__
    - Sequences must start with _sq__
    - Other code must start with _o__

For example:

```c#
./Database/Current/Functions/fn_get_new_records.sql
```

TODO: Dependency tracking syntax 

### Creating a Post Script
Created like any SQL code file, with the following additional requirements:
- The file must be placed in the Post directory.
- The file must have the extension of .sql.

For example:

```c#
./Database/Post/update_indices.sql
```

## Running dbmc

## Connecting to the database
To specify how to connect to the database, there are several options: 

Use the command line and specify the connection format as appropriate for the database provider.
```c#
dbmc -c <connection_format>
dbmc -c (local)\database
```
SQL Server Format: [user:password@]myServer\instanceName


You may use a file to store your connection information (a "vault" file) to keep your passwords off of the command line.  Simply place the same connection_format string you would use on the command line into a text file on the file system.

```c#
dbmc -f <path_to_vault_file>
dbmc -f vault.txt
```

---
> **_NOTE:_** Specifying one of the two connection options to the database is a requirement for any of the following commands.
---

#### Support for dry-runs
You may run any migration to see what would happen with the --dry option.  Nothing in the database will be updated.

### Deploying the database changes
In order to run the code, navigate to the project directory (which will contain the Database folder) and execute the migration command:

```c#
dbmc -m
```

dbmc will verify and install tracking tables as necessary, and run the migration process.  The command will execute the deployment of the Deltas, followed by the Current scripts, followed by the Post scripts (unless altered by the blue/green deployment options described below).


## Advanced Topics

### Initializing the database tracking separately

In some cases, creation of the tracking tables requires elevated database privileges and must be separated from the normal migration procedures.  
dbmc supports this use case with a command that will just set up the tracking tables that dbmc uses in the target database.

```c#
dbmc -i
```

### Reverse Engineering
You may wish to seed the Current directories.  This can be done using the extract feature of dbmc.  

To use the tool, navigate to the base directory of your project that contains the Database folder.
```c#
dbmc -x
```

### Support for Blue/Green deployments
For blue/green deployments, the process is slightly changed.  The basic premise is to run the blue changes, which will contain backward compatible changes, followed by the green changes, which will "finish off" the deployment.

To achieve this, the folder structure is changed to include a blue and green folder undern the Deltas folder.
- Database
  - Deltas
    - Blue
    - Green
  - Current
  - Post

To initialize, run the initialize command with either the --blue or --green flags.
```c#
dbmc -s --blue
```

Running a deployment with the --blue flag will cause the Blue deltas to run, followed by the Current.
```c#
dbmc -m --blue
```

Running a deployment with the --green flag will cause the Green deltas to run, followe by the Post scripts.
```c#
dbmc -m --green
```

Note: The blue scripts should operate in a manner that will not cause the code to break; i.e. they will be backwards compatible with the existing code.


## Best Practices
- The Delta folder can be broken into subdirectories by major release version, to ensure you don't have too many scripts in the root folder.
  For example, consider breaking your deltas by "release":
  - Deltas
    - Release1 <-- all of the scripts in "release 1" are here
    - Release2 <-- all of the scripts in "release 2" are placed here
    - Release3 <-- all of the scripts in "release 3" are placed here

- Do not include explicit transaction statements in your Current and/or Delta code files.

---

## Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

Please make sure to update tests as appropriate.

## License
[MIT](https://choosealicense.com/licenses/mit/)