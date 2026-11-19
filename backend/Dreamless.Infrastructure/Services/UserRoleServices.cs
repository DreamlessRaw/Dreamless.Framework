using Microsoft.Extensions.Hosting;

namespace Dreamless.Infrastructure.Services;

public sealed class UserService(
    ISqlSugarClient db,
    ICurrentUser current,
    IDataScopeService scopes,
    IFieldAccessService fields,
    IExcelService excel,
    ILocalizationService l10n,
    PasswordService passwords,
    IPermissionService permissions,
    IHostEnvironment environment) : IUserService
{
    private const string Page = "system:user";

    public async Task<PageResult<UserListDto>> ListAsync(UserQuery query)
    {
        var q = await Scoped();
        q = q.WhereIF(query.Status != null, u => u.Status == query.Status);
        q = q.WhereIF(query.DeptId != null, u => u.DeptId == query.DeptId);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(u => u.UserName.Contains(keyword) || u.NickName.Contains(keyword) || (u.EmployeeNo != null && u.EmployeeNo.Contains(keyword)) || (u.Phone != null && u.Phone.Contains(keyword)) || (u.Email != null && u.Email.Contains(keyword)));
        }

        q = QueryFilter.Apply(q, query.FilterClauses, UserFilter);
        var total = await q.CountAsync();
        q = query.SortClauses.Count == 0
            ? q.OrderBy(u => u.Id, OrderByType.Desc)
            : QuerySort.Apply(q, query.SortClauses, UserSort);
        var rows = await q.Skip((query.NormalizedPage - 1) * query.NormalizedSize)
            .Take(query.NormalizedSize)
            .ToListAsync();
        return new PageResult<UserListDto> { Total = total, Items = await MapList(rows) };
    }

    public async Task<UserListDto> GetAsync(long id)
    {
        var user = await (await Scoped()).FirstAsync(u => u.Id == id) ?? throw await NotFound();
        return (await MapList([user])).First();
    }

    public async Task<long> CreateAsync(UserSaveDto dto)
    {
        EnsurePassword(dto.Password, required: true);
        await EnsureUserName(dto.UserName, 0);
        await EnsureDept(dto.DeptId);
        await EnsurePosition(dto.PositionId);
        var entity = new SysUser { TenantId = current.TenantId, Status = CommonStatus.Enabled };
        await Copy(dto, entity);
        entity.UserName = dto.UserName.Trim();
        entity.NickName = string.IsNullOrWhiteSpace(entity.NickName) ? dto.NickName.Trim() : entity.NickName;
        entity.PasswordHash = passwords.Hash(entity, dto.Password!);
        var id = await db.Insertable(entity).ExecuteReturnBigIdentityAsync();
        await SaveRoles(id, dto.RoleIds);
        permissions.Invalidate();
        return id;
    }

    public async Task UpdateAsync(long id, UserSaveDto dto)
    {
        var entity = await (await Scoped()).FirstAsync(u => u.Id == id) ?? throw await NotFound();
        if (entity.Id == current.UserId && dto.Status == CommonStatus.Disabled)
        {
            throw new BusinessException(await l10n.T("error.user.self", "不能停用当前登录账号"));
        }

        await EnsureUserName(dto.UserName, id);
        await EnsureDept(dto.DeptId);
        await EnsurePosition(dto.PositionId);
        await Copy(dto, entity);
        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            EnsurePassword(dto.Password, required: true);
            entity.PasswordHash = passwords.Hash(entity, dto.Password);
        }

        await db.Updateable(entity).ExecuteCommandAsync();
        await SaveRoles(id, dto.RoleIds);
        permissions.Invalidate();
    }

    public async Task ResetPasswordAsync(long id, string password)
    {
        EnsurePassword(password, required: true);
        var entity = await (await Scoped()).FirstAsync(u => u.Id == id) ?? throw await NotFound();
        entity.PasswordHash = passwords.Hash(entity, password);
        await db.Updateable(entity).UpdateColumns(u => new { u.PasswordHash, u.UpdateTime, u.UpdateUserId }).ExecuteCommandAsync();
    }

    public async Task DeleteAsync(long id)
    {
        var entity = await (await Scoped()).FirstAsync(u => u.Id == id) ?? throw await NotFound();
        if (entity.Id == current.UserId)
        {
            throw new BusinessException(await l10n.T("error.cannotDeleteSelf", "不能删除当前登录账号"));
        }

        entity.IsDeleted = true;
        await db.Updateable(entity).UpdateColumns(u => new { u.IsDeleted, u.UpdateTime, u.UpdateUserId }).ExecuteCommandAsync();
        permissions.Invalidate();
    }

    public async Task<byte[]> ExportAsync(UserQuery query)
    {
        query.Page = 1;
        query.PageSize = 200;
        var all = new List<UserListDto>();
        while (all.Count < 20000)
        {
            var page = await ListAsync(query);
            all.AddRange(page.Items);
            if (all.Count >= page.Total || page.Items.Count == 0) break;
            query.Page++;
        }

        return await excel.ExportAsync(all, Page);
    }

    public Task<byte[]> TemplateAsync() => excel.TemplateAsync<UserImportDto>(Page);

    public async Task<ImportResultDto> ImportAsync(Stream stream)
    {
        var read = await excel.ReadAsync<UserImportDto>(stream, Page);
        if (read.Errors.Count > 0)
        {
            return new ImportResultDto { Failed = read.Errors.Count, Errors = read.Errors };
        }

        var success = 0;
        foreach (var row in read.Rows)
        {
            long? deptId = null;
            if (!string.IsNullOrWhiteSpace(row.DeptCode))
            {
                var dept = await db.Queryable<SysDept>().FirstAsync(d => d.Code == row.DeptCode)
                    ?? throw new BusinessException($"部门不存在：{row.DeptCode}");
                deptId = dept.Id;
            }

            long? positionId = null;
            if (!string.IsNullOrWhiteSpace(row.PositionCode))
            {
                var position = await db.Queryable<SysPosition>().FirstAsync(p => p.Code == row.PositionCode)
                    ?? throw new BusinessException($"职位不存在：{row.PositionCode}");
                positionId = position.Id;
            }

            var existing = await db.Queryable<SysUser>().FirstAsync(u => u.UserName == row.UserName);
            var dto = new UserSaveDto
            {
                UserName = row.UserName,
                NickName = row.NickName,
                Password = string.IsNullOrWhiteSpace(row.Password) ? "Import@123456" : row.Password,
                DeptId = deptId,
                PositionId = positionId,
                Avatar = existing?.Avatar,
                EmployeeNo = row.EmployeeNo,
                Gender = ParseGender(row.Gender),
                IdCard = row.IdCard,
                Birthday = ParseDate(row.Birthday),
                HireDate = ParseDate(row.HireDate),
                Address = row.Address,
                Email = row.Email,
                Phone = row.Phone,
                Status = ValueParser.Status(row.Status),
                Remark = row.Remark
            };
            if (existing == null)
            {
                await CreateAsync(dto);
            }
            else
            {
                dto.Password = string.IsNullOrWhiteSpace(row.Password) ? null : row.Password;
                dto.RoleIds = await db.Queryable<SysUserRole>().Where(x => x.UserId == existing.Id).Select(x => x.RoleId).ToListAsync();
                await UpdateAsync(existing.Id, dto);
            }

            success++;
        }

        return new ImportResultDto { Success = success };
    }

    private async Task<ISugarQueryable<SysUser>> Scoped()
    {
        var query = db.Queryable<SysUser>();
        var scope = await scopes.GetAsync();
        if (scope.All)
        {
            return query;
        }

        var ids = scope.DeptIds;
        var self = current.UserId;
        if (ids.Count > 0 && scope.IncludeSelf)
        {
            return query.Where(u => (u.DeptId != null && ids.Contains(u.DeptId.Value)) || u.CreateUserId == self);
        }

        if (ids.Count > 0)
        {
            return query.Where(u => u.DeptId != null && ids.Contains(u.DeptId.Value));
        }

        return query.Where(u => u.CreateUserId == self);
    }

    private static ISugarQueryable<SysUser>? UserSort(ISugarQueryable<SysUser> query, SortClause sort)
    {
        var type = sort.Descending ? OrderByType.Desc : OrderByType.Asc;
        if (sort.Field.Equals("deptName", StringComparison.OrdinalIgnoreCase))
        {
            return query.OrderBy(u => SqlFunc.Subqueryable<SysDept>().Where(d => d.Id == u.DeptId).Select(d => d.Name), type);
        }

        if (sort.Field.Equals("positionName", StringComparison.OrdinalIgnoreCase))
        {
            return query.OrderBy(u => SqlFunc.Subqueryable<SysPosition>().Where(p => p.Id == u.PositionId).Select(p => p.Name), type);
        }

        return null;
    }

    private static ISugarQueryable<SysUser>? UserFilter(ISugarQueryable<SysUser> query, FilterClause filter)
    {
        if (string.IsNullOrWhiteSpace(filter.Value))
        {
            return null;
        }

        if (filter.Field.Equals("deptName", StringComparison.OrdinalIgnoreCase))
        {
            var value = filter.Value;
            return query.Where(u => SqlFunc.Subqueryable<SysDept>().Where(d => d.Id == u.DeptId && d.Name.Contains(value)).Any());
        }

        if (filter.Field.Equals("positionName", StringComparison.OrdinalIgnoreCase))
        {
            var value = filter.Value;
            return query.Where(u => SqlFunc.Subqueryable<SysPosition>().Where(p => p.Id == u.PositionId && p.Name.Contains(value)).Any());
        }

        return null;
    }

    private async Task<List<UserListDto>> MapList(List<SysUser> rows)
    {
        var deptIds = rows.Where(x => x.DeptId != null).Select(x => x.DeptId!.Value).Distinct().ToList();
        var depts = deptIds.Count == 0
            ? new Dictionary<long, string>()
            : (await db.Queryable<SysDept>().Where(d => deptIds.Contains(d.Id)).ToListAsync()).ToDictionary(d => d.Id, d => d.Name);
        var positionIds = rows.Where(x => x.PositionId != null).Select(x => x.PositionId!.Value).Distinct().ToList();
        var positions = positionIds.Count == 0
            ? new Dictionary<long, string>()
            : (await db.Queryable<SysPosition>().Where(p => positionIds.Contains(p.Id)).ToListAsync()).ToDictionary(p => p.Id, p => p.Name);
        var userIds = rows.Select(x => x.Id).ToList();
        var links = userIds.Count == 0
            ? []
            : await db.Queryable<SysUserRole>().Where(x => userIds.Contains(x.UserId)).ToListAsync();
        var access = await fields.GetAsync(Page);
        return rows.Select(row =>
        {
            var dto = new UserListDto
            {
                Id = row.Id,
                TenantId = row.TenantId,
                UserName = row.UserName,
                NickName = row.NickName,
                DeptId = row.DeptId,
                DeptName = row.DeptId is long deptId && depts.TryGetValue(deptId, out var name) ? name : null,
                PositionId = row.PositionId,
                PositionName = row.PositionId is long positionId && positions.TryGetValue(positionId, out var positionName) ? positionName : null,
                Avatar = row.Avatar,
                EmployeeNo = row.EmployeeNo,
                Gender = row.Gender,
                IdCard = row.IdCard,
                Birthday = row.Birthday,
                HireDate = row.HireDate,
                Address = row.Address,
                Email = row.Email,
                Phone = row.Phone,
                Status = row.Status,
                IsHostAdmin = row.IsHostAdmin,
                Remark = row.Remark,
                CreateTime = row.CreateTime,
                RoleIds = links.Where(x => x.UserId == row.Id).Select(x => x.RoleId).ToList()
            };
            fields.Mask(dto, access);
            return dto;
        }).ToList();
    }

    private async Task Copy(UserSaveDto dto, SysUser entity)
    {
        var access = await fields.GetAsync(Page);
        fields.CopyEditable(dto, entity, access);
        entity.TenantId = current.TenantId;
    }

    private async Task SaveRoles(long userId, List<long> roleIds)
    {
        var distinct = roleIds.Distinct().ToList();
        if (distinct.Count > 0)
        {
            var count = await db.Queryable<SysRole>().Where(r => distinct.Contains(r.Id)).CountAsync();
            if (count != distinct.Count)
            {
                throw new BusinessException(await l10n.T("error.role.missing", "角色不存在"));
            }
        }

        await db.Deleteable<SysUserRole>().Where(x => x.UserId == userId).ExecuteCommandAsync();
        if (distinct.Count == 0)
        {
            return;
        }

        await db.Insertable(distinct.Select(id => new SysUserRole
        {
            UserId = userId,
            RoleId = id,
            TenantId = current.TenantId
        }).ToList()).ExecuteCommandAsync();
    }

    private async Task EnsureUserName(string userName, long id)
    {
        if (await db.Queryable<SysUser>().AnyAsync(u => u.UserName == userName && u.Id != id))
        {
            throw new BusinessException(await l10n.T("error.user.exists", "用户名已存在"));
        }
    }

    public async Task<string> SaveAvatarAsync(Stream stream, string fileName, string? contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var allowed = new HashSet<string> { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg" };
        if (!allowed.Contains(extension) || contentType is not null && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && contentType != "application/octet-stream")
        {
            throw new BusinessException(await l10n.T("error.avatar.type", "请上传图片文件"));
        }

        var directory = Path.Combine(environment.ContentRootPath, "App_Data", "avatars");
        Directory.CreateDirectory(directory);
        var stored = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(directory, stored);
        await using var output = File.Create(path);
        await stream.CopyToAsync(output);
        if (output.Length > 2 * 1024 * 1024)
        {
            output.Close();
            File.Delete(path);
            throw new BusinessException(await l10n.T("error.avatar.size", "头像不能超过 2MB"));
        }

        return $"/api/files/avatars/{stored}";
    }

    private static string? ParseGender(string? value) => value?.Trim() switch
    {
        "男" or "Male" or "male" => "Male",
        "女" or "Female" or "female" => "Female",
        "未知" or "Unknown" or "unknown" => "Unknown",
        null or "" => null,
        _ => value.Trim()
    };

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, out var date) ? date.Date : null;

    private async Task EnsurePosition(long? positionId)
    {
        if (positionId == null)
        {
            return;
        }

        if (!await db.Queryable<SysPosition>().AnyAsync(p => p.Id == positionId))
        {
            throw new BusinessException(await l10n.T("error.position.missing", "职位不存在"));
        }
    }

    private async Task EnsureDept(long? deptId)
    {
        if (deptId == null)
        {
            return;
        }

        if (!await db.Queryable<SysDept>().AnyAsync(d => d.Id == deptId))
        {
            throw new BusinessException(await l10n.T("error.dept.missing", "部门不存在"));
        }
    }

    private static void EnsurePassword(string? password, bool required)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            if (required) throw new BusinessException("密码至少 6 位");
            return;
        }

        if (password.Length < 6)
        {
            throw new BusinessException("密码至少 6 位");
        }
    }

    private async Task<BusinessException> NotFound() => new(await l10n.T("error.notFound", "数据不存在"), 404);
}

