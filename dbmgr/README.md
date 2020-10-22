# Overview

dbmc is an opinionated database migration and management framework that utilizes convention over configuration.  It enables deployment pipelines to manage database schema, programability and data changes using direct SQL.

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

To ensure the database is verisoned properly, each database will become aware of its state using system tables that are created using dbmc.  These tables are also used to ensure good runtime performance of dbmc.


### Deltas
- Used to incrementally change the database over time.
- Each change to the database is recorded in a SQL script.
- Script files must follow proper naming convention.
- SCM keeps track of these changes in our codebase.

Warning: You can no longer change history!


### Current
- Used to reflect the "current" set of database code (views, functions, stored procedures, etc.) as normal code files.
- Provides an up-to-date catalogue of all views, functions, stored procedures, etc. for your application in the codebase.
- Script files must follow proper naming convention for the database provider.
- Supports dependency tracking and resolution between code files.
- SCM keeps track of these changes in our codebase.


### Post
- Run at the end of every deployment.
- No environment tracking of the post scripts.

---

# Getting Started

## Installation

Extract the binaries, dbmc into a directory.  Ensure the system PATH is set to this directory.

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
dbmc --db <database> --host <host> --port <port> --user <user> --pwd <password> --opt1 <provider parameter 1> --opt2 <provider parameter 2>
dbmc --db Northwind --host (local) --user sa --pwd password --opt1 true
```
SQL Server Provider format:

_Data Source={\<host>};Initial Catalog={\<database>};Integrated Security={\<opt1>};User ID={\<user>};Password={\<password>}_

To use the standard database format from a file instead of the command line, use a "vault" file.  Place your standard information into a text file on the file system and refer to it.
```c#
dbmc -dbf <path_to_vault_file>
dbmc -dbf vault_dbf.txt
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
dbmc -ci <connection_format>
dbmc -ci (local)\database
```
SQL Server Provider Format: 

_[user:password@]myServer\instanceName_

To use a provider-specific format from a file instead of the command line, use a "vault" file.  Place your connection information into a text file on the file system and refer to it.
```c#
dbmc -cif <path_to_vault_file>
dbmc -cif vault_cif.txt
```

Use a fully specified .NET Connection String on the command line.
```c#
dbmc -cs <connection string>
dbmc -cs "Data Source=(local);Initial Catalog=db;Integrated Security=true;MultipleActiveResultSets=True"
```

To use a .NET connection string from a file instead of the command line, use a "vault" file.  Place your .NET connection string into a text file on the file system and refer to it.
```c#
dbmc -csf <connection string file>
dbmc -csf vault_csf.txt
```

SQL Server Provider Note: 

You must ensure your connectionstring specified _MultipleActiveResultSets=True_

---

That's it - you are now ready to use dbmgr!

## Development

### Initializing the basic project folder structure

```c#
dbmc -s
```



### Creating a Delta Script
dbmc contains a helper to create Delta script code files, because the naming convention is more challenging to generate.  
To use the tool, navigate to the base directory of your project that contains the Database folder.

```c#
dbmc -g "new database script"
```


```c#
dbmc -g "new database script"
```

### Creating a Current Script
Created like any SQL code file, with the following additional requirements:
- The file must be placed in the Current folder or a sub-directory of Current.
- The file must follow the naming convention:

You may wish to seed the current directories.  This can be done using the extract feature of dbmc.  

To use the tool, navigate to the base directory of your project that contains the Database folder.
```c#
dbmc -x
```

### Creating a Post Script
Created like any SQL code file, with the following additional requirements:
- The file must be placed in the Post directory.
- The file must have the extension of .sql.

## Deployment


### Deploying the database changes

```c#
dbmc -m
```

---

### Initializing the database tracking

```c#
dbmc -i
```


### Support for dry-runs
You may run any migration to see what would happen with the --dry option.  Nothing in the database will be updated.

## Support for Blue/Green deployments
For blue/green deployments, the process is slightly changed.  The basic premise is to run the blue changes, which will contain backward compatible changes, followed by the green changes, which will "finish off" the deployment.

To achieve this, the folder structure is changed.  To initialize, run the initialize command with either the --blue or --green flags.
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

Note: The blue scripts should operate in a manner that will not cause the code to break.


## Best Practices
The Delta folder can be broken into subdirectories to ensure you don't have too many scripts in the root folder.

---

## Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

Please make sure to update tests as appropriate.

## License
[MIT](https://choosealicense.com/licenses/mit/)