namespace Dreamless.Infrastructure.Services;

public sealed class PositionService(
    ISqlSugarClient db,
    ICurrentUser current,
    IFieldAccessService fields,
    IExcelService excel,
    ILocalizationService l10n) : IPositionService
{
    private const string Page = "system:position";

    public async Task<PageResult<PositionListDto>> ListAsync(PositionQuery query)
    {
        var q = db.Queryable<SysPosition>();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(p => p.Name.Contains(keyword) || p.Code.Contains(keyword));
        }

        q = QueryFilter.Apply(q, query.FilterClauses);
        q = q.WhereIF(query.Status != null, p => p.Status == query.Status);
        var total = await q.CountAsync();
        q = query.SortClauses.Count == 0
            ? q.OrderBy(p => p.Sort).OrderBy(p => p.Id)
            : QuerySort.Apply(q, query.SortClauses);
        var rows = await q
            .Skip((query.NormalizedPage - 1) * query.NormalizedSize)
            .Take(query.NormalizedSize)
            .ToListAsync();
        var access = await fields.GetAsync(Page);
        return new PageResult<PositionListDto>
        {
            Total = total,
            Items = rows.Select(row =>
            {
                var dto = new PositionListDto
                {
                    Id = row.Id,
                    Name = row.Name,
                    Code = row.Code,
                    Sort = row.Sort,
                    Status = row.Status,
                    Remark = row.Remark
                };
                fields.Mask(dto, access);
                return dto;
            }).ToList()
        };
    }

    public async Task<long> CreateAsync(PositionSaveDto dto)
    {
        await EnsureCode(dto.Code, 0);
        var entity = new SysPosition { TenantId = current.TenantId, Status = CommonStatus.Enabled };
        await Copy(dto, entity);
        return await db.Insertable(entity).ExecuteReturnBigIdentityAsync();
    }

    public async Task UpdateAsync(long id, PositionSaveDto dto)
    {
        var entity = await db.Queryable<SysPosition>().FirstAsync(p => p.Id == id) ?? throw await NotFound();
        await EnsureCode(dto.Code, id);
        await Copy(dto, entity);
        await db.Updateable(entity).ExecuteCommandAsync();
    }

    public async Task DeleteAsync(long id)
    {
        var entity = await db.Queryable<SysPosition>().FirstAsync(p => p.Id == id) ?? throw await NotFound();
        if (await db.Queryable<SysUser>().AnyAsync(u => u.PositionId == id))
        {
            throw new BusinessException(await l10n.T("error.position.inUse", "职位下仍有人员"));
        }

        entity.IsDeleted = true;
        await db.Updateable(entity).UpdateColumns(p => new { p.IsDeleted, p.UpdateTime, p.UpdateUserId }).ExecuteCommandAsync();
    }

    public async Task<byte[]> ExportAsync(PositionQuery query)
    {
        query.Page = 1;
        query.PageSize = 200;
        var all = new List<PositionListDto>();
        while (all.Count < 20000)
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

    public Task<byte[]> TemplateAsync() => excel.TemplateAsync<PositionImportDto>(Page);

    public async Task<ImportResultDto> ImportAsync(Stream stream)
    {
        var read = await excel.ReadAsync<PositionImportDto>(stream, Page);
        if (read.Errors.Count > 0)
        {
            return new ImportResultDto { Failed = read.Errors.Count, Errors = read.Errors };
        }

        var success = 0;
        foreach (var row in read.Rows)
        {
            var dto = new PositionSaveDto
            {
                Name = row.Name,
                Code = row.Code,
                Sort = row.Sort,
                Status = ValueParser.Status(row.Status),
                Remark = row.Remark
            };
            var existing = await db.Queryable<SysPosition>().FirstAsync(p => p.Code == row.Code);
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

    private async Task Copy(PositionSaveDto dto, SysPosition entity)
    {
        var access = await fields.GetAsync(Page);
        fields.CopyEditable(dto, entity, access);
        entity.TenantId = current.TenantId;
        entity.Name = dto.Name.Trim();
        entity.Code = dto.Code.Trim();
    }

    private async Task EnsureCode(string code, long id)
    {
        if (await db.Queryable<SysPosition>().AnyAsync(p => p.Code == code && p.Id != id))
        {
            throw new BusinessException(await l10n.T("error.position.exists", "职位编码已存在"));
        }
    }

    private async Task<BusinessException> NotFound() =>
        new(await l10n.T("error.notFound", "数据不存在"), 404);
}
