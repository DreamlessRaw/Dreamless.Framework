using System.Text.Json;

namespace Dreamless.Infrastructure.Services;

public sealed class I18nAdminService(ISqlSugarClient db, IExcelService excel, ILocalizationService l10n) : II18nAdminService
{
    private const string Page = "system:i18n";

    public async Task<PageResult<I18nDto>> ListAsync(I18nQuery query)
    {
        var q = db.Queryable<SysI18n>();
        if (!string.IsNullOrWhiteSpace(query.Culture))
        {
            q = q.Where(x => x.Culture == query.Culture);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(x => x.Key.Contains(keyword) || x.Value.Contains(keyword));
        }

        q = QueryFilter.Apply(q, query.FilterClauses);
        var total = await q.CountAsync();
        q = query.SortClauses.Count == 0
            ? q.OrderBy(x => x.Culture).OrderBy(x => x.Key)
            : QuerySort.Apply(q, query.SortClauses);
        var rows = await q
            .Skip((query.NormalizedPage - 1) * query.NormalizedSize)
            .Take(query.NormalizedSize)
            .ToListAsync();
        return new PageResult<I18nDto> { Total = total, Items = rows.Select(Map).ToList() };
    }

    public async Task<long> CreateAsync(I18nSaveDto dto)
    {
        if (await db.Queryable<SysI18n>().AnyAsync(x => x.Culture == dto.Culture && x.Key == dto.Key))
        {
            throw new BusinessException(await l10n.T("error.i18n.exists", "翻译键已存在"));
        }

        var id = await db.Insertable(new SysI18n { Culture = dto.Culture.Trim(), Key = dto.Key.Trim(), Value = dto.Value }).ExecuteReturnBigIdentityAsync();
        l10n.Invalidate(dto.Culture);
        return id;
    }

    public async Task UpdateAsync(long id, I18nSaveDto dto)
    {
        var entity = await db.Queryable<SysI18n>().FirstAsync(x => x.Id == id)
            ?? throw new BusinessException(await l10n.T("error.notFound", "数据不存在"), 404);
        if (await db.Queryable<SysI18n>().AnyAsync(x => x.Culture == dto.Culture && x.Key == dto.Key && x.Id != id))
        {
            throw new BusinessException(await l10n.T("error.i18n.exists", "翻译键已存在"));
        }

        var previous = entity.Culture;
        entity.Culture = dto.Culture.Trim();
        entity.Key = dto.Key.Trim();
        entity.Value = dto.Value;
        await db.Updateable(entity).ExecuteCommandAsync();
        l10n.Invalidate(previous);
        l10n.Invalidate(entity.Culture);
    }

    public async Task DeleteAsync(long id)
    {
        var entity = await db.Queryable<SysI18n>().FirstAsync(x => x.Id == id)
            ?? throw new BusinessException(await l10n.T("error.notFound", "数据不存在"), 404);
        entity.IsDeleted = true;
        await db.Updateable(entity).UpdateColumns(x => new { x.IsDeleted, x.UpdateTime, x.UpdateUserId }).ExecuteCommandAsync();
        l10n.Invalidate(entity.Culture);
    }

    public async Task<byte[]> ExportAsync(I18nQuery query)
    {
        query.Page = 1;
        query.PageSize = 200;
        var all = new List<I18nDto>();
        while (true)
        {
            var page = await ListAsync(query);
            all.AddRange(page.Items);
            if (all.Count >= page.Total || page.Items.Count == 0) break;
            query.Page++;
        }

        return await excel.ExportAsync(all, Page);
    }

    public Task<byte[]> TemplateAsync() => excel.TemplateAsync<I18nImportDto>(Page);

    public async Task<ImportResultDto> ImportAsync(Stream stream)
    {
        var read = await excel.ReadAsync<I18nImportDto>(stream, Page);
        if (read.Errors.Count > 0) return new ImportResultDto { Failed = read.Errors.Count, Errors = read.Errors };
        var success = 0;
        foreach (var row in read.Rows)
        {
            var existing = await db.Queryable<SysI18n>().FirstAsync(x => x.Culture == row.Culture && x.Key == row.Key);
            var dto = new I18nSaveDto { Culture = row.Culture, Key = row.Key, Value = row.Value };
            if (existing == null) await CreateAsync(dto);
            else await UpdateAsync(existing.Id, dto);
            success++;
        }

        return new ImportResultDto { Success = success };
    }

    private static I18nDto Map(SysI18n row) => new() { Id = row.Id, Culture = row.Culture, Key = row.Key, Value = row.Value };
}

