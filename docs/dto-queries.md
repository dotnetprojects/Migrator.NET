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

Byte-array comparisons and ordering of strings, booleans, GUIDs and enums are
unsupported. Methods, property-to-property comparisons, arithmetic, navigation
properties, lossy casts, joins and general LINQ operators are unsupported and
throw before executing a query. Capture computed values in local scalar variables
first; the translator reads constants and fields without compiling a lambda or
executing arbitrary getters/methods.

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

## Explicit typed filters

Use `Filter<T>` when you prefer explicit operations to predicate translation:

```csharp
var filter = Filter<ScreenDto>.Equal(x => x.Archive, false)
    .And(Filter<ScreenDto>.GreaterThan(x => x.Id, minimumId));

var screens = Database.Select("Common_Screens", filter);
// Explicit generic syntax is also supported:
var sameScreens = Database.Select<ScreenDto>("Common_Screens", filter);
```

The builder supports `Equal`, `NotEqual`, `LessThan`, `LessThanOrEqual`,
`GreaterThan`, `GreaterThanOrEqual`, `IsNull` and `IsNotNull`. Combine filters using
`And`, `Or` and `Not()`; each returns a new immutable filter, preserving grouping.
Reusing a filter does not reuse commands or parameter collections. Values are
captured when the filter is built, so later changes to local variables do not
change it.

Selectors must name a direct mapped property in its declared type. The generic
`TValue` connects selector and comparison value types; widened/boxed selectors,
casts, calculations, method calls and nested properties are rejected. Ordering
has the same type restrictions as the lambda API. `IsNull` and `IsNotNull` require
a nullable value or reference property; equality with a null reference/nullable
value also renders a SQL null check.

```csharp
public sealed class OptionalDto
{
    public int? Optional { get; set; }
}

// Include NULL rows explicitly when comparing against a non-null value:
var optionalFilter = Filter<OptionalDto>.IsNull(x => x.Optional)
    .Or(Filter<OptionalDto>.NotEqual(x => x.Optional, 7));
```

Both APIs share the DTO mapping, quoting, native parameter binding, SQL null
semantics, transaction and resource-lifetime behavior described above. A null
filter throws instead of selecting all rows; omit the predicate/filter argument
to select all rows. For explicit null arguments, use `predicate: null` to select
the lambda overload or `filter: null` to select the filter overload.
