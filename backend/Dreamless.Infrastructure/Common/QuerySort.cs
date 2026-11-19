using System.Reflection;

namespace Dreamless.Infrastructure.Common;

public static class QuerySort
{
    public static ISugarQueryable<T> Apply<T>(
        ISugarQueryable<T> query,
        IReadOnlyList<SortClause> sorts,
        Func<ISugarQueryable<T>, SortClause, ISugarQueryable<T>?>? custom = null)
    {
        var applied = false;
        foreach (var sort in sorts)
        {
            var customQuery = custom?.Invoke(query, sort);
            if (customQuery != null)
            {
                query = customQuery;
                applied = true;
                continue;
            }

            var property = typeof(T).GetProperty(sort.Field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property == null || !property.CanRead || property.Name == "PasswordHash")
            {
                continue;
            }

            query = query.OrderByPropertyName(property.Name, sort.Descending ? OrderByType.Desc : OrderByType.Asc);
            applied = true;
        }

        if (applied && !sorts.Any(sort => sort.Field.Equals("id", StringComparison.OrdinalIgnoreCase))
            && typeof(T).GetProperty("Id") != null)
        {
            query = query.OrderByPropertyName("Id", OrderByType.Asc);
        }

        return query;
    }
}
