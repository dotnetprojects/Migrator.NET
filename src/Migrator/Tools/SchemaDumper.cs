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
using System.Linq;
using DotNetProjects.Migrator.Framework;
using Migrator.Framework;
using Migrator.Providers;
using Index = Migrator.Framework.Index;

namespace Migrator.Tools
{
    public class SchemaDumper
    {
        private readonly ITransformationProvider _provider;
        string[] tables;
        List<ForeignKeyConstraint> foreignKeys = new List<ForeignKeyConstraint>();
        List<Column> columns = new List<Column>();
        string dumpResult;
        public SchemaDumper(ProviderTypes provider, string connectionString, string defaultSchema, string path = null, string tablePrefix = null)
        {
            _provider = ProviderFactory.Create(provider, connectionString, defaultSchema);
            this.Dump(tablePrefix, path);
        }
        public string GetDump()
        {
            return this.dumpResult;
        }
        private void Dump(string tablePrefix, string path)
        {
            if (String.IsNullOrEmpty(tablePrefix))
                this.tables = this._provider.GetTables();
            else
                this.tables = this._provider.GetTables().Where(o => o.ToUpper().StartsWith(tablePrefix.ToUpper())).ToArray();

            foreach (var tab in this.tables)
            {
                foreignKeys.AddRange(this._provider.GetForeignKeyConstraints(tab));
            }

            var writer = new StringWriter();
            writer.WriteLine("using System.Data;");
            writer.WriteLine("using Migrator.Framework;\n");
            writer.WriteLine("\t[Migration(1)]");
            writer.WriteLine("\tpublic class SchemaDump : Migration");
            writer.WriteLine("\t{");
            writer.WriteLine("\tpublic override void Up()");
            writer.WriteLine("\t{");
            this.addTableStatement(writer);
            this.addForeignKeys(writer);
            writer.WriteLine("\t}");
            writer.WriteLine("\tpublic override void Down(){}");
            writer.WriteLine("}");
            this.dumpResult = writer.ToString();
            File.WriteAllText(path, dumpResult);
        }

        private string GetListString(string[] list)
        {
            if (list == null)
                return "new string[]{}";
            for (int i = 0; i < list.Length; i++)
            {
                list[i] = $"\"{list[i]}\"";
            }
            return $"new []{String.Format("{{{0}}}", String.Join(",", list))}";
        }

        private void addForeignKeys(StringWriter writer)
        {
            foreach (var fk in this.foreignKeys)
            {
                var fkCols = fk.ParentColumns;

                foreach (var col in fkCols)
                {
                    writer.WriteLine($"\t\tDatabase.AddForeignKey(\"{fk.Name}\", \"{fk.ParentTable}\", {this.GetListString(fk.ParentColumns)}, \"{fk.ChildTable}\", {this.GetListString(fk.ChildColumns)});");
                }
                //this._provider.AddForeignKey(name, fktable, fkcols, pktable, primaryCols);
            }
        }
        private void addTableStatement(StringWriter writer)
        {
            foreach (string table in this.tables)
            {
                string cols = this.getColsStatement(table);
                writer.WriteLine($"\t\tDatabase.AddTable(\"{table}\",{cols});");
                this.AddIndexes(table, writer);
            }
        }

        private void AddIndexes(string table, StringWriter writer)
        {
            Index[] inds = this._provider.GetIndexes(table);
            foreach (Index ind in inds)
            {
                if (ind.PrimaryKey == true)
                {
                    string nonclusteredString = (ind.Clustered == false ? "NonClustered" : "");

                    string[] keys = ind.KeyColumns;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        keys[i] = $"\"{keys[i]}\"";
                    }
                    string keysString = string.Join(",", keys);
                    writer.WriteLine($"\t\tDatabase.AddPrimaryKey{nonclusteredString}(\"{ind.Name}\",\"{table}\",new string[]{String.Format("{{{0}}}", keysString)});");
                    continue;
                }
                writer.WriteLine($"\t\tDatabase.AddIndex(\"{table}\",new Index() {String.Format("{{Name = \"{0}\",Clustered = {1}, KeyColumns={2}, IncludeColumns={3}, Unique={4}, UniqueConstraint={5}}}", ind.Name, ind.Clustered.ToString().ToLower(), this.GetListString(ind.KeyColumns), this.GetListString(ind.IncludeColumns), ind.Unique.ToString().ToLower(), ind.UniqueConstraint.ToString().ToLower())});");
            }
        }

