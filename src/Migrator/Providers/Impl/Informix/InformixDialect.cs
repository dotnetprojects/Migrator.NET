using System.Data;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.Informix;

public class InformixDialect : Dialect
{
    public InformixDialect()
    {
        RegisterColumnType(DbType.AnsiStringFixedLength, "CHAR(255)");
        RegisterColumnType(DbType.AnsiString, "VARCHAR(255)");
        RegisterColumnType(DbType.StringFixedLength, "CHAR(255)");
        RegisterColumnType(DbType.String, "VARCHAR(255)");
        RegisterColumnType(DbType.Binary, "BYTE");
        RegisterColumnType(DbType.Boolean, "BOOLEAN");
        RegisterColumnType(DbType.Byte, "SMALLINT");
        RegisterColumnType(DbType.Currency, "DECIMAL(18,4)");
        RegisterColumnType(DbType.Date, "DATE");
        RegisterColumnType(DbType.DateTime, "DATETIME YEAR TO FRACTION(5)");
        RegisterColumnType(DbType.DateTime2, "DATETIME YEAR TO FRACTION(5)");
        RegisterColumnType(DbType.DateTimeOffset, "DATETIME YEAR TO FRACTION(5)");
        RegisterColumnType(DbType.Decimal, "DECIMAL(18,5)");
        RegisterColumnType(DbType.Double, "DOUBLE PRECISION");
        RegisterColumnType(DbType.Guid, "CHAR(36)");
        RegisterColumnType(DbType.Int16, "SMALLINT");
        RegisterColumnType(DbType.Int32, "INTEGER");
        RegisterColumnType(DbType.Int64, "BIGINT");
        RegisterColumnType(DbType.Single, "SMALLFLOAT");
        RegisterColumnType(DbType.Time, "INTERVAL HOUR TO SECOND");
        RegisterColumnType(DbType.String, 255, "VARCHAR($l)");
        RegisterColumnType(DbType.String, int.MaxValue, "LVARCHAR");
        RegisterColumnType(DbType.AnsiString, 255, "VARCHAR($l)");
        RegisterColumnType(DbType.AnsiString, int.MaxValue, "LVARCHAR");
        RegisterColumnType(DbType.StringFixedLength, 255, "CHAR($l)");
        RegisterColumnType(DbType.AnsiStringFixedLength, 255, "CHAR($l)");
        RegisterProperty(ColumnProperty.Identity, "");
    }

    public override ColumnPropertiesMapper GetColumnMapper(Column column)
    {
        var type = column.Size > 0 ? GetTypeName(column.Type, column.Size) : GetTypeName(column.Type);
        if (column.IsIdentity) type = column.Type == DbType.Int64 ? "BIGSERIAL" : "SERIAL";
        return new NativeColumnMapper(this, type);
    }

    private sealed class NativeColumnMapper(Dialect dialect, string type) : ColumnPropertiesMapper(dialect, type)
    {
        public override void MapColumnProperties(Column column)
        {
            Name = column.Name;
            var parts = new System.Collections.Generic.List<string>();
            AddName(parts);
            AddType(parts);
            AddIdentityAgain(column, parts);
            AddDefaultValue(column, parts);
            AddNotNull(column, parts);
            AddPrimaryKey(column, parts);
            AddUnique(column, parts);
            _ColumnSql = string.Join(" ", parts);
        }
    }

    public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString,
        string defaultSchema, string scope, string providerName)
    {
        return new InformixTransformationProvider(dialect, connectionString, scope, providerName);
    }

    public override ITransformationProvider GetTransformationProvider(Dialect dialect, IDbConnection connection,
        string defaultSchema,
        string scope, string providerName)
    {
        return new InformixTransformationProvider(dialect, connection, scope, providerName);
    }
}
