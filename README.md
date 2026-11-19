# Dreamless

前后端分离的管理框架：.NET 10、SQL Server、SqlSugar、JWT、Ant Design Pro 组件、Magicodes.IE、多语言，以及精确到字段的 RBAC。

## 启动

本机 1433 端口经常已被其它 SQL Server 占用，所以这个项目的数据库映射在 **1434**。

```bash
docker compose up -d
cd backend
dotnet run --project Dreamless.WebApi --launch-profile http
```

另开一个终端：

```bash
cd frontend
npm install
npm run dev
```

打开 http://localhost:5173 。接口是 http://localhost:5088 ，前端通过 Vite 把 `/api` 代理过去。

首次启动会建库、CodeFirst 建表，并写入演示数据。

## 演示账号

租户编码都是 `default`。

| 用户 | 密码 | 说明 |
| --- | --- | --- |
| admin | Admin@123456 | 宿主管理员，跳过按钮权限、字段权限和数据范围 |
| clerk | Clerk@123456 | 研发部，角色 Clerk，数据范围「本部门及下级」。不能查看、导出邮箱和手机 |
| fe | Clerk@123456 | 前端组，研发部的下级，clerk 能看到 |
| marketing | Clerk@123456 | 市场部，clerk 看不到 |

右上角可以切换中文 / English。菜单和字段名来自接口，界面文案来自前端语言包。Excel 表头跟随当前语言，导入时请使用同一种语言下载的模板。

## 能力

- **租户必选，部门可选。** 业务实体继承 `BaseTenantEntity`（必须带租户）或 `BaseDeptEntity`（再带可空 `DeptId`）。登录后的查询始终按当前租户过滤，宿主管理员也不跨租户读用户。数据范围按角色取并集：全部、本部门及下级、本部门、仅本人、自定义部门。
- **字段权限。** 角色可以分别授予查看、编辑、导出。某个角色如果还没配置任何字段，则该角色不收紧字段；一旦配置，只放行被勾选的字段。界面上不能查看的列不会返回明文。
- **列设置。** 每个用户可以保存自己每个页面的列显示、排序和固定。没有查看权限的字段不能打开，标记为锁定的字段不能隐藏。
- **导入导出。** 系统管理页使用 Magicodes.IE。导出列受「可导出」约束，导入和模板受「可编辑」约束。
- **低代码。** 在「开发工具 / 低代码生成」里选择数据表（演示表 `Biz_Customer`），预览后生成实体、DTO、服务、控制器和前端页面，并登记菜单与字段。生成后需要重新编译并重启 API，再重新登录才能看到新菜单。

## 配置

连接串、JWT 和跨域在 `backend/Dreamless.WebApi/appsettings.json`。也可以用环境变量 `DREAMLESS_CONNECTION` 覆盖连接串。字符串列统一建成 nvarchar，菜单和字段里的中文会按原文保存。

上线前请更换 SA 密码和 JWT 签名密钥 `Dreamless-Dev-Signing-Key-Change-In-Production`。
# DreamlessRaw.Framework
