using Dreamless.Application.Abstractions;
using Dreamless.Application.Common;
using Dreamless.Application.Dtos;
using Dreamless.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Dreamless.WebApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    public Task<LoginResponse> Login([FromBody] LoginRequest request) => auth.LoginAsync(request);

    [HttpPost("refresh")]
    public Task<LoginResponse> Refresh([FromBody] RefreshRequest request) => auth.RefreshAsync(request.RefreshToken);

    [HttpPost("logout")]
    public Task Logout([FromBody] RefreshRequest? request) => auth.LogoutAsync(request?.RefreshToken);

    [Authorize]
    [HttpGet("profile")]
    public Task<ProfileDto> Profile() => auth.ProfileAsync();

    [Authorize]
    [HttpPut("password")]
    public Task Password([FromBody] ChangePasswordRequest request) => auth.ChangePasswordAsync(request);
}

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet("summary")]
    [HasPermission(Permissions.Dashboard)]
    public Task<DashboardDto> Summary() => dashboard.SummaryAsync();

    [AllowAnonymous]
    [HttpGet("/api/health")]
    public IActionResult Health() => Ok(new { status = "ok" });
}

[ApiController]
[Authorize]
[Route("api/tenants")]
public class TenantsController(ITenantService service) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.TenantList)]
    public Task<PageResult<TenantListDto>> List([FromQuery] TenantQuery query) => service.ListAsync(query);

    [HttpPost]
    [HasPermission(Permissions.TenantAdd)]
    public Task<long> Create([FromBody] TenantSaveDto dto) => service.CreateAsync(dto);

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.TenantEdit)]
    public Task Update(long id, [FromBody] TenantSaveDto dto) => service.UpdateAsync(id, dto);

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.TenantDelete)]
    public Task Delete(long id) => service.DeleteAsync(id);

    [HttpGet("export")]
    [HasPermission(Permissions.TenantExport)]
    public async Task<IActionResult> Export([FromQuery] TenantQuery query) => Excel(await service.ExportAsync(query), "tenants.xlsx");

    [HttpGet("import-template")]
    [HasPermission(Permissions.TenantImport)]
    public async Task<IActionResult> Template() => Excel(await service.TemplateAsync(), "tenants-template.xlsx");

    [HttpPost("import")]
    [HasPermission(Permissions.TenantImport)]
    public async Task<ImportResultDto> Import(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return await service.ImportAsync(stream);
    }

    private FileContentResult Excel(byte[] bytes, string name) =>
        File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
}

[ApiController]
[Authorize]
[Route("api/depts")]
public class DeptsController(IDeptService service) : ControllerBase
{
    [HttpGet("tree")]
    [HasPermission(Permissions.DeptList)]
    public Task<List<DeptTreeDto>> Tree() => service.TreeAsync();

    [HttpPost]
    [HasPermission(Permissions.DeptAdd)]
    public Task<long> Create([FromBody] DeptSaveDto dto) => service.CreateAsync(dto);

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.DeptEdit)]
    public Task Update(long id, [FromBody] DeptSaveDto dto) => service.UpdateAsync(id, dto);

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.DeptDelete)]
    public Task Delete(long id) => service.DeleteAsync(id);

    [HttpGet("export")]
    [HasPermission(Permissions.DeptExport)]
    public async Task<IActionResult> Export() => File(await service.ExportAsync(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "depts.xlsx");

    [HttpGet("import-template")]
    [HasPermission(Permissions.DeptImport)]
    public async Task<IActionResult> Template() => File(await service.TemplateAsync(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "depts-template.xlsx");

    [HttpPost("import")]
    [HasPermission(Permissions.DeptImport)]
    public async Task<ImportResultDto> Import(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return await service.ImportAsync(stream);
    }
}

[ApiController]
[Authorize]
[Route("api/positions")]
public class PositionsController(IPositionService service) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.PositionList)]
    public Task<PageResult<PositionListDto>> List([FromQuery] PositionQuery query) => service.ListAsync(query);

    [HttpPost]
    [HasPermission(Permissions.PositionAdd)]
    public Task<long> Create([FromBody] PositionSaveDto dto) => service.CreateAsync(dto);

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.PositionEdit)]
    public Task Update(long id, [FromBody] PositionSaveDto dto) => service.UpdateAsync(id, dto);

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.PositionDelete)]
    public Task Delete(long id) => service.DeleteAsync(id);

    [HttpGet("export")]
    [HasPermission(Permissions.PositionExport)]
    public async Task<IActionResult> Export([FromQuery] PositionQuery query) =>
        File(await service.ExportAsync(query), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "positions.xlsx");

    [HttpGet("import-template")]
    [HasPermission(Permissions.PositionImport)]
    public async Task<IActionResult> Template() =>
        File(await service.TemplateAsync(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "positions-template.xlsx");

    [HttpPost("import")]
    [HasPermission(Permissions.PositionImport)]
    public async Task<ImportResultDto> Import(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return await service.ImportAsync(stream);
    }
}

