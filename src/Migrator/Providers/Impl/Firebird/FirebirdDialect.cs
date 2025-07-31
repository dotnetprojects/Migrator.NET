using System;
using System.Data;
using Migrator.Framework;

namespace Migrator.Providers.Impl.Firebird;

public class FirebirdDialect : Dialect
{
    public FirebirdDialect()
    {
        RegisterColumnType(DbType.AnsiStringFixedLength, 8000, "CHAR($l)");
        RegisterColumnType(DbType.AnsiString, 8000, "CHAR($l)");
        RegisterColumnType(DbType.Binary, "BLOB");
        RegisterColumnType(DbType.Binary, 8000, "CHAR");
        RegisterColumnType(DbType.Boolean, "SMALLINT");
        RegisterColumnType(DbType.Byte, "TINYINT");
        RegisterColumnType(DbType.Currency, "MONEY");
        RegisterColumnType(DbType.Date, "TIMESTAMP");
        RegisterColumnType(DbType.DateTime, "TIMESTAMP");
        RegisterColumnType(DbType.DateTimeOffset, "TIMESTAMP");
        RegisterColumnType(DbType.Decimal, "DECIMAL");
        RegisterColumnType(DbType.Double, "DOUBLE PRECISION"); //synonym for FLOAT(53)
        RegisterColumnType(DbType.Guid, "CHAR(38)");
        RegisterColumnType(DbType.Int16, "SMALLINT");
        RegisterColumnType(DbType.Int32, "INT");
        RegisterColumnType(DbType.Int64, "BIGINT");
        RegisterColumnType(DbType.Single, "REAL"); //synonym for FLOAT(24) 
        RegisterColumnType(DbType.StringFixedLength, "NCHAR(255)");
        RegisterColumnType(DbType.String, "VARCHAR(255) CHARACTER SET UNICODE_FSS");
        RegisterColumnType(DbType.String, 4000, "VARCHAR($l) CHARACTER SET UNICODE_FSS");
        RegisterColumnType(DbType.String, int.MaxValue, "BLOB SUB_TYPE TEXT");
        RegisterColumnType(DbType.Time, "INTEGER");

        this.RegisterProperty(ColumnProperty.Unsigned, "UNSIGNED");

        this.RegisterUnsignedCompatible(DbType.Int16);
        this.RegisterUnsignedCompatible(DbType.Int32);
        this.RegisterUnsignedCompatible(DbType.Int64);
        this.RegisterUnsignedCompatible(DbType.Decimal);
        this.RegisterUnsignedCompatible(DbType.Double);
        this.RegisterUnsignedCompatible(DbType.Single);

        this.AddReservedWords("KEY", "TIMESTAMP", "VALUE");
    }


    public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
    {
        return new FirebirdTransformationProvider(dialect, connectionString, scope, providerName);
    }

    public override ColumnPropertiesMapper GetColumnMapper(Column column)
    {
        var type = column.Size > 0 ? GetTypeName(column.Type, column.Size) : GetTypeName(column.Type);
        if (column.Precision.HasValue || column.Scale.HasValue)
            type = GetTypeNameParametrized(column.Type, column.Size, column.Precision ?? 0, column.Scale ?? 0);
        if (!IdentityNeedsType && column.IsIdentity)
            type = String.Empty;

        return new FirebirdColumnPropertiesMapper(this, type);
    }

    public override ITransformationProvider GetTransformationProvider(Dialect dialect, IDbConnection connection,
      string defaultSchema,
      string scope, string providerName)
    {
        return new FirebirdTransformationProvider(dialect, connection, scope, providerName);
    }
}
