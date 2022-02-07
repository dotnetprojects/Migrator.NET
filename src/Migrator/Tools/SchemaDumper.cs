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
using System.Data;
using System.IO;
using System.Linq;
using Migrator.Framework;
using Migrator.Providers;
using ForeignKeyConstraint = Migrator.Framework.ForeignKeyConstraint;
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
		/// <summary>
		/// Creates a dumpfile of a MsSQL-Database and saves it to a specified path. It is possible to do a dump of both schema and/or data. 
		/// </summary>
		/// <param name="providerType">e. g. ProviderTypes.SqlServer</param>
		/// <param name="connectionString"></param>
		/// <param name="defaultSchema">needed for TransformationProvider</param>
		/// <param name="path">directory and name of dumpfile. e.g. c:\test\dump.txt</param>
		/// <param name="tablePrefix"></param>
		/// <param name="isSchema">true when schema of db shall be dumped, default true</param>
		/// <param name="isData">true when data shalle be dumped, default false</param>
		public SchemaDumper(ProviderTypes providerType, string connectionString, string defaultSchema, string path = null, string tablePrefix = null, bool isSchema = true, bool isData = false)
		{
			_provider = ProviderFactory.Create(providerType, connectionString, defaultSchema);
			this.Dump(tablePrefix, path, isSchema, isData);
		}
		public string GetDump()
		{
			return this.dumpResult;
		}

		private void Dump(string tablePrefix, string path, bool isSchema = true, bool isData = false)
		{
			if (String.IsNullOrEmpty(tablePrefix))
				this.tables = this._provider.GetTables().Where(o => o != "SchemaInfo").ToArray();
			else
				this.tables = this._provider.GetTables().Where(o => o != "SchemaInfo" && o.ToUpper().StartsWith(tablePrefix.ToUpper())).ToArray();

			if (isSchema)
			{
				foreach (var tab in this.tables)
				{
					foreignKeys.AddRange(this._provider.GetForeignKeyConstraints(tab));
				}
			}


			var writer = new StringWriter();
			writer.WriteLine("using System.Data;");
			writer.WriteLine("using Migrator.Framework;\n");

			writer.WriteLine($"namespace MCC.{tablePrefix}.DL.DBMigration");
			writer.WriteLine("{");

			writer.WriteLine("\t[Migration(1)]");
			writer.WriteLine("\tpublic class Migration_01 : Migration");
			writer.WriteLine("\t{");
			writer.WriteLine("\tpublic override void Up()");
			writer.WriteLine("\t{");
			if (isSchema)
			{
				this.addTableStatement(writer);
				this.addForeignKeys(writer);
				if (!String.IsNullOrEmpty(tablePrefix))
					writer.WriteLine("\n\t\t" + $@"for (int i = XXXX; i < XXXXX; i++,this.Database.MigrationApplied(i,{String.Format("\"{0}\"", tablePrefix.ToUpper())}));");

			}
			if (isData)
				this.GetInserts(this.tables, writer);

			writer.WriteLine("\t}");
			writer.WriteLine("\tpublic override void Down(){}");
			writer.WriteLine("}\n}");
			this.dumpResult = writer.ToString();
			if (!String.IsNullOrEmpty(path))
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
				string[] fkCols = fk.Columns;
				foreach (var col in fkCols)
					writer.WriteLine($"\t\tDatabase.AddForeignKey(\"{fk.Name}\", \"{fk.Table}\", {this.GetListString(fk.Columns)}, \"{fk.PkTable}\", {this.GetListString(fk.PkColumns)});");
			}
		}
		private void addTableStatement(StringWriter writer)
		{
			foreach (string table in this.tables)
			{
				string cols = this.GetColsStatement(table);
				writer.WriteLine($"\t\tDatabase.AddTable(\"{table}\",{cols.Replace("\"DateTime.UtcNow\"", "DateTime.UtcNow")});");
				this.AddIndexes(table, writer);
			}
		}

		private void AddIndexes(string table, StringWriter writer)
		{
			Index[] inds = this._provider.GetIndexes(table);
			foreach (Index ind in inds)
			{
				if (ind.Unique == true)
					if (this.UniquAlreadyCreated.Contains(table))
						continue;
				if (ind.PrimaryKey == true)
				{
					if (this.PKAlreadyCreated.Contains(table))
						continue;
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
				writer.WriteLine($"\t\tDatabase.AddIndex(\"{table}\",new Index() { String.Format("{{Name = \"{0}\",Clustered = {1}, KeyColumns={2}, IncludeColumns={3}, Unique={4}, UniqueConstraint={5}}}", GetIndexNameOracleConform(ind.Name), ind.Clustered.ToString().ToLower(), this.GetListString(ind.KeyColumns), this.GetListString(ind.IncludeColumns), ind.Unique.ToString().ToLower(), ind.UniqueConstraint.ToString().ToLower()) });");
			}
		}
		private string GetIndexNameOracleConform(string s)
		{
			var result = s.Substring(0, s.Length > 30 ? 30 : s.Length);
			return result;
		}
		private string GetColsStatement(string table)
		{
			Column[] cols = this._provider.GetColumns(table);
			List<string> colList = new List<string>();
			foreach (var col in cols)
			{
				var s = this.GetColStatement(col, table);
				colList.Add(s.Replace(",)", ")"));
			}
			string result = String.Format("{0}", string.Join(",", colList));
			return result;
		}
		private string GetColStatement(Column col, string table)
		{
			string size = col.Size.ToString();
			object defaultValue = col.DefaultValue;
			if ((col.Type == System.Data.DbType.Date || col.Type == System.Data.DbType.DateTime || col.Type == System.Data.DbType.DateTime2 || col.Type == System.Data.DbType.DateTimeOffset))
			{
				col.Type = System.Data.DbType.DateTime;
				if (col.DefaultValue != null)
					defaultValue = "DateTime.UtcNow";
			}


			if (col.Type == System.Data.DbType.AnsiString && col.Size == -1)
			{
				size = "int.MaxValue";
			}
			if (col.Type == System.Data.DbType.String && col.Size == -1)
			{
				size = "int.MaxValue";
			}
			if (col.Type == System.Data.DbType.VarNumeric && col.Size == -1)
			{
				size = "int.MaxValue";
			}
			if (col.Type == System.Data.DbType.Binary && col.Size == -1)
			{
				size = "int.MaxValue";
			}

			defaultValue = $"\"{defaultValue}\"";

			if (col.Type == DbType.Guid)
			{
				defaultValue = defaultValue.ToString().Trim(new[] { '\"' });
				defaultValue = $"this.Database.Encode(new Guid(\"{defaultValue}\"))";
				defaultValue = defaultValue.ToString().Trim(new[] { '\"' });
			}
			string precision = "";
			if (col.Precision != null)
				precision = $"({col.Precision})";
			string propertyString = this.GetColumnPropertyString(col.ColumnProperty, table);
			propertyString += this.GetColumnPropertyUnique(col.Name, table);

			if ((col.Name == "CreatedTimeStamp" || col.Name == "ModifiedTimeStamp") && col.DefaultValue != null)
			{
				defaultValue = "DateTime.UtcNow";
			}
			if (col.Size != 0 && col.DefaultValue == null && col.ColumnProperty == ColumnProperty.None)
			{
				return String.Format("new Column(\"{0}\",DbType.{1},{2})", col.Name, col.Type, size);
			}

			if (col.DefaultValue != null && col.ColumnProperty == ColumnProperty.None && col.Size == 0)
			{
				return String.Format("new Column(\"{0}\",DbType.{1},{2})", col.Name, col.Type, String.Format("{0}", defaultValue));
			}
			if (col.ColumnProperty != ColumnProperty.None && col.Size == 0 && col.DefaultValue == null)
			{
				return String.Format("new Column(\"{0}\",DbType.{1},{2})", col.Name, col.Type, propertyString);
			}
			if (col.ColumnProperty != ColumnProperty.None && col.Size != 0 && col.DefaultValue == null)
			{
				return String.Format("new Column(\"{0}\",DbType.{1},{2},{3})", col.Name, col.Type, size, propertyString);
			}
			if (col.ColumnProperty != ColumnProperty.None && col.Size != 0 && col.DefaultValue != null)
			{
				return String.Format("new Column(\"{0}\",DbType.{1},{2},{3},{4})", col.Name, col.Type, size, propertyString, String.Format("{0}", defaultValue));
			}
			if (col.ColumnProperty != ColumnProperty.None && col.Size == 0 && col.DefaultValue != null)
			{
				return String.Format("new Column(\"{0}\",DbType.{1},{2},{3})", col.Name, col.Type, propertyString, String.Format("{0}", defaultValue));
			}
			return String.Format("new Column(\"{0}\",{1})", col.Name, col.Type);

		}
		private string GetColumnPropertyUnique(String col, String tableName)
		{
			if (UniquAlreadyCreated.Contains(tableName))
				return "";
			Index[] inds = this._provider.GetIndexes(tableName);

			for (int i = 0; i < inds.Length; i++)
			{
				Index currIndex = inds[i];
				if (currIndex.KeyColumns != null && currIndex.KeyColumns.Count() == 1 && currIndex.KeyColumns.Contains(col))
				{
					if (currIndex.PrimaryKey)
						return "";
					if (currIndex.Unique)
					{
						this.UniquAlreadyCreated.Add(tableName);
						return "| ColumnProperty.Unique ";
					}
				}
			}
			return "";
		}
		private List<String> PKAlreadyCreated = new List<string>();
		private List<String> UniquAlreadyCreated = new List<string>();

		private string GetColumnPropertyString(ColumnProperty prp, String tableName)
		{
			string retVal = "";

			bool isNonclusteredPk = false;
			Index[] inds = this._provider.GetIndexes(tableName);
			for (int i = 0; i < inds.Length; i++)
			{
				Index currIndex = inds[i];
				if (!currIndex.Clustered && currIndex.PrimaryKey)
				{
					isNonclusteredPk = true;
				}
			}

			if ((prp & ColumnProperty.ForeignKey) == ColumnProperty.ForeignKey) retVal += "ColumnProperty.ForeignKey | ";
			if ((prp & ColumnProperty.Indexed) == ColumnProperty.Indexed) retVal += "ColumnProperty.Indexed | ";
			if ((prp & ColumnProperty.NotNull) == ColumnProperty.NotNull) retVal += "ColumnProperty.NotNull | ";
			if ((prp & ColumnProperty.Null) == ColumnProperty.Null) retVal += "ColumnProperty.Null | ";

			if (!isNonclusteredPk && (prp & ColumnProperty.PrimaryKey) == ColumnProperty.PrimaryKey && (prp & ColumnProperty.Identity) != ColumnProperty.Identity)
			{
				retVal += "ColumnProperty.PrimaryKey | ";
				this.PKAlreadyCreated.Add(tableName);
			}
			if (isNonclusteredPk && (prp & ColumnProperty.PrimaryKey) == ColumnProperty.PrimaryKey)
			{
				retVal += "ColumnProperty.PrimaryKeyNonClustered | ";
				this.PKAlreadyCreated.Add(tableName);
			}
			if ((prp & ColumnProperty.Identity) == ColumnProperty.Identity)
			{
				if ((prp & ColumnProperty.PrimaryKeyWithIdentity) == ColumnProperty.PrimaryKeyWithIdentity)
				{
					this.PKAlreadyCreated.Add(tableName);
					retVal += "ColumnProperty.PrimaryKeyWithIdentity | ";
				}
				else
					retVal += "ColumnProperty.Identity | ";
			}
			if ((prp & ColumnProperty.Unsigned) == ColumnProperty.Unsigned) retVal += "ColumnProperty.Unsigned | ";

			if (retVal != "") retVal = retVal.Substring(0, retVal.Length - 3);

			if (retVal == "") retVal = "ColumnProperty.None";

			return retVal;
		}


		public String GetDatas(string table)
		{
			string result = "";
			var cols = this._provider.GetColumns(table);
			for (int i = 0; i < cols.Length; i++)
			{
				cols[i].Name = String.Format("\"{0}\"", cols[i].Name);
			}
			string columString = string.Join(",", cols.Select(o => o.Name));
			List<String> valueList = new List<string>();
			List<String> columList = new List<string>();

			using (var cmd = this._provider.CreateCommand())
			{
				using (IDataReader reader = this._provider.ExecuteQuery(cmd, String.Format("select * from {0}", table.ToUpper())))
				{

					Type[] typesNumber = { typeof(Decimal), typeof(Double), typeof(int), typeof(Int16), typeof(Int32), typeof(Int64), typeof(UInt16), typeof(UInt32), typeof(UInt64), typeof(Byte), typeof(byte), typeof(double), typeof(float) };
					while (reader.Read())
					{
						object[] vals = new object[cols.Length];
						valueList = new List<string>();
						columList = new List<string>();

						for (int i = 0; i < cols.Length; i++)
						{
							try
							{
								object curr = reader.GetValue(i);
								if (curr is System.DBNull)
								{
									continue;
								}
								if (curr == null)
								{
									continue;
								}
								if (reader.GetFieldType(i) == typeof(Boolean))
								{
									valueList.Add(String.Format("{0}", curr.ToString().ToLower()));
								}
								else
								if (reader.GetFieldType(i) == typeof(DateTime))
								{
									valueList.Add(String.Format("DateTime.Parse(\"{0}\").ToUniversalTime()", curr));
								}
								else
								if (reader.GetFieldType(i) == typeof(Guid))
								{
									valueList.Add(String.Format("this.Database.Encode(new Guid(\"{0}\"))", curr).Trim(new char[] { '\"' }));
								}
								else
								{
									if (typesNumber.Contains(reader.GetFieldType(i)))
									{
										if (curr.GetType() == typeof(Array))
										{
											continue;
										}
										if (curr == null)
											continue;

										valueList.Add(string.Format(System.Globalization.CultureInfo.GetCultureInfo("en-EN"), "{0}", curr));
									}
									else
									{
										string val = String.Format("\"{0}\"", curr);
										if (val == "\"\"")
											continue;
										valueList.Add(String.Format("\"{0}\"", curr));
									}
								}
								columList.Add(cols[i].Name);

							}
							catch (Exception exc)
							{
								vals[i] = "null";
							}
						}
						for (int i = 0; i < columList.Count; i++)
							columList[i] = $"{columList[i]}";

						result += $"\t\tDatabase.Insert(\"{table}\",new[]{String.Format("{{{0}}}", string.Join(",", columList))},new object[]{String.Format("{{{0}}}", string.Join(",", valueList.ToArray()))});\n";

					}
				}
			}
			return result;

		}
		public void GetInserts(string[] tables, StringWriter writer)
		{
			for (int i = 0; i < tables.Length; i++)
			{
				string data = this.GetDatas(tables[i]);
				if (data != null)
					writer.WriteLine(data);
				if (data == null)
				{
				}
			}
		}
	}
}