public sealed class MetaService(ISqlSugarClient db, ICurrentUser current, IFieldAccessService fields, ILocalizationService l10n) : IMetaService
{
    public async Task<PageColumnDto> ColumnsAsync(string pageCode)
    {
        var access = await fields.GetAsync(pageCode);
        var metas = await db.Queryable<SysField>().Where(f => f.PageCode == pageCode).ToListAsync();
        var metaMap = metas.ToDictionary(x => x.FieldName, StringComparer.OrdinalIgnoreCase);
        var prefs = await db.Queryable<SysUserColumn>()
            .Where(x => x.UserId == current.UserId && x.PageCode == pageCode)
            .ToListAsync();
        var prefMap = prefs.ToDictionary(x => x.FieldName, StringComparer.OrdinalIgnoreCase);
        var columns = new List<ColumnMetaDto>();
        foreach (var item in access.Where(x => x.CanView))
        {
            metaMap.TryGetValue(item.Field, out var meta);
            prefMap.TryGetValue(item.Field, out var pref);
            columns.Add(new ColumnMetaDto
            {
                Field = item.Field,
                Label = await l10n.T(meta?.I18nKey ?? item.Field, meta?.DisplayName ?? item.Field),
                ValueType = meta?.ValueType ?? "text",
                CanView = true,
                CanEdit = item.CanEdit,
                CanExport = item.CanExport,
                Visible = meta?.LockVisible == true || (pref?.Visible ?? meta?.DefaultVisible ?? true),
                Sort = pref?.Sort ?? meta?.Sort ?? 0,
                Width = pref?.Width,
                Fixed = pref?.Fixed,
                SortOrder = pref?.SortOrder is "asc" or "desc" ? pref.SortOrder : null,
                SortRank = pref?.SortRank is >= 1 and <= 5 ? pref.SortRank : null,
                LockVisible = meta?.LockVisible ?? false,
                DictCode = meta?.DictCode,
                Options = await ResolveOptionsAsync(meta)
            });
        }

        return new PageColumnDto { PageCode = pageCode, Columns = columns.OrderBy(x => x.Sort).ToList() };
    }

