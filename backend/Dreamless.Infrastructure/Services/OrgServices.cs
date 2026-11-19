using Dreamless.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace Dreamless.Infrastructure.Services;

public sealed class AuthService(
    ISqlSugarClient db,
    ICurrentUser current,
    PasswordService passwords,
    JwtTokenService tokens,
    IOptions<JwtOptions> jwtOptions,
    IPermissionService permissions,
    IMenuService menus,
    ILocalizationService l10n) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var tenantCode = string.IsNullOrWhiteSpace(request.TenantCode) ? "default" : request.TenantCode.Trim();
        var tenant = await db.Queryable<SysTenant>().FirstAsync(t => t.Code == tenantCode && t.Status == CommonStatus.Enabled);
        var user = tenant == null
            ? null
            : await db.Queryable<SysUser>().FirstAsync(u => u.TenantId == tenant.Id && u.UserName == request.UserName);
        if (tenant == null || user == null || user.Status != CommonStatus.Enabled || !passwords.Verify(user, request.Password))
        {
            throw new BusinessException(await l10n.T("error.login", "账号或密码错误"));
        }

        return await IssueAsync(user, tenant);
    }

    public async Task<LoginResponse> RefreshAsync(string refreshToken)
    {
        var hash = JwtTokenService.Hash(refreshToken);
        var stored = await db.Queryable<SysRefreshToken>().FirstAsync(x => x.TokenHash == hash && !x.Revoked);
        if (stored == null || stored.ExpireTime < DateTime.Now)
        {
            throw new BusinessException(await l10n.T("error.unauthorized", "未登录或登录已过期"), 401);
        }

        var user = await db.Queryable<SysUser>().FirstAsync(u => u.Id == stored.UserId && u.Status == CommonStatus.Enabled);
        var tenant = user == null ? null : await db.Queryable<SysTenant>().FirstAsync(t => t.Id == user.TenantId && t.Status == CommonStatus.Enabled);
        if (user == null || tenant == null)
        {
            throw new BusinessException(await l10n.T("error.unauthorized", "未登录或登录已过期"), 401);
        }

        stored.Revoked = true;
        await db.Updateable(stored).UpdateColumns(x => x.Revoked).ExecuteCommandAsync();
        return await IssueAsync(user, tenant);
    }

    public async Task LogoutAsync(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = JwtTokenService.Hash(refreshToken);
        await db.Updateable<SysRefreshToken>()
            .SetColumns(x => x.Revoked == true)
            .Where(x => x.TokenHash == hash)
            .ExecuteCommandAsync();
    }

    public async Task<ProfileDto> ProfileAsync()
    {
        var user = await db.Queryable<SysUser>().FirstAsync(u => u.Id == current.UserId)
            ?? throw new BusinessException(await l10n.T("error.unauthorized", "未登录或登录已过期"), 401);
        var tenant = await db.Queryable<SysTenant>().FirstAsync(t => t.Id == user.TenantId);
        return await BuildProfile(user, tenant);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request)
    {
        var user = await db.Queryable<SysUser>().FirstAsync(u => u.Id == current.UserId)
            ?? throw new BusinessException(await l10n.T("error.notFound", "数据不存在"), 404);
        if (!passwords.Verify(user, request.OldPassword))
        {
            throw new BusinessException(await l10n.T("error.password.old", "原密码不正确"));
        }

        EnsurePassword(request.NewPassword);
        user.PasswordHash = passwords.Hash(user, request.NewPassword);
        await db.Updateable(user).UpdateColumns(u => new { u.PasswordHash, u.UpdateTime, u.UpdateUserId }).ExecuteCommandAsync();
    }

    private async Task<LoginResponse> IssueAsync(SysUser user, SysTenant tenant)
    {
        var (access, expires) = tokens.CreateAccessToken(user);
        var refresh = JwtTokenService.CreateRefreshToken();
        await db.Insertable(new SysRefreshToken
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            TokenHash = JwtTokenService.Hash(refresh),
            ExpireTime = DateTime.Now.AddDays(jwtOptions.Value.RefreshDays),
            CreateTime = DateTime.Now
        }).ExecuteCommandAsync();
        return new LoginResponse
        {
            AccessToken = access,
            RefreshToken = refresh,
            ExpiresAt = expires,
            Profile = await BuildProfile(user, tenant)
        };
    }

    private async Task<ProfileDto> BuildProfile(SysUser user, SysTenant? tenant)
    {
        var deptName = user.DeptId == null
            ? null
            : (await db.Queryable<SysDept>().FirstAsync(d => d.Id == user.DeptId.Value))?.Name;
        return new ProfileDto
        {
            Id = user.Id,
            UserName = user.UserName,
            NickName = user.NickName,
            TenantId = user.TenantId,
            TenantName = tenant?.Name ?? string.Empty,
            DeptId = user.DeptId,
            DeptName = deptName,
            Avatar = user.Avatar,
            IsHostAdmin = user.IsHostAdmin,
            Permissions = (await permissions.GetCodesAsync(user.Id, user.IsHostAdmin)).OrderBy(x => x).ToList(),
            Menus = await menus.ProfileTreeAsync(user.Id, user.IsHostAdmin)
        };
    }

    private static void EnsurePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new BusinessException("密码至少 6 位");
        }
    }
}

