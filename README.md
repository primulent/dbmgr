# Overview

dbmgr is an opinionated database migration and management framework that utilizes convention over configuration.  It enables deployment pipelines to manage database schema, programmability and data changes using direct SQL.   More information on [the dbmgr website](http://www.dbmgr.net/).

## Benefits
Makes database deployments consistent and reliable by ensuring your database always matches your application.
Database and code are in one package, versioned using standard SCM tooling, making it easier to detect and debug database problems.

## Explained

### Convention
A folder structure will be added to your .NET project.
Database
  Deltas
  Current
    Functions
    Procedures
    Triggers
    Views
  Post

Each child folder of Database has a defined purpose.  

To ensure the database is verisoned properly, each database will become aware of its state using system tables that are created using dbmgr.  These tables are also used to ensure good runtime performance of dbmgr.

Each folder under the Database folder has a specific purpose in maintaining your versioned database.  These are explained in the following sections.

### Deltas

Deltas, or migration, scripts are used to incrementally change your database over time.  They should be used to manipulate schema structure or maintain seed/reference data in the database.

- Each change to the database is recorded in a SQL script.
- Delta script files are generated using dbmgr to ensure they follow the naming convention.
- Deltas begin with a UTC-based timestamp that is used to order them.
- Standards source control keeps track of these files.

### Current

Current, or program code, scripts are used to maintain the latest set of database code in your database.  In effect, anything that can be safely replaced in your database should be considered a Current script.  Some examples are Stored Procedures, Views, Stored Procedures and Triggers.  

- Provides an up-to-date catalogue of all views, functions, stored procedures, etc. for your application in your codebase.
- Current script files must follow proper naming convention for the database provider.
- Current scripts are classified by a prefix before the script file, which influences the order in which they run.
- Supports advanced dependency tracking and resolution between code files to break the classified order.
- Standards source control keeps track of these files.


### Post

Post scripts are run at the end of each deployment.

- No environment tracking of the post scripts.
- Used for post-deployment maintenance tasks.

---

# Getting Started

## Installation

Extract the binaries, dbmgr into a directory.  Ensure the system PATH is set to this directory.

## Basics

dbmgr will return a successfull OS error code (0) if it's operations worked properly.  If an error occurred, you will receive an error code from the console application.

dbmgr produces log messages to the console as you run the command.  In addition, it will emit a log file, that you can use to determine what worked and what did not.

You can always get help about the available commands by using the help command.
```c#
dbmgr --help
```

## Establish Connectivity

To specify how to connect to the database, select a database provider.

In order to establish connectivity from there, there are several options: 
* Standard parameters
* Provider-specific format
* Full Connection String

We'll explain each below, so you can choose the method that works best for your needs.

---

To use the standard database connectivity parameters, use the command line and specify the individual parts of a standard connection string.  These will be interpreted by the database provider.
```c#
dbmgr --db <database> --host <host> --port <port> --user <user> --pwd <password> --opt1 <provider parameter 1> --opt2 <provider parameter 2>
dbmgr --db Northwind --host (local) --user sa --pwd password --opt1 true
```
SQL Server Provider format:

_Data Source={\<host>};Initial Catalog={\<database>};Integrated Security={\<opt1>};User ID={\<user>};Password={\<password>}_

To use the standard database format from a file instead of the command line, use a "vault" file.  Place your standard information into a text file on the file system and refer to it.
```c#
dbmgr -dbf <path_to_vault_file>
dbmgr -dbf vault_dbf.txt
```
SQL Server Provider file format: 
* \<database>
* \<host>
* \<port>
* \<user>
* \<password>
* \<opt1>
* \<opt2>

Note: One line per item - leave a blank row for non-specified items.

To use the provider-specific format, use the command line and specify the connection format as appropriate for the database provider.
```c#
dbmgr -ci <connection_format>
dbmgr -ci (local)\database
```
SQL Server Provider Format: 

_[user:password@]myServer\instanceName_

To use a provider-specific format from a file instead of the command line, use a "vault" file.  Place your connection information into a text file on the file system and refer to it.
```c#
dbmgr -cif <path_to_vault_file>
dbmgr -cif vault_cif.txt
```

Use a fully specified .NET Connection String on the command line.
```c#
dbmgr -cs <connection string>
dbmgr -cs "Data Source=(local);Initial Catalog=db;Integrated Security=true;MultipleActiveResultSets=True"
```

To use a .NET connection string from a file instead of the command line, use a "vault" file.  Place your .NET connection string into a text file on the file system and refer to it.
```c#
dbmgr -csf <connection string file>
dbmgr -csf vault_csf.txt
```

SQL Server Provider Note: 

You must ensure your connectionstring specified _MultipleActiveResultSets=True_

---

That's it - you are now ready to use dbmgr!

## Development

### Initializing the basic project folder structure

```c#
dbmgr -s
```



### Creating a Delta Script
dbmgr contains a helper to create Delta script code files, because the naming convention is more challenging to generate.  
To use the tool, navigate to the base directory of your project that contains the Database folder.

```c#
dbmgr -g "new database script"
```


```c#
dbmgr -g "new database script"
```

**Important:**
* Once a committed valid script has been deployed to any environment, you should not change it - instead, create a new change that is sequenced after the original script.
* If a committed script is invalid and will not succesfully deploy to any environment, you should correct it in the script.

#### Reverse engineering your database schema

Currently this feature is not supported by any provider of dbmgr.

### Creating a Current Script

Current scripts are like any SQL code file, placed in the proper directory and conforming to the proper naming convention.

- The file must be placed in the Current folder or a sub-directory of Current.
- The file name must follow the naming convention of the selected database provider.

For SQL Server, the naming convention and order of scripts are as follows:
  - Other scripts, prefixed with "o_" and end in ".sql"
  - Sequence scripts, prefixed with "sq_" and end in ".sql"
  - Synonym scripts, prefixed with "sn_" and end in ".sql"
  - Function scripts, prefixed with "fn_" and end in ".sql"
  - View scripts, prefixed with "vw_" and end in ".sql"
  - Stored Procedure scripts, prefixed with "sp_" and end in ".sql"
  - Trigger scripts, prefixed with "tr_" and end in ".sql"

#### Reverse engineering your database code

If you want to extract the current database code into source files, use the extract feature of dbmgr.

To use the tool, navigate to the base directory of your project that contains the Database folder and run the Extract command.
```c#
dbmgr -x
```

dbmgr will place all of the code of your database in the proper locations with the proper naming convention.  Any issues reverse engineering will be displayed in the logs.

Currently, only the SQL Server database provider supports it this feature.
 

### Creating a Post Script
Created like any SQL code file, with the following additional requirements:
- The file must be placed in the Post directory.
- The file must have the extension of .sql.

## Deployment

### Deploying the database changes

To deploy your changes, navigate to the root project folder (the one containing the Database subdirectory) and run the migration command:

```c#
dbmgr -m
```

dbmgr will first execute all unrun deltas, then all unrun current scripts, and lastly all the post scripts.


By default, the migration command will create the database tracking tables in the environment if they do not yet exist.  If you want to disable the feature and fail if the tracking tables do not yet exist, pass in the nocreate option to the migration command:
```c#
dbmgr -m --nocreate
```

And, if you wish to create the tracking tables independently, you can do that with the initialize command:

```c#
dbmgr -i
```


### Support for dry-runs
You may run any migration to see what would happen with the --dry option.  Nothing in the database will be updated.

```c#
dbmgr -m --dry
```


## Support for Blue/Green deployments
For blue/green deployments, the process is slightly changed.  The basic premise is to run the blue changes, which will contain backward compatible changes, followed by the green changes, which will "finish off" the deployment.

To achieve this, the folder structure is changed.  To initialize, run the initialize command with either the --blue or --green flags.
```c#
dbmgr -s --blue
```

Running a deployment with the --blue flag will cause the Blue deltas to run, followed by the Current.
```c#
dbmgr -m --blue
```

Running a deployment with the --green flag will cause the Green deltas to run, followed by the Post scripts.
```c#
dbmgr -m --green
```

Note: The blue scripts should operate in a manner that will not cause the application to break.



## Advanced Topics
There are times when you need to alter the order of a current script; for example you may have a dependency that needs to be resolved in a different order than the default provider order.  In order to do this, follow the steps below:
At the top of your current file, add a comment, and then in {} braces, reference the name of the dependent script.


The Delta folder can be broken into subdirectories to ensure you don't have too many scripts in the root folder.

---

## Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

Please make sure to update tests as appropriate.

## License
GNU AGPLv3
https://choosealicense.com/licenses/agpl-3.0/
