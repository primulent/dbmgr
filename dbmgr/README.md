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

## Installation and configuration

Extract the binaries, dbmc into a directory.  Ensure the system PATH is set to this directory.

To specify how to connect to the database, there are several options: 

Use the command line and specify the connection format as appropriate for the database provider.
```c#
dbmc -c <connection_format>
dbmc -c (local)\database
```
SQL Server Format: [user:password@]myServer\instanceName


Use a "vault" file and place your connection_format into a text file on the file system.
```c#
dbmc -f <path_to_vault_file>
dbmc -f vault.txt
```

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

### Initializing the database tracking

```c#
dbmc -i
```

### Deploying the database changes

```c#
dbmc -m
```

---

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