public sealed class RoleService(
    ISqlSugarClient db,
    ICurrentUser current,
    IExcelService excel,
    IFieldAccessService fields,
    ILocalizationService l10n,
    IPermissionService permissions) : IRoleService
{
    private const string Page = "system:role";

    public async Task<PageResult<RoleListDto>> ListAsync(RoleQuery query)
    {
        var q = db.Queryable<SysRole>();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(r => r.Name.Contains(keyword) || r.Code.Contains(keyword));
        }

        q = QueryFilter.Apply(q, query.FilterClauses);
        q = q.WhereIF(query.Status != null, r => r.Status == query.Status);
        var total = await q.CountAsync();
        q = query.SortClauses.Count == 0
            ? q.OrderBy(r => r.Sort).OrderBy(r => r.Id)
            : QuerySort.Apply(q, query.SortClauses);
        var rows = await q
            .Skip((query.NormalizedPage - 1) * query.NormalizedSize)
            .Take(query.NormalizedSize)
            .ToListAsync();
        var access = await fields.GetAsync(Page);
        var items = rows.Select(row =>
        {
            var dto = Map(row);
            fields.Mask(dto, access);
            return dto;
        }).ToList();
        return new PageResult<RoleListDto> { Total = total, Items = items };
    }

    public async Task<RoleListDto> GetAsync(long id)
    {
        var role = await db.Queryable<SysRole>().FirstAsync(r => r.Id == id) ?? throw await NotFound();
        var dto = Map(role);
        dto.MenuIds = await db.Queryable<SysRoleMenu>().Where(x => x.RoleId == id).Select(x => x.MenuId).ToListAsync();
        dto.DeptIds = await db.Queryable<SysRoleDept>().Where(x => x.RoleId == id).Select(x => x.DeptId).ToListAsync();
        dto.Fields = await db.Queryable<SysRoleField>().Where(x => x.RoleId == id)
            .Select(x => new RoleFieldGrantDto { FieldId = x.FieldId, CanView = x.CanView, CanEdit = x.CanEdit, CanExport = x.CanExport })
            .ToListAsync();
        dto.FieldConfigured = dto.Fields.Count > 0;
        fields.Mask(dto, await fields.GetAsync(Page));
        return dto;
    }

    public async Task<long> CreateAsync(RoleSaveDto dto)
    {
        await EnsureCode(dto.Code, 0);
        var entity = new SysRole { TenantId = current.TenantId };
        await Copy(dto, entity);
        var id = await db.Insertable(entity).ExecuteReturnBigIdentityAsync();
        await SaveGrants(id, dto);
        return id;
    }

    public async Task UpdateAsync(long id, RoleSaveDto dto)
    {
        var entity = await db.Queryable<SysRole>().FirstAsync(r => r.Id == id) ?? throw await NotFound();
        if (entity.Code == "SuperAdmin" && dto.Code != "SuperAdmin")
        {
            throw new BusinessException(await l10n.T("error.role.protected", "内置角色不能修改编码"));
        }

        await EnsureCode(dto.Code, id);
        await Copy(dto, entity);
        await db.Updateable(entity).ExecuteCommandAsync();
        await SaveGrants(id, dto);
    }

    public async Task DeleteAsync(long id)
    {
        var entity = await db.Queryable<SysRole>().FirstAsync(r => r.Id == id) ?? throw await NotFound();
        if (entity.Code is "SuperAdmin" or "Clerk")
        {
            throw new BusinessException(await l10n.T("error.role.protected", "内置角色不能删除"));
        }

        if (await db.Queryable<SysUserRole>().AnyAsync(x => x.RoleId == id))
        {
            throw new BusinessException(await l10n.T("error.role.inUse", "角色仍分配给用户"));
        }

        entity.IsDeleted = true;
        await db.Updateable(entity).UpdateColumns(r => new { r.IsDeleted, r.UpdateTime, r.UpdateUserId }).ExecuteCommandAsync();
        permissions.Invalidate();
    }

    public async Task<byte[]> ExportAsync(RoleQuery query)
    {
        query.Page = 1;
        query.PageSize = 200;
        var all = new List<RoleListDto>();
        while (true)
        {
            var page = await ListAsync(query);
            all.AddRange(page.Items);
            if (all.Count >= page.Total || page.Items.Count == 0) break;
            query.Page++;
        }

        return await excel.ExportAsync(all, Page);
    }

    public Task<byte[]> TemplateAsync() => excel.TemplateAsync<RoleImportDto>(Page);

    public async Task<ImportResultDto> ImportAsync(Stream stream)
    {
        var read = await excel.ReadAsync<RoleImportDto>(stream, Page);
        if (read.Errors.Count > 0) return new ImportResultDto { Failed = read.Errors.Count, Errors = read.Errors };
        var success = 0;
        foreach (var row in read.Rows)
        {
            var existing = await db.Queryable<SysRole>().FirstAsync(r => r.Code == row.Code);
            var dto = new RoleSaveDto
            {
                Name = row.Name,
                Code = row.Code,
                DataScope = ValueParser.Scope(row.DataScope),
                Status = ValueParser.Status(row.Status),
                Remark = row.Remark,
                MenuIds = existing == null ? [] : await db.Queryable<SysRoleMenu>().Where(x => x.RoleId == existing.Id).Select(x => x.MenuId).ToListAsync(),
                DeptIds = existing == null ? [] : await db.Queryable<SysRoleDept>().Where(x => x.RoleId == existing.Id).Select(x => x.DeptId).ToListAsync(),
                Fields = existing == null ? [] : await db.Queryable<SysRoleField>().Where(x => x.RoleId == existing.Id)
                    .Select(x => new RoleFieldGrantDto { FieldId = x.FieldId, CanView = x.CanView, CanEdit = x.CanEdit, CanExport = x.CanExport }).ToListAsync()
            };
            if (existing == null) await CreateAsync(dto);
            else await UpdateAsync(existing.Id, dto);
            success++;
        }

        return new ImportResultDto { Success = success };
    }

    private async Task Copy(RoleSaveDto dto, SysRole entity)
    {
        var access = await fields.GetAsync(Page);
        fields.CopyEditable(dto, entity, access);
        entity.TenantId = current.TenantId;
        entity.DataScope = dto.DataScope;
        if (string.IsNullOrWhiteSpace(entity.Name)) entity.Name = dto.Name.Trim();
        if (string.IsNullOrWhiteSpace(entity.Code)) entity.Code = dto.Code.Trim();
    }

    private async Task SaveGrants(long roleId, RoleSaveDto dto)
    {
        var menuIds = await WithAncestors(dto.MenuIds);
        await db.Deleteable<SysRoleMenu>().Where(x => x.RoleId == roleId).ExecuteCommandAsync();
        if (menuIds.Count > 0)
        {
            await db.Insertable(menuIds.Select(id => new SysRoleMenu { RoleId = roleId, MenuId = id }).ToList()).ExecuteCommandAsync();
        }

        await db.Deleteable<SysRoleDept>().Where(x => x.RoleId == roleId).ExecuteCommandAsync();
        if (dto.DataScope == DataScope.Custom && dto.DeptIds.Count > 0)
        {
            await db.Insertable(dto.DeptIds.Distinct().Select(id => new SysRoleDept { RoleId = roleId, DeptId = id }).ToList()).ExecuteCommandAsync();
        }

        await db.Deleteable<SysRoleField>().Where(x => x.RoleId == roleId).ExecuteCommandAsync();
        if (dto.Fields.Count > 0)
        {
            await db.Insertable(dto.Fields.Select(field => new SysRoleField
            {
                RoleId = roleId,
                FieldId = field.FieldId,
                CanView = field.CanView,
                CanEdit = field.CanView && field.CanEdit,
                CanExport = field.CanView && field.CanExport
            }).ToList()).ExecuteCommandAsync();
        }

        permissions.Invalidate();
    }

    private async Task<List<long>> WithAncestors(List<long> menuIds)
    {
        var ids = menuIds.Distinct().ToHashSet();
        if (ids.Count == 0) return [];
        var all = await db.Queryable<SysMenu>().Select(m => new { m.Id, m.ParentId }).ToListAsync();
        var map = all.ToDictionary(x => x.Id, x => x.ParentId);
        foreach (var id in menuIds.ToList())
        {
            var currentId = id;
            while (map.TryGetValue(currentId, out var parent) && parent > 0 && ids.Add(parent))
            {
                currentId = parent;
            }
        }

        return ids.ToList();
    }

    private static RoleListDto Map(SysRole row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Code = row.Code,
        Sort = row.Sort,
        DataScope = row.DataScope,
        Status = row.Status,
        Remark = row.Remark
    };

    private async Task EnsureCode(string code, long id)
    {
        if (await db.Queryable<SysRole>().AnyAsync(r => r.Code == code && r.Id != id))
        {
            throw new BusinessException(await l10n.T("error.role.exists", "角色编码已存在"));
        }
    }

    private async Task<BusinessException> NotFound() => new(await l10n.T("error.notFound", "数据不存在"), 404);
}