public sealed class TenantService(ISqlSugarClient db, ICurrentUser current, IExcelService excel, IFieldAccessService fields, ILocalizationService l10n) : ITenantService
{
    private const string Page = "system:tenant";

    public async Task<PageResult<TenantListDto>> ListAsync(TenantQuery query)
    {
        var q = Filtered();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(t => t.Name.Contains(keyword) || t.Code.Contains(keyword));
        }

        q = QueryFilter.Apply(q, query.FilterClauses);
        q = q.WhereIF(query.Status != null, t => t.Status == query.Status);
        var total = await q.CountAsync();
        q = query.SortClauses.Count == 0
            ? q.OrderBy(t => t.Id, OrderByType.Desc)
            : QuerySort.Apply(q, query.SortClauses);
        var rows = await q
            .Skip((query.NormalizedPage - 1) * query.NormalizedSize)
            .Take(query.NormalizedSize)
            .ToListAsync();
        var access = await fields.GetAsync(Page);
        return new PageResult<TenantListDto>
        {
            Total = total,
            Items = rows.Select(row => Mask(Map(row), access)).ToList()
        };
    }

    public async Task<long> CreateAsync(TenantSaveDto dto)
    {
        EnsureHost();
        await EnsureCode(dto.Code, 0);
        var entity = new SysTenant();
        await Copy(dto, entity);
        return await db.Insertable(entity).ExecuteReturnBigIdentityAsync();
    }

    public async Task UpdateAsync(long id, TenantSaveDto dto)
    {
        var entity = await Filtered().FirstAsync(t => t.Id == id) ?? throw await NotFound();
        if (!current.IsHostAdmin && entity.Id != current.TenantId)
        {
            throw await NotFound();
        }

        await EnsureCode(dto.Code, id);
        await Copy(dto, entity);
        await db.Updateable(entity).ExecuteCommandAsync();
    }

    public async Task DeleteAsync(long id)
    {
        EnsureHost();
        var entity = await db.Queryable<SysTenant>().FirstAsync(t => t.Id == id) ?? throw await NotFound();
        if (entity.Id == current.TenantId || entity.Code == "default")
        {
            throw new BusinessException(await l10n.T("error.tenant.protected", "默认租户不能删除"));
        }

        if (await db.Queryable<SysUser>().AnyAsync(u => u.TenantId == id))
        {
            throw new BusinessException(await l10n.T("error.tenant.inUse", "租户下仍有用户"));
        }

        entity.IsDeleted = true;
        await db.Updateable(entity).UpdateColumns(t => new { t.IsDeleted, t.UpdateTime, t.UpdateUserId }).ExecuteCommandAsync();
    }

    public async Task<byte[]> ExportAsync(TenantQuery query)
    {
        query.PageSize = 200;
        query.Page = 1;
        var all = new List<TenantListDto>();
        while (true)
        {
            var page = await ListAsync(query);
            all.AddRange(page.Items);
            if (all.Count >= page.Total || page.Items.Count == 0)
            {
                break;
            }

            query.Page++;
        }

        return await excel.ExportAsync(all, Page);
    }

    public Task<byte[]> TemplateAsync() => excel.TemplateAsync<TenantImportDto>(Page);

    public async Task<ImportResultDto> ImportAsync(Stream stream)
    {
        EnsureHost();
        var read = await excel.ReadAsync<TenantImportDto>(stream, Page);
        if (read.Errors.Count > 0)
        {
            return new ImportResultDto { Failed = read.Errors.Count, Errors = read.Errors };
        }

        var success = 0;
        foreach (var row in read.Rows)
        {
            var existing = await db.Queryable<SysTenant>().FirstAsync(t => t.Code == row.Code);
            var dto = new TenantSaveDto
            {
                Name = row.Name,
                Code = row.Code,
                Contact = row.Contact,
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
                await UpdateAsync(existing.Id, dto);
            }

            success++;
        }

        return new ImportResultDto { Success = success };
    }

    private ISugarQueryable<SysTenant> Filtered()
    {
        var query = db.Queryable<SysTenant>();
        return current.IsHostAdmin ? query : query.Where(t => t.Id == current.TenantId);
    }

    private async Task Copy(TenantSaveDto dto, SysTenant entity)
    {
        var access = await fields.GetAsync(Page);
        fields.CopyEditable(dto, entity, access);
        if (string.IsNullOrWhiteSpace(entity.Name))
        {
            entity.Name = dto.Name.Trim();
        }

        if (string.IsNullOrWhiteSpace(entity.Code))
        {
            entity.Code = dto.Code.Trim();
        }
    }

    private TenantListDto Mask(TenantListDto dto, IReadOnlyList<FieldAccess> access)
    {
        fields.Mask(dto, access);
        return dto;
    }

    private static TenantListDto Map(SysTenant row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Code = row.Code,
        Contact = row.Contact,
        Phone = row.Phone,
        Status = row.Status,
        ExpireTime = row.ExpireTime,
        Remark = row.Remark,
        CreateTime = row.CreateTime
    };

    private async Task EnsureCode(string code, long id)
    {
        if (await db.Queryable<SysTenant>().AnyAsync(t => t.Code == code && t.Id != id))
        {
            throw new BusinessException(await l10n.T("error.tenant.exists", "租户编码已存在"));
        }
    }

    private void EnsureHost()
    {
        if (!current.IsHostAdmin)
        {
            throw new BusinessException(l10n.T("error.forbidden", "无权限访问").GetAwaiter().GetResult(), 403);
        }
    }

    private async Task<BusinessException> NotFound() => new(await l10n.T("error.notFound", "数据不存在"), 404);
}

