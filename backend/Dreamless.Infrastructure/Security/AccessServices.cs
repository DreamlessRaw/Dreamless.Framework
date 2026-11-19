using Dreamless.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;

namespace Dreamless.Infrastructure.Security;

public sealed class PermissionService(ISqlSugarClient db, IMemoryCache cache) : IPermissionService
{
    public async Task<bool> HasAsync(long userId, string code)
    {
        var codes = await GetCodesAsync(userId, false);
        return codes.Contains(code);
    }

    public async Task<HashSet<string>> GetCodesAsync(long userId, bool isHostAdmin)
    {
        var version = cache.TryGetValue("perm:ver", out long current) ? current : 0L;
        var key = $"perm:{version}:{userId}:{isHostAdmin}";
        if (cache.TryGetValue(key, out HashSet<string>? cached) && cached != null)
        {
            return cached;
        }

        List<string?> codes;
        if (isHostAdmin)
        {
            codes = await db.Queryable<SysMenu>()
                .Where(m => m.Status == CommonStatus.Enabled && m.Permission != null && m.Permission != "")
                .Select(m => m.Permission)
                .ToListAsync();
        }
        else
        {
            var roleIds = await db.Queryable<SysUserRole>()
                .Where(x => x.UserId == userId)
                .Select(x => x.RoleId)
                .ToListAsync();
            var enabledRoles = roleIds.Count == 0
                ? []
                : await db.Queryable<SysRole>()
                    .Where(r => roleIds.Contains(r.Id) && r.Status == CommonStatus.Enabled)
                    .Select(r => r.Id)
                    .ToListAsync();
            var menuIds = enabledRoles.Count == 0
                ? []
                : await db.Queryable<SysRoleMenu>()
                    .Where(x => enabledRoles.Contains(x.RoleId))
                    .Select(x => x.MenuId)
                    .ToListAsync();
            codes = menuIds.Count == 0
                ? []
                : await db.Queryable<SysMenu>()
                    .Where(m => menuIds.Contains(m.Id) && m.Status == CommonStatus.Enabled && m.Permission != null)
                    .Select(m => m.Permission)
                    .ToListAsync();
        }

        var set = codes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        cache.Set(key, set, TimeSpan.FromMinutes(10));
        return set;
    }

    public void Invalidate()
    {
        var version = cache.TryGetValue("perm:ver", out long current) ? current : 0L;
        cache.Set("perm:ver", version + 1);
    }
}

public sealed class FieldAccessService(ISqlSugarClient db, ICurrentUser current) : IFieldAccessService
{
    public async Task<IReadOnlyList<FieldAccess>> GetAsync(string pageCode)
    {
        var fields = await db.Queryable<SysField>().Where(f => f.PageCode == pageCode).OrderBy(f => f.Sort).ToListAsync();
        if (current.IsHostAdmin)
        {
            return Allow(fields);
        }

        var roleIds = await EnabledRoleIds();
        var fieldIds = fields.Select(f => f.Id).ToList();
        var rules = fieldIds.Count == 0 || roleIds.Count == 0
            ? []
            : await db.Queryable<SysRoleField>()
                .Where(x => roleIds.Contains(x.RoleId) && fieldIds.Contains(x.FieldId))
                .ToListAsync();
        var configured = rules.Select(x => x.RoleId).ToHashSet();
        var configuredRoles = roleIds.Where(configured.Contains).ToHashSet();
        if (configuredRoles.Count == 0)
        {
            return Allow(fields);
        }

        return fields.Select(field =>
        {
            var matched = rules.Where(x => x.FieldId == field.Id && configuredRoles.Contains(x.RoleId)).ToList();
            var canView = matched.Any(x => x.CanView);
            return new FieldAccess
            {
                FieldId = field.Id,
                Field = field.FieldName,
                CanView = canView,
                CanEdit = canView && matched.Any(x => x.CanEdit),
                CanExport = canView && matched.Any(x => x.CanExport)
            };
        }).ToList();
    }

    public void Mask<T>(T target, IReadOnlyList<FieldAccess> access)
    {
        if (target == null)
        {
            return;
        }

        var hidden = access.Where(x => !x.CanView).Select(x => x.Field).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var property in target.GetType().GetProperties().Where(p => p.CanWrite && hidden.Contains(p.Name)))
        {
            var underlying = Nullable.GetUnderlyingType(property.PropertyType);
            if (!property.PropertyType.IsValueType || underlying != null)
            {
                property.SetValue(target, null);
            }
            else
            {
                property.SetValue(target, Activator.CreateInstance(property.PropertyType));
            }
        }
    }

    public void CopyEditable<TSource, TTarget>(TSource source, TTarget target, IReadOnlyList<FieldAccess> access)
    {
        if (source == null || target == null)
        {
            return;
        }

        if (access.Count == 0)
        {
            AppMap.Copy(source, target, null);
            return;
        }

        var editable = access.Where(x => x.CanEdit).Select(x => x.Field).ToHashSet(StringComparer.OrdinalIgnoreCase);
        AppMap.Copy(source, target, editable);
    }

    private static List<FieldAccess> Allow(List<SysField> fields) => fields.Select(field => new FieldAccess
    {
        FieldId = field.Id,
        Field = field.FieldName,
        CanView = true,
        CanEdit = field.FieldName != "createTime",
        CanExport = true
    }).ToList();

    private async Task<List<long>> EnabledRoleIds()
    {
        var roleIds = await db.Queryable<SysUserRole>()
            .Where(x => x.UserId == current.UserId)
            .Select(x => x.RoleId)
            .ToListAsync();
        if (roleIds.Count == 0)
        {
            return [];
        }

        return await db.Queryable<SysRole>()
            .Where(r => roleIds.Contains(r.Id) && r.Status == CommonStatus.Enabled)
            .Select(r => r.Id)
            .ToListAsync();
    }
}

