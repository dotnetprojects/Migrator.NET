using System.Reflection;
using DotNetProjects.Migrator;
using Migrator.Framework;
using Migrator.Framework.Loggers;
using NSubstitute;
using NUnit.Framework;

namespace Migrator.Tests;

[TestFixture]
public class MigrationLoaderTest
{
    #region Setup/Teardown

    [SetUp]
    public void SetUp()
    {
        SetUpCurrentVersion(0, false);
    }

    #endregion

    private MigrationLoader _migrationLoader;

    private void SetUpCurrentVersion(int version, bool assertRollbackIsCalled)
    {
        var providerMock = Substitute.For<ITransformationProvider>();

        providerMock.Logger = new Logger(false);
        providerMock.When(x => x.Dispose()).Do(_ =>
        {
            if (assertRollbackIsCalled)
            {
                providerMock.Received().Rollback();
            }
            else
            {
                providerMock.DidNotReceive().Rollback();
            }
        });

        _migrationLoader = new MigrationLoader(providerMock, Assembly.GetExecutingAssembly(), true);
        _migrationLoader.MigrationsTypes.Add(typeof(MigratorTest.FirstMigration));
        _migrationLoader.MigrationsTypes.Add(typeof(MigratorTest.SecondMigration));
        _migrationLoader.MigrationsTypes.Add(typeof(MigratorTest.ThirdMigration));
        _migrationLoader.MigrationsTypes.Add(typeof(MigratorTest.ForthMigration));
        _migrationLoader.MigrationsTypes.Add(typeof(MigratorTest.BadMigration));
        _migrationLoader.MigrationsTypes.Add(typeof(MigratorTest.SixthMigration));
        _migrationLoader.MigrationsTypes.Add(typeof(MigratorTest.NonIgnoredMigration));
    }

    [Test]
    public void CheckForDuplicatedVersion()
    {
        _migrationLoader.MigrationsTypes.Add(typeof(MigratorTest.FirstMigration));
        Assert.Throws<DuplicatedVersionException>(() =>
        {
            _migrationLoader.CheckForDuplicatedVersion();
        });
    }

    [Test]
    public void LastVersion()
    {
        Assert.That(7, Is.EqualTo(_migrationLoader.LastVersion));
    }

    [Test]
    public void NullIfNoMigrationForVersion()
    {
        Assert.That(_migrationLoader.GetMigration(99999999), Is.Null);
    }

    [Test]
    public void ZeroIfNoMigrations()
    {
        _migrationLoader.MigrationsTypes.Clear();
        Assert.That(0, Is.EqualTo(_migrationLoader.LastVersion));
    }
}
