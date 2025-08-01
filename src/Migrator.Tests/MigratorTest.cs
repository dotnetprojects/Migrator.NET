using System;
using System.Collections.Generic;
using System.Reflection;
using Migrator.Framework;
using Migrator.Framework.Loggers;
using NSubstitute;
using NUnit.Framework;

namespace Migrator.Tests;

[TestFixture]
public class MigratorTest
{
    #region Setup/Teardown

    [SetUp]
    public void SetUp()
    {
        SetUpCurrentVersion(0);
    }

    #endregion

    private Migrator _migrator;

    // Collections that contain the version that are called migrating up and down
    private static readonly List<long> _upCalled = new List<long>();
    private static readonly List<long> _downCalled = new List<long>();

    private void SetUpCurrentVersion(long version)
    {
        SetUpCurrentVersion(version, false);
    }

    private void SetUpCurrentVersion(long version, bool assertRollbackIsCalled)
    {
        SetUpCurrentVersion(version, assertRollbackIsCalled, true);
    }

    private void SetUpCurrentVersion(long version, bool assertRollbackIsCalled, bool includeBad)
    {
        var providerMock = Substitute.For<ITransformationProvider>();

        var appliedVersions = new List<long>();

        for (long i = 1; i <= version; i++)
        {
            appliedVersions.Add(i);
        }

        providerMock.AppliedMigrations.Returns(appliedVersions);
        providerMock.Logger.Returns(new Logger(false));

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

        _migrator = new Migrator((ITransformationProvider)providerMock, Assembly.GetExecutingAssembly(), false);

        _migrator.MigrationsTypes.Clear();
        _upCalled.Clear();
        _downCalled.Clear();

        _migrator.MigrationsTypes.Add(typeof(FirstMigration));
        _migrator.MigrationsTypes.Add(typeof(SecondMigration));
        _migrator.MigrationsTypes.Add(typeof(ThirdMigration));
        _migrator.MigrationsTypes.Add(typeof(ForthMigration));
        _migrator.MigrationsTypes.Add(typeof(SixthMigration));

        if (includeBad)
        {
            _migrator.MigrationsTypes.Add(typeof(BadMigration));
        }
    }

    public class AbstractTestMigration : Migration
    {
        public override void Up()
        {
            _upCalled.Add(MigrationLoader.GetMigrationVersion(GetType()));
        }

        public override void Down()
        {
            _downCalled.Add(MigrationLoader.GetMigrationVersion(GetType()));
        }
    }

    [Migration(1, Ignore = true)]
    public class FirstMigration : AbstractTestMigration
    {
    }

    [Migration(2, Ignore = true)]
    public class SecondMigration : AbstractTestMigration
    {
    }

    [Migration(3, Ignore = true)]
    public class ThirdMigration : AbstractTestMigration
    {
    }

    [Migration(4, Ignore = true)]
    public class ForthMigration : AbstractTestMigration
    {
    }

    [Migration(5, Ignore = true)]
    public class BadMigration : AbstractTestMigration
    {
        public override void Up()
        {
            throw new Exception("oh uh!");
        }

        public override void Down()
        {
            throw new Exception("oh uh!");
        }
    }

    [Migration(6, Ignore = true)]
    public class SixthMigration : AbstractTestMigration
    {
    }

    [Migration(7)]
    public class NonIgnoredMigration : AbstractTestMigration
    {
    }

    [Test]
    public void MigrateBackward()
    {
        SetUpCurrentVersion(3);
        _migrator.MigrateTo(1);

        Assert.That(0, Is.EqualTo(_upCalled.Count));
        Assert.That(2, Is.EqualTo(_downCalled.Count));

        Assert.That(3, Is.EqualTo(_downCalled[0]));
        Assert.That(2, Is.EqualTo(_downCalled[1]));
    }

    [Test]
    public void MigrateDownwardWithRollback()
    {
        SetUpCurrentVersion(6, true);

        try
        {
            _migrator.MigrateTo(3);
            Assert.Fail("La migration 5 devrait lancer une exception");
        }
        catch (Exception)
        {
        }

        Assert.That(0, Is.EqualTo(_upCalled.Count));
        Assert.That(1, Is.EqualTo(_downCalled.Count));

        Assert.That(6, Is.EqualTo(_downCalled[0]));
    }

    [Test]
    public void MigrateToCurrentVersion()
    {
        SetUpCurrentVersion(3);

        _migrator.MigrateTo(3);

        Assert.That(0, Is.EqualTo(_upCalled.Count));
        Assert.That(0, Is.EqualTo(_downCalled.Count));
    }

    [Test]
    public void MigrateToLastVersion()
    {
        SetUpCurrentVersion(3, false, false);

        _migrator.MigrateToLastVersion();

        Assert.That(2, Is.EqualTo(_upCalled.Count));
        Assert.That(0, Is.EqualTo(_downCalled.Count));
    }

    [Test]
    public void MigrateUpward()
    {
        SetUpCurrentVersion(1);
        _migrator.MigrateTo(3);

        Assert.That(2, Is.EqualTo(_upCalled.Count));
        Assert.That(0, Is.EqualTo(_downCalled.Count));

        Assert.That(2, Is.EqualTo(_upCalled[0]));
        Assert.That(3, Is.EqualTo(_upCalled[1]));
    }

    [Test]
    public void MigrateUpwardFrom0()
    {
        _migrator.MigrateTo(3);

        Assert.That(3, Is.EqualTo(_upCalled.Count));
        Assert.That(0, Is.EqualTo(_downCalled.Count));

        Assert.That(1, Is.EqualTo(_upCalled[0]));
        Assert.That(2, Is.EqualTo(_upCalled[1]));
        Assert.That(3, Is.EqualTo(_upCalled[2]));
    }

    [Test]
    public void MigrateUpwardWithRollback()
    {
        SetUpCurrentVersion(3, true);

        try
        {
            _migrator.MigrateTo(6);
            Assert.Fail("La migration 5 devrait lancer une exception");
        }
        catch (Exception)
        {
        }

        Assert.That(1, Is.EqualTo(_upCalled.Count));
        Assert.That(0, Is.EqualTo(_downCalled.Count));

        Assert.That(4, Is.EqualTo(_upCalled[0]));
    }

    [Test]
    public void ToHumanName()
    {
        Assert.That("Create a table", Is.EqualTo(StringUtils.ToHumanName("CreateATable")));
    }
}
