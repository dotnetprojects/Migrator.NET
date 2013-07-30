#region License

//The contents of this file are subject to the Mozilla Public License
//Version 1.1 (the "License"); you may not use this file except in
//compliance with the License. You may obtain a copy of the License at
//http://www.mozilla.org/MPL/
//Software distributed under the License is distributed on an "AS IS"
//basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//License for the specific language governing rights and limitations
//under the License.

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using Migrator.Framework;
using Migrator.Providers;

namespace Migrator.Tools
{
	public class SchemaDumper
	{
	    private readonly ITransformationProvider _provider;

		public SchemaDumper(ProviderTypes provider, string connectionString, string defaultSchema)
		{
			_provider = ProviderFactory.Create(provider, connectionString, defaultSchema);
		}

		public string Dump()
		{
			var writer = new StringWriter();

			writer.WriteLine("using Migrator;\n");
			writer.WriteLine("[Migration(1)]");
			writer.WriteLine("public class SchemaDump : Migration");
			writer.WriteLine("{");
			writer.WriteLine("\tpublic override void Up()");
			writer.WriteLine("\t{");

			foreach (string table in _provider.GetTables())
			{
				writer.WriteLine("\t\tDatabase.AddTable(\"{0}\",", table);
				var columnLines = new List<string>();
				foreach (Column column in _provider.GetColumns(table))
				{
                    if (column.Size>0 && column.DefaultValue!=null)
				        columnLines.Add(string.Format("\t\t\tnew Column(\"{0}\", DbType.{1}, {2}, {3}, \"{4}\")", column.Name, column.Type, column.Size, getColumnPropertyString(column.ColumnProperty), column.DefaultValue));
                    else if (column.Size > 0)
                        columnLines.Add(string.Format("\t\t\tnew Column(\"{0}\", DbType.{1}, {2}, {3})", column.Name, column.Type, column.Size, getColumnPropertyString(column.ColumnProperty)));
                    else if (column.DefaultValue != null)
                        columnLines.Add(string.Format("\t\t\tnew Column(\"{0}\", DbType.{1}, {2}, \"{3}\")", column.Name, column.Type, getColumnPropertyString(column.ColumnProperty), column.DefaultValue));                    
                    else
                        columnLines.Add(string.Format("\t\t\tnew Column(\"{0}\", DbType.{1}, {2})", column.Name, column.Type, getColumnPropertyString(column.ColumnProperty)));
				}
                foreach (var constraint in _provider.GetForeignKeyConstraints(table))
                {
                    columnLines.Add(string.Format("\t\t\tnew ForeignKeyConstraint(\"{0}\", \"{1}\", new[] {{\"{2}\"}}, \"{3}\", new[] {{\"{4}\"}})", constraint.Name, constraint.Table, string.Join("\",\"", constraint.Columns), constraint.PkTable, string.Join("\",\"", constraint.PkColumns)));                    
                }
				writer.WriteLine(string.Join(string.Format(",{0}", Environment.NewLine), columnLines.ToArray()));
				writer.WriteLine("\t\t);");
                
                foreach (Index index in _provider.GetIndexes(table))
                {
                    if (index.IncludeColumns == null)
                    {
                        writer.WriteLine(string.Format("\t\tDatabase.AddIndex(\"{0}\", new Index() {{ Name = \"{1}\", Unique = {2}, Clustered = {3}, KeyColumns = new[] {{\"{4}\"}}, IncludeColumns = null }});",
                            table,
                            index.Name,
                            index.Unique.ToString().ToLower(),
                            index.Clustered.ToString().ToLower(),
                            string.Join("\",\"", index.KeyColumns)));
                    }
                    else
                    {
                        writer.WriteLine(string.Format("\t\tDatabase.AddIndex(\"{0}\", new Index() {{ Name = \"{1}\", Unique = {2}, Clustered = {3}, KeyColumns = new[] {{\"{4}\"}}, IncludeColumns = new[] {{\"{5}\"}} }});",
                            table,
                            index.Name,
                            index.Unique.ToString().ToLower(),
                            index.Clustered.ToString().ToLower(),
                            string.Join("\",\"", index.KeyColumns),
                            string.Join("\",\"", index.IncludeColumns)));
                    }
                }

                writer.WriteLine("");
			}

            writer.WriteLine("");
            writer.WriteLine("");

            /*foreach (string table in _provider.GetTables())
            {
                foreach (var constraint in _provider.GetForeignKeyConstraints(table))
                {
                    writer.WriteLine("\t\tDatabase.AddForeignKey(\"{0}\", \"{1}\", new[] {{\"{2}\"}}, \"{3}\", new[] {{\"{4}\"}});", constraint.Name, constraint.Table, string.Join("\",\"", constraint.Columns), constraint.PkTable, string.Join("\",\"", constraint.PkColumns));
                    writer.WriteLine("");
                }                                                                
            }*/

            writer.WriteLine("");
            writer.WriteLine("");

			writer.WriteLine("\t}\n");
			writer.WriteLine("\tpublic override void Down()");
			writer.WriteLine("\t{");

			foreach (string table in _provider.GetTables())
			{
				writer.WriteLine("\t\tDatabase.RemoveTable(\"{0}\");", table);
                writer.WriteLine("");
			}

			writer.WriteLine("\t}");
			writer.WriteLine("}");

			return writer.ToString();
		}

        private string getColumnPropertyString(ColumnProperty prp)
        {
            string retVal = "";
            if ((prp & ColumnProperty.ForeignKey) == ColumnProperty.ForeignKey) retVal += "ColumnProperty.ForeignKey | ";
            if ((prp & ColumnProperty.Identity) == ColumnProperty.Identity) retVal += "ColumnProperty.Identity | ";
            if ((prp & ColumnProperty.Indexed) == ColumnProperty.Indexed) retVal += "ColumnProperty.Indexed | ";
            if ((prp & ColumnProperty.NotNull) == ColumnProperty.NotNull) retVal += "ColumnProperty.NotNull | ";
            if ((prp & ColumnProperty.Null) == ColumnProperty.Null) retVal += "ColumnProperty.Null | ";
            if ((prp & ColumnProperty.PrimaryKey) == ColumnProperty.PrimaryKey) retVal += "ColumnProperty.PrimaryKey | ";
            if ((prp & ColumnProperty.PrimaryKeyWithIdentity) == ColumnProperty.PrimaryKeyWithIdentity) retVal += "ColumnProperty.PrimaryKeyWithIdentity | ";
            if ((prp & ColumnProperty.Unique) == ColumnProperty.Unique) retVal += "ColumnProperty.Unique | ";
            if ((prp & ColumnProperty.Unsigned) == ColumnProperty.Unsigned) retVal += "ColumnProperty.Unsigned | ";

            if (retVal != "") retVal = retVal.Substring(0, retVal.Length - 3);

            if (retVal == "") retVal = "ColumnProperty.None";

            return retVal;
        }

		public void DumpTo(string file)
		{
			using (var writer = new StreamWriter(file))
			{
				writer.Write(Dump());
			}
		}
	}
}