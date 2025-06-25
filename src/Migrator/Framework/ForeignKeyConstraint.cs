using Migrator.Framework;

namespace DotNetProjects.Migrator.Framework;

    public class ForeignKeyConstraint : IDbField
    {
        public ForeignKeyConstraint()
        { }

        public ForeignKeyConstraint(string name, string table, string[] columns, string pkTable, string[] pkColumns)
        {
            Name = name;
            Table = table;
            Columns = columns;
            PkTable = pkTable;
            PkColumns = pkColumns;
        }

        public string Name { get; set; }
        public string Table { get; set; }
        public string[] Columns { get; set; }
        public string PkTable { get; set; }
        public string[] PkColumns { get; set; }        
    }
