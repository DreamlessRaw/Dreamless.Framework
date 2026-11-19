using System.Linq.Expressions;
using System.Reflection;

namespace Dreamless.Infrastructure.Services;

public sealed class CrudEngine(
    ISqlSugarClient db,
    ICurrentUser current,
    IDataScopeService scopes,
    IFieldAccessService fields,
    IExcelService excel,
    ILocalizationService l10n) : ICrudEngine
{
    public async Task<PageResult<TDto>> PageAsync<TEntity, TDto>(string pageCode, PageQuery query)
        where TEntity : BaseEntity, new()
        where TDto : class, new()
    {
        var source = QueryFilter.Apply(await Filtered<TEntity>(query.Keyword), query.FilterClauses);
        var total = await source.CountAsync();
        source = query.SortClauses.Count == 0
            ? source.OrderBy(x => x.Id, OrderByType.Desc)
            : QuerySort.Apply(source, query.SortClauses);
        var rows = await source
            .Skip((query.NormalizedPage - 1) * query.NormalizedSize)
            .Take(query.NormalizedSize)
            .ToListAsync();
        var access = await fields.GetAsync(pageCode);
        return new PageResult<TDto>
        {
            Total = total,
            Items = rows.Select(row =>
            {
                var dto = AppMap.Map<TDto>(row);
                fields.Mask(dto, access);
                return dto;
            }).ToList()
        };
    }

    public async Task<long> CreateAsync<TEntity>(string pageCode, object dto) where TEntity : BaseEntity, new()
    {
        var entity = new TEntity();
        fields.CopyEditable(dto, entity, await fields.GetAsync(pageCode));
        if (entity is ITenantEntity tenant && tenant.TenantId == 0)
        {
            tenant.TenantId = current.TenantId;
        }

        return await db.Insertable(entity).ExecuteReturnBigIdentityAsync();
    }

    public async Task UpdateAsync<TEntity>(string pageCode, long id, object dto) where TEntity : BaseEntity, new()
    {
        var entity = await (await Filtered<TEntity>(null)).FirstAsync(x => x.Id == id)
            ?? throw new BusinessException(await l10n.T("error.notFound", "数据不存在"), 404);
        fields.CopyEditable(dto, entity, await fields.GetAsync(pageCode));
        await db.Updateable(entity).ExecuteCommandAsync();
    }

    public async Task DeleteAsync<TEntity>(long id) where TEntity : BaseEntity, new()
    {
        var entity = await (await Filtered<TEntity>(null)).FirstAsync(x => x.Id == id)
            ?? throw new BusinessException(await l10n.T("error.notFound", "数据不存在"), 404);
        entity.IsDeleted = true;
        await db.Updateable(entity).UpdateColumns(x => new { x.IsDeleted, x.UpdateTime, x.UpdateUserId }).ExecuteCommandAsync();
    }

    public async Task<byte[]> ExportAsync<TEntity, TExport>(string pageCode, PageQuery query)
        where TEntity : BaseEntity, new()
        where TExport : class, new()
    {
        var rows = await QueryFilter.Apply(await Filtered<TEntity>(query.Keyword), query.FilterClauses).Take(20000).ToListAsync();
        var access = await fields.GetAsync(pageCode);
        var dtos = rows.Select(row =>
        {
            var dto = AppMap.Map<TExport>(row);
            fields.Mask(dto, access);
            return dto;
        }).ToList();
        return await excel.ExportAsync(dtos, pageCode);
    }

    public Task<byte[]> TemplateAsync<TImport>(string pageCode) where TImport : class, new() =>
        excel.TemplateAsync<TImport>(pageCode);

    public async Task<ImportResultDto> ImportAsync<TEntity, TImport>(string pageCode, Stream stream)
        where TEntity : BaseEntity, new()
        where TImport : class, new()
    {
        var read = await excel.ReadAsync<TImport>(stream, pageCode);
        if (read.Errors.Count > 0)
        {
            return new ImportResultDto { Failed = read.Errors.Count, Errors = read.Errors };
        }

        var access = await fields.GetAsync(pageCode);
        var success = 0;
        foreach (var row in read.Rows)
        {
            var entity = new TEntity();
            fields.CopyEditable(row, entity, access);
            if (entity is ITenantEntity tenant && tenant.TenantId == 0)
            {
                tenant.TenantId = current.TenantId;
            }

            await db.Insertable(entity).ExecuteCommandAsync();
            success++;
        }

        return new ImportResultDto { Success = success };
    }

    private async Task<ISugarQueryable<TEntity>> Filtered<TEntity>(string? keyword) where TEntity : BaseEntity, new()
    {
        var query = db.Queryable<TEntity>();
        if (typeof(BaseDeptEntity).IsAssignableFrom(typeof(TEntity)))
        {
            var scope = await scopes.GetAsync();
            if (!scope.All)
            {
                var method = typeof(CrudEngine).GetMethod(nameof(ApplyDept), BindingFlags.NonPublic | BindingFlags.Static)!;
                query = (ISugarQueryable<TEntity>)method.MakeGenericMethod(typeof(TEntity)).Invoke(null, [query, scope, current.UserId])!;
            }
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var body = Keyword<TEntity>(keyword.Trim());
            if (body != null)
            {
                query = query.Where(body);
            }
        }

        return query;
    }

    private static ISugarQueryable<T> ApplyDept<T>(ISugarQueryable<T> query, DataScopeFilter scope, long userId)
        where T : BaseDeptEntity, new()
    {
        var ids = scope.DeptIds;
        if (ids.Count > 0 && scope.IncludeSelf)
        {
            return query.Where(x => (x.DeptId != null && ids.Contains(x.DeptId.Value)) || x.CreateUserId == userId);
        }

        if (ids.Count > 0)
        {
            return query.Where(x => x.DeptId != null && ids.Contains(x.DeptId.Value));
        }

        return query.Where(x => x.CreateUserId == userId);
    }

    private static Expression<Func<TEntity, bool>>? Keyword<TEntity>(string keyword)
    {
        var properties = typeof(TEntity).GetProperties()
            .Where(p => p.PropertyType == typeof(string) && p.Name != "PasswordHash")
            .ToList();
        if (properties.Count == 0)
        {
            return null;
        }

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        Expression? body = null;
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        foreach (var property in properties)
        {
            var member = Expression.Property(parameter, property);
            var call = Expression.Call(member, contains, Expression.Constant(keyword));
            var notNull = Expression.NotEqual(member, Expression.Constant(null, typeof(string)));
            var clause = Expression.AndAlso(notNull, call);
            body = body == null ? clause : Expression.OrElse(body, clause);
        }

        return Expression.Lambda<Func<TEntity, bool>>(body!, parameter);
    }
}
