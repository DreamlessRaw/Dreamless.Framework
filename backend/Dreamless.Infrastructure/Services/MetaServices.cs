namespace Dreamless.Infrastructure.Services;

public sealed class MenuService(ISqlSugarClient db, ILocalizationService l10n, IPermissionService permissions, IExcelService excel) : IMenuService
{
    public async Task<List<MenuNodeDto>> TreeAsync(bool includeButtons)
    {
        var rows = await db.Queryable<SysMenu>().OrderBy(m => m.Sort).OrderBy(m => m.Id).ToListAsync();
        if (!includeButtons)
        {
            rows = rows.Where(m => m.Type != MenuType.Button).ToList();
        }

        var nodes = new List<MenuNodeDto>();
        foreach (var row in rows)
        {
            nodes.Add(await ToNode(row));
        }

        return Build(nodes, 0);
    }

    public async Task<long> CreateAsync(MenuSaveDto dto)
    {
        await EnsureCode(dto.Code, 0);
        var entity = new SysMenu();
        AppMap.Copy(dto, entity, null);
        entity.Code = dto.Code.Trim();
        entity.Name = dto.Name.Trim();
        var id = await db.Insertable(entity).ExecuteReturnBigIdentityAsync();
        permissions.Invalidate();
        return id;
    }

    public async Task UpdateAsync(long id, MenuSaveDto dto)
    {
        var entity = await db.Queryable<SysMenu>().FirstAsync(m => m.Id == id)
            ?? throw new BusinessException(await l10n.T("error.notFound", "数据不存在"), 404);
        await EnsureCode(dto.Code, id);
        AppMap.Copy(dto, entity, null);
        entity.Code = dto.Code.Trim();
        entity.Name = dto.Name.Trim();
        entity.ParentId = dto.ParentId;
        await db.Updateable(entity).ExecuteCommandAsync();
        permissions.Invalidate();
    }

    public async Task DeleteAsync(long id)
    {
        if (await db.Queryable<SysMenu>().AnyAsync(m => m.ParentId == id))
        {
            throw new BusinessException(await l10n.T("error.menu.hasChildren", "请先删除子菜单"));
        }

        var entity = await db.Queryable<SysMenu>().FirstAsync(m => m.Id == id)
            ?? throw new BusinessException(await l10n.T("error.notFound", "数据不存在"), 404);
        entity.IsDeleted = true;
        await db.Updateable(entity).UpdateColumns(m => new { m.IsDeleted, m.UpdateTime, m.UpdateUserId }).ExecuteCommandAsync();
        await db.Deleteable<SysRoleMenu>().Where(x => x.MenuId == id).ExecuteCommandAsync();
        permissions.Invalidate();
    }

    public async Task<byte[]> ExportAsync()
    {
        var rows = await db.Queryable<SysMenu>().OrderBy(m => m.Sort).ToListAsync();
        var codes = rows.ToDictionary(x => x.Id, x => x.Code);
        var dtos = rows.Select(row => new MenuExportDto
        {
            Code = row.Code,
            Name = row.Name,
            ParentCode = row.ParentId > 0 && codes.TryGetValue(row.ParentId, out var code) ? code : null,
            Type = row.Type,
            Permission = row.Permission,
            Path = row.Path,
            Component = row.Component,
            Icon = row.Icon,
            Sort = row.Sort,
            Visible = row.Visible
        }).ToList();
        return await excel.ExportAsync(dtos, "system:menu");
    }

    public Task<byte[]> TemplateAsync() => excel.TemplateAsync<MenuImportDto>("system:menu");

    public async Task<ImportResultDto> ImportAsync(Stream stream)
    {
        var read = await excel.ReadAsync<MenuImportDto>(stream, "system:menu");
        if (read.Errors.Count > 0)
        {
            return new ImportResultDto { Failed = read.Errors.Count, Errors = read.Errors };
        }

        var success = 0;
        foreach (var row in read.Rows.OrderBy(x => x.ParentCode == null ? 0 : 1))
        {
            var parentId = 0L;
            if (!string.IsNullOrWhiteSpace(row.ParentCode))
            {
                var parent = await db.Queryable<SysMenu>().FirstAsync(m => m.Code == row.ParentCode)
                    ?? throw new BusinessException($"上级菜单不存在：{row.ParentCode}");
                parentId = parent.Id;
            }

            var dto = new MenuSaveDto
            {
                Code = row.Code,
                Name = row.Name,
                ParentId = parentId,
                Type = ValueParser.Menu(row.Type),
                Permission = row.Permission,
                Path = row.Path,
                Component = row.Component,
                Icon = row.Icon,
                Sort = row.Sort,
                Visible = row.Visible,
                Status = CommonStatus.Enabled
            };
            var existing = await db.Queryable<SysMenu>().FirstAsync(m => m.Code == row.Code);
            if (existing == null) await CreateAsync(dto);
            else await UpdateAsync(existing.Id, dto);
            success++;
        }

        return new ImportResultDto { Success = success };
    }

    public async Task<List<MenuNodeDto>> ProfileTreeAsync(long userId, bool isHostAdmin)
    {
        var codes = await permissions.GetCodesAsync(userId, isHostAdmin);
        var rows = await db.Queryable<SysMenu>()
            .Where(m => m.Status == CommonStatus.Enabled && m.Visible && m.Type != MenuType.Button)
            .OrderBy(m => m.Sort)
            .ToListAsync();
        return await Prune(rows, codes, isHostAdmin, 0);
    }

    private async Task<List<MenuNodeDto>> Prune(List<SysMenu> rows, HashSet<string> codes, bool isHost, long parentId)
    {
        var list = new List<MenuNodeDto>();
        foreach (var row in rows.Where(x => x.ParentId == parentId))
        {
            var children = await Prune(rows, codes, isHost, row.Id);
            var allowed = isHost || string.IsNullOrWhiteSpace(row.Permission) || codes.Contains(row.Permission);
            if (row.Type == MenuType.Directory && children.Count == 0)
            {
                continue;
            }

            if (row.Type == MenuType.Menu && !allowed)
            {
                continue;
            }

            var node = await ToNode(row);
            node.Children = children;
            list.Add(node);
        }

        return list;
    }

    private async Task<MenuNodeDto> ToNode(SysMenu row) => new()
    {
        Id = row.Id,
        ParentId = row.ParentId,
        Code = row.Code,
        Name = await l10n.T(row.I18nKey ?? row.Code, row.Name),
        I18nKey = row.I18nKey,
        Path = row.Path,
        Component = row.Component,
        Icon = row.Icon,
        Sort = row.Sort,
        Type = row.Type,
        Permission = row.Permission,
        Visible = row.Visible,
        Status = row.Status
    };

    private static List<MenuNodeDto> Build(List<MenuNodeDto> nodes, long parentId)
    {
        return nodes.Where(x => x.ParentId == parentId).OrderBy(x => x.Sort).Select(node =>
        {
            node.Children = Build(nodes, node.Id);
            return node;
        }).ToList();
    }

    private async Task EnsureCode(string code, long id)
    {
        if (await db.Queryable<SysMenu>().AnyAsync(m => m.Code == code && m.Id != id))
        {
            throw new BusinessException(await l10n.T("error.menu.exists", "菜单编码已存在"));
        }
    }
}