        private string getColsStatement(string table)
        {
            Column[] cols = this._provider.GetColumns(table);
            List<string> colList = new List<string>();
            foreach (var col in cols)
            {
                colList.Add(this.getColStatement(col, table));
            }
            string result = String.Format("{0}", string.Join(",", colList));
            return result;
        }
        private string getColStatement(Column col, string table)
        {
            string precision = "";
            if (col.Precision != null)
                precision = $"({col.Precision})";
            string propertyString = this.GetColumnPropertyString(col.ColumnProperty);

            if (col.Size != 0 && col.DefaultValue == null && col.ColumnProperty == ColumnProperty.None)
            {
                return String.Format("new Column(\"{0}\",DbType.{1},{2})", col.Name, col.Type, col.Size);
            }
            if (col.DefaultValue != null && col.ColumnProperty == ColumnProperty.None && col.Size == 0)
            {
                return String.Format("new Column(\"{0}\",DbType.{1},\"{2}\")", col.Name, col.Type, col.DefaultValue);
            }
            if (col.ColumnProperty != ColumnProperty.None && col.Size == 0 && col.DefaultValue == null)
            {
                return String.Format("new Column(\"{0}\",DbType.{1},{2})", col.Name, col.Type, propertyString);
            }
            if (col.ColumnProperty != ColumnProperty.None && col.Size != 0 && col.DefaultValue == null)
            {
                return String.Format("new Column(\"{0}\",DbType.{1},{2},{3})", col.Name, col.Type, col.Size, propertyString);
            }
            if (col.ColumnProperty != ColumnProperty.None && col.Size != 0 && col.DefaultValue != null)
            {
                return String.Format("new Column(\"{0}\",DbType.{1},{2},{3},\"{4}\")", col.Name, col.Type, col.Size, propertyString, col.DefaultValue);
            }
            if (col.ColumnProperty != ColumnProperty.None && col.Size == 0 && col.DefaultValue != null)
            {
                return String.Format("new Column(\"{0}\",DbType.{1},{2},\"{3}\")", col.Name, col.Type, propertyString, col.DefaultValue);
            }
            return String.Format("new Column(\"{0}\",{1})", col.Name, col.Type);

        }
        private string GetColumnPropertyString(ColumnProperty prp)
        {
            string retVal = "";
            if ((prp & ColumnProperty.ForeignKey) == ColumnProperty.ForeignKey) retVal += "ColumnProperty.ForeignKey | ";
            if ((prp & ColumnProperty.Identity) == ColumnProperty.Identity) retVal += "ColumnProperty.Identity | ";
            if ((prp & ColumnProperty.Indexed) == ColumnProperty.Indexed) retVal += "ColumnProperty.Indexed | ";
            if ((prp & ColumnProperty.NotNull) == ColumnProperty.NotNull) retVal += "ColumnProperty.NotNull | ";
            if ((prp & ColumnProperty.Null) == ColumnProperty.Null) retVal += "ColumnProperty.Null | ";
            //if ((prp & ColumnProperty.PrimaryKey) == ColumnProperty.PrimaryKey) retVal += "ColumnProperty.PrimaryKey | ";
            //if ((prp & ColumnProperty.PrimaryKeyWithIdentity) == ColumnProperty.PrimaryKeyWithIdentity) retVal += "ColumnProperty.PrimaryKeyWithIdentity | ";
            //if ((prp & ColumnProperty.PrimaryKeyNonClustered) == ColumnProperty.PrimaryKeyNonClustered) retVal += "ColumnProperty.PrimaryKeyNonClustered | ";
            if ((prp & ColumnProperty.Unique) == ColumnProperty.Unique) retVal += "ColumnProperty.Unique | ";
            if ((prp & ColumnProperty.Unsigned) == ColumnProperty.Unsigned) retVal += "ColumnProperty.Unsigned | ";

            if (retVal != "") retVal = retVal.Substring(0, retVal.Length - 3);

            if (retVal == "") retVal = "ColumnProperty.None";

            return retVal;
        }
    }
}
