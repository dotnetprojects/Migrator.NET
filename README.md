Info
----
This Project includes most of the changes from all forks on GitHub! 

But has also a few changes:
  - SQLite support is better (Schema Reading, multiple Primary Keys, and much more)
  - Remove direct References to the Dataproviders
  - Dump Indexes from SQLServer
  - Different Migration Scopes in one Database


NuGet
-----
https://www.nuget.org/packages/DotNetProjects.Migrator/

Introduction
------------

This project is a fork of "ye olde trusty" Migrator.Net - the original project can be found [here on google code][1], and has since [moved to github][2]
  
Usage Example
-------------

M_001_InitialSchema.cs
```cs
[Migration(1)]
public class M_001_InitialSchema : Migration
{
	public override void Up()
	{
		Database.AddTable(
			"Users",
			new Column("Id", DbType.Guid, ColumnProperty.NotNull | ColumnProperty.PrimaryKey),
			new Column("CreationDate", DbType.DateTime, ColumnProperty.NotNull),
			new Column("ModificationDate", DbType.DateTime, ColumnProperty.NotNull),
			new Column("Name", DbType.String, 255),
			new Column("Password", DbType.String, 255),
			new Column("ExplicitRoles", DbType.String, int.MaxValue));
	}

	public override void Down()
	{
	}
}
```

Code to Apply Migration:
```cs
using (var p = ProviderFactory.Create(ProviderTypes.SQLite, connection, null))
{
	var migrator = new Migrator(p, typeof(M_001_InitialSchema).Assembly, false));

	if (migrator.LastAppliedMigrationVersion != null && migrator.LastAppliedMigrationVersion.Value > migrator.AssemblyLastMigrationVersion)
	{
		throw new Exception("Database has newer Migrations applied then the Software supports");
	}
	else
	{
		migrator.MigrateToLastVersion();
	}
}
```

What's different in the fork
----------------------------

In this fork the main changes are:

* Now targets .Net Framework 4.0 instead of 2.0/3.5.
* NetStandart 2.0 will be supported when released
* Support for reserved words.
* Support for guid types across all databases.
* Utility classes for removing all tables etc. from a database (to support migration integration tests).
* Warnings in Oracle when attempting to create column/table/key names that are over-length.
* Removed reliance on deprecated SqlServer views such as Sysconstraints (to support Azure Sql Server deployment).

  [1]: http://code.google.com/p/migratordotnet/
  [2]: https://github.com/migratordotnet/Migrator.NET
