using System;
using System.Linq.Expressions;
using System.Reflection;
using DotNetProjects.Migrator.Framework.TypedSelect;

namespace DotNetProjects.Migrator.Framework;

/// <summary>
/// An immutable single-table DTO filter. Selectors must name a direct mapped property;
/// comparisons use SQL null and collation semantics. Values are captured when the filter is built.
/// </summary>
public sealed class Filter<T> where T : class, new()
{
    internal SelectPredicate Predicate { get; }
    private Filter(SelectPredicate predicate) => Predicate = predicate;

    /// <summary>Compare a mapped property with a value of its declared type.</summary>
    public static Filter<T> Equal<TValue>(Expression<Func<T, TValue>> column, TValue value) =>
        Compare(column, ExpressionType.Equal, value);

    /// <summary>Compare for inequality. SQL NULL rows are excluded unless explicitly included.</summary>
    public static Filter<T> NotEqual<TValue>(Expression<Func<T, TValue>> column, TValue value) =>
        Compare(column, ExpressionType.NotEqual, value);

    public static Filter<T> LessThan<TValue>(Expression<Func<T, TValue>> column, TValue value) =>
        Compare(column, ExpressionType.LessThan, value);

    public static Filter<T> LessThanOrEqual<TValue>(Expression<Func<T, TValue>> column, TValue value) =>
        Compare(column, ExpressionType.LessThanOrEqual, value);

    public static Filter<T> GreaterThan<TValue>(Expression<Func<T, TValue>> column, TValue value) =>
        Compare(column, ExpressionType.GreaterThan, value);

    public static Filter<T> GreaterThanOrEqual<TValue>(Expression<Func<T, TValue>> column, TValue value) =>
        Compare(column, ExpressionType.GreaterThanOrEqual, value);

    /// <summary>Test a reference or nullable value property for SQL NULL.</summary>
    public static Filter<T> IsNull<TValue>(Expression<Func<T, TValue>> column) => NullCheck(column, ExpressionType.Equal);

    /// <summary>Test a reference or nullable value property for a non-NULL value.</summary>
    public static Filter<T> IsNotNull<TValue>(Expression<Func<T, TValue>> column) => NullCheck(column, ExpressionType.NotEqual);

    public Filter<T> And(Filter<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Filter<T>(new LogicalPredicate(ExpressionType.AndAlso, Predicate, other.Predicate));
    }

    public Filter<T> Or(Filter<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Filter<T>(new LogicalPredicate(ExpressionType.OrElse, Predicate, other.Predicate));
    }

    public Filter<T> Not() => new(new LogicalPredicate(ExpressionType.Not, Predicate));

    private static Filter<T> Compare<TValue>(Expression<Func<T, TValue>> column, ExpressionType operation, TValue value) =>
        new(PredicateTranslator.Compare(Property(column), operation, value));

    private static Filter<T> NullCheck<TValue>(Expression<Func<T, TValue>> column, ExpressionType operation)
    {
        var property = Property(column);
        var type = property.Property.PropertyType;
        if (type.IsValueType && Nullable.GetUnderlyingType(type) == null)
            throw new ArgumentException($"Property '{property.Property.Name}' is not nullable.", nameof(column));
        return new Filter<T>(new ComparisonPredicate(property, operation, null));
    }

    private static MappedProperty Property<TValue>(Expression<Func<T, TValue>> column)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (column.Body is not MemberExpression { Member: PropertyInfo property } member ||
            member.Expression != column.Parameters[0] || property.PropertyType != typeof(TValue))
            throw new ArgumentException("Select a direct mapped property using its declared type, without casts or calculations.", nameof(column));
        return DtoMapping.For(typeof(T)).Find(property);
    }
}
