# Reading DTOs in migrations

Import `DotNetProjects.Migrator.Framework` to use `Select<T>` on an
`ITransformationProvider`. Keep DTO definitions with their migration so later
application model changes do not change historical migrations.

```csharp
using DotNetProjects.Migrator.Framework;
using DtoColumn = System.ComponentModel.DataAnnotations.Schema.ColumnAttribute;

public sealed class ScreenDto
{
    public int Id { get; set; }
    public bool Archive { get; set; }
    [DtoColumn("GenericDefinition")]
    public string Definition { get; set; }
}

// Inside a migration's Up():
var minimumId = 10;
var screens = Database.Select<ScreenDto>(
    "Common_Screens", x => !x.Archive && x.Id > minimumId);
```

The table is explicit; no pluralization or application entity metadata is needed.
The query selects public readable/writable instance properties, with column names
matching property names unless the standard `ColumnAttribute` overrides them.
Use the `DtoColumn` alias above because Migrator also has a `ColumnAttribute`
enum. The standard `NotMappedAttribute` excludes a property. Indexers, fields,
static properties and properties without public getters and setters are ignored.
DTOs must be reference types with a public parameterless constructor. Empty
mappings, duplicate column names and unsupported property types fail explicitly.

Supported scalar types are strings, byte arrays, booleans, integer types, float,
double, decimal, enums, Guid, DateTime, DateTimeOffset, TimeOnly and TimeSpan,
including nullable value types. Their database representation and precision
remain provider-specific. SQL NULL maps to nullable/reference properties; mapping
it to a non-nullable value type throws a `DataException` naming the column and
property. Enums map from their numeric storage; GUIDs accept GUID values, strings
or 16-byte storage. The mapper uses invariant numeric conversions and reports
format and overflow errors. It does not track entities or load related objects.

`Select<T>(table)` reads all rows. The optional predicate accepts:

- Direct property comparisons with constants or captured scalar fields using
  `==`, `!=`, `<`, `<=`, `>` and `>=`.
- `&&`, `||`, `!`, grouping, and direct boolean properties such as `x.Active`.
- `== null` and `!= null`, translated to `IS NULL` and `IS NOT NULL`.
- Numeric enum equality and lossless integral promotions inserted by C#.
- String `Contains(string)`, `StartsWith(string)` and `EndsWith(string)` on direct
  mapped properties, with constants or captured strings. Search values are
  parameterized and LIKE wildcard/escape characters are treated literally.

Byte-array comparisons and ordering of strings, booleans, GUIDs and enums are
unsupported. Other methods, property-to-property comparisons, arithmetic, navigation
properties, lossy casts, joins and general LINQ operators are unsupported and
throw before executing a query. Capture computed values in local scalar variables
first; the translator reads constants and fields without compiling a lambda or
executing arbitrary getters/methods.

```csharp
var matching = Database.Select<ScreenDto>("Common_Screens",
    x => (!x.Archive && x.Id > 4) || x.Definition.Contains("aa"));
```

String searches follow database collation, not .NET ordinal matching. NULL column
values are excluded, including under negation; empty search text matches every
non-NULL value. Null search text throws before execution. Character and
`StringComparison` overloads are unsupported. SQL Server and Sybase bracket
wildcards are escaped through their dialects; other dialects escape `%`, `_` and
the explicit `~` escape character.

Predicates use SQL null semantics and database collation. For example,
`x.Optional != 7` excludes NULL rows; use
`x.Optional == null || x.Optional != 7` to include them. There is no implicit
client-side filtering. All non-null values become driver parameters; identifiers
use the provider's quoting rules. SQL parameter occurrence order is preserved
for positional drivers, and built-in providers retain their native value
conversions. Custom providers can override `AddQueryParameter` if they need
different parameter names or storage conversions.

The returned `IReadOnlyList<T>` is fully materialized. The command and reader are
disposed before return, also on errors. The provider connection stays open and
the query participates in its active migration transaction. Row order is
unspecified; sorting after materialization is explicit client-side work.

In a `FluentMigration`, call `Context.Select<T>` for immediate reads. Reads during
`BuildUp` cannot see schema operations merely queued in that same builder.

## Typed updates and deletes

Both mutation APIs require a predicate and return the driver-reported affected
row count. They reuse the selection predicates, including string searches.

```csharp
int updated = Database.Update<ScreenDto>("Common_Screens",
    x => new ScreenDto { Archive = true, Definition = "archived" },
    x => x.Id > 4 && x.Definition.StartsWith("old"));

int deleted = Database.Delete<ScreenDto>("Common_Screens",
    x => x.Archive && x.Definition.EndsWith(".obsolete"));
```

Only initializer-assigned properties are updated; unassigned properties keep
their existing values. Explicit null assignments render SQL NULL. Initializer
values must be constants or captured scalar fields. Empty initializers,
row-dependent calculations, constructor arguments, and unmapped assignments are
rejected before execution. The DTO is not constructed and its setters are not
executed during translation. Null predicates are rejected and there are no
predicate-free typed mutation overloads. Commands are disposed on success and
failure; native parameter conversions, positional parameter order, command
timeouts and active migration transactions are retained. In fluent migrations,
use `Context.Update<T>` / `Context.Delete<T>` for immediate operations.
