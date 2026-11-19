using System.Linq.Expressions;
using System.Reflection;

namespace Dreamless.Infrastructure.Common;

public static class QueryFilter
{
    public static ISugarQueryable<T> Apply<T>(
        ISugarQueryable<T> query,
        IReadOnlyList<FilterClause> filters,
        Func<ISugarQueryable<T>, FilterClause, ISugarQueryable<T>?>? custom = null)
    {
        foreach (var filter in filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Value))
            {
                continue;
            }

            var customQuery = custom?.Invoke(query, filter);
            if (customQuery != null)
            {
                query = customQuery;
                continue;
            }

            var property = typeof(T).GetProperty(filter.Field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property == null || !property.CanRead || property.Name == "PasswordHash")
            {
                continue;
            }

            var parameter = Expression.Parameter(typeof(T), "row");
            var body = Build(parameter, property, filter);
            if (body == null)
            {
                continue;
            }

            var lambda = Expression.Lambda<Func<T, bool>>(body, parameter);
            query = query.Where(lambda);
        }

        return query;
    }

    private static Expression? Build(ParameterExpression parameter, PropertyInfo property, FilterClause filter)
    {
        var member = Expression.Property(parameter, property);
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (filter.ValueTo != null)
        {
            return Range(member, property.PropertyType, type, filter.Value!, filter.ValueTo);
        }

        if (type == typeof(string))
        {
            var method = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
            return Expression.Call(member, method, Expression.Constant(filter.Value));
        }

        var parsed = Parse(type, filter.Value!);
        if (parsed == null)
        {
            return null;
        }

        return Expression.Equal(member, Constant(parsed, property.PropertyType));
    }

    private static Expression? Range(Expression member, Type propertyType, Type type, string startText, string endText)
    {
        if (type != typeof(DateTime))
        {
            return null;
        }

        Expression? body = null;
        if (DateTime.TryParse(startText, out var start))
        {
            body = Expression.GreaterThanOrEqual(member, Constant(start, propertyType));
        }

        if (DateTime.TryParse(endText, out var end))
        {
            var upper = Expression.LessThan(member, Constant(end.Date.AddDays(1), propertyType));
            body = body == null ? upper : Expression.AndAlso(body, upper);
        }

        return body;
    }

    private static object? Parse(Type type, string text)
    {
        if (type == typeof(string)) return text;
        if (type == typeof(bool) && bool.TryParse(text, out var boolean)) return boolean;
        if (type == typeof(int) && int.TryParse(text, out var integer)) return integer;
        if (type == typeof(long) && long.TryParse(text, out var number)) return number;
        if (type == typeof(decimal) && decimal.TryParse(text, out var money)) return money;
        if (type == typeof(double) && double.TryParse(text, out var real)) return real;
        if (type == typeof(DateTime) && DateTime.TryParse(text, out var time)) return time;
        if (type.IsEnum && Enum.TryParse(type, text, true, out var enumValue)) return enumValue;
        return null;
    }

    private static Expression Constant(object value, Type propertyType)
    {
        if (Nullable.GetUnderlyingType(propertyType) != null)
        {
            return Expression.Convert(Expression.Constant(value), propertyType);
        }

        return Expression.Constant(value, propertyType);
    }
}