[ApiController]
[Route("api/files")]
public class FilesController(IHostEnvironment environment) : ControllerBase
{
    [HttpGet("avatars/{name}")]
    [AllowAnonymous]
    public IActionResult Avatar(string name)
    {
        if (name.Length > 80 || Path.GetFileName(name) != name || !name.Contains('.'))
        {
            return NotFound();
        }

        var path = Path.Combine(environment.ContentRootPath, "App_Data", "avatars", name);
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        var type = Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
        return PhysicalFile(path, type);
    }
}

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController(IUserService service) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.UserList)]
    public Task<PageResult<UserListDto>> List([FromQuery] UserQuery query) => service.ListAsync(query);

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.UserList)]
    public Task<UserListDto> Get(long id) => service.GetAsync(id);

    [HttpPost]
    [HasPermission(Permissions.UserAdd)]
    public Task<long> Create([FromBody] UserSaveDto dto) => service.CreateAsync(dto);

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.UserEdit)]
    public Task Update(long id, [FromBody] UserSaveDto dto) => service.UpdateAsync(id, dto);

    [HttpPut("{id:long}/password")]
    [HasPermission(Permissions.UserEdit)]
    public Task ResetPassword(long id, [FromBody] ResetPasswordDto dto) => service.ResetPasswordAsync(id, dto.Password);

    [HttpPost("avatar")]
    [HasPermission(Permissions.UserEdit)]
    public async Task<IActionResult> Avatar(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "请选择头像文件" });
        }

        await using var stream = file.OpenReadStream();
        var url = await service.SaveAvatarAsync(stream, file.FileName, file.ContentType);
        return Ok(new { url });
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.UserDelete)]
    public Task Delete(long id) => service.DeleteAsync(id);

    [HttpGet("export")]
    [HasPermission(Permissions.UserExport)]
    public async Task<IActionResult> Export([FromQuery] UserQuery query) =>
        File(await service.ExportAsync(query), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "users.xlsx");

    [HttpGet("import-template")]
    [HasPermission(Permissions.UserImport)]
    public async Task<IActionResult> Template() =>
        File(await service.TemplateAsync(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "users-template.xlsx");

    [HttpPost("import")]
    [HasPermission(Permissions.UserImport)]
    public async Task<ImportResultDto> Import(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return await service.ImportAsync(stream);
    }
}

[ApiController]
[Authorize]
[Route("api/roles")]
public class RolesController(IRoleService service) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.RoleList)]
    public Task<PageResult<RoleListDto>> List([FromQuery] RoleQuery query) => service.ListAsync(query);

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.RoleList)]
    public Task<RoleListDto> Get(long id) => service.GetAsync(id);

    [HttpPost]
    [HasPermission(Permissions.RoleAdd)]
    public Task<long> Create([FromBody] RoleSaveDto dto) => service.CreateAsync(dto);

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.RoleEdit)]
    public Task Update(long id, [FromBody] RoleSaveDto dto) => service.UpdateAsync(id, dto);

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.RoleDelete)]
    public Task Delete(long id) => service.DeleteAsync(id);

    [HttpGet("export")]
    [HasPermission(Permissions.RoleExport)]
    public async Task<IActionResult> Export([FromQuery] RoleQuery query) =>
        File(await service.ExportAsync(query), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "roles.xlsx");

    [HttpGet("import-template")]
    [HasPermission(Permissions.RoleImport)]
    public async Task<IActionResult> Template() =>
        File(await service.TemplateAsync(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "roles-template.xlsx");

    [HttpPost("import")]
    [HasPermission(Permissions.RoleImport)]
    public async Task<ImportResultDto> Import(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return await service.ImportAsync(stream);
    }
}

[ApiController]
[Authorize]
[Route("api/menus")]
public class MenusController(IMenuService service) : ControllerBase
{
    [HttpGet("tree")]
    [HasPermission(Permissions.MenuList)]
    public Task<List<MenuNodeDto>> Tree() => service.TreeAsync(true);

