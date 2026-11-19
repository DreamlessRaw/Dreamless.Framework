using System.ComponentModel.DataAnnotations;
using Dreamless.Application.Common;
using Dreamless.Core.Enums;
using Magicodes.ExporterAndImporter.Core;
using Magicodes.ExporterAndImporter.Excel;

namespace Dreamless.Application.Dtos;

public class LoginRequest
{
    public string? TenantCode { get; set; }

    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [Required]
    public string OldPassword { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public ProfileDto Profile { get; set; } = new();
}

public class ProfileDto
{
    public long Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string NickName { get; set; } = string.Empty;

    public long TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public long? DeptId { get; set; }

    public string? DeptName { get; set; }

    public string? Avatar { get; set; }

    public bool IsHostAdmin { get; set; }

    public List<string> Permissions { get; set; } = [];

    public List<MenuNodeDto> Menus { get; set; } = [];
}

public class MenuNodeDto
{
    public long Id { get; set; }

    public long ParentId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? I18nKey { get; set; }

    public string? Path { get; set; }

    public string? Component { get; set; }

    public string? Icon { get; set; }

    public int Sort { get; set; }

    public MenuType Type { get; set; }

    public string? Permission { get; set; }

    public bool Visible { get; set; }

    public CommonStatus Status { get; set; }

    public List<MenuNodeDto> Children { get; set; } = [];
}

public class ImportResultDto
{
    public int Success { get; set; }

    public int Failed { get; set; }

    public List<string> Errors { get; set; } = [];
}

public class ExcelReadResult<T>
{
    public List<T> Rows { get; set; } = [];

    public List<string> Errors { get; set; } = [];
}

public class OptionDto
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public class DashboardDto
{
    public int UserCount { get; set; }

    public int DeptCount { get; set; }

    public int RoleCount { get; set; }

    public int TenantCount { get; set; }
}

public class TenantQuery : PageQuery
{
    public CommonStatus? Status { get; set; }
}

[ExcelExporter(Name = "租户", ExporterHeadersFilter = typeof(InfrastructureHeaderFilter))]
public class TenantListDto
{
    public long Id { get; set; }

    [ExporterHeader(DisplayName = "field.system.tenant.name")]
    public string Name { get; set; } = string.Empty;

    [ExporterHeader(DisplayName = "field.system.tenant.code")]
    public string Code { get; set; } = string.Empty;

    [ExporterHeader(DisplayName = "field.system.tenant.contact")]
    public string? Contact { get; set; }

    [ExporterHeader(DisplayName = "field.system.tenant.phone")]
    public string? Phone { get; set; }

    [ExporterHeader(DisplayName = "field.system.tenant.status")]
    public CommonStatus Status { get; set; }

    [ExporterHeader(DisplayName = "field.system.tenant.expireTime", Format = "yyyy-MM-dd")]
    public DateTime? ExpireTime { get; set; }

    [ExporterHeader(DisplayName = "field.system.tenant.remark")]
    public string? Remark { get; set; }

    [ExporterHeader(DisplayName = "field.common.createTime", Format = "yyyy-MM-dd HH:mm:ss")]
    public DateTime CreateTime { get; set; }
}

[ExcelImporter(IsLabelingError = true, ImportHeaderFilter = typeof(InfrastructureHeaderFilter))]
public class TenantImportDto
{
    [ImporterHeader(Name = "field.system.tenant.name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [ImporterHeader(Name = "field.system.tenant.code")]
    [Required]
    public string Code { get; set; } = string.Empty;

    [ImporterHeader(Name = "field.system.tenant.contact")]
    public string? Contact { get; set; }

    [ImporterHeader(Name = "field.system.tenant.phone")]
    public string? Phone { get; set; }

    [ImporterHeader(Name = "field.system.tenant.status")]
    public string? Status { get; set; }

    [ImporterHeader(Name = "field.system.tenant.remark")]
    public string? Remark { get; set; }
}

public class TenantSaveDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;

    public string? Contact { get; set; }

    public string? Phone { get; set; }

    public CommonStatus Status { get; set; } = CommonStatus.Enabled;

    public DateTime? ExpireTime { get; set; }

    public string? Remark { get; set; }
}

public class DeptSaveDto
{
    public long ParentId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;

