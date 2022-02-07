using Migrator.Framework;
using System;

using System.IO;
using System.Linq;
using Index = Migrator.Framework.Index;

namespace Migrator.Tools
{
	[Migration(62)]
	internal class CreateNonclusteredDump : Migration
	{
		public override void Up()
		{

			var tables = Database.GetTables();
			var tab = tables.Where(o => o.ToUpper().StartsWith("WMS") || o.ToUpper().StartsWith("aaCOMMON") || o.ToUpper().StartsWith("aaVISU") || o.ToUpper().StartsWith("aaMFCV2")).ToList();
			string variable = "";
			string removeFKString = "";
			string removePKString = "";
			string removeIndexString = "";
			string addPKString = "";
			string addFKString = "";
			foreach (var table in tab)
			{
				foreach (var fk in Database.GetForeignKeyConstraints(table))
				{
					removeFKString += $"Database.RemoveForeignKey(\"{table}\", \"{fk.Name}\");";
					var fkcols = fk.Columns;
					var pkcols = fk.PkColumns;
					var arr = pkcols.ToArray();
					for (int i = 0; i < arr.Length; i++)
					{
						arr[i] = $"\"{arr[i]}\"";
					}
					addFKString += $"Database.AddForeignKey(\"{fk.Name}\", \"{fk.Table}\", {doArr(fkcols)}, \"{fk.PkTable}\", {doArr(pkcols)});";
				}
				Index[] inds = Database.GetIndexes(table);
				var pK = inds.FirstOrDefault(o => o.PrimaryKey && o.KeyColumns.Length == 1);
				if (pK == null)
					continue;
				var pkCol = Database.GetColumnByName(table, pK.KeyColumns[0]);
				if (pkCol.Type != System.Data.DbType.Guid)
					continue;

				string var = $"pK_{table}String";
				variable += $"String {var}={String.Format("getVariableName(\"{0}\");", table)}";
				removePKString += $"Database.RemovePrimaryKey(\"{table}\");";
				removeIndexString += $"Database.RemoveIndex(\"{table}\",{String.Format("{0}", var)});";
				addPKString += $"Database.AddPrimaryKeyNonClustered({String.Format("{0}", var)}, \"{table}\", new string[] { String.Format("{{\"{0}\"}}", pK.KeyColumns[0]) });";
			}
			File.WriteAllText(@"c:\mlog\wms.txt", variable + "" + removeFKString + "" + removePKString + "" + removeIndexString + "" + addPKString + "" + addFKString);
		}
		private string getVariableName(string table)
		{
			Index[] inds = Database.GetIndexes(table);
			var pK = inds.FirstOrDefault(o => o.PrimaryKey && o.KeyColumns.Length == 1);
			if (pK == null)
				return $"PK_{table}_N";
			var pkCol = Database.GetColumnByName(table, pK.KeyColumns[0]);
			if (pkCol.Type != System.Data.DbType.Guid)
				return $"PK_{table}_N";
			return pK.Name;
		}
		private string doArr(string[] arr)
		{
			for (int i = 0; i < arr.Length; i++)
			{
				arr[i] = $"\"{arr[i]}\"";
			}
			return $"new string[]{String.Format("{{{0}}}", string.Join(",", arr))}";
		}

		public override void Down()
		{
		}
	}
}
