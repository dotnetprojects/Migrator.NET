



// Does not work because we cannot reuse the connection at this point in time.






// using System.Data;
// using DotNetProjects.Migrator.Providers.Impl.SQLite;
// using Migrator.Framework;
// using Migrator.Tests.Providers.SQLite.Base;
// using NUnit.Framework;

// namespace Migrator.Tests.Providers.SQLite;

// [TestFixture]
// [Category("SQLite")]
// public class SQLiteTransformationProvider_PRAGMAForeignKeysTests : SQLiteTransformationProviderTestBase
// {
//     [Test, Description("Tests the set ON indirectly. Integrity violation should throw.")]
//     public void PragmaForeignKeys_IntegrityViolation_Throws()
//     {
//         const string parentTableName = "ParentTable";
//         const string childTableName = "ChildTable";
//         const string propertyIdName = "Id";
//         const string foreignKeyColumnName = "ParentId";

//         Provider.AddTable(parentTableName, new Column(propertyIdName, DbType.Int32, ColumnProperty.PrimaryKey));
//         Provider.AddTable(childTableName, new Column(propertyIdName, DbType.Int32, ColumnProperty.PrimaryKey), new Column(foreignKeyColumnName, DbType.Int32));

//         ((SQLiteTransformationProvider)Provider).BeginTransaction();
//         ((SQLiteTransformationProvider)Provider).SetPragmaForeignKeys(false);
//         var pragmaForeignKeyState1 = ((SQLiteTransformationProvider)Provider).IsPragmaForeignKeysOn();

//         Provider.ExecuteNonQuery($"INSERT INTO {parentTableName} ({propertyIdName}) VALUES (1)");

//         // Integrity violation does not throw due to set OFF validation
//         Provider.ExecuteNonQuery($"INSERT INTO {childTableName} ({propertyIdName}, {foreignKeyColumnName}) VALUES (1, 999)");

//         Provider.ExecuteNonQuery($"DELETE FROM {childTableName}");

//         ((SQLiteTransformationProvider)Provider).SetPragmaForeignKeys(true);
//         var pragmaForeignKeyState2 = ((SQLiteTransformationProvider)Provider).IsPragmaForeignKeysOn();


//         Assert.That(pragmaForeignKeyState1, Is.False);
//         Assert.That(pragmaForeignKeyState2, Is.True);
//     }
// }
