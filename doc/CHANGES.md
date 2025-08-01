# Change history

## 7.0.159 - 7.0.209 (Nuget Version)
### Breaking Changes
+ Minimum SQLite Version: 3.8.2 (2013-12-06)

#### ColumnProperty
+ Removed `ForeignKey` use method `AddForeignKey(...)` instead.
+ `Unique` is now obsolete because you cannot add a constraint name using it. Removing it by name is therefore impossible without investigation. 

### Other changes
Several fixes see PRs

## Version 0.8.0
- Implemented specific support for SQL Server 2005 (MAX parameter basically)
- Implemented specific support for SQL Server CE (Thanks Gustavo Ringel)

- Changed the parameter names of AddForeignKey to try and make them easier to understand
- Made Version for MigrationAttribute non-optional
- Changed the Migration number to a long value so datestamps could be used.
- Added a SqlScriptFileLogger to log all of the SQL changes to a file (Thanks carl.lotz)
	- Supported in MSBuild and NAnt tasks
- Implemented Fluent interface using SchemaBuilder

- Breaking Change!: Changed the storage of versions applied to one that keeps all the versions. This
  will help deal with branches during development. (Thanks evonzee)
  You will need to update your SchemaInfo table by inserting a row for all previously applied versions.

- Added contribs for extra interesting code
  - Added Migration.Web to show how to run migrations from a Web App directly.

## Version 0.7.0
- Geoff's major refactorings fork re-merged back into the trunk
- Improved the build process allowing developers to override things in local.properties
- Added a packaging task to generate a zip file for a build
- SQL Server Default value now supports non-quoted functions and NULL
- Compound Primary Key bug was unsetting NotNull from all the columns has been fixed
- Added a Visual Studio Template that can be installed to help create new Migrations
- Various Small bug fixes
	- Logger set after Migrations loaded in NAnt/MSBuild tasks
	- Patch for wrong string format in SQLTransformationProvider
	- One of the TranformationProvider.GenerateForeignKey methods ignores constraint
	- No warning is issued if there are no migrations found
	- Migration.InitializeOnce outputs to console

## Version 0.6.0
- Better API documentation
- Support declaring compound primary keys in the AddTable methods.
- Add support for Renaming Tables and Columns
- Add support for adding and removing Unique Constraints and Check Constraints
- Add support for changing a column definition
- Support compiling the migrations on the fly 

- Breaking Change!: Change the Insert and Update methods to separate the column names from the column values.
- Breaking Change!: Reversed the columns to have the table name first on ConstraintExists, PrimaryKeyExists and 
	RemovePrimaryKey

## Version 0.5.0
Forked the project to fix a bunch of issues.
- Major refactoring
    - Breaking Change!: Changing to DBTypes instead of .NET Types for specifying column types
    - Separated out Framework and Providers DLLs
    - Made the Providers more of a Template model so they have to do less work and are more
      declarative in nature.
- Fixed SQL Server Provider
- Added support for SQLite
- Added "Multi-DB" support for DB Specific SQL code
    - Database["ProviderName"].ExecuteSQL() will only run if you are running against "ProviderName"
- Much more Unit Testing
      
See http://code.macournoyer.com/migrator for further info

## Version 0.2.0
- Added support for char type on SQL Server by Luke Melia & Daniel Berlinger
- Fix some issues with SQL Server 2005 pointed out by Luke Melia & Daniel Berlinger
- Added migrate NAnt task
- Added basic schema dumper functionnality for MySql.
- Restructured project tree
- Applied patch from Tanner Burson to fix first run problem with SchemaInfo table creation

## Version 0.1.0
- Renamed "RemoveConstraint" to "RemoveForeignKey". We need to add Unique constraint support, but it's not in here yet.
- Merged most of the provider unit test code to a base class.
- Changed the hard dependencies on the ADO.NET providers to be a reflection-based load, just like NHibernate.
- Changed the MySQL provider "RemoveForeignKey" method to call two SQL calls to the DB before the constraint would actually be deleted. This is the wierd piece, and I am not sure if it's just my OS or version of MySQL that needs this.
- Added a few more assertions to the provider unit tests just to be sure the expectations are being met.
- Changed the build file to handle different platforms, since the Npgsql driver is so platform-specific.

