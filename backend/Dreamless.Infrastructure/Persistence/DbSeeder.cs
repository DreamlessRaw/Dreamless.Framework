using Microsoft.Extensions.Hosting;

namespace Dreamless.Infrastructure.Persistence;

public sealed class DbSeeder(ISqlSugarClient db, PasswordService passwords, IHostEnvironment environment)
{
    public async Task SeedAsync()
    {
        db.DbMaintenance.CreateDatabase();
        db.CodeFirst.SetStringDefaultLength(200).InitTables(EntityTypes.All);
        await db.Ado.ExecuteCommandAsync("""
            IF OBJECT_ID(N'dbo.Biz_Customer', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Biz_Customer (
                    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId BIGINT NOT NULL,
                    DeptId BIGINT NULL,
                    Name NVARCHAR(100) NOT NULL,
                    Contact NVARCHAR(50) NULL,
                    Phone NVARCHAR(30) NULL,
                    Level INT NOT NULL CONSTRAINT DF_Biz_Customer_Level DEFAULT (1),
                    Remark NVARCHAR(500) NULL,
                    CreateTime DATETIME2 NOT NULL CONSTRAINT DF_Biz_Customer_CreateTime DEFAULT (SYSDATETIME()),
                    CreateUserId BIGINT NULL,
                    UpdateTime DATETIME2 NULL,
                    UpdateUserId BIGINT NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_Biz_Customer_IsDeleted DEFAULT (0)
                );
            END
            """);

        if (await db.Queryable<SysUser>().AnyAsync(u => u.UserName == "admin"))
        {
            await EnsurePositionFeature();
            await EnsureMessageFeature();
            return;
        }

        var tenantId = await db.Insertable(new SysTenant
        {
            Name = "默认租户",
            Code = "default",
            Status = CommonStatus.Enabled,
            Contact = "系统管理员",
            Remark = "框架初始化租户"
        }).ExecuteReturnBigIdentityAsync();

        var hq = await Dept(tenantId, 0, "总部", "HQ", 1);
        var rd = await Dept(tenantId, hq, "研发部", "RD", 1);
        var fe = await Dept(tenantId, rd, "前端组", "FE", 1);
        var mkt = await Dept(tenantId, hq, "市场部", "MKT", 2);
        var manager = await Position(tenantId, "总经理", "GM", 1);
        var engineer = await Position(tenantId, "工程师", "ENG", 2);
        var staff = await Position(tenantId, "职员", "CLK", 3);

        var menuIds = await Menus();
        var fieldIds = await Fields();
        await Texts();

        var superId = await Role(tenantId, "超级管理员", "SuperAdmin", DataScope.All, 1);
        var clerkId = await Role(tenantId, "职员", "Clerk", DataScope.DeptAndChild, 2);
        await db.Insertable(menuIds.Select(id => new SysRoleMenu { RoleId = superId, MenuId = id }).ToList()).ExecuteCommandAsync();
        await db.Insertable(fieldIds.Select(field => new SysRoleField
        {
            RoleId = superId,
            FieldId = field.Id,
            CanView = true,
            CanEdit = field.Field != "createTime",
            CanExport = true
        }).ToList()).ExecuteCommandAsync();

        var clerkMenus = new[] { "dashboard", "system", "system.user", "system.user.export", "message", "message.notice", "message.notice.delete" };
        var clerkMenuIds = await db.Queryable<SysMenu>().Where(m => clerkMenus.Contains(m.Code)).Select(m => m.Id).ToListAsync();
        await db.Insertable(clerkMenuIds.Select(id => new SysRoleMenu { RoleId = clerkId, MenuId = id }).ToList()).ExecuteCommandAsync();
        var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "email", "phone", "idCard", "address" };
        await db.Insertable(fieldIds.Where(f => f.Page == "system:user").Select(field => new SysRoleField
        {
            RoleId = clerkId,
            FieldId = field.Id,
            CanView = !hidden.Contains(field.Field),
            CanEdit = false,
            CanExport = !hidden.Contains(field.Field)
        }).ToList()).ExecuteCommandAsync();