    [HttpPost]
    [HasPermission(Permissions.MenuAdd)]
    public Task<long> Create([FromBody] MenuSaveDto dto) => service.CreateAsync(dto);

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.MenuEdit)]
    public Task Update(long id, [FromBody] MenuSaveDto dto) => service.UpdateAsync(id, dto);

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.MenuDelete)]
    public Task Delete(long id) => service.DeleteAsync(id);

    [HttpGet("export")]
    [HasPermission(Permissions.MenuExport)]
    public async Task<IActionResult> Export() =>
        File(await service.ExportAsync(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "menus.xlsx");

    [HttpGet("import-template")]
    [HasPermission(Permissions.MenuImport)]
    public async Task<IActionResult> Template() =>
        File(await service.TemplateAsync(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "menus-template.xlsx");

    [HttpPost("import")]
    [HasPermission(Permissions.MenuImport)]
    public async Task<ImportResultDto> Import(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return await service.ImportAsync(stream);
    }
}

[ApiController]
[Authorize]
[Route("api/i18n")]
public class I18nController(II18nAdminService service) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.I18nList)]
    public Task<PageResult<I18nDto>> List([FromQuery] I18nQuery query) => service.ListAsync(query);

    [HttpPost]
    [HasPermission(Permissions.I18nAdd)]
    public Task<long> Create([FromBody] I18nSaveDto dto) => service.CreateAsync(dto);

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.I18nEdit)]
    public Task Update(long id, [FromBody] I18nSaveDto dto) => service.UpdateAsync(id, dto);

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.I18nDelete)]
    public Task Delete(long id) => service.DeleteAsync(id);

    [HttpGet("export")]
    [HasPermission(Permissions.I18nExport)]
    public async Task<IActionResult> Export([FromQuery] I18nQuery query) =>
        File(await service.ExportAsync(query), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "i18n.xlsx");

    [HttpGet("import-template")]
    [HasPermission(Permissions.I18nImport)]
    public async Task<IActionResult> Template() =>
        File(await service.TemplateAsync(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "i18n-template.xlsx");

    [HttpPost("import")]
    [HasPermission(Permissions.I18nImport)]
    public async Task<ImportResultDto> Import(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return await service.ImportAsync(stream);
    }
}

[ApiController]
[Authorize]
[Route("api/meta")]
public class MetaController(IMetaService service) : ControllerBase
{
    [HttpGet("pages/{pageCode}/columns")]
    public Task<PageColumnDto> Columns(string pageCode) => service.ColumnsAsync(pageCode);

    [HttpPut("pages/{pageCode}/columns")]
    public Task SaveColumns(string pageCode, [FromBody] List<ColumnSettingDto> columns) => service.SaveColumnsAsync(pageCode, columns);

    [HttpGet("fields")]
    [HasPermission(Permissions.RoleList)]
    public Task<List<FieldPageDto>> Fields() => service.FieldsAsync();
}

[ApiController]
[Authorize]
[Route("api/messages")]
public class MessagesController(IMessageService service) : ControllerBase
{
    [HttpPost]
    [HasPermission(Permissions.MessageSend)]
    public Task<long> Send([FromBody] MessageSendRequest request) => service.SendAsync(request);

    [HttpGet("inbox")]
    public Task<PageResult<MessageInboxDto>> Inbox([FromQuery] MessageInboxQuery query) => service.InboxAsync(query);

    [HttpGet("sent")]
    [HasPermission(Permissions.MessageSend)]
    public Task<PageResult<MessageSentDto>> Sent([FromQuery] PageQuery query) => service.SentAsync(query);

    [HttpGet("unread-count")]
    public Task<int> UnreadCount() => service.UnreadCountAsync();

    [HttpGet("recipients")]
    [HasPermission(Permissions.MessageSend)]
    public Task<List<MessageUserOptionDto>> Recipients([FromQuery] string? keyword) => service.RecipientsAsync(keyword);

    [HttpPut("read-all")]
    public Task ReadAll() => service.ReadAllAsync();

    [HttpPut("{messageId:long}/read")]
    public Task Read(long messageId) => service.ReadAsync(messageId);

    [HttpDelete("inbox/{id:long}")]
    [HasPermission(Permissions.MessageDelete)]
    public Task DeleteInbox(long id) => service.DeleteInboxAsync(id);
}

[ApiController]
[Authorize]
[Route("api/codegen")]
public class CodeGenController(ICodeGenService service) : ControllerBase
{
    [HttpGet("tables")]
    [HasPermission(Permissions.CodegenList)]
    public Task<List<TableInfoDto>> Tables() => service.TablesAsync();

    [HttpGet("tables/{table}/columns")]
    [HasPermission(Permissions.CodegenList)]
    public Task<List<ColumnInfoDto>> Columns(string table) => service.ColumnsAsync(table);

    [HttpPost("preview")]
    [HasPermission(Permissions.CodegenPreview)]
    public Task<CodeGenPreviewDto> Preview([FromBody] CodeGenRequest request) => service.PreviewAsync(request);

    [HttpPost("generate")]
    [HasPermission(Permissions.CodegenGenerate)]
    public Task<CodeGenResultDto> Generate([FromBody] CodeGenRequest request) => service.GenerateAsync(request);
}
