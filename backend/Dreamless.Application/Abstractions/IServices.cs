using Dreamless.Application.Common;
using Dreamless.Application.Dtos;

namespace Dreamless.Application.Abstractions;

public interface IExcelService : IAppService
{
    Task<byte[]> ExportAsync<T>(IEnumerable<T> rows, string pageCode) where T : class, new();

    Task<byte[]> TemplateAsync<T>(string pageCode) where T : class, new();

    Task<ExcelReadResult<T>> ReadAsync<T>(Stream stream, string pageCode) where T : class, new();
}

public interface IAuthService : IAppService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);

    Task<LoginResponse> RefreshAsync(string refreshToken);

    Task LogoutAsync(string? refreshToken);

    Task<ProfileDto> ProfileAsync();

    Task ChangePasswordAsync(ChangePasswordRequest request);
}

public interface ITenantService : IAppService
{
    Task<PageResult<TenantListDto>> ListAsync(TenantQuery query);

    Task<long> CreateAsync(TenantSaveDto dto);

    Task UpdateAsync(long id, TenantSaveDto dto);

    Task DeleteAsync(long id);

    Task<byte[]> ExportAsync(TenantQuery query);

    Task<byte[]> TemplateAsync();

    Task<ImportResultDto> ImportAsync(Stream stream);
}

public interface IDeptService : IAppService
{
    Task<List<DeptTreeDto>> TreeAsync();

    Task<long> CreateAsync(DeptSaveDto dto);

    Task UpdateAsync(long id, DeptSaveDto dto);

    Task DeleteAsync(long id);

    Task<byte[]> ExportAsync();

    Task<byte[]> TemplateAsync();

    Task<ImportResultDto> ImportAsync(Stream stream);
}

public interface IPositionService : IAppService
{
    Task<PageResult<PositionListDto>> ListAsync(PositionQuery query);

    Task<long> CreateAsync(PositionSaveDto dto);

    Task UpdateAsync(long id, PositionSaveDto dto);

    Task DeleteAsync(long id);

    Task<byte[]> ExportAsync(PositionQuery query);

    Task<byte[]> TemplateAsync();

    Task<ImportResultDto> ImportAsync(Stream stream);
}

public interface IUserService : IAppService
{
    Task<PageResult<UserListDto>> ListAsync(UserQuery query);

    Task<UserListDto> GetAsync(long id);

    Task<long> CreateAsync(UserSaveDto dto);

    Task UpdateAsync(long id, UserSaveDto dto);

    Task ResetPasswordAsync(long id, string password);

    Task<string> SaveAvatarAsync(Stream stream, string fileName, string? contentType);

    Task DeleteAsync(long id);

    Task<byte[]> ExportAsync(UserQuery query);

    Task<byte[]> TemplateAsync();

    Task<ImportResultDto> ImportAsync(Stream stream);
}

public interface IRoleService : IAppService
{
    Task<PageResult<RoleListDto>> ListAsync(RoleQuery query);

    Task<RoleListDto> GetAsync(long id);

    Task<long> CreateAsync(RoleSaveDto dto);

    Task UpdateAsync(long id, RoleSaveDto dto);

    Task DeleteAsync(long id);

    Task<byte[]> ExportAsync(RoleQuery query);

    Task<byte[]> TemplateAsync();

    Task<ImportResultDto> ImportAsync(Stream stream);
}

public interface IMenuService : IAppService
{
    Task<List<MenuNodeDto>> TreeAsync(bool includeButtons);

    Task<long> CreateAsync(MenuSaveDto dto);

    Task UpdateAsync(long id, MenuSaveDto dto);

    Task DeleteAsync(long id);

    Task<byte[]> ExportAsync();

    Task<byte[]> TemplateAsync();

    Task<ImportResultDto> ImportAsync(Stream stream);

    Task<List<MenuNodeDto>> ProfileTreeAsync(long userId, bool isHostAdmin);
}

public interface II18nAdminService : IAppService
{
    Task<PageResult<I18nDto>> ListAsync(I18nQuery query);

    Task<long> CreateAsync(I18nSaveDto dto);

    Task UpdateAsync(long id, I18nSaveDto dto);

    Task DeleteAsync(long id);

    Task<byte[]> ExportAsync(I18nQuery query);

    Task<byte[]> TemplateAsync();

    Task<ImportResultDto> ImportAsync(Stream stream);
}

public interface IMetaService : IAppService
{
    Task<PageColumnDto> ColumnsAsync(string pageCode);

    Task SaveColumnsAsync(string pageCode, List<ColumnSettingDto> columns);

    Task<List<FieldPageDto>> FieldsAsync();
}

public interface IDashboardService : IAppService
{
    Task<DashboardDto> SummaryAsync();
}

public interface IMessageService : IAppService
{
    Task<long> SendAsync(MessageSendRequest request);

    Task<PageResult<MessageInboxDto>> InboxAsync(MessageInboxQuery query);

    Task<PageResult<MessageSentDto>> SentAsync(PageQuery query);

    Task<int> UnreadCountAsync();

    Task ReadAsync(long messageId);

    Task ReadAllAsync();

    Task DeleteInboxAsync(long id);

    Task<List<MessageUserOptionDto>> RecipientsAsync(string? keyword);
}

public interface ICodeGenService : IAppService
{
    Task<List<TableInfoDto>> TablesAsync();

    Task<List<ColumnInfoDto>> ColumnsAsync(string table);

    Task<CodeGenPreviewDto> PreviewAsync(CodeGenRequest request);

    Task<CodeGenResultDto> GenerateAsync(CodeGenRequest request);
}

public interface ICrudEngine : IAppService
{
    Task<PageResult<TDto>> PageAsync<TEntity, TDto>(string pageCode, PageQuery query)
        where TEntity : Dreamless.Core.Entities.BaseEntity, new()
        where TDto : class, new();

    Task<long> CreateAsync<TEntity>(string pageCode, object dto)
        where TEntity : Dreamless.Core.Entities.BaseEntity, new();

    Task UpdateAsync<TEntity>(string pageCode, long id, object dto)
        where TEntity : Dreamless.Core.Entities.BaseEntity, new();

    Task DeleteAsync<TEntity>(long id)
        where TEntity : Dreamless.Core.Entities.BaseEntity, new();

    Task<byte[]> ExportAsync<TEntity, TExport>(string pageCode, PageQuery query)
        where TEntity : Dreamless.Core.Entities.BaseEntity, new()
        where TExport : class, new();

    Task<byte[]> TemplateAsync<TImport>(string pageCode)
        where TImport : class, new();

    Task<ImportResultDto> ImportAsync<TEntity, TImport>(string pageCode, Stream stream)
        where TEntity : Dreamless.Core.Entities.BaseEntity, new()
        where TImport : class, new();
}