public sealed class DeptService(ISqlSugarClient db, ICurrentUser current, IDataScopeService scopes, IExcelService excel, IFieldAccessService fields, ILocalizationService l10n) : IDeptService
{
    private const string Page = "system:dept";

    public async Task<List<DeptTreeDto>> TreeAsync()
    {
        var rows = await VisibleDepts();
        var access = await fields.GetAsync(Page);
        var nodes = rows.Select(row =>
        {
            var dto = new DeptTreeDto
            {
                Id = row.Id,
                ParentId = row.ParentId,
                Name = row.Name,
                Code = row.Code,
                Sort = row.Sort,
                Leader = row.Leader,
                Phone = row.Phone,
                Status = row.Status,
                Remark = row.Remark
            };
            fields.Mask(dto, access);
            return dto;
        }).ToList();
        return Build(nodes, nodes.Select(x => x.Id).ToHashSet());
    }

    public async Task<long> CreateAsync(DeptSaveDto dto)
    {
        await EnsureCode(dto.Code, 0);
        await EnsureParent(dto.ParentId);
        var entity = new SysDept { TenantId = current.TenantId };
        await Copy(dto, entity);
        entity.ParentId = dto.ParentId;
        return await db.Insertable(entity).ExecuteReturnBigIdentityAsync();
    }

    public async Task UpdateAsync(long id, DeptSaveDto dto)
    {
        var entity = await FindInScope(id);
        await EnsureCode(dto.Code, id);
        if (dto.ParentId == id)
        {
            throw new BusinessException(await l10n.T("error.dept.parent", "上级部门不能是自己"));
        }

        await EnsureParent(dto.ParentId);
        await Copy(dto, entity);
        entity.ParentId = dto.ParentId;
        await db.Updateable(entity).ExecuteCommandAsync();
    }

    public async Task DeleteAsync(long id)
    {
        var entity = await FindInScope(id);
        if (await db.Queryable<SysDept>().AnyAsync(d => d.ParentId == id))
        {
            throw new BusinessException(await l10n.T("error.dept.hasChildren", "请先删除下级部门"));
        }

        if (await db.Queryable<SysUser>().AnyAsync(u => u.DeptId == id))
        {
            throw new BusinessException(await l10n.T("error.dept.hasUsers", "部门下仍有用户"));
        }

        entity.IsDeleted = true;
        await db.Updateable(entity).UpdateColumns(d => new { d.IsDeleted, d.UpdateTime, d.UpdateUserId }).ExecuteCommandAsync();
    }