        await User(tenantId, hq, manager, "admin", "系统管理员", "Admin@123456", true, superId, "#1677ff");
        await User(tenantId, rd, staff, "clerk", "研发职员", "Clerk@123456", false, clerkId, "#13a8a8");
        await User(tenantId, fe, engineer, "fe", "前端工程师", "Clerk@123456", false, clerkId, "#722ed1");
        await User(tenantId, mkt, staff, "marketing", "市场专员", "Clerk@123456", false, clerkId, "#d48806");
        await EnsureMessageFeature();
    }

    private async Task<long> Dept(long tenantId, long parentId, string name, string code, int sort) =>
        await db.Insertable(new SysDept
        {
            TenantId = tenantId,
            ParentId = parentId,
            Name = name,
            Code = code,
            Sort = sort,
            Status = CommonStatus.Enabled
        }).ExecuteReturnBigIdentityAsync();

    private async Task<long> Position(long tenantId, string name, string code, int sort) =>
        await db.Insertable(new SysPosition
        {
            TenantId = tenantId,
            Name = name,
            Code = code,
            Sort = sort,
            Status = CommonStatus.Enabled
        }).ExecuteReturnBigIdentityAsync();

    private async Task<long> Role(long tenantId, string name, string code, DataScope scope, int sort) =>
        await db.Insertable(new SysRole
        {
            TenantId = tenantId,
            Name = name,
            Code = code,
            Sort = sort,
            DataScope = scope,
            Status = CommonStatus.Enabled
        }).ExecuteReturnBigIdentityAsync();

    private async Task User(long tenantId, long deptId, long positionId, string userName, string nickName, string password, bool host, long roleId, string color)
    {
        var user = new SysUser
        {
            TenantId = tenantId,
            DeptId = deptId,
            PositionId = positionId,
            Avatar = WriteAvatar(userName, nickName, color),
            UserName = userName,
            NickName = nickName,
            EmployeeNo = userName switch
            {
                "admin" => "EMP0001",
                "clerk" => "EMP0101",
                "fe" => "EMP0102",
                _ => "EMP0201"
            },
            Gender = userName is "clerk" or "marketing" ? "Female" : "Male",
            IdCard = userName switch
            {
                "admin" => "110101198001011234",
                "clerk" => "110101199203084321",
                "fe" => "110101199508156789",
                _ => "110101199611227890"
            },
            Birthday = userName switch
            {
                "admin" => new DateTime(1980, 1, 1),
                "clerk" => new DateTime(1992, 3, 8),
                "fe" => new DateTime(1995, 8, 15),
                _ => new DateTime(1996, 11, 22)
            },
            HireDate = userName switch
            {
                "admin" => new DateTime(2018, 3, 1),
                "clerk" => new DateTime(2021, 7, 1),
                "fe" => new DateTime(2022, 4, 18),
                _ => new DateTime(2023, 1, 9)
            },
            Address = userName switch
            {
                "admin" => "北京市东城区",
                "clerk" => "北京市海淀区",
                "fe" => "北京市朝阳区",
                _ => "上海市浦东新区"
            },
            Email = $"{userName}@dreamless.local",
            Phone = userName switch
            {
                "admin" => "13800000001",
                "clerk" => "13800000002",
                "fe" => "13800000003",
                _ => "13800000004"
            },
            Status = CommonStatus.Enabled,
            IsHostAdmin = host
        };
        user.PasswordHash = passwords.Hash(user, password);
        var id = await db.Insertable(user).ExecuteReturnBigIdentityAsync();
        await db.Insertable(new SysUserRole { TenantId = tenantId, UserId = id, RoleId = roleId }).ExecuteCommandAsync();
    }

    private async Task<List<long>> Menus()
    {
        var seeds = new List<MenuSeed>
        {
            new("dashboard", "", "工作台", "menu.dashboard", MenuType.Menu, "/dashboard", "dashboard/index", "DashboardOutlined", Permissions.Dashboard, 1),
            new("system", "", "系统管理", "menu.system", MenuType.Directory, "/system", null, "SettingOutlined", null, 2),
            new("system.tenant", "system", "租户管理", "menu.system.tenant", MenuType.Menu, "/system/tenant", "system/tenant/index", "BankOutlined", Permissions.TenantList, 1),
            new("system.dept", "system", "部门管理", "menu.system.dept", MenuType.Menu, "/system/dept", "system/dept/index", "ApartmentOutlined", Permissions.DeptList, 2),
            new("system.position", "system", "职位管理", "menu.system.position", MenuType.Menu, "/system/position", "system/position/index", "IdcardOutlined", Permissions.PositionList, 3),
            new("system.user", "system", "用户管理", "menu.system.user", MenuType.Menu, "/system/user", "system/user/index", "UserOutlined", Permissions.UserList, 4),
            new("system.role", "system", "角色权限", "menu.system.role", MenuType.Menu, "/system/role", "system/role/index", "SafetyOutlined", Permissions.RoleList, 4),
            new("system.menu", "system", "菜单管理", "menu.system.menu", MenuType.Menu, "/system/menu", "system/menu/index", "MenuOutlined", Permissions.MenuList, 5),
            new("system.i18n", "system", "多语言", "menu.system.i18n", MenuType.Menu, "/system/i18n", "system/i18n/index", "TranslationOutlined", Permissions.I18nList, 6),
            new("dev", "", "开发工具", "menu.dev", MenuType.Directory, "/dev", null, "CodeOutlined", null, 3),
            new("dev.codegen", "dev", "低代码生成", "menu.dev.codegen", MenuType.Menu, "/dev/codegen", "dev/codegen/index", "TableOutlined", Permissions.CodegenList, 1),
            new("message", "", "消息中心", "menu.message", MenuType.Directory, "/message", null, "BellOutlined", null, 4),
            new("message.notice", "message", "消息通知", "menu.message.notice", MenuType.Menu, "/message/notice", "message/notice/index", "BellOutlined", Permissions.MessageList, 1)
        };
        Buttons(seeds, "system.tenant", "system:tenant", ["add", "edit", "delete", "import", "export"]);
        Buttons(seeds, "system.dept", "system:dept", ["add", "edit", "delete", "import", "export"]);
        Buttons(seeds, "system.position", "system:position", ["add", "edit", "delete", "import", "export"]);
        Buttons(seeds, "system.user", "system:user", ["add", "edit", "delete", "import", "export"]);
        Buttons(seeds, "system.role", "system:role", ["add", "edit", "delete", "import", "export"]);
        Buttons(seeds, "system.menu", "system:menu", ["add", "edit", "delete", "import", "export"]);
        Buttons(seeds, "system.i18n", "system:i18n", ["add", "edit", "delete", "import", "export"]);
        Buttons(seeds, "dev.codegen", "dev:codegen", ["preview", "generate"]);
        Buttons(seeds, "message.notice", "message:notice", ["send", "delete"]);

        var ids = new Dictionary<string, long>();
        var ordered = new List<MenuSeed>();
        var pending = seeds.ToList();
        while (pending.Count > 0)
        {
            var ready = pending.Where(x => string.IsNullOrEmpty(x.Parent) || ids.ContainsKey(x.Parent) || ordered.Any(o => o.Code == x.Parent)).ToList();
            if (ready.Count == 0) break;
            ordered.AddRange(ready.Where(x => ordered.All(o => o.Code != x.Code)));
            pending.RemoveAll(x => ordered.Any(o => o.Code == x.Code));
            foreach (var item in ordered.Where(x => !ids.ContainsKey(x.Code)))
            {
                var parentId = string.IsNullOrEmpty(item.Parent) ? 0 : ids[item.Parent];
                var id = await db.Insertable(new SysMenu
                {
                    ParentId = parentId,
                    Code = item.Code,
                    Name = item.Name,
                    I18nKey = item.I18n,
                    Permission = item.Permission,
                    Type = item.Type,
                    Path = item.Path,
                    Component = item.Component,
                    Icon = item.Icon,
                    Sort = item.Sort,
                    Visible = true,
                    Status = CommonStatus.Enabled
                }).ExecuteReturnBigIdentityAsync();
                ids[item.Code] = id;
            }
        }

        foreach (var item in seeds)
        {
            _texts.Add(("zh-CN", item.I18n, item.Name));
            _texts.Add(("en-US", item.I18n, EnMenu(item.Code, item.Name)));
        }

        return ids.Values.ToList();
    }

    private readonly List<(string Culture, string Key, string Value)> _texts = [];

    private static void Buttons(List<MenuSeed> seeds, string parent, string prefix, string[] actions)
    {
        var index = 1;
        foreach (var action in actions)
        {
            var (zh, en) = action switch
            {
                "add" => ("新增", "Create"),
                "edit" => ("编辑", "Edit"),
                "delete" => ("删除", "Delete"),
                "import" => ("导入", "Import"),
                "export" => ("导出", "Export"),
                "preview" => ("预览", "Preview"),
                "generate" => ("生成", "Generate"),
                "send" => ("发送", "Send"),
                _ => (action, action)
            };
            seeds.Add(new MenuSeed($"{parent}.{action}", parent, zh, $"menu.{parent}.{action}", MenuType.Button, null, null, null, $"{prefix}:{action}", index++));
            _ = en;
        }
    }

    private static string EnMenu(string code, string fallback) => code switch
    {
        "dashboard" => "Dashboard",
        "system" => "System",
        "system.tenant" => "Tenants",
        "system.dept" => "Departments",
        "system.position" => "Positions",
        "system.user" => "Users",
        "system.role" => "Roles",
        "system.menu" => "Menus",
        "system.i18n" => "Languages",
        "dev" => "Developer",
        "dev.codegen" => "Code Generator",
        "message" => "Messages",
        "message.notice" => "Notifications",
        _ when code.EndsWith(".add") => "Create",
        _ when code.EndsWith(".edit") => "Edit",
        _ when code.EndsWith(".delete") => "Delete",
        _ when code.EndsWith(".import") => "Import",
        _ when code.EndsWith(".export") => "Export",
        _ when code.EndsWith(".preview") => "Preview",
        _ when code.EndsWith(".generate") => "Generate",
        _ when code.EndsWith(".send") => "Send",
        _ => fallback
    };

    private async Task<List<FieldRef>> Fields()
    {
        var seeds = new List<FieldSeed>
        {
            F("system:tenant", "name", "租户名称", "Tenant name", "text", true, true, null, 1),
            F("system:tenant", "code", "租户编码", "Tenant code", "text", true, true, null, 2),
            F("system:tenant", "contact", "联系人", "Contact", "text", false, true, null, 3),
            F("system:tenant", "phone", "电话", "Phone", "text", false, true, null, 4),
            F("system:tenant", "status", "状态", "Status", "select", false, true, "status", 5),
            F("system:tenant", "expireTime", "到期时间", "Expire time", "dateTime", false, true, null, 6),
            F("system:tenant", "remark", "备注", "Remark", "textarea", false, false, null, 7),
            F("system:tenant", "createTime", "创建时间", "Created at", "dateTime", false, true, null, 8),
            F("system:dept", "name", "部门名称", "Department", "text", true, true, null, 1),
            F("system:dept", "code", "部门编码", "Code", "text", false, true, null, 2),
            F("system:dept", "parentId", "上级部门", "Parent", "digit", false, false, null, 3),
            F("system:dept", "leader", "负责人", "Leader", "text", false, true, null, 4),
            F("system:dept", "phone", "电话", "Phone", "text", false, true, null, 5),
            F("system:dept", "sort", "排序", "Sort", "digit", false, true, null, 6),
            F("system:dept", "status", "状态", "Status", "select", false, true, "status", 7),
            F("system:dept", "remark", "备注", "Remark", "textarea", false, false, null, 8),
            F("system:user", "userName", "用户名", "Username", "text", true, true, null, 1),
            F("system:user", "nickName", "姓名", "Name", "text", false, true, null, 2),
            F("system:user", "deptId", "部门", "Department", "digit", false, false, null, 3),
            F("system:user", "deptName", "部门名称", "Department name", "text", false, true, null, 4),
            F("system:user", "positionId", "职位", "Position", "digit", false, false, null, 5),
            F("system:user", "positionName", "职位名称", "Position name", "text", false, true, null, 6),
            F("system:user", "avatar", "头像", "Avatar", "image", false, true, null, 7),
            F("system:user", "employeeNo", "工号", "Employee no.", "text", false, true, null, 8),
            F("system:user", "gender", "性别", "Gender", "select", false, true, "gender", 9),
            F("system:user", "idCard", "身份证", "ID card", "text", false, true, null, 10),
            F("system:user", "birthday", "出生日期", "Birthday", "date", false, true, null, 11),
            F("system:user", "hireDate", "入职日期", "Hire date", "date", false, true, null, 12),
            F("system:user", "address", "地址", "Address", "textarea", false, true, null, 13),
            F("system:user", "email", "邮箱", "Email", "text", false, true, null, 14),
            F("system:user", "phone", "手机", "Phone", "text", false, true, null, 15),
            F("system:user", "status", "状态", "Status", "select", false, true, "status", 16),
            F("system:user", "remark", "备注", "Remark", "textarea", false, false, null, 17),
            F("system:user", "createTime", "创建时间", "Created at", "dateTime", false, true, null, 18),
            F("system:position", "name", "职位名称", "Position", "text", true, true, null, 1),
            F("system:position", "code", "职位编码", "Code", "text", false, true, null, 2),
            F("system:position", "sort", "排序", "Sort", "digit", false, true, null, 3),
            F("system:position", "status", "状态", "Status", "select", false, true, "status", 4),
            F("system:position", "remark", "备注", "Remark", "textarea", false, false, null, 5),
            F("system:role", "name", "角色名称", "Role", "text", true, true, null, 1),
            F("system:role", "code", "角色编码", "Code", "text", false, true, null, 2),
            F("system:role", "sort", "排序", "Sort", "digit", false, true, null, 3),
            F("system:role", "dataScope", "数据范围", "Data scope", "select", false, true, "dataScope", 4),
            F("system:role", "status", "状态", "Status", "select", false, true, "status", 5),
            F("system:role", "remark", "备注", "Remark", "textarea", false, false, null, 6),
            F("system:menu", "name", "菜单名称", "Menu", "text", true, true, null, 1),
            F("system:menu", "code", "编码", "Code", "text", false, true, null, 2),
            F("system:menu", "type", "类型", "Type", "select", false, true, "menuType", 3),
            F("system:menu", "permission", "权限码", "Permission", "text", false, true, null, 4),
            F("system:menu", "path", "路由", "Path", "text", false, false, null, 5),
            F("system:menu", "component", "组件", "Component", "text", false, false, null, 6),
            F("system:menu", "icon", "图标", "Icon", "text", false, false, null, 7),
            F("system:menu", "sort", "排序", "Sort", "digit", false, true, null, 8),
            F("system:menu", "visible", "显示", "Visible", "select", false, true, "bool", 9),
            F("system:menu", "status", "状态", "Status", "select", false, true, "status", 10),
            F("system:i18n", "culture", "语言", "Culture", "text", false, true, null, 1),
            F("system:i18n", "key", "键", "Key", "text", true, true, null, 2),
            F("system:i18n", "value", "文案", "Text", "text", false, true, null, 3)
        };

        var refs = new List<FieldRef>();
        foreach (var field in seeds)
        {
            var key = $"field.{field.Page.Replace(':', '.')}.{field.Field}";
            var id = await db.Insertable(new SysField
            {
                PageCode = field.Page,
                FieldName = field.Field,
                DisplayName = field.Zh,
                I18nKey = key,
                ValueType = field.ValueType,
                Sort = field.Sort,
                LockVisible = field.Lock,
                DefaultVisible = field.Visible,
                DictCode = field.Dict
            }).ExecuteReturnBigIdentityAsync();
            refs.Add(new FieldRef(id, field.Page, field.Field));
            _texts.Add(("zh-CN", key, field.Zh));
            _texts.Add(("en-US", key, field.En));
            _texts.Add(("zh-CN", $"page.{field.Page.Replace(':', '.')}", PageZh(field.Page)));
            _texts.Add(("en-US", $"page.{field.Page.Replace(':', '.')}", PageEn(field.Page)));
        }

        Extra("field.system.user.password", "初始密码", "Password");
        Extra("field.system.dept.code", "部门编码", "Department code");
        Extra("field.system.dept.parentCode", "上级编码", "Parent code");
        Extra("field.system.menu.parentCode", "上级编码", "Parent code");
        Extra("field.common.createTime", "创建时间", "Created at");
        return refs;
    }

    private async Task Texts()
    {
        Extra("error.login", "账号或密码错误", "Incorrect account or password");
        Extra("error.unauthorized", "未登录或登录已过期", "Sign in required");
        Extra("error.forbidden", "无权限访问", "Permission denied");
        Extra("error.notFound", "数据不存在", "Record not found");
        Extra("error.user.exists", "用户名已存在", "Username already exists");
        Extra("error.user.self", "不能停用当前登录账号", "You cannot disable the signed-in account");
        Extra("error.cannotDeleteSelf", "不能删除当前登录账号", "You cannot delete the signed-in account");
        Extra("error.password.old", "原密码不正确", "Current password is incorrect");
        Extra("error.tenant.exists", "租户编码已存在", "Tenant code already exists");
        Extra("error.tenant.protected", "默认租户不能删除", "The default tenant cannot be deleted");
        Extra("error.tenant.inUse", "租户下仍有用户", "The tenant still has users");
        Extra("error.dept.exists", "部门编码已存在", "Department code already exists");
        Extra("error.dept.parent", "上级部门不能是自己", "A department cannot be its own parent");
        Extra("error.dept.hasChildren", "请先删除下级部门", "Delete child departments first");
        Extra("error.dept.hasUsers", "部门下仍有用户", "The department still has users");
        Extra("error.dept.missing", "部门不存在", "Department not found");
        Extra("error.position.exists", "职位编码已存在", "Position code already exists");
        Extra("error.position.inUse", "职位下仍有人员", "The position still has people");
        Extra("error.position.missing", "职位不存在", "Position not found");
        Extra("error.avatar.type", "请上传图片文件", "Upload an image file");
        Extra("error.avatar.size", "头像不能超过 2MB", "Avatar must be 2MB or smaller");
        Extra("field.system.position.code", "职位编码", "Position code");
        Extra("error.role.exists", "角色编码已存在", "Role code already exists");
        Extra("error.role.protected", "内置角色不能删除", "Built-in roles cannot be removed");
        Extra("error.role.inUse", "角色仍分配给用户", "The role is still assigned");
        Extra("error.role.missing", "角色不存在", "Role not found");
        Extra("error.menu.exists", "菜单编码已存在", "Menu code already exists");
        Extra("error.menu.hasChildren", "请先删除子菜单", "Delete child menus first");
        Extra("error.i18n.exists", "翻译键已存在", "Translation key already exists");
        Extra("error.message.title", "请填写 120 字以内的标题", "Enter a title of at most 120 characters");
        Extra("error.message.content", "请填写 2000 字以内的内容", "Enter content of at most 2000 characters");
        Extra("error.message.tenant", "当前账号没有租户，不能发送通知", "This account has no tenant and cannot send notices");
        Extra("error.message.dept", "当前账号没有部门，不能按部门通知", "This account has no department and cannot notify one");
        Extra("error.message.users", "请选择要通知的用户", "Choose the users to notify");
        Extra("error.message.user", "有用户不存在或已停用", "A selected user does not exist or is disabled");
        Extra("error.message.empty", "没有可通知的用户", "There is nobody to notify");
        Extra("error.field.denied", "包含无权查看的字段", "The selection contains a forbidden field");
        Extra("dict.status.enabled", "启用", "Enabled");
        Extra("dict.status.disabled", "停用", "Disabled");
        Extra("dict.scope.all", "全部", "All");
        Extra("dict.scope.deptAndChild", "本部门及下级", "Department and children");
        Extra("dict.scope.dept", "本部门", "Department");
        Extra("dict.scope.self", "仅本人", "Self");
        Extra("dict.scope.custom", "自定义", "Custom");
        Extra("dict.menu.directory", "目录", "Directory");
        Extra("dict.menu.menu", "菜单", "Menu");
        Extra("dict.menu.button", "按钮", "Button");
        Extra("dict.gender.male", "男", "Male");
        Extra("dict.gender.female", "女", "Female");
        Extra("dict.gender.unknown", "未知", "Unknown");
        Extra("dict.bool.yes", "是", "Yes");
        Extra("dict.bool.no", "否", "No");
        var rows = _texts
            .GroupBy(x => $"{x.Culture}|{x.Key}")
            .Select(g => g.First())
            .Select(x => new SysI18n { Culture = x.Culture, Key = x.Key, Value = x.Value })
            .ToList();
        await db.Insertable(rows).ExecuteCommandAsync();
    }

    private void Extra(string key, string zh, string en)
    {
        _texts.Add(("zh-CN", key, zh));
        _texts.Add(("en-US", key, en));
    }

    private static FieldSeed F(string page, string field, string zh, string en, string valueType, bool locked, bool visible, string? dict, int sort) =>
        new(page, field, zh, en, valueType, locked, visible, dict, sort);

    private static string PageZh(string page) => page switch
    {
        "system:tenant" => "租户管理",
        "system:dept" => "部门管理",
        "system:position" => "职位管理",
        "system:user" => "用户管理",
        "system:role" => "角色权限",
        "system:menu" => "菜单管理",
        "system:i18n" => "多语言",
        _ => page
    };

    private static string PageEn(string page) => page switch
    {
        "system:tenant" => "Tenants",
        "system:dept" => "Departments",
        "system:position" => "Positions",
        "system:user" => "Users",
        "system:role" => "Roles",
        "system:menu" => "Menus",
        "system:i18n" => "Languages",
        _ => page
    };

    private async Task EnsureMessageFeature()
    {
        if (!await db.Queryable<SysMenu>().AnyAsync(m => m.Code == "message"))
        {
            await db.Insertable(new SysMenu
            {
                ParentId = 0,
                Code = "message",
                Name = "消息中心",
                I18nKey = "menu.message",
                Type = MenuType.Directory,
                Path = "/message",
                Icon = "BellOutlined",
                Sort = 4,
                Visible = true,
                Status = CommonStatus.Enabled
            }).ExecuteReturnBigIdentityAsync();
        }

        var directory = await db.Queryable<SysMenu>().FirstAsync(m => m.Code == "message");
        if (directory != null && !await db.Queryable<SysMenu>().AnyAsync(m => m.Code == "message.notice"))
        {
            var menuId = await db.Insertable(new SysMenu
            {
                ParentId = directory.Id,
                Code = "message.notice",
                Name = "消息通知",
                I18nKey = "menu.message.notice",
                Type = MenuType.Menu,
                Path = "/message/notice",
                Component = "message/notice/index",
                Icon = "BellOutlined",
                Permission = Permissions.MessageList,
                Sort = 1,
                Visible = true,
                Status = CommonStatus.Enabled
            }).ExecuteReturnBigIdentityAsync();
            var actions = new (string Action, string Zh, string Permission)[]
            {
                ("send", "发送", Permissions.MessageSend),
                ("delete", "删除", Permissions.MessageDelete)
            };
            for (var i = 0; i < actions.Length; i++)
            {
                await db.Insertable(new SysMenu
                {
                    ParentId = menuId,
                    Code = $"message.notice.{actions[i].Action}",
                    Name = actions[i].Zh,
                    I18nKey = $"menu.message.notice.{actions[i].Action}",
                    Type = MenuType.Button,
                    Permission = actions[i].Permission,
                    Sort = i + 1,
                    Visible = true,
                    Status = CommonStatus.Enabled
                }).ExecuteCommandAsync();
            }
        }

        await Text("menu.message", "消息中心", "Messages");
        await Text("menu.message.notice", "消息通知", "Notifications");
        await Text("menu.message.notice.send", "发送", "Send");
        await Text("menu.message.notice.delete", "删除", "Delete");
        await Text("error.message.title", "请填写 120 字以内的标题", "Enter a title of at most 120 characters");
        await Text("error.message.content", "请填写 2000 字以内的内容", "Enter content of at most 2000 characters");
        await Text("error.message.tenant", "当前账号没有租户，不能发送通知", "This account has no tenant and cannot send notices");
        await Text("error.message.dept", "当前账号没有部门，不能按部门通知", "This account has no department and cannot notify one");
        await Text("error.message.users", "请选择要通知的用户", "Choose the users to notify");
        await Text("error.message.user", "有用户不存在或已停用", "A selected user does not exist or is disabled");
        await Text("error.message.empty", "没有可通知的用户", "There is nobody to notify");

        var all = await db.Queryable<SysMenu>().Where(m => m.Code == "message" || m.Code.StartsWith("message.notice")).Select(m => new { m.Id, m.Code }).ToListAsync();
        await GrantMenus("SuperAdmin", all.Select(m => m.Id).ToList());
        await GrantMenus("Clerk", all.Where(m => m.Code is "message" or "message.notice" or "message.notice.delete").Select(m => m.Id).ToList());
    }

    private async Task GrantMenus(string roleCode, List<long> menuIds)
    {
        var role = await db.Queryable<SysRole>().ClearFilter().FirstAsync(r => r.Code == roleCode && r.IsDeleted == false);
        if (role == null || menuIds.Count == 0)
        {
            return;
        }

        var owned = await db.Queryable<SysRoleMenu>().Where(x => x.RoleId == role.Id).Select(x => x.MenuId).ToListAsync();
        var missing = menuIds.Except(owned).Select(id => new SysRoleMenu { RoleId = role.Id, MenuId = id }).ToList();
        if (missing.Count > 0)
        {
            await db.Insertable(missing).ExecuteCommandAsync();
        }
    }

    private async Task EnsurePositionFeature()
    {
        var tenant = await db.Queryable<SysTenant>().FirstAsync(t => t.Code == "default");
        if (tenant == null)
        {
            return;
        }

        foreach (var item in new (string Name, string Code, int Sort)[] { ("总经理", "GM", 1), ("工程师", "ENG", 2), ("职员", "CLK", 3) })
        {
            if (!await db.Queryable<SysPosition>().AnyAsync(p => p.Code == item.Code))
            {
                await Position(tenant.Id, item.Name, item.Code, item.Sort);
            }
        }

        var system = await db.Queryable<SysMenu>().FirstAsync(m => m.Code == "system");
        if (system != null && !await db.Queryable<SysMenu>().AnyAsync(m => m.Code == "system.position"))
        {
            var menuId = await db.Insertable(new SysMenu
            {
                ParentId = system.Id,
                Code = "system.position",
                Name = "职位管理",
                I18nKey = "menu.system.position",
                Type = MenuType.Menu,
                Path = "/system/position",
                Component = "system/position/index",
                Icon = "IdcardOutlined",
                Permission = Permissions.PositionList,
                Sort = 3,
                Visible = true,
                Status = CommonStatus.Enabled
            }).ExecuteReturnBigIdentityAsync();
            var actions = new (string Action, string Zh)[] { ("add", "新增"), ("edit", "编辑"), ("delete", "删除"), ("import", "导入"), ("export", "导出") };
            for (var i = 0; i < actions.Length; i++)
            {
                await db.Insertable(new SysMenu
                {
                    ParentId = menuId,
                    Code = $"system.position.{actions[i].Action}",
                    Name = actions[i].Zh,
                    I18nKey = $"menu.system.position.{actions[i].Action}",
                    Type = MenuType.Button,
                    Permission = $"system:position:{actions[i].Action}",
                    Sort = i + 1,
                    Visible = true,
                    Status = CommonStatus.Enabled
                }).ExecuteCommandAsync();
            }
        }

        var fields = new (string Page, string Field, string Zh, string En, string Type, bool Lock, bool Visible, string? Dict, int Sort)[]
        {
            ("system:position", "name", "职位名称", "Position", "text", true, true, null, 1),
            ("system:position", "code", "职位编码", "Code", "text", false, true, null, 2),
            ("system:position", "sort", "排序", "Sort", "digit", false, true, null, 3),
            ("system:position", "status", "状态", "Status", "select", false, true, "status", 4),
            ("system:position", "remark", "备注", "Remark", "textarea", false, false, null, 5),
            ("system:user", "positionId", "职位", "Position", "digit", false, false, null, 5),
            ("system:user", "positionName", "职位名称", "Position name", "text", false, true, null, 6),
            ("system:user", "avatar", "头像", "Avatar", "image", false, true, null, 7),
            ("system:user", "employeeNo", "工号", "Employee no.", "text", false, true, null, 8),
            ("system:user", "gender", "性别", "Gender", "select", false, true, "gender", 9),
            ("system:user", "idCard", "身份证", "ID card", "text", false, true, null, 10),
            ("system:user", "birthday", "出生日期", "Birthday", "date", false, true, null, 11),
            ("system:user", "hireDate", "入职日期", "Hire date", "date", false, true, null, 12),
            ("system:user", "address", "地址", "Address", "textarea", false, true, null, 13)
        };
        foreach (var field in fields)
        {
            if (await db.Queryable<SysField>().AnyAsync(f => f.PageCode == field.Page && f.FieldName == field.Field))
            {
                continue;
            }

            var key = $"field.{field.Page.Replace(':', '.')}.{field.Field}";
            var id = await db.Insertable(new SysField
            {
                PageCode = field.Page,
                FieldName = field.Field,
                DisplayName = field.Zh,
                I18nKey = key,
                ValueType = field.Type,
                Sort = field.Sort,
                LockVisible = field.Lock,
                DefaultVisible = field.Visible,
                DictCode = field.Dict
            }).ExecuteReturnBigIdentityAsync();
            await GrantField(id, field.Page, field.Field);
            await Text(key, field.Zh, field.En);
        }

        var userOrder = new Dictionary<string, int>
        {
            ["userName"] = 1, ["nickName"] = 2, ["deptId"] = 3, ["deptName"] = 4,
            ["positionId"] = 5, ["positionName"] = 6, ["avatar"] = 7, ["employeeNo"] = 8,
            ["gender"] = 9, ["idCard"] = 10, ["birthday"] = 11, ["hireDate"] = 12,
            ["address"] = 13, ["email"] = 14, ["phone"] = 15, ["status"] = 16,
            ["remark"] = 17, ["createTime"] = 18
        };
        var userFields = await db.Queryable<SysField>().Where(f => f.PageCode == "system:user").ToListAsync();
        foreach (var field in userFields)
        {
            if (userOrder.TryGetValue(field.FieldName, out var sort) && field.Sort != sort)
            {
                field.Sort = sort;
                await db.Updateable(field).UpdateColumns(f => new { f.Sort }).ExecuteCommandAsync();
            }
        }

        await Text("menu.system.position", "职位管理", "Positions");
        await Text("page.system.position", "职位管理", "Positions");
        await Text("error.position.exists", "职位编码已存在", "Position code already exists");
        await Text("error.position.inUse", "职位下仍有人员", "The position still has people");
        await Text("error.position.missing", "职位不存在", "Position not found");
        await Text("error.avatar.type", "请上传图片文件", "Upload an image file");
        await Text("error.avatar.size", "头像不能超过 2MB", "Avatar must be 2MB or smaller");
        await Text("dict.gender.male", "男", "Male");
        await Text("dict.gender.female", "女", "Female");
        await Text("dict.gender.unknown", "未知", "Unknown");
        foreach (var action in new[] { "add", "edit", "delete", "import", "export" })
        {
            var zh = action switch { "add" => "新增", "edit" => "编辑", "delete" => "删除", "import" => "导入", _ => "导出" };
            var en = action switch { "add" => "Create", "edit" => "Edit", "delete" => "Delete", "import" => "Import", _ => "Export" };
            await Text($"menu.system.position.{action}", zh, en);
        }

        var menuIds = await db.Queryable<SysMenu>().Where(m => m.Code.StartsWith("system.position")).Select(m => m.Id).ToListAsync();
        var super = await db.Queryable<SysRole>().FirstAsync(r => r.Code == "SuperAdmin");
        if (super != null)
        {
            var owned = await db.Queryable<SysRoleMenu>().Where(x => x.RoleId == super.Id).Select(x => x.MenuId).ToListAsync();
            var missing = menuIds.Except(owned).Select(id => new SysRoleMenu { RoleId = super.Id, MenuId = id }).ToList();
            if (missing.Count > 0)
            {
                await db.Insertable(missing).ExecuteCommandAsync();
            }
        }

        var users = await db.Queryable<SysUser>().ToListAsync();
        var positions = await db.Queryable<SysPosition>().ToListAsync();
        long? IdOf(string code) => positions.FirstOrDefault(p => p.Code == code)?.Id;
        foreach (var user in users.Where(u => u.PositionId == null || string.IsNullOrWhiteSpace(u.Avatar)))
        {
            user.PositionId ??= user.UserName switch
            {
                "admin" => IdOf("GM"),
                "fe" => IdOf("ENG"),
                _ => IdOf("CLK")
            };
            if (string.IsNullOrWhiteSpace(user.Avatar))
            {
                var color = user.UserName switch { "admin" => "#1677ff", "clerk" => "#13a8a8", "fe" => "#722ed1", _ => "#d48806" };
                user.Avatar = WriteAvatar(user.UserName, user.NickName, color);
            }

            await db.Updateable(user).UpdateColumns(u => new { u.PositionId, u.Avatar }).ExecuteCommandAsync();
        }

        foreach (var user in await db.Queryable<SysUser>().Where(u => u.EmployeeNo == null).ToListAsync())
        {
            user.EmployeeNo = user.UserName switch
            {
                "admin" => "EMP0001",
                "clerk" => "EMP0101",
                "fe" => "EMP0102",
                _ => "EMP0201"
            };
            user.Gender = user.UserName is "clerk" or "marketing" ? "Female" : "Male";
            user.IdCard = user.UserName switch
            {
                "admin" => "110101198001011234",
                "clerk" => "110101199203084321",
                "fe" => "110101199508156789",
                _ => "110101199611227890"
            };
            user.Birthday = user.UserName switch
            {
                "admin" => new DateTime(1980, 1, 1),
                "clerk" => new DateTime(1992, 3, 8),
                "fe" => new DateTime(1995, 8, 15),
                _ => new DateTime(1996, 11, 22)
            };
            user.HireDate = user.UserName switch
            {
                "admin" => new DateTime(2018, 3, 1),
                "clerk" => new DateTime(2021, 7, 1),
                "fe" => new DateTime(2022, 4, 18),
                _ => new DateTime(2023, 1, 9)
            };
            user.Address = user.UserName switch
            {
                "admin" => "北京市东城区",
                "clerk" => "北京市海淀区",
                "fe" => "北京市朝阳区",
                _ => "上海市浦东新区"
            };
            await db.Updateable(user).UpdateColumns(u => new { u.EmployeeNo, u.Gender, u.IdCard, u.Birthday, u.HireDate, u.Address }).ExecuteCommandAsync();
        }
    }

    private async Task GrantField(long fieldId, string page, string field)
    {
        var roles = await db.Queryable<SysRole>().Where(r => r.Code == "SuperAdmin" || r.Code == "Clerk").ToListAsync();
        foreach (var role in roles)
        {
            if (role.Code == "Clerk" && page != "system:user")
            {
                continue;
            }

            if (await db.Queryable<SysRoleField>().AnyAsync(x => x.RoleId == role.Id && x.FieldId == fieldId))
            {
                continue;
            }

            var hidden = field is "email" or "phone" or "idCard" or "address";
            await db.Insertable(new SysRoleField
            {
                RoleId = role.Id,
                FieldId = fieldId,
                CanView = role.Code != "Clerk" || !hidden,
                CanEdit = role.Code != "Clerk" && field != "createTime",
                CanExport = role.Code != "Clerk" || !hidden
            }).ExecuteCommandAsync();
        }
    }

    private async Task Text(string key, string zh, string en)
    {
        foreach (var item in new[] { ("zh-CN", zh), ("en-US", en) })
        {
            if (await db.Queryable<SysI18n>().AnyAsync(x => x.Culture == item.Item1 && x.Key == key))
            {
                continue;
            }

            await db.Insertable(new SysI18n { Culture = item.Item1, Key = key, Value = item.Item2 }).ExecuteCommandAsync();
        }
    }

    private string WriteAvatar(string userName, string nickName, string color)
    {
        var directory = Path.Combine(environment.ContentRootPath, "App_Data", "avatars");
        Directory.CreateDirectory(directory);
        var file = $"{userName}.svg";
        var letter = string.IsNullOrWhiteSpace(nickName) ? userName[..1] : nickName[..1];
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64">
              <rect width="64" height="64" rx="32" fill="{color}"/>
              <text x="32" y="42" text-anchor="middle" font-size="28" fill="#ffffff" font-family="sans-serif">{letter}</text>
            </svg>
            """;
        File.WriteAllText(Path.Combine(directory, file), svg);
        return $"/api/files/avatars/{file}";
    }

    private sealed record MenuSeed(string Code, string Parent, string Name, string I18n, MenuType Type, string? Path, string? Component, string? Icon, string? Permission, int Sort);
    private sealed record FieldSeed(string Page, string Field, string Zh, string En, string ValueType, bool Lock, bool Visible, string? Dict, int Sort);
    private sealed record FieldRef(long Id, string Page, string Field);
}
