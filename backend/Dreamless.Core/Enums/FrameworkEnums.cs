namespace Dreamless.Core.Enums;

public enum CommonStatus
{
    Disabled = 0,
    Enabled = 1
}

/// <summary>
/// 部门数据范围。租户隔离始终生效，部门范围按角色可选叠加。
/// 多角色取并集；任一角色为 All 时不再按部门收窄。
/// </summary>
public enum DataScope
{
    All = 1,
    DeptAndChild = 2,
    Dept = 3,
    Self = 4,
    Custom = 5
}

public enum MenuType
{
    Directory = 1,
    Menu = 2,
    Button = 3
}

/// <summary>消息接收范围。租户和部门都取发送人当前所在的范围。</summary>
public enum MessageScope
{
    Tenant = 1,
    Dept = 2,
    Users = 3
}