    public async Task<byte[]> ExportAsync()
    {
        var rows = await VisibleDepts();
        var codes = rows.ToDictionary(x => x.Id, x => x.Code);
        var dtos = rows.Select(row => new DeptExportDto
        {
            Name = row.Name,
            Code = row.Code,
            ParentCode = row.ParentId > 0 && codes.TryGetValue(row.ParentId, out var code) ? code : null,
            Leader = row.Leader,
            Phone = row.Phone,
            Sort = row.Sort,
            Status = row.Status
        }).ToList();
        var access = await fields.GetAsync(Page);
        dtos.ForEach(dto => fields.Mask(dto, access));
        return await excel.ExportAsync(dtos, Page);
    }

    public Task<byte[]> TemplateAsync() => excel.TemplateAsync<DeptImportDto>(Page);

    public async Task<ImportResultDto> ImportAsync(Stream stream)
    {
        var read = await excel.ReadAsync<DeptImportDto>(stream, Page);
        if (read.Errors.Count > 0)
        {
            return new ImportResultDto { Failed = read.Errors.Count, Errors = read.Errors };
        }

        var success = 0;
        foreach (var row in read.Rows)
        {
            var parentId = 0L;
            if (!string.IsNullOrWhiteSpace(row.ParentCode))
            {
                var parent = await db.Queryable<SysDept>().FirstAsync(d => d.Code == row.ParentCode)
                    ?? throw new BusinessException($"上级部门不存在：{row.ParentCode}");
                parentId = parent.Id;
            }

            var dto = new DeptSaveDto
            {
                Name = row.Name,
                Code = row.Code,
                ParentId = parentId,
                Leader = row.Leader,
                Phone = row.Phone,
                Sort = row.Sort,
                Status = ValueParser.Status(row.Status)
            };
            var existing = await db.Queryable<SysDept>().FirstAsync(d => d.Code == row.Code);
            if (existing == null)
            {
                await CreateAsync(dto);
            }
            else
            {
                await UpdateAsync(existing.Id, dto);
            }

            success++;
        }

        return new ImportResultDto { Success = success };
    }

    private async Task<List<SysDept>> VisibleDepts()
    {
        var rows = await db.Queryable<SysDept>().OrderBy(d => d.Sort).OrderBy(d => d.Id).ToListAsync();
        var scope = await scopes.GetAsync();
        if (scope.All)
        {
            return rows;
        }

        var allowed = scope.DeptIds.ToHashSet();
        if (allowed.Count == 0 && current.DeptId is long self)
        {
            allowed.Add(self);
        }

        return rows.Where(d => allowed.Contains(d.Id)).ToList();
    }

    private async Task<SysDept> FindInScope(long id)
    {
        var rows = await VisibleDepts();
        return rows.FirstOrDefault(d => d.Id == id) ?? throw new BusinessException(await l10n.T("error.notFound", "数据不存在"), 404);
    }

    private async Task Copy(DeptSaveDto dto, SysDept entity)
    {
        var access = await fields.GetAsync(Page);
        fields.CopyEditable(dto, entity, access);
        entity.TenantId = current.TenantId;
        if (string.IsNullOrWhiteSpace(entity.Name)) entity.Name = dto.Name.Trim();
        if (string.IsNullOrWhiteSpace(entity.Code)) entity.Code = dto.Code.Trim();
    }

    private async Task EnsureCode(string code, long id)
    {
        if (await db.Queryable<SysDept>().AnyAsync(d => d.Code == code && d.Id != id))
        {
            throw new BusinessException(await l10n.T("error.dept.exists", "部门编码已存在"));
        }
    }

    private async Task EnsureParent(long parentId)
    {
        if (parentId == 0)
        {
            return;
        }

        _ = await FindInScope(parentId);
    }

    private static List<DeptTreeDto> Build(List<DeptTreeDto> nodes, HashSet<long> ids)
    {
        foreach (var node in nodes)
        {
            node.Children = nodes.Where(x => x.ParentId == node.Id).OrderBy(x => x.Sort).ToList();
        }

        return nodes.Where(x => x.ParentId == 0 || !ids.Contains(x.ParentId)).OrderBy(x => x.Sort).ToList();
    }
}
