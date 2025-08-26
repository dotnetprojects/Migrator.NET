namespace DotNetProjects.Migrator.Providers.Models.Indexes;

public class IndexItem
{
    /// <summary>
    /// Indicates whether the index is clustered (false for NONCLUSTERED). 
    /// </summary>
    public bool Clustered { get; set; }

    /// <summary>
    /// Gets or sets the column order.
    /// </summary>
    public int ColumnOrder { get; set; }




    /// <summary>
    /// Gets or sets the index name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Indicates whether the index is unique.
    /// </summary>
    public bool Unique { get; set; }



    /// <summary>
    /// Indicates whether it is a primary key constraint.
    /// </summary>
    public bool PrimaryKey { get; internal set; }

    /// <summary>
    /// Indicates whether it is a unique constraint. 
    /// </summary>
    public bool UniqueConstraint { get; internal set; }

    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// Gets or sets the included columns. Not supported in SQLite and Oracle.
    /// </summary>
    public bool IsIncludedColumn { get; set; }

    /// <summary>
    /// Indicates whether the index is a filtered index.
    /// </summary>
    public bool IsFilteredIndex { get; set; }

    /// <summary>
    /// Gets or sets items that represent filter expressions in filtered indexes. Currently string, integer and boolean values are supported.
    /// Attention: In SQL Server the column used in the filter must be NOT NULL.
    /// </summary>
    public string FilterString { get; set; }

    /// <summary>
    /// Gets or sets the schema name.
    /// </summary>
    public string SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    public string TableName { get; set; }

}