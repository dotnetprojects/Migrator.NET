using System;
using System.Collections.Generic;
using Migrator.Framework;

namespace Migrator.Providers
{
    /// <summary>
    /// This is basically a just a helper base class
    /// per-database implementors may want to override ColumnSql
    /// </summary>
    public class ColumnPropertiesMapper
    {
        /// <summary>
        /// the type of the column
        /// </summary>
        protected string columnSql;

        /// <summary>
        /// Sql if this column has a default value
        /// </summary>
        protected object defaultVal;

        protected Dialect dialect;

        /// <summary>
        /// Sql if This column is Indexed
        /// </summary>
        protected bool indexed;

        /// <summary>The name of the column</summary>
        protected string name;

        /// <summary>The SQL type</summary>
        public string type { get; private set; }

        public ColumnPropertiesMapper(Dialect dialect, string type)
        {
            this.dialect = dialect;
            this.type = type;
        }

        /// <summary>
        /// The sql for this column, override in database-specific implementation classes
        /// </summary>
        public virtual string ColumnSql
        {
            get { return columnSql; }
        }

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public object Default
        {
            get { return defaultVal; }
            set { defaultVal = value; }
        }

        public string QuotedName
        {
            get { return dialect.Quote(Name); }
        }

        public string IndexSql
        {
            get
            {
                if (dialect.SupportsIndex && indexed)
                    return String.Format("INDEX({0})", dialect.Quote(name));
                return null;
            }
        }

        public virtual void MapColumnProperties(Column column)
        {
            Name = column.Name;

            indexed = PropertySelected(column.ColumnProperty, ColumnProperty.Indexed);

            var vals = new List<string>();

            AddName(vals);

            AddType(vals);

            AddCaseSensitive(column, vals);

            AddIdentity(column, vals);

            AddUnsigned(column, vals);

            AddNotNull(column, vals);

            AddNull(column, vals);

            AddPrimaryKey(column, vals);

            AddPrimaryKeyNonClustered(column, vals);

            AddIdentityAgain(column, vals);

            AddUnique(column, vals);

            AddForeignKey(column, vals);

            AddDefaultValue(column, vals);

            columnSql = String.Join(" ", vals.ToArray());
        }

        public virtual void MapColumnPropertiesWithoutDefault(Column column)
        {
            Name = column.Name;

            indexed = PropertySelected(column.ColumnProperty, ColumnProperty.Indexed);

            var vals = new List<string>();

            AddName(vals);

            AddType(vals);

            AddCaseSensitive(column, vals);

            AddIdentity(column, vals);

            AddUnsigned(column, vals);

            AddNotNull(column, vals);

            AddNull(column, vals);

            AddPrimaryKey(column, vals);

            AddIdentityAgain(column, vals);

            AddPrimaryKeyNonClustered(column, vals);

            AddUnique(column, vals);

            AddForeignKey(column, vals);

            columnSql = string.Join(" ", vals.ToArray());
        }

        protected virtual void AddCaseSensitive(Column column, List<string> vals)
        {
            AddValueIfSelected(column, ColumnProperty.CaseSensitive, vals);
        }

        protected virtual void AddDefaultValue(Column column, List<string> vals)
        {
            if (column.DefaultValue != null)
            {
                vals.Add(dialect.Default(column.DefaultValue));
            }
        }

        protected virtual void AddForeignKey(Column column, List<string> vals)
        {
            // TODO Does that really make sense?
            AddValueIfSelected(column, ColumnProperty.ForeignKey, vals);
        }

        protected virtual void AddUnique(Column column, List<string> vals)
        {
            AddValueIfSelected(column, ColumnProperty.Unique, vals);
        }

        protected virtual void AddIdentityAgain(Column column, List<string> vals)
        {
            if (dialect.IdentityNeedsType)
                AddValueIfSelected(column, ColumnProperty.Identity, vals);
        }
        protected virtual void AddPrimaryKeyNonClustered(Column column, List<string> vals)
        {
            if (dialect.SupportsNonClustered)
                AddValueIfSelected(column, ColumnProperty.PrimaryKeyNonClustered, vals);
        }
        protected virtual void AddPrimaryKey(Column column, List<string> vals)
        {
            AddValueIfSelected(column, ColumnProperty.PrimaryKey, vals);
        }

        protected virtual void AddNull(Column column, List<string> vals)
        {
            if (!PropertySelected(column.ColumnProperty, ColumnProperty.PrimaryKey))
            {
                if (dialect.NeedsNullForNullableWhenAlteringTable)
                {
                    AddValueIfSelected(column, ColumnProperty.Null, vals);
                }
            }
        }

        protected virtual void AddNotNull(Column column, List<string> vals)
        {
            if (!PropertySelected(column.ColumnProperty, ColumnProperty.Null) && (!PropertySelected(column.ColumnProperty, ColumnProperty.PrimaryKey) || dialect.NeedsNotNullForIdentity))
            {
                AddValueIfSelected(column, ColumnProperty.NotNull, vals);
            }
        }

        protected virtual void AddUnsigned(Column column, List<string> vals)
        {
            if (dialect.IsUnsignedCompatible(column.Type))
            {
                AddValueIfSelected(column, ColumnProperty.Unsigned, vals);
            }
        }

        protected virtual void AddIdentity(Column column, List<string> vals)
        {
            if (!dialect.IdentityNeedsType)
            {
                AddValueIfSelected(column, ColumnProperty.Identity, vals);
            }
        }

        protected virtual void AddType(List<string> vals)
        {
            vals.Add(type);
        }

        protected virtual void AddName(List<string> vals)
        {
            vals.Add(dialect.ColumnNameNeedsQuote || dialect.IsReservedWord(Name) ? QuotedName : Name);
        }

        protected virtual void AddValueIfSelected(Column column, ColumnProperty property, ICollection<string> vals)
        {
            if (PropertySelected(column.ColumnProperty, property))
            {
                vals.Add(dialect.SqlForProperty(property, column));
            }
        }

        public static bool PropertySelected(ColumnProperty source, ColumnProperty comparison)
        {
            return (source & comparison) == comparison;
        }
    }
}
