using System.IO;
using System.Reflection;
using Migrator.Compile;
using NUnit.Framework;

namespace Migrator.Tests
{
    [TestFixture]
    public class ScriptEngineTests
    {
        [Test]
        public void CanCompileAssemblies()
        {
            var engine = new ScriptEngine();

            // This should let it work on windows or mono/unix I hope
            var dataPath = Path.Combine(Path.Combine("..", Path.Combine("src", "Migrator.Tests")), "Data");

            var asm = engine.Compile(dataPath);
            Assert.That(asm, Is.Not.Null);

            var loader = new MigrationLoader(null, asm, false);
            Assert.That(2, Is.EqualTo(loader.LastVersion));

            Assert.That(2, Is.EqualTo(MigrationLoader.GetMigrationTypes(asm).Count));
        }
    }
}