    public async Task SaveColumnsAsync(string pageCode, List<ColumnSettingDto> columns)
    {
        var access = await fields.GetAsync(pageCode);
        var visible = access.Where(x => x.CanView).Select(x => x.Field).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var metas = await db.Queryable<SysField>().Where(f => f.PageCode == pageCode).ToListAsync();
        var locks = metas.Where(x => x.LockVisible).Select(x => x.FieldName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        await db.Deleteable<SysUserColumn>().Where(x => x.UserId == current.UserId && x.PageCode == pageCode).ExecuteCommandAsync();
        var rows = new List<SysUserColumn>();
        foreach (var column in columns)
        {
            if (!visible.Contains(column.Field))
            {
                throw new BusinessException(await l10n.T("error.field.denied", "包含无权查看的字段"));
            }

            var fix = column.Fixed is "left" or "right" ? column.Fixed : null;
            var order = column.SortOrder is "asc" or "desc" ? column.SortOrder : null;
            rows.Add(new SysUserColumn
            {
                TenantId = current.TenantId,
                UserId = current.UserId,
                PageCode = pageCode,
                FieldName = column.Field,
                Visible = locks.Contains(column.Field) || column.Visible,
                Sort = Math.Clamp(column.Sort, 0, 9999),
                Width = column.Width is >= 40 and <= 800 ? column.Width : null,
                Fixed = fix,
                SortOrder = order,
                SortRank = order == null ? null : Math.Clamp(column.SortRank ?? 1, 1, 5)
            });
        }

        var ranked = rows.Where(x => x.SortOrder != null).OrderBy(x => x.SortRank).ThenBy(x => x.FieldName).ToList();
        for (var i = 0; i < ranked.Count; i++)
        {
            if (i >= 5)
            {
                ranked[i].SortOrder = null;
                ranked[i].SortRank = null;
            }
            else
            {
                ranked[i].SortRank = i + 1;
            }
        }

        if (rows.Count > 0)
        {
            await db.Insertable(rows).ExecuteCommandAsync();
        }
    }

    public async Task<List<FieldPageDto>> FieldsAsync()
    {
        var fields = await db.Queryable<SysField>().OrderBy(f => f.PageCode).OrderBy(f => f.Sort).ToListAsync();
        var list = new List<FieldPageDto>();
        foreach (var group in fields.GroupBy(x => x.PageCode))
        {
            var page = new FieldPageDto
            {
                PageCode = group.Key,
                PageName = await l10n.T($"page.{group.Key.Replace(':', '.')}", group.Key)
            };
            foreach (var field in group)
            {
                page.Fields.Add(new FieldItemDto
                {
                    Id = field.Id,
                    Field = field.FieldName,
                    Label = await l10n.T(field.I18nKey, field.DisplayName),
                    ValueType = field.ValueType
                });
            }

            list.Add(page);
        }

        return list;
    }

    private async Task<List<OptionDto>> ResolveOptionsAsync(SysField? meta)
    {
        if (meta == null)
        {
            return [];
        }

        if (!string.IsNullOrWhiteSpace(meta.RelationTable) && !string.IsNullOrWhiteSpace(meta.RelationValue) && !string.IsNullOrWhiteSpace(meta.RelationLabel))
        {
            return await LookupAsync(meta.RelationTable, meta.RelationValue, meta.RelationLabel);
        }

        if (!string.IsNullOrWhiteSpace(meta.OptionJson))
        {
            try
            {
                return JsonSerializer.Deserialize<List<OptionDto>>(meta.OptionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        return await OptionsAsync(meta.DictCode);
    }

    private async Task<List<OptionDto>> LookupAsync(string table, string value, string label)
    {
        if (!IsName(table) || !IsName(value) || !IsName(label))
        {
            return [];
        }

        var columns = db.DbMaintenance.GetColumnInfosByTableName(table, false);
        if (columns == null
            || !columns.Any(column => column.DbColumnName.Equals(value, StringComparison.OrdinalIgnoreCase))
            || !columns.Any(column => column.DbColumnName.Equals(label, StringComparison.OrdinalIgnoreCase)))
        {
            return [];
        }

        var where = new List<string>();
        var parameters = new List<SugarParameter>();
        if (columns.Any(column => column.DbColumnName.Equals("IsDeleted", StringComparison.OrdinalIgnoreCase)))
        {
            where.Add("IsDeleted = 0");
        }

        if (columns.Any(column => column.DbColumnName.Equals("TenantId", StringComparison.OrdinalIgnoreCase)) && current.TenantId > 0)
        {
            where.Add("TenantId = @tenantId");
            parameters.Add(new SugarParameter("@tenantId", current.TenantId));
        }

        var sql = $"SELECT TOP 500 CAST([{value}] AS NVARCHAR(200)) AS [Value], CAST([{label}] AS NVARCHAR(200)) AS [Label] FROM [{table}]";
        if (where.Count > 0)
        {
            sql += " WHERE " + string.Join(" AND ", where);
        }

        sql += $" ORDER BY [{label}]";
        return await db.Ado.SqlQueryAsync<OptionDto>(sql, parameters);
    }

    private static bool IsName(string name) => System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$");

    private async Task<List<OptionDto>> OptionsAsync(string? dict)
    {
        return dict switch
        {
            "status" =>
            [
                new OptionDto { Value = nameof(CommonStatus.Enabled), Label = await l10n.T("dict.status.enabled", "启用") },
                new OptionDto { Value = nameof(CommonStatus.Disabled), Label = await l10n.T("dict.status.disabled", "停用") }
            ],
            "dataScope" =>
            [
                new OptionDto { Value = nameof(DataScope.All), Label = await l10n.T("dict.scope.all", "全部") },
                new OptionDto { Value = nameof(DataScope.DeptAndChild), Label = await l10n.T("dict.scope.deptAndChild", "本部门及下级") },
                new OptionDto { Value = nameof(DataScope.Dept), Label = await l10n.T("dict.scope.dept", "本部门") },
                new OptionDto { Value = nameof(DataScope.Self), Label = await l10n.T("dict.scope.self", "仅本人") },
                new OptionDto { Value = nameof(DataScope.Custom), Label = await l10n.T("dict.scope.custom", "自定义") }
            ],
            "menuType" =>
            [
                new OptionDto { Value = nameof(MenuType.Directory), Label = await l10n.T("dict.menu.directory", "目录") },
                new OptionDto { Value = nameof(MenuType.Menu), Label = await l10n.T("dict.menu.menu", "菜单") },
                new OptionDto { Value = nameof(MenuType.Button), Label = await l10n.T("dict.menu.button", "按钮") }
            ],
            "gender" =>
            [
                new OptionDto { Value = "Male", Label = await l10n.T("dict.gender.male", "男") },
                new OptionDto { Value = "Female", Label = await l10n.T("dict.gender.female", "女") },
                new OptionDto { Value = "Unknown", Label = await l10n.T("dict.gender.unknown", "未知") }
            ],
            "bool" =>
            [
                new OptionDto { Value = "true", Label = await l10n.T("dict.bool.yes", "是") },
                new OptionDto { Value = "false", Label = await l10n.T("dict.bool.no", "否") }
            ],
            _ => []
        };
    }
}

public sealed class DashboardService(ISqlSugarClient db, ICurrentUser current, IDataScopeService scopes) : IDashboardService
{
    public async Task<DashboardDto> SummaryAsync()
    {
        var scope = await scopes.GetAsync();
        var users = db.Queryable<SysUser>();
        var depts = db.Queryable<SysDept>();
        if (!scope.All)
        {
            var ids = scope.DeptIds;
            var self = current.UserId;
            users = ids.Count > 0 && scope.IncludeSelf
                ? users.Where(u => (u.DeptId != null && ids.Contains(u.DeptId.Value)) || u.CreateUserId == self)
                : ids.Count > 0
                    ? users.Where(u => u.DeptId != null && ids.Contains(u.DeptId.Value))
                    : users.Where(u => u.CreateUserId == self);
            depts = ids.Count > 0
                ? depts.Where(d => ids.Contains(d.Id))
                : current.DeptId is long deptId
                    ? depts.Where(d => d.Id == deptId)
                    : depts.Where(d => false);
        }

        return new DashboardDto
        {
            UserCount = await users.CountAsync(),
            DeptCount = await depts.CountAsync(),
            RoleCount = await db.Queryable<SysRole>().CountAsync(),
            TenantCount = current.IsHostAdmin
                ? await db.Queryable<SysTenant>().CountAsync()
                : await db.Queryable<SysTenant>().Where(t => t.Id == current.TenantId).CountAsync()
        };
    }
}
