using Dreamless.Core.Enums;
using SqlSugar;

namespace Dreamless.Core.Entities;

[SugarTable("SysTenant")]
[SugarIndex("UX_SysTenant_Code", nameof(Code), OrderByType.Asc, true)]
public class SysTenant : BaseEntity
{
    [SugarColumn(Length = 100)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 64)]
    public string Code { get; set; } = string.Empty;

    public CommonStatus Status { get; set; } = CommonStatus.Enabled;

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? Contact { get; set; }

    [SugarColumn(Length = 32, IsNullable = true)]
    public string? Phone { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ExpireTime { get; set; }

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Remark { get; set; }
}

[SugarTable("SysDept")]
[SugarIndex("IX_SysDept_Tenant_Parent", nameof(TenantId), OrderByType.Asc, nameof(ParentId), OrderByType.Asc)]
public class SysDept : BaseTenantEntity
{
    public long ParentId { get; set; }

    [SugarColumn(Length = 100)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 64)]
    public string Code { get; set; } = string.Empty;

    public int Sort { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? Leader { get; set; }

    [SugarColumn(Length = 32, IsNullable = true)]
    public string? Phone { get; set; }

    public CommonStatus Status { get; set; } = CommonStatus.Enabled;

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Remark { get; set; }
}

[SugarTable("SysPosition")]
[SugarIndex("IX_SysPosition_Tenant_Code", nameof(TenantId), OrderByType.Asc, nameof(Code), OrderByType.Asc)]
public class SysPosition : BaseTenantEntity
{
    [SugarColumn(Length = 64)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 64)]
    public string Code { get; set; } = string.Empty;

    public int Sort { get; set; }

    public CommonStatus Status { get; set; } = CommonStatus.Enabled;

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Remark { get; set; }
}

[SugarTable("SysUser")]
[SugarIndex("IX_SysUser_Tenant_UserName", nameof(TenantId), OrderByType.Asc, nameof(UserName), OrderByType.Asc)]
public class SysUser : BaseDeptEntity
{
    [SugarColumn(Length = 64)]
    public string UserName { get; set; } = string.Empty;

    [SugarColumn(Length = 256)]
    public string PasswordHash { get; set; } = string.Empty;

    [SugarColumn(Length = 64)]
    public string NickName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public long? PositionId { get; set; }

    /// <summary>头像地址，指向 /api/files/avatars 下的文件。</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Avatar { get; set; }

    [SugarColumn(Length = 32, IsNullable = true)]
    public string? EmployeeNo { get; set; }

    [SugarColumn(Length = 16, IsNullable = true)]
    public string? Gender { get; set; }

    [SugarColumn(Length = 32, IsNullable = true)]
    public string? IdCard { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? Birthday { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? HireDate { get; set; }

    [SugarColumn(Length = 200, IsNullable = true)]
    public string? Address { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? Email { get; set; }

    [SugarColumn(Length = 32, IsNullable = true)]
    public string? Phone { get; set; }

    public CommonStatus Status { get; set; } = CommonStatus.Enabled;

    /// <summary>宿主管理员跨租户，并跳过功能权限与字段权限校验。</summary>
    public bool IsHostAdmin { get; set; }

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Remark { get; set; }
}

[SugarTable("SysRole")]
[SugarIndex("IX_SysRole_Tenant_Code", nameof(TenantId), OrderByType.Asc, nameof(Code), OrderByType.Asc)]
public class SysRole : BaseTenantEntity
{
    [SugarColumn(Length = 64)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 64)]
    public string Code { get; set; } = string.Empty;

    public int Sort { get; set; }

    public DataScope DataScope { get; set; } = DataScope.Self;

    public CommonStatus Status { get; set; } = CommonStatus.Enabled;

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Remark { get; set; }
}

[SugarTable("SysUserRole")]
public class SysUserRole : ITenantEntity
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    public long TenantId { get; set; }

    public long UserId { get; set; }

    public long RoleId { get; set; }
}

[SugarTable("SysMenu")]
[SugarIndex("UX_SysMenu_Code", nameof(Code), OrderByType.Asc, true)]
public class SysMenu : BaseEntity
{
    public long ParentId { get; set; }

    [SugarColumn(Length = 64)]
    public string Code { get; set; } = string.Empty;