public sealed class DataScopeService(ISqlSugarClient db, ICurrentUser current) : IDataScopeService
{
    public async Task<DataScopeFilter> GetAsync()
    {
        if (current.IsHostAdmin)
        {
            return new DataScopeFilter { All = true };
        }

        var roleIds = await db.Queryable<SysUserRole>().Where(x => x.UserId == current.UserId).Select(x => x.RoleId).ToListAsync();
        var roles = roleIds.Count == 0
            ? []
            : await db.Queryable<SysRole>().Where(r => roleIds.Contains(r.Id) && r.Status == CommonStatus.Enabled).ToListAsync();
        if (roles.Count == 0)
        {
            return new DataScopeFilter { IncludeSelf = true };
        }

        if (roles.Any(r => r.DataScope == DataScope.All))
        {
            return new DataScopeFilter { All = true };
        }

        var nodes = (await db.Queryable<SysDept>().ToListAsync())
            .Select(d => new DeptNode(d.Id, d.ParentId))
            .ToList();
        var deptIds = new HashSet<long>();
        var self = false;
        foreach (var role in roles)
        {
            switch (role.DataScope)
            {
                case DataScope.Dept:
                    AddOrSelf(current.DeptId, deptIds, ref self);
                    break;
                case DataScope.DeptAndChild:
                    if (current.DeptId is long root)
                    {
                        foreach (var id in Descendants(nodes, root))
                        {
                            deptIds.Add(id);
                        }
                    }
                    else
                    {
                        self = true;
                    }
                    break;
                case DataScope.Custom:
                    var custom = await db.Queryable<SysRoleDept>().Where(x => x.RoleId == role.Id).Select(x => x.DeptId).ToListAsync();
                    if (custom.Count == 0)
                    {
                        self = true;
                    }
                    foreach (var id in custom)
                    {
                        deptIds.Add(id);
                    }
                    break;
                default:
                    self = true;
                    break;
            }
        }

        return new DataScopeFilter { IncludeSelf = self, DeptIds = deptIds.ToList() };
    }

    private static void AddOrSelf(long? deptId, HashSet<long> deptIds, ref bool self)
    {
        if (deptId is long id)
        {
            deptIds.Add(id);
        }
        else
        {
            self = true;
        }
    }

    private static IEnumerable<long> Descendants(List<DeptNode> nodes, long root)
    {
        var children = nodes.GroupBy(x => x.ParentId).ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
        var queue = new Queue<long>();
        var seen = new HashSet<long>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!seen.Add(id))
            {
                continue;
            }

            yield return id;
            if (children.TryGetValue(id, out var kids))
            {
                foreach (var kid in kids)
                {
                    queue.Enqueue(kid);
                }
            }
        }
    }

    private sealed record DeptNode(long Id, long ParentId);
}

public sealed class LocalizationService(ISqlSugarClient db, ICurrentUser current, IMemoryCache cache) : ILocalizationService
{
    public async Task<string> T(string key, string? fallback = null)
    {
        var culture = string.IsNullOrWhiteSpace(current.Culture) ? "zh-CN" : current.Culture;
        var map = await GetMapAsync(culture);
        if (map.TryGetValue(key, out var value))
        {
            return value;
        }

        if (culture != "zh-CN")
        {
            var zh = await GetMapAsync("zh-CN");
            if (zh.TryGetValue(key, out var zhValue))
            {
                return zhValue;
            }
        }

        return fallback ?? key;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetMapAsync(string culture)
    {
        var result = await cache.GetOrCreateAsync($"i18n:{culture}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            var rows = await db.Queryable<SysI18n>().Where(x => x.Culture == culture).ToListAsync();
            return (IReadOnlyDictionary<string, string>)rows.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        });
        return result ?? new Dictionary<string, string>();
    }

    public void Invalidate(string? culture = null)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            cache.Remove("i18n:zh-CN");
            cache.Remove("i18n:en-US");
            return;
        }

        cache.Remove($"i18n:{culture}");
    }
}
