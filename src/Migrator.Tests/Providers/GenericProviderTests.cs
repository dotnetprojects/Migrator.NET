using System.Collections.Generic;

using Migrator.Providers;
using NUnit.Framework;

namespace Migrator.Tests.Providers
{
	[TestFixture]
	public class Create
	{
		[Test]
		public void CanJoinColumnsAndValues()
		{
			var provider = new GenericTransformationProvider();
			string result = provider.JoinColumnsAndValues(new[] {"foo", "bar"}, new[] {"123", "456"});

			Assert.AreEqual("foo='123', bar='456'", result);
		}
	}

	internal class GenericTransformationProvider : TransformationProvider
	{
		public GenericTransformationProvider() : base(null, null as string, null, "default")
		{
		}

		public override bool ConstraintExists(string table, string name)
		{
			return false;
		}

	    public override List<string> GetDatabases()
	    {
	        throw new System.NotImplementedException();
	    }

	    public override bool IndexExists(string table, string name)
		{
			return false;
		}
	}
}