    [SugarColumn(Length = 64)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? I18nKey { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? Permission { get; set; }

    public MenuType Type { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? Path { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? Component { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? Icon { get; set; }

    public int Sort { get; set; }

    public bool Visible { get; set; } = true;

    public CommonStatus Status { get; set; } = CommonStatus.Enabled;
}

[SugarTable("SysRoleMenu")]
public class SysRoleMenu
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    public long RoleId { get; set; }

    public long MenuId { get; set; }
}

/// <summary>页面字段注册表。字段权限和用户列配置都引用这里的 FieldName。</summary>
[SugarTable("SysField")]
[SugarIndex("UX_SysField_Page_Field", nameof(PageCode), OrderByType.Asc, nameof(FieldName), OrderByType.Asc, true)]
public class SysField : BaseEntity
{
    [SugarColumn(Length = 64)]
    public string PageCode { get; set; } = string.Empty;

    [SugarColumn(Length = 64)]
    public string FieldName { get; set; } = string.Empty;

    [SugarColumn(Length = 64)]
    public string DisplayName { get; set; } = string.Empty;

    [SugarColumn(Length = 128)]
    public string I18nKey { get; set; } = string.Empty;

    [SugarColumn(Length = 32)]
    public string ValueType { get; set; } = "text";

    public int Sort { get; set; }

    /// <summary>为 true 时用户不能把该列隐藏。</summary>
    public bool LockVisible { get; set; }

    /// <summary>没有个人列配置时是否默认显示。</summary>
    public bool DefaultVisible { get; set; } = true;

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? DictCode { get; set; }

    /// <summary>自定义枚举，JSON 数组 [{value,label}]。</summary>
    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? OptionJson { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? RelationTable { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? RelationValue { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? RelationLabel { get; set; }
}

[SugarTable("SysRoleField")]
public class SysRoleField
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    public long RoleId { get; set; }

    public long FieldId { get; set; }

    public bool CanView { get; set; }

    public bool CanEdit { get; set; }

    public bool CanExport { get; set; }
}

[SugarTable("SysRoleDept")]
public class SysRoleDept
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    public long RoleId { get; set; }

    public long DeptId { get; set; }
}

/// <summary>用户在某个页面上的列显示偏好，不能突破字段查看权限。</summary>
[SugarTable("SysUserColumn")]
[SugarIndex("UX_SysUserColumn", nameof(UserId), OrderByType.Asc, nameof(PageCode), OrderByType.Asc, nameof(FieldName), OrderByType.Asc, true)]
public class SysUserColumn
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    public long TenantId { get; set; }

    public long UserId { get; set; }

    [SugarColumn(Length = 64)]
    public string PageCode { get; set; } = string.Empty;

    [SugarColumn(Length = 64)]
    public string FieldName { get; set; } = string.Empty;

    public bool Visible { get; set; } = true;

    public int Sort { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? Width { get; set; }

    [SugarColumn(Length = 8, IsNullable = true)]
    public string? Fixed { get; set; }

    /// <summary>默认排序方向，asc 或 desc。为空表示不参与默认排序。</summary>
    [SugarColumn(Length = 4, IsNullable = true)]
    public string? SortOrder { get; set; }

    /// <summary>默认排序优先级，从 1 开始，越小越优先。</summary>
    [SugarColumn(IsNullable = true)]
    public int? SortRank { get; set; }
}

[SugarTable("SysI18n")]
[SugarIndex("UX_SysI18n_Culture_Key", nameof(Culture), OrderByType.Asc, nameof(Key), OrderByType.Asc, true)]
public class SysI18n : BaseEntity
{
    [SugarColumn(Length = 16)]
    public string Culture { get; set; } = "zh-CN";

    [SugarColumn(Length = 160)]
    public string Key { get; set; } = string.Empty;

    [SugarColumn(Length = 1000)]
    public string Value { get; set; } = string.Empty;
}

[SugarTable("SysMessage")]
[SugarIndex("IX_SysMessage_Tenant_Time", nameof(TenantId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
public class SysMessage : BaseTenantEntity
{
    [SugarColumn(Length = 120)]
    public string Title { get; set; } = string.Empty;

    [SugarColumn(Length = 2000)]
    public string Content { get; set; } = string.Empty;

    public MessageScope Scope { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? DeptId { get; set; }

    public long SenderId { get; set; }

    [SugarColumn(Length = 64)]
    public string SenderName { get; set; } = string.Empty;
}

[SugarTable("SysMessageUser")]
[SugarIndex("IX_SysMessageUser_User", nameof(TenantId), OrderByType.Asc, nameof(UserId), OrderByType.Asc)]
public class SysMessageUser : BaseTenantEntity
{
    public long MessageId { get; set; }

    public long UserId { get; set; }

    public bool IsRead { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ReadTime { get; set; }
}

[SugarTable("SysRefreshToken")]
public class SysRefreshToken
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    public long UserId { get; set; }

    public long TenantId { get; set; }

    [SugarColumn(Length = 128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpireTime { get; set; }

    public DateTime CreateTime { get; set; }

    public bool Revoked { get; set; }
}
