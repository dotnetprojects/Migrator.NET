using System;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNetProjects.Migrator.Framework.TypedSelect;

internal abstract record SelectPredicate;
internal sealed record ComparisonPredicate(MappedProperty Property, ExpressionType Operator, object Value) : SelectPredicate;
internal sealed record LogicalPredicate(ExpressionType Operator, SelectPredicate Left, SelectPredicate Right = null) : SelectPredicate;

internal sealed class PredicateTranslator(DtoMapping mapping, ParameterExpression parameter)
{
    internal SelectPredicate Translate(Expression expression)
    {
        if (expression is BinaryExpression logical && logical.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
            return new LogicalPredicate(logical.NodeType, Translate(logical.Left), Translate(logical.Right));
        if (expression is UnaryExpression { NodeType: ExpressionType.Not } not)
            return new LogicalPredicate(ExpressionType.Not, Translate(not.Operand));
        if (expression is BinaryExpression comparison && IsComparison(comparison.NodeType))
        {
            if (TryProperty(comparison.Left, out var property))
                return Compare(property, comparison.NodeType, ReadValue(comparison.Right));
            if (TryProperty(comparison.Right, out property))
                return Compare(property, Reverse(comparison.NodeType), ReadValue(comparison.Left));
        }
        if (TryProperty(expression, out var boolean) && boolean.Property.PropertyType == typeof(bool))
            return Compare(boolean, ExpressionType.Equal, true);
        throw new NotSupportedException($"Unsupported DTO predicate '{expression}'. Use direct property comparisons and boolean operators.");
    }

    internal static bool IsComparison(ExpressionType operation) => operation is ExpressionType.Equal or ExpressionType.NotEqual
        or ExpressionType.LessThan or ExpressionType.LessThanOrEqual or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual;

    internal static ComparisonPredicate Compare(MappedProperty property, ExpressionType operation, object value)
    {
        var type = DtoMapping.ScalarType(property.Property.PropertyType);
        if (type == typeof(byte[]) || (operation is not (ExpressionType.Equal or ExpressionType.NotEqual) &&
            (type == typeof(bool) || type == typeof(Guid) || type == typeof(string) || type.IsEnum)))
            throw new NotSupportedException($"Comparison '{operation}' is not supported for '{property.Property.Name}'.");
        if (value == null && operation is not (ExpressionType.Equal or ExpressionType.NotEqual))
            throw new NotSupportedException("Only equality and inequality can compare with SQL NULL.");
        if (value != null && !DtoMapping.IsScalar(value.GetType()))
            throw new NotSupportedException($"Predicate value type '{value.GetType().Name}' is not a scalar.");
        return new ComparisonPredicate(property, operation, value);
    }

    private bool TryProperty(Expression expression, out MappedProperty property)
    {
        // C# inserts numeric promotions (e.g. byte -> int) and enum conversions in comparisons.
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert } conversion &&
            conversion.Method == null && IsSafeConversion(conversion.Operand.Type, conversion.Type))
            expression = conversion.Operand;
        if (expression is MemberExpression { Member: PropertyInfo info } member && member.Expression == parameter)
        {
            property = mapping.Find(info);
            return true;
        }
        property = null;
        return false;
    }

    private static bool IsSafeConversion(Type source, Type target)
    {
        source = DtoMapping.ScalarType(source);
        target = DtoMapping.ScalarType(target);
        if (source == target || (source.IsEnum && Enum.GetUnderlyingType(source) == target)) return true;
        return (source == typeof(byte) || source == typeof(sbyte) || source == typeof(short) || source == typeof(ushort)) &&
            (target == typeof(int) || target == typeof(long)) ||
            source == typeof(int) && target == typeof(long) || source == typeof(uint) && (target == typeof(long) || target == typeof(ulong));
    }

    private static object ReadValue(Expression expression)
    {
        if (expression is ConstantExpression constant) return constant.Value;
        if (expression is MemberExpression { Member: FieldInfo field } member)
            return field.GetValue(member.Expression == null ? null : ReadValue(member.Expression));
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert, Method: null } conversion &&
            IsSafeConversion(conversion.Operand.Type, conversion.Type))
        {
            var value = ReadValue(conversion.Operand);
            if (value == null) return null;
            var type = DtoMapping.ScalarType(conversion.Type);
            return type.IsEnum ? Enum.ToObject(type, value)
                : Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture);
        }
        throw new NotSupportedException($"Unsupported predicate value '{expression}'. Capture a scalar value in a local variable first.");
    }

    private static ExpressionType Reverse(ExpressionType operation) => operation switch
    {
        ExpressionType.LessThan => ExpressionType.GreaterThan,
        ExpressionType.LessThanOrEqual => ExpressionType.GreaterThanOrEqual,
        ExpressionType.GreaterThan => ExpressionType.LessThan,
        ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
        _ => operation
    };
}