    public int Sort { get; set; }

    public string? Leader { get; set; }

    public string? Phone { get; set; }

    public CommonStatus Status { get; set; } = CommonStatus.Enabled;

    public string? Remark { get; set; }
}

[ExcelExporter(Name = "部门", ExporterHeadersFilter = typeof(InfrastructureHeaderFilter))]
public class DeptExportDto
{
    [ExporterHeader(DisplayName = "field.system.dept.name")]
    public string Name { get; set; } = string.Empty;

    [ExporterHeader(DisplayName = "field.system.dept.code")]
    public string Code { get; set; } = string.Empty;

    [ExporterHeader(DisplayName = "field.system.dept.parentCode")]
    public string? ParentCode { get; set; }

    [ExporterHeader(DisplayName = "field.system.dept.leader")]
    public string? Leader { get; set; }

    [ExporterHeader(DisplayName = "field.system.dept.phone")]
    public string? Phone { get; set; }

    [ExporterHeader(DisplayName = "field.system.dept.sort")]
    public int Sort { get; set; }

    [ExporterHeader(DisplayName = "field.system.dept.status")]
    public CommonStatus Status { get; set; }
}

[ExcelImporter(IsLabelingError = true, ImportHeaderFilter = typeof(InfrastructureHeaderFilter))]
public class DeptImportDto
{
    [ImporterHeader(Name = "field.system.dept.name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [ImporterHeader(Name = "field.system.dept.code")]
    [Required]
    public string Code { get; set; } = string.Empty;

    [ImporterHeader(Name = "field.system.dept.parentCode")]
    public string? ParentCode { get; set; }

    [ImporterHeader(Name = "field.system.dept.leader")]
    public string? Leader { get; set; }

    [ImporterHeader(Name = "field.system.dept.phone")]
    public string? Phone { get; set; }

    [ImporterHeader(Name = "field.system.dept.sort")]
    public int Sort { get; set; }

    [ImporterHeader(Name = "field.system.dept.status")]
    public string? Status { get; set; }
}

public class DeptTreeDto
{
    public long Id { get; set; }

    public long ParentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public int Sort { get; set; }

    public string? Leader { get; set; }

    public string? Phone { get; set; }

    public CommonStatus Status { get; set; }

    public string? Remark { get; set; }

    public List<DeptTreeDto> Children { get; set; } = [];
}

public class PositionQuery : PageQuery
{
    public CommonStatus? Status { get; set; }
}

[ExcelExporter(Name = "职位", ExporterHeadersFilter = typeof(InfrastructureHeaderFilter))]
public class PositionListDto
{
    [ExporterHeader(IsIgnore = true)]
    public long Id { get; set; }

    [ExporterHeader(DisplayName = "field.system.position.name")]
    public string Name { get; set; } = string.Empty;

    [ExporterHeader(DisplayName = "field.system.position.code")]
    public string Code { get; set; } = string.Empty;

    [ExporterHeader(DisplayName = "field.system.position.sort")]
    public int Sort { get; set; }

    [ExporterHeader(DisplayName = "field.system.position.status")]
    public CommonStatus Status { get; set; }

    [ExporterHeader(DisplayName = "field.system.position.remark")]
    public string? Remark { get; set; }
}

public class PositionSaveDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;

    public int Sort { get; set; }

    public CommonStatus Status { get; set; } = CommonStatus.Enabled;

    public string? Remark { get; set; }
}

[ExcelImporter(IsLabelingError = true, ImportHeaderFilter = typeof(InfrastructureHeaderFilter))]
public class PositionImportDto
{
    [ImporterHeader(Name = "field.system.position.name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [ImporterHeader(Name = "field.system.position.code")]
    [Required]
    public string Code { get; set; } = string.Empty;

    [ImporterHeader(Name = "field.system.position.sort")]
    public int Sort { get; set; }

    [ImporterHeader(Name = "field.system.position.status")]
    public string? Status { get; set; }

    [ImporterHeader(Name = "field.system.position.remark")]
    public string? Remark { get; set; }
}

public class UserQuery : PageQuery
{
    public CommonStatus? Status { get; set; }

    public long? DeptId { get; set; }
}

[ExcelExporter(Name = "用户", ExporterHeadersFilter = typeof(InfrastructureHeaderFilter))]
public class UserListDto
{
    [ExporterHeader(IsIgnore = true)]
    public long Id { get; set; }

    [ExporterHeader(IsIgnore = true)]
    public long TenantId { get; set; }

    [ExporterHeader(DisplayName = "field.system.user.userName")]
    public string UserName { get; set; } = string.Empty;

    [ExporterHeader(DisplayName = "field.system.user.nickName")]
    public string NickName { get; set; } = string.Empty;

    [ExporterHeader(IsIgnore = true)]
    public long? DeptId { get; set; }

    [ExporterHeader(DisplayName = "field.system.user.deptName")]
    public string? DeptName { get; set; }

    [ExporterHeader(IsIgnore = true)]
    public long? PositionId { get; set; }

    [ExporterHeader(DisplayName = "field.system.user.positionName")]
    public string? PositionName { get; set; }

    [ExporterHeader(DisplayName = "field.system.user.avatar")]
    public string? Avatar { get; set; }

    [ExporterHeader(DisplayName = "field.system.user.employeeNo")]
    public string? EmployeeNo { get; set; }

    [ExporterHeader(DisplayName = "field.system.user.gender")]
    public string? Gender { get; set; }

    [ExporterHeader(DisplayName = "field.system.user.idCard")]
    public string? IdCard { get; set; }

    [ExporterHeader(DisplayName = "field.system.user.birthday", Format = "yyyy-MM-dd")]
    public DateTime? Birthday { get; set; }

    [ExporterHeader(DisplayName = "field.system.user.hireDate", Format = "yyyy-MM-dd")]
    public DateTime? HireDate { get; set; }

    [ExporterHeader(DisplayName = "field.system.user.address")]
    public string? Address { get; set; }

    [ExporterHeader(DisplayName = "field.system.user.email")]
    public string? Email { get; set; }

    [ExporterHeader(DisplayName = "field.system.user.phone")]
    public string? Phone { get; set; }

    [ExporterHeader(DisplayName = "field.system.user.status")]
    public CommonStatus Status { get; set; }

    [ExporterHeader(IsIgnore = true)]
    public bool IsHostAdmin { get; set; }

    [ExporterHeader(IsIgnore = true)]
    public List<long> RoleIds { get; set; } = [];

    [ExporterHeader(DisplayName = "field.system.user.remark")]
    public string? Remark { get; set; }

    [ExporterHeader(DisplayName = "field.common.createTime", Format = "yyyy-MM-dd HH:mm:ss")]
    public DateTime CreateTime { get; set; }
}

public class UserSaveDto
{
    public long? TenantId { get; set; }

    [Required]
    public string UserName { get; set; } = string.Empty;

    public string? Password { get; set; }

    [Required]
    public string NickName { get; set; } = string.Empty;

    public long? DeptId { get; set; }

    public long? PositionId { get; set; }

    public string? Avatar { get; set; }

    public string? EmployeeNo { get; set; }

    public string? Gender { get; set; }

    public string? IdCard { get; set; }

    public DateTime? Birthday { get; set; }

    public DateTime? HireDate { get; set; }

    public string? Address { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public CommonStatus Status { get; set; } = CommonStatus.Enabled;

    public string? Remark { get; set; }

    public List<long> RoleIds { get; set; } = [];
}

[ExcelImporter(IsLabelingError = true, ImportHeaderFilter = typeof(InfrastructureHeaderFilter))]
public class UserImportDto
{
    [ImporterHeader(Name = "field.system.user.userName")]
    [Required]
    public string UserName { get; set; } = string.Empty;

    [ImporterHeader(Name = "field.system.user.nickName")]
    [Required]
    public string NickName { get; set; } = string.Empty;

    [ImporterHeader(Name = "field.system.user.password")]
    public string? Password { get; set; }

    [ImporterHeader(Name = "field.system.dept.code")]
    public string? DeptCode { get; set; }

    [ImporterHeader(Name = "field.system.position.code")]
    public string? PositionCode { get; set; }

    [ImporterHeader(Name = "field.system.user.employeeNo")]
    public string? EmployeeNo { get; set; }

    [ImporterHeader(Name = "field.system.user.gender")]
    public string? Gender { get; set; }

    [ImporterHeader(Name = "field.system.user.idCard")]
    public string? IdCard { get; set; }

    [ImporterHeader(Name = "field.system.user.birthday")]
    public string? Birthday { get; set; }

    [ImporterHeader(Name = "field.system.user.hireDate")]
    public string? HireDate { get; set; }

    [ImporterHeader(Name = "field.system.user.address")]
    public string? Address { get; set; }

    [ImporterHeader(Name = "field.system.user.email")]
    public string? Email { get; set; }

    [ImporterHeader(Name = "field.system.user.phone")]
    public string? Phone { get; set; }

    [ImporterHeader(Name = "field.system.user.status")]
    public string? Status { get; set; }

    [ImporterHeader(Name = "field.system.user.remark")]
    public string? Remark { get; set; }
}

public class ResetPasswordDto
{
    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RoleQuery : PageQuery
{
    public CommonStatus? Status { get; set; }
}

public class RoleFieldGrantDto
{
    public long FieldId { get; set; }

    public bool CanView { get; set; }

    public bool CanEdit { get; set; }

    public bool CanExport { get; set; }
}

public class RoleSaveDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;

    public int Sort { get; set; }

    public DataScope DataScope { get; set; } = DataScope.Self;

    public CommonStatus Status { get; set; } = CommonStatus.Enabled;

    public string? Remark { get; set; }

    public List<long> MenuIds { get; set; } = [];

    public List<long> DeptIds { get; set; } = [];

    public List<RoleFieldGrantDto> Fields { get; set; } = [];
}

[ExcelExporter(Name = "角色", ExporterHeadersFilter = typeof(InfrastructureHeaderFilter))]
public class RoleListDto
{
    public long Id { get; set; }

    [ExporterHeader(DisplayName = "field.system.role.name")]
    public string Name { get; set; } = string.Empty;

    [ExporterHeader(DisplayName = "field.system.role.code")]
    public string Code { get; set; } = string.Empty;

    [ExporterHeader(DisplayName = "field.system.role.sort")]
    public int Sort { get; set; }

    [ExporterHeader(DisplayName = "field.system.role.dataScope")]
    public DataScope DataScope { get; set; }

    [ExporterHeader(DisplayName = "field.system.role.status")]
    public CommonStatus Status { get; set; }

    [ExporterHeader(DisplayName = "field.system.role.remark")]
    public string? Remark { get; set; }

    [ExporterHeader(IsIgnore = true)]
    public bool FieldConfigured { get; set; }

    [ExporterHeader(IsIgnore = true)]
    public List<long> MenuIds { get; set; } = [];

    [ExporterHeader(IsIgnore = true)]
    public List<long> DeptIds { get; set; } = [];

    [ExporterHeader(IsIgnore = true)]
    public List<RoleFieldGrantDto> Fields { get; set; } = [];
}

[ExcelImporter(IsLabelingError = true, ImportHeaderFilter = typeof(InfrastructureHeaderFilter))]
public class RoleImportDto
{
    [ImporterHeader(Name = "field.system.role.name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [ImporterHeader(Name = "field.system.role.code")]
    [Required]
    public string Code { get; set; } = string.Empty;

    [ImporterHeader(Name = "field.system.role.dataScope")]
    public string? DataScope { get; set; }

    [ImporterHeader(Name = "field.system.role.status")]
    public string? Status { get; set; }

    [ImporterHeader(Name = "field.system.role.remark")]
    public string? Remark { get; set; }
}

public class MenuSaveDto
{
    public long ParentId { get; set; }

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? I18nKey { get; set; }

    public string? Permission { get; set; }

    public MenuType Type { get; set; } = MenuType.Menu;

    public string? Path { get; set; }

    public string? Component { get; set; }

    public string? Icon { get; set; }

    public int Sort { get; set; }

    public bool Visible { get; set; } = true;

    public CommonStatus Status { get; set; } = CommonStatus.Enabled;
}

[ExcelExporter(Name = "菜单", ExporterHeadersFilter = typeof(InfrastructureHeaderFilter))]
public class MenuExportDto
{
    [ExporterHeader(DisplayName = "field.system.menu.code")]
    public string Code { get; set; } = string.Empty;

    [ExporterHeader(DisplayName = "field.system.menu.name")]
    public string Name { get; set; } = string.Empty;

    [ExporterHeader(DisplayName = "field.system.menu.parentCode")]
    public string? ParentCode { get; set; }

    [ExporterHeader(DisplayName = "field.system.menu.type")]
    public MenuType Type { get; set; }

    [ExporterHeader(DisplayName = "field.system.menu.permission")]
    public string? Permission { get; set; }

    [ExporterHeader(DisplayName = "field.system.menu.path")]
    public string? Path { get; set; }

    [ExporterHeader(DisplayName = "field.system.menu.component")]
    public string? Component { get; set; }

    [ExporterHeader(DisplayName = "field.system.menu.icon")]
    public string? Icon { get; set; }

    [ExporterHeader(DisplayName = "field.system.menu.sort")]
    public int Sort { get; set; }

    [ExporterHeader(DisplayName = "field.system.menu.visible")]
    public bool Visible { get; set; }
}

[ExcelImporter(IsLabelingError = true, ImportHeaderFilter = typeof(InfrastructureHeaderFilter))]
public class MenuImportDto
{
    [ImporterHeader(Name = "field.system.menu.code")]
    [Required]
    public string Code { get; set; } = string.Empty;

    [ImporterHeader(Name = "field.system.menu.name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [ImporterHeader(Name = "field.system.menu.parentCode")]
    public string? ParentCode { get; set; }

    [ImporterHeader(Name = "field.system.menu.type")]
    public string? Type { get; set; }

    [ImporterHeader(Name = "field.system.menu.permission")]
    public string? Permission { get; set; }

    [ImporterHeader(Name = "field.system.menu.path")]
    public string? Path { get; set; }

    [ImporterHeader(Name = "field.system.menu.component")]
    public string? Component { get; set; }

    [ImporterHeader(Name = "field.system.menu.icon")]
    public string? Icon { get; set; }

    [ImporterHeader(Name = "field.system.menu.sort")]
    public int Sort { get; set; }

    [ImporterHeader(Name = "field.system.menu.visible")]
    public bool Visible { get; set; } = true;
}

public class I18nQuery : PageQuery
{
    public string? Culture { get; set; }
}

[ExcelExporter(Name = "多语言", ExporterHeadersFilter = typeof(InfrastructureHeaderFilter))]
public class I18nDto
{
    public long Id { get; set; }

    [ExporterHeader(DisplayName = "field.system.i18n.culture")]
    public string Culture { get; set; } = string.Empty;

    [ExporterHeader(DisplayName = "field.system.i18n.key")]
    public string Key { get; set; } = string.Empty;

    [ExporterHeader(DisplayName = "field.system.i18n.value")]
    public string Value { get; set; } = string.Empty;
}

[ExcelImporter(IsLabelingError = true, ImportHeaderFilter = typeof(InfrastructureHeaderFilter))]
public class I18nImportDto
{
    [ImporterHeader(Name = "field.system.i18n.culture")]
    [Required]
    public string Culture { get; set; } = string.Empty;

    [ImporterHeader(Name = "field.system.i18n.key")]
    [Required]
    public string Key { get; set; } = string.Empty;

    [ImporterHeader(Name = "field.system.i18n.value")]
    [Required]
    public string Value { get; set; } = string.Empty;
}

public class I18nSaveDto
{
    [Required]
    public string Culture { get; set; } = "zh-CN";

    [Required]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;
}

public class ColumnMetaDto
{
    public string Field { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string ValueType { get; set; } = "text";

    public bool CanView { get; set; }

    public bool CanEdit { get; set; }

    public bool CanExport { get; set; }

    public bool Visible { get; set; }

    public int Sort { get; set; }

    public int? Width { get; set; }

    public string? Fixed { get; set; }

    public string? SortOrder { get; set; }

    public int? SortRank { get; set; }

    public bool LockVisible { get; set; }

    public string? DictCode { get; set; }

    public List<OptionDto> Options { get; set; } = [];
}

public class PageColumnDto
{
    public string PageCode { get; set; } = string.Empty;

    public List<ColumnMetaDto> Columns { get; set; } = [];
}

public class ColumnSettingDto
{
    [Required]
    public string Field { get; set; } = string.Empty;

    public bool Visible { get; set; } = true;

    public int Sort { get; set; }

    public int? Width { get; set; }

    public string? Fixed { get; set; }

    public string? SortOrder { get; set; }

    public int? SortRank { get; set; }
}

public class FieldItemDto
{
    public long Id { get; set; }

    public string Field { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string ValueType { get; set; } = "text";
}

public class FieldPageDto
{
    public string PageCode { get; set; } = string.Empty;

    public string PageName { get; set; } = string.Empty;

    public List<FieldItemDto> Fields { get; set; } = [];
}

public class TableInfoDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public class ColumnInfoDto
{
    public string Name { get; set; } = string.Empty;

    public string DbType { get; set; } = string.Empty;

    public string CsType { get; set; } = string.Empty;

    public bool Nullable { get; set; }

    public bool PrimaryKey { get; set; }

    public int Length { get; set; }

    public string? Description { get; set; }

    public bool Inherited { get; set; }

    public string ValueType { get; set; } = "text";
}

public class CodeGenRequest
{
    [Required]
    public string TableName { get; set; } = string.Empty;

    [Required]
    public string EntityName { get; set; } = string.Empty;

    [Required]
    public string Module { get; set; } = string.Empty;

    public string? PageCode { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string Icon { get; set; } = "TableOutlined";

    public bool Overwrite { get; set; }

    public List<CodeGenFieldDto> Fields { get; set; } = [];

    /// <summary>为空时生成全部标准按钮。</summary>
    public CodeGenButtonsDto? Buttons { get; set; }
}

public class CodeGenButtonsDto
{
    public bool Add { get; set; } = true;

    public bool Edit { get; set; } = true;

    public bool Delete { get; set; } = true;

    public bool Import { get; set; } = true;

    public bool Export { get; set; } = true;

    public List<CodeGenCustomButtonDto> Custom { get; set; } = [];
}

public class CodeGenCustomButtonDto
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

public class CodeGenFieldDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string ValueType { get; set; } = "text";

    public string? DictCode { get; set; }

    public List<OptionDto> Options { get; set; } = [];

    public string? RelationTable { get; set; }

    public string? RelationValue { get; set; }

    public string? RelationLabel { get; set; }
}

public class CodeGenFileDto
{
    public string Path { get; set; } = string.Empty;

    public string Language { get; set; } = "csharp";

    public string Content { get; set; } = string.Empty;
}

public class CodeGenPreviewDto
{
    public string EntityBase { get; set; } = string.Empty;

    public List<string> Warnings { get; set; } = [];

    public List<CodeGenFileDto> Files { get; set; } = [];
}

public class CodeGenResultDto
{
    public List<string> Written { get; set; } = [];

    public List<string> Skipped { get; set; } = [];

    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Magicodes 列头过滤器。翻译放在当次导出的 HeaderInfo 上，导入列头会克隆特性，避免改写缓存的 Attribute。
/// </summary>
public class InfrastructureHeaderFilter :
    Magicodes.ExporterAndImporter.Core.Filters.IExporterHeadersFilter,
    Magicodes.ExporterAndImporter.Core.Filters.IImportHeaderFilter
{
    public IList<Magicodes.ExporterAndImporter.Core.Models.ExporterHeaderInfo> Filter(
        IList<Magicodes.ExporterAndImporter.Core.Models.ExporterHeaderInfo> exporterHeaderInfos)
    {
        return ExcelAmbient.ApplyExport(exporterHeaderInfos);
    }

    public List<Magicodes.ExporterAndImporter.Core.Models.ImporterHeaderInfo> Filter(
        List<Magicodes.ExporterAndImporter.Core.Models.ImporterHeaderInfo> importerHeaderInfos)
    {
        return ExcelAmbient.ApplyImport(importerHeaderInfos);
    }
}

public static class ExcelAmbient
{
    private static readonly AsyncLocal<ExcelAmbientState?> Current = new();

    public static ExcelAmbientState? State => Current.Value;

    public static IDisposable Push(ExcelAmbientState state)
    {
        var previous = Current.Value;
        Current.Value = state;
        return new Popper(previous);
    }

    public static IList<Magicodes.ExporterAndImporter.Core.Models.ExporterHeaderInfo> ApplyExport(
        IList<Magicodes.ExporterAndImporter.Core.Models.ExporterHeaderInfo> headers)
    {
        var state = Current.Value;
        if (state == null)
        {
            return headers;
        }

        var list = new List<Magicodes.ExporterAndImporter.Core.Models.ExporterHeaderInfo>();
        foreach (var header in headers)
        {
            if (header.ExporterHeaderAttribute?.IsIgnore == true)
            {
                continue;
            }

            if (state.DeniedFields.Contains(header.PropertyName ?? string.Empty))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(header.DisplayName)
                && state.Labels.TryGetValue(header.DisplayName, out var label))
            {
                header.DisplayName = label;
            }

            list.Add(header);
        }

        return list;
    }

    public static List<Magicodes.ExporterAndImporter.Core.Models.ImporterHeaderInfo> ApplyImport(
        List<Magicodes.ExporterAndImporter.Core.Models.ImporterHeaderInfo> headers)
    {
        var state = Current.Value;
        if (state == null)
        {
            return headers;
        }

        foreach (var header in headers)
        {
            if (header.Header == null)
            {
                continue;
            }

            var name = header.Header.Name;
            if (!string.IsNullOrEmpty(name) && state.Labels.TryGetValue(name, out var label))
            {
                name = label;
            }

            var denied = state.DeniedFields.Contains(header.PropertyName ?? string.Empty);
            header.Header = new ImporterHeaderAttribute
            {
                Name = name,
                Description = header.Header.Description,
                Author = header.Header.Author,
                AutoTrim = header.Header.AutoTrim,
                FixAllSpace = header.Header.FixAllSpace,
                Format = header.Header.Format,
                ColumnIndex = header.Header.ColumnIndex,
                IsAllowRepeat = header.Header.IsAllowRepeat,
                IsIgnore = header.Header.IsIgnore || denied,
                IsInterValidation = header.Header.IsInterValidation,
                ShowInputMessage = header.Header.ShowInputMessage
            };
        }

        return headers;
    }

    private sealed class Popper(ExcelAmbientState? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}

public sealed class ExcelAmbientState
{
    public Dictionary<string, string> Labels { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> DeniedFields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public class MessageSendRequest
{
    [Required]
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    public MessageScope Scope { get; set; } = MessageScope.Tenant;

    public List<long> UserIds { get; set; } = [];
}

public class MessageInboxQuery : PageQuery
{
    public bool? Unread { get; set; }
}

public class MessageInboxDto
{
    public long Id { get; set; }

    public long MessageId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public MessageScope Scope { get; set; }

    public string SenderName { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime? ReadTime { get; set; }
}

public class MessageSentDto
{
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public MessageScope Scope { get; set; }

    public int RecipientCount { get; set; }

    public DateTime CreateTime { get; set; }
}

public class MessageNoticeDto
{
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public MessageScope Scope { get; set; }

    public long SenderId { get; set; }

    public string SenderName { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }
}

public class MessageUserOptionDto
{
    public long Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string NickName { get; set; } = string.Empty;

    public string? DeptName { get; set; }
}
