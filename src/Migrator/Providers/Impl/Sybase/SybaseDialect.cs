using System.Data;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.Sybase;

public class SybaseDialect : Dialect
{
    // This flag controls MySQL-style inline INDEX syntax, not CREATE INDEX support.
    public override bool SupportsIndex => false;

    public SybaseDialect()
    {
        RegisterColumnType(DbType.Int16, "SMALLINT");
        RegisterColumnType(DbType.Int32, "INT");
        RegisterColumnType(DbType.Int64, "BIGINT");
        RegisterColumnType(DbType.Byte, "TINYINT");
        RegisterColumnType(DbType.Boolean, "BIT");
        RegisterColumnType(DbType.Decimal, "DECIMAL(18,5)");
        RegisterColumnTypeWithPrecision(DbType.Decimal, "DECIMAL({precision},{scale})");
        RegisterColumnType(DbType.Currency, "MONEY");
        RegisterColumnType(DbType.Double, "FLOAT");
        RegisterColumnType(DbType.Single, "REAL");
        RegisterColumnType(DbType.Date, "DATE");
        RegisterColumnType(DbType.Time, "TIME");
        RegisterColumnType(DbType.DateTime, "DATETIME");
        RegisterColumnType(DbType.DateTime2, "BIGDATETIME");
        RegisterColumnType(DbType.DateTimeOffset, "BIGDATETIME");
        RegisterColumnType(DbType.Guid, "CHAR(36)");
        RegisterColumnType(DbType.Binary, "IMAGE");
        RegisterColumnType(DbType.String, "VARCHAR(255)");
        RegisterColumnType(DbType.String, 16384, "VARCHAR($l)");
        RegisterColumnType(DbType.String, int.MaxValue, "TEXT");
        RegisterColumnType(DbType.AnsiString, "VARCHAR(255)");
        RegisterColumnType(DbType.AnsiString, 16384, "VARCHAR($l)");
        RegisterColumnType(DbType.AnsiString, int.MaxValue, "TEXT");
        RegisterColumnType(DbType.StringFixedLength, "CHAR(255)");
        RegisterColumnType(DbType.StringFixedLength, 255, "CHAR($l)");
        RegisterColumnType(DbType.AnsiStringFixedLength, "CHAR(255)");
        RegisterColumnType(DbType.AnsiStringFixedLength, 255, "CHAR($l)");
        RegisterProperty(ColumnProperty.Identity, "IDENTITY");
    }

    public override string Default(object value) => value is bool boolean ? (boolean ? "DEFAULT 1" : "DEFAULT 0") : base.Default(value);

    public override string QuoteTemplate => "[{0}]";
    public override bool NeedsNullForNullableWhenAlteringTable => true;

    public override ColumnPropertiesMapper GetColumnMapper(Column column)
    {
        var type = column.Size > 0 ? GetTypeName(column.Type, column.Size) : GetTypeName(column.Type);
        if (column.Precision.HasValue || column.Scale.HasValue)
            type = GetTypeNameParametrized(column.Type, column.Size, column.Precision ?? 18, column.Scale ?? 0);
        return new NativeColumnMapper(this, type);
    }

    private sealed class NativeColumnMapper(Dialect dialect, string type) : ColumnPropertiesMapper(dialect, type)
    {
        public override void MapColumnProperties(Column column)
        {
            Name = column.Name;
            _Indexed = PropertySelected(column.ColumnProperty, ColumnProperty.Indexed);
            var parts = new System.Collections.Generic.List<string>();
            AddName(parts);
            AddType(parts);
            AddDefaultValue(column, parts);
            if (column.IsIdentity) AddIdentityAgain(column, parts);
            else
            {
                AddNotNull(column, parts);
                AddNull(column, parts);
            }
            AddPrimaryKey(column, parts);
            AddUnique(column, parts);
            _ColumnSql = string.Join(" ", parts);
        }
    }

    public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
    {
        return new SybaseTransformationProvider(dialect, connectionString, scope, providerName);
    }

    public override ITransformationProvider GetTransformationProvider(Dialect dialect, IDbConnection connection,
   string defaultSchema,
   string scope, string providerName)
    {
        return new SybaseTransformationProvider(dialect, connection, scope, providerName);
    }
}
