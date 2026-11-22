# Interpolated SQL queries

Import `DotNetProjects.Migrator.Framework`. Pass interpolation directly to
`Query<T>` as a `FormattableString`, preserving argument types:

```csharp
public sealed class ScreenDto
{
    public int Id { get; set; }
    public bool Archive { get; set; }
    public string GenericDefinition { get; set; }
}

var screens = Database.Query<ScreenDto>($"""
    SELECT {Sql.Col("Id")}, {Sql.Col("Archive")},
           {Sql.Col("GenericDefinition")}
    FROM {Sql.Table("Common_Screens")}
    WHERE {Sql.Col("Archive")} = {false}
    """);
```

`Sql.Col` wraps a column identifier atom; a dot inside its name is part of that
column name, not an alias separator. Use trusted literal aliases such as
`s.{Sql.Col("Id")}` in a join. `Sql.Table` wraps a table path and supports
schema qualification through the existing provider quoting rules. Ordinary
values (including ordinary strings) become native driver parameters. Identifiers
must be non-empty. Complex objects and collections are rejected rather than
converted to SQL text.

The same API also supports format specifiers:

```csharp
var screens = Database.Query<ScreenDto>($"""
    SELECT {"Id":column}, {"Archive":column}, {"GenericDefinition":column}
    FROM {"Common_Screens":table}
    WHERE {"Archive":column} = {false}
    """);
```

`:column` and `:table` require identifier strings (or matching wrappers). Other
format specifiers such as `:D2` throw; values retain their native types rather
than being formatted into strings. Escaped braces and alignment use the .NET
composite-format parser. Each placeholder occurrence gets its own parameter,
including repeated argument indexes, so positional drivers bind in SQL order.

For tuples, constructor mapping or unusual projections, supply a mapper:

```csharp
var screens = Database.Query($"""
    SELECT {Sql.Col("Id")}, {Sql.Col("GenericDefinition")}
    FROM {Sql.Table("Common_Screens")}
    WHERE {Sql.Col("Archive")} = {false}
    """, r => (
        Id: Convert.ToInt32(r.GetValue(0)),
        Definition: r.IsDBNull(1) ? null : r.GetString(1)));
```

The signatures are:

```csharp
IReadOnlyList<T> Query<T>(FormattableString sql) where T : class, new();
IReadOnlyList<T> Query<T>(FormattableString sql, Func<IDataReader, T> mapper);
```

DTO results reuse the [typed DTO mapping](dto-queries.md): columns must match
mapped names, including standard `ColumnAttribute` overrides; SQL aliases should
use those mapped names. Extra result columns are ignored, missing columns throw
with DTO/property context. The mapper overload has no constructor/type
constraints and is invoked once per row; the mapper must not dispose or advance
the reader itself.

Both return materialized lists and dispose their command and reader before
return, including binding, execution and mapping failures. They retain the
provider's connection, command timeout, native parameter conversions and active
migration transaction. In a fluent migration use `Context.Query(...)` for an
immediate query.

SQL literal text is trusted migration code. Interpolated values are parameterized,
but literal text is not validated or made portable. SQL null, collation, ordering,
joins and other SQL semantics remain explicit in the query. For example,
`WHERE Column = {null}` does not become `IS NULL`; author null predicates in SQL.
Do not first assign interpolation to `var`/`string`, concatenate it into a string,
or call `ToString()`: this loses arguments, and there is deliberately no `string`
overload. To store a query, use `FormattableString sql = $"..."`.

This uses `FormattableString` rather than a custom interpolated string handler.
Both identifier syntaxes already work with it; a handler can be considered later
if profiling justifies the additional complexity.
