using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dreamless.Infrastructure.CodeGen;

public sealed class CodeGenOptions
{
    public string EntityDir { get; set; } = "../Dreamless.Core/Entities/Generated";

    public string DtoDir { get; set; } = "../Dreamless.Application/Dtos/Generated";

    public string ContractDir { get; set; } = "../Dreamless.Application/Abstractions/Generated";

    public string ServiceDir { get; set; } = "../Dreamless.Infrastructure/Services/Generated";

    public string ControllerDir { get; set; } = "Controllers/Generated";

    public string FrontendDir { get; set; } = "../../frontend/src/pages";
}

public sealed class CodeGenService(
    ISqlSugarClient db,
    IHostEnvironment environment,
    IOptions<CodeGenOptions> options,
    ILocalizationService l10n,
    IPermissionService permissions) : ICodeGenService
{
    private static readonly HashSet<string> KnownDicts = new(StringComparer.OrdinalIgnoreCase)
    {
        "status", "gender", "bool", "dataScope", "menuType"
    };

    private static readonly HashSet<string> Inherited = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "TenantId", "DeptId", "CreateTime", "CreateUserId", "UpdateTime", "UpdateUserId", "IsDeleted"
    };

    public Task<List<TableInfoDto>> TablesAsync()
    {
        var tables = db.DbMaintenance.GetTableInfoList(false)
            .Where(t => !t.Name.StartsWith("__", StringComparison.Ordinal))
            .Select(t => new TableInfoDto { Name = t.Name, Description = t.Description })
            .OrderBy(t => t.Name)
            .ToList();
        return Task.FromResult(tables);
    }

    public Task<List<ColumnInfoDto>> ColumnsAsync(string table)
    {
        var plan = Inspect(table);
        return Task.FromResult(plan.Columns);
    }

    public Task<CodeGenPreviewDto> PreviewAsync(CodeGenRequest request)
    {
        var plan = Build(request);
        return Task.FromResult(new CodeGenPreviewDto
        {
            EntityBase = plan.BaseType,
            Warnings = plan.Warnings,
            Files = Render(plan)
        });
    }

    public async Task<CodeGenResultDto> GenerateAsync(CodeGenRequest request)
    {
        var plan = Build(request);
        var files = Render(plan);
        var written = new List<string>();
        var skipped = new List<string>();
        foreach (var file in files)
        {
            var path = Path.GetFullPath(file.Path);
            EnsureInside(path);
            if (System.IO.File.Exists(path) && !request.Overwrite)
            {
                skipped.Add(path);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await System.IO.File.WriteAllTextAsync(path, file.Content, new UTF8Encoding(false));
            written.Add(path);
        }

        await RegisterAsync(plan);
        permissions.Invalidate();
        l10n.Invalidate();
        return new CodeGenResultDto
        {
            Written = written,
            Skipped = skipped,
            Message = "文件已写入，并注册了菜单和字段。请重新编译并启动 API，然后重新登录以刷新菜单。"
        };
    }

    private async Task RegisterAsync(GenPlan plan)
    {
        var parentCode = $"dir.{plan.Module}";
        var parent = await db.Queryable<SysMenu>().FirstAsync(m => m.Code == parentCode);
        var parentId = parent?.Id ?? await db.Insertable(new SysMenu
        {
            Code = parentCode,
            Name = plan.Module,
            I18nKey = $"menu.{plan.Module}",
            Type = MenuType.Directory,
            Path = $"/{plan.Module}",
            Icon = "AppstoreOutlined",
            Sort = 20,
            Visible = true,
            Status = CommonStatus.Enabled
        }).ExecuteReturnBigIdentityAsync();
        await UpsertText($"menu.{plan.Module}", plan.Module, plan.Module);

        var menuCode = $"menu.{plan.Module}.{plan.Camel}";
        var menu = await db.Queryable<SysMenu>().FirstAsync(m => m.Code == menuCode);
        if (menu == null)
        {
            await db.Insertable(new SysMenu
            {
                ParentId = parentId,
                Code = menuCode,
                Name = plan.Title,
                I18nKey = $"menu.{plan.Module}.{plan.Camel}",
                Permission = $"{plan.PageCode}:list",
                Type = MenuType.Menu,
                Path = $"/{plan.Module}/{plan.Camel}",
                Component = $"{plan.Module}/{plan.Camel}/index",
                Icon = plan.Icon,
                Sort = 1,
                Visible = true,
                Status = CommonStatus.Enabled
            }).ExecuteCommandAsync();
        }

        await UpsertText($"menu.{plan.Module}.{plan.Camel}", plan.Title, plan.Title);
        await UpsertText($"page.{plan.PageCode.Replace(':', '.')}", plan.Title, plan.Title);
        await SyncButtonsAsync(plan, menuCode);

        var sort = 1;
        foreach (var prop in plan.Props)
        {
            await UpsertConfiguredField(plan, prop, sort++, defaultVisible: true, canEdit: true);
        }

        if (plan.Dept != null)
        {
            await UpsertConfiguredField(plan, plan.Dept, sort++, defaultVisible: true, canEdit: true);
        }

        await EnsureAuditField(plan, "createTime", "创建时间", "dateTime", sort);
    }

    private async Task UpsertConfiguredField(GenPlan plan, GenProp prop, int sort, bool defaultVisible, bool canEdit)
    {
        var key = $"field.{plan.PageCode.Replace(':', '.')}.{prop.Camel}";
        await UpsertText(key, prop.Display, prop.Name);
        var field = await db.Queryable<SysField>().FirstAsync(f => f.PageCode == plan.PageCode && f.FieldName == prop.Camel);
        if (field == null)
        {
            var fieldId = await db.Insertable(new SysField
            {
                PageCode = plan.PageCode,
                FieldName = prop.Camel,
                DisplayName = prop.Display,
                I18nKey = key,
                ValueType = prop.ValueType,
                Sort = sort,
                DefaultVisible = defaultVisible,
                DictCode = prop.DictCode,
                OptionJson = prop.OptionJson,
                RelationTable = prop.RelationTable,
                RelationValue = prop.RelationValue,
                RelationLabel = prop.RelationLabel
            }).ExecuteReturnBigIdentityAsync();
            var roles = await db.Queryable<SysRole>().Where(r => r.Code == "SuperAdmin").Select(r => r.Id).ToListAsync();
            if (roles.Count > 0)
            {
                await db.Insertable(roles.Select(roleId => new SysRoleField
                {
                    RoleId = roleId,
                    FieldId = fieldId,
                    CanView = true,
                    CanEdit = canEdit,
                    CanExport = true
                }).ToList()).ExecuteCommandAsync();
            }

            return;
        }

        field.DisplayName = prop.Display;
        field.ValueType = prop.ValueType;
        field.DictCode = prop.DictCode;
        field.OptionJson = prop.OptionJson;
        field.RelationTable = prop.RelationTable;
        field.RelationValue = prop.RelationValue;
        field.RelationLabel = prop.RelationLabel;
        await db.Updateable(field).UpdateColumns(f => new
        {
            f.DisplayName,
            f.ValueType,
            f.DictCode,
            f.OptionJson,
            f.RelationTable,
            f.RelationValue,
            f.RelationLabel
        }).ExecuteCommandAsync();
    }

    private async Task EnsureAuditField(GenPlan plan, string field, string display, string valueType, int sort)
    {
        var key = $"field.{plan.PageCode.Replace(':', '.')}.{field}";
        await UpsertText(key, display, field);
        if (await db.Queryable<SysField>().AnyAsync(f => f.PageCode == plan.PageCode && f.FieldName == field))
        {
            return;
        }

        var fieldId = await db.Insertable(new SysField
        {
            PageCode = plan.PageCode,
            FieldName = field,
            DisplayName = display,
            I18nKey = key,
            ValueType = valueType,
            Sort = sort,
            DefaultVisible = field != "deptId",
            LockVisible = false
        }).ExecuteReturnBigIdentityAsync();
        var roles = await db.Queryable<SysRole>().Where(r => r.Code == "SuperAdmin").Select(r => r.Id).ToListAsync();
        if (roles.Count > 0)
        {
            await db.Insertable(roles.Select(roleId => new SysRoleField
            {
                RoleId = roleId,
                FieldId = fieldId,
                CanView = true,
                CanEdit = field != "createTime",
                CanExport = true
            }).ToList()).ExecuteCommandAsync();
        }
    }

    private async Task UpsertText(string key, string zh, string en)
    {
        await UpsertCulture("zh-CN", key, zh);
        await UpsertCulture("en-US", key, en);
    }

    private async Task UpsertCulture(string culture, string key, string value)
    {
        var row = await db.Queryable<SysI18n>().FirstAsync(x => x.Culture == culture && x.Key == key);
        if (row == null)
        {
            await db.Insertable(new SysI18n { Culture = culture, Key = key, Value = value }).ExecuteCommandAsync();
            return;
        }

        row.Value = value;
        await db.Updateable(row).UpdateColumns(x => x.Value).ExecuteCommandAsync();
    }

    private GenPlan Build(CodeGenRequest request)
    {
        if (!Regex.IsMatch(request.EntityName, "^[A-Z][A-Za-z0-9]{1,40}$"))
        {
            throw new BusinessException("实体名需要使用大写开头的字母和数字");
        }

        if (!Regex.IsMatch(request.Module, "^[a-z][a-z0-9]{0,20}$"))
        {
            throw new BusinessException("模块名需要使用小写字母开头");
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 40)
        {
            throw new BusinessException("标题不能为空，且不超过 40 个字符");
        }

        if (!Regex.IsMatch(request.Icon, "^[A-Za-z]{3,40}$"))
        {
            throw new BusinessException("图标名不合法");
        }

        var inspected = Inspect(request.TableName);
        var names = inspected.Columns.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasTenant = names.Contains("TenantId");
        var hasDept = names.Contains("DeptId");
        var warnings = new List<string>();
        string baseType;
        if (hasTenant && hasDept)
        {
            baseType = "BaseDeptEntity";
        }
        else if (hasTenant)
        {
            baseType = "BaseTenantEntity";
            warnings.Add("表没有 DeptId，生成实体只继承租户，不启用部门数据范围。");
        }
        else
        {
            baseType = "BaseEntity";
            warnings.Add("表没有 TenantId。框架默认按租户隔离，建议补上 TenantId 后再生成。");
        }

        var camel = char.ToLowerInvariant(request.EntityName[0]) + request.EntityName[1..];
        var pageCode = string.IsNullOrWhiteSpace(request.PageCode) ? $"{request.Module}:{camel}" : request.PageCode.Trim();
        if (!Regex.IsMatch(pageCode, "^[a-z][a-z0-9]*:[A-Za-z][A-Za-z0-9]*$"))
        {
            throw new BusinessException("页面编码格式应为 module:entity，例如 biz:customer");
        }

        var props = inspected.Columns
            .Where(c => !c.PrimaryKey && !Inherited.Contains(c.Name))
            .Select(c => ToProp(c, request, warnings))
            .ToList();
        if (props.Count == 0)
        {
            throw new BusinessException("没有可生成的业务字段");
        }

        GenProp? dept = null;
        if (hasDept)
        {
            var column = inspected.Columns.First(c => c.Name.Equals("DeptId", StringComparison.OrdinalIgnoreCase));
            dept = ToProp(column, request, warnings) with { Display = "部门" };
        }

        return new GenPlan(request.TableName, request.EntityName, camel, request.Module, pageCode, Plural(camel), request.Title.Trim(), request.Icon, baseType, hasDept && hasTenant, hasTenant, warnings, props, dept, ResolveButtons(request));
    }

    private static GenButtons ResolveButtons(CodeGenRequest request)
    {
        var source = request.Buttons;
        var custom = new List<GenCustomButton>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "add", "edit", "delete", "import", "export", "list", "create", "update", "template"
        };
        foreach (var item in source?.Custom ?? [])
        {
            var code = item.Code?.Trim() ?? string.Empty;
            var name = item.Name?.Trim() ?? string.Empty;
            if (code.Length == 0 && name.Length == 0)
            {
                continue;
            }

            if (!Regex.IsMatch(code, "^[a-z][A-Za-z0-9]{0,20}$") || !seen.Add(code))
            {
                throw new BusinessException($"按钮编码不合法或与已有按钮重复：{code}");
            }

            if (name.Length is < 1 or > 20 || name.Any(char.IsControl))
            {
                throw new BusinessException($"按钮名称需要 1 到 20 个字符：{code}");
            }

            var method = Pascal(code);
            if (method is "List" or "Create" or "Update" or "Delete" or "Export" or "Template" or "Import")
            {
                throw new BusinessException($"按钮编码与标准接口冲突：{code}");
            }

            custom.Add(new GenCustomButton(code, name, method));
        }

        if (custom.Count > 12)
        {
            throw new BusinessException("自定义按钮最多 12 个");
        }

        return new GenButtons(source?.Add ?? true, source?.Edit ?? true, source?.Delete ?? true, source?.Import ?? true, source?.Export ?? true, custom);
    }

    private async Task SyncButtonsAsync(GenPlan plan, string menuCode)
    {
        var menu = await db.Queryable<SysMenu>().FirstAsync(m => m.Code == menuCode);
        var selected = new List<(string Code, string Zh, string En)>();
        if (plan.Buttons.Add) selected.Add(("add", "新增", "Create"));
        if (plan.Buttons.Edit) selected.Add(("edit", "编辑", "Edit"));
        if (plan.Buttons.Delete) selected.Add(("delete", "删除", "Delete"));
        if (plan.Buttons.Import) selected.Add(("import", "导入", "Import"));
        if (plan.Buttons.Export) selected.Add(("export", "导出", "Export"));
        selected.AddRange(plan.Buttons.Custom.Select(button => (button.Code, button.Name, button.Name)));

        var existing = await db.Queryable<SysMenu>().ClearFilter().Where(m => m.ParentId == menu!.Id && m.Type == MenuType.Button).ToListAsync();
        var keep = selected.Select(item => $"{menuCode}.{item.Code}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in existing.Where(item => !keep.Contains(item.Code) && !item.IsDeleted))
        {
            row.IsDeleted = true;
            await db.Updateable(row).UpdateColumns(x => x.IsDeleted).ExecuteCommandAsync();
            await db.Deleteable<SysRoleMenu>().Where(x => x.MenuId == row.Id).ExecuteCommandAsync();
        }

        var sort = 1;
        foreach (var (code, zh, en) in selected)
        {
            var full = $"{menuCode}.{code}";
            var key = $"menu.{plan.Module}.{plan.Camel}.{code}";
            await UpsertText(key, zh, en);
            var row = existing.FirstOrDefault(item => item.Code.Equals(full, StringComparison.OrdinalIgnoreCase));
            if (row == null)
            {
                await db.Insertable(new SysMenu
                {
                    ParentId = menu!.Id,
                    Code = full,
                    Name = zh,
                    I18nKey = key,
                    Permission = $"{plan.PageCode}:{code}",
                    Type = MenuType.Button,
                    Sort = sort++,
                    Visible = true,
                    Status = CommonStatus.Enabled
                }).ExecuteCommandAsync();
                continue;
            }

            row.Name = zh;
            row.I18nKey = key;
            row.Permission = $"{plan.PageCode}:{code}";
            row.Sort = sort++;
            row.IsDeleted = false;
            row.Status = CommonStatus.Enabled;
            await db.Updateable(row).UpdateColumns(x => new { x.Name, x.I18nKey, x.Permission, x.Sort, x.IsDeleted, x.Status }).ExecuteCommandAsync();
        }
    }

    private GenProp ToProp(ColumnInfoDto column, CodeGenRequest request, List<string> warnings)
    {
        var display = string.IsNullOrWhiteSpace(column.Description) ? column.Name : column.Description!;
        var config = request.Fields.FirstOrDefault(field => field.Name.Equals(column.Name, StringComparison.OrdinalIgnoreCase));
        var choice = Resolve(column, config, warnings);
        var name = Pascal(column.Name);
        return new GenProp(name, Camel(name), column.CsType, column.Nullable, column.Length, display, choice.ValueType, choice.DictCode, choice.OptionJson, choice.RelationTable, choice.RelationValue, choice.RelationLabel);
    }

    private FieldChoice Resolve(ColumnInfoDto column, CodeGenFieldDto? config, List<string> warnings)
    {
        var valueType = config?.ValueType;
        if (string.IsNullOrWhiteSpace(valueType))
        {
            if (column.Name.Equals("Status", StringComparison.OrdinalIgnoreCase) && column.CsType.StartsWith("string", StringComparison.Ordinal))
            {
                return new FieldChoice("select", "status", null, null, null, null);
            }

            if (column.ValueType == "select")
            {
                return new FieldChoice("select", "bool", null, null, null, null);
            }

            var guessed = GuessRelation(column.Name);
            if (guessed != null)
            {
                return guessed;
            }

            if (column.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"{column.Name} 看起来是关联字段，但没有找到对应的表，已按数字处理。");
            }

            return new FieldChoice(column.ValueType, null, null, null, null, null);
        }

        if (valueType is not ("text" or "textarea" or "digit" or "date" or "dateTime" or "select"))
        {
            throw new BusinessException($"字段 {column.Name} 的控件类型不支持");
        }

        if (valueType != "select")
        {
            return new FieldChoice(valueType, null, null, null, null, null);
        }

        if (!string.IsNullOrWhiteSpace(config!.RelationTable))
        {
            var table = config.RelationTable.Trim();
            var value = string.IsNullOrWhiteSpace(config.RelationValue) ? "Id" : config.RelationValue.Trim();
            var label = string.IsNullOrWhiteSpace(config.RelationLabel) ? "Name" : config.RelationLabel.Trim();
            EnsureColumn(table, value);
            EnsureColumn(table, label);
            return new FieldChoice("select", null, null, table, value, label);
        }

        if (!string.IsNullOrWhiteSpace(config.DictCode))
        {
            if (!KnownDicts.Contains(config.DictCode))
            {
                throw new BusinessException($"字段 {column.Name} 的字典不存在");
            }

            if (IsNumber(column.CsType) && config.DictCode != "bool")
            {
                warnings.Add($"{column.Name} 是数字字段，内置字典的值不是数字，保存时会失败。");
            }

            return new FieldChoice("select", config.DictCode, null, null, null, null);
        }

        var options = config.Options.Where(option => !string.IsNullOrWhiteSpace(option.Value)).Take(50).ToList();
        if (options.Count == 0)
        {
            throw new BusinessException($"字段 {column.Name} 需要选择字典、填写枚举，或指定关联表");
        }

        if (options.Any(option => option.Value.Length > 40 || (option.Label?.Length ?? 0) > 40))
        {
            throw new BusinessException($"字段 {column.Name} 的枚举值不能超过 40 个字符");
        }

        if (IsNumber(column.CsType) && options.Any(option => !long.TryParse(option.Value, out _)))
        {
            warnings.Add($"{column.Name} 是数字字段，枚举值请写成数字，例如 1=重点。");
        }

        var json = JsonSerializer.Serialize(options.Select(option => new { value = option.Value.Trim(), label = string.IsNullOrWhiteSpace(option.Label) ? option.Value.Trim() : option.Label.Trim() }));
        return new FieldChoice("select", null, json, null, null, null);
    }

    private FieldChoice? GuessRelation(string columnName)
    {
        if (!columnName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) || columnName.Equals("Id", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var stem = columnName[..^2];
        var tables = db.DbMaintenance.GetTableInfoList(false).Select(table => table.Name).ToList();
        var table = new[] { $"Sys{stem}", $"Biz_{stem}", stem }.FirstOrDefault(candidate => tables.Contains(candidate, StringComparer.OrdinalIgnoreCase));
        if (table == null)
        {
            return null;
        }

        var columns = db.DbMaintenance.GetColumnInfosByTableName(table, false) ?? [];
        var value = columns.Any(column => column.DbColumnName.Equals("Id", StringComparison.OrdinalIgnoreCase)) ? "Id" : columns.FirstOrDefault()?.DbColumnName;
        var label = new[] { "Name", "Title", "Code" }.FirstOrDefault(candidate => columns.Any(column => column.DbColumnName.Equals(candidate, StringComparison.OrdinalIgnoreCase))) ?? value;
        if (value == null || label == null)
        {
            return null;
        }

        return new FieldChoice("select", null, null, table, value, label);
    }

    private void EnsureColumn(string table, string column)
    {
        if (!Regex.IsMatch(table, "^[A-Za-z_][A-Za-z0-9_]*$") || !Regex.IsMatch(column, "^[A-Za-z_][A-Za-z0-9_]*$"))
        {
            throw new BusinessException("关联表或字段名不合法");
        }

        var columns = db.DbMaintenance.GetColumnInfosByTableName(table, false);
        if (columns == null || !columns.Any(item => item.DbColumnName.Equals(column, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException($"关联字段不存在：{table}.{column}");
        }
    }

    private Inspected Inspect(string table)
    {
        if (!Regex.IsMatch(table, "^[A-Za-z_][A-Za-z0-9_]*$"))
        {
            throw new BusinessException("表名不合法");
        }

        var columns = db.DbMaintenance.GetColumnInfosByTableName(table, false);
        if (columns == null || columns.Count == 0)
        {
            throw new BusinessException("数据表不存在或没有字段");
        }

        return new Inspected(columns.Select(column =>
        {
            var (cs, valueType) = MapType(column.DataType, column.IsNullable, column.Length);
            return new ColumnInfoDto
            {
                Name = column.DbColumnName,
                DbType = column.DataType,
                CsType = cs,
                Nullable = column.IsNullable,
                PrimaryKey = column.IsPrimarykey,
                Length = column.Length,
                Description = column.ColumnDescription,
                Inherited = Inherited.Contains(column.DbColumnName),
                ValueType = valueType
            };
        }).ToList());
    }

    private static string RenderPage(GenPlan plan)
    {
        var needsForm = plan.Buttons.Add || plan.Buttons.Edit;
        var needsMessage = needsForm || plan.Buttons.Delete || plan.Buttons.Custom.Count > 0;
        var needsRef = needsForm || plan.Buttons.Custom.Count > 0;
        var hooks = new List<string>();
        if (needsRef) hooks.Add("useRef");
        if (needsForm) hooks.Add("useState");
        var lines = new List<string>();
        if (hooks.Count > 0) lines.Add($"import {{ {string.Join(", ", hooks)} }} from 'react';");
        if (needsForm) lines.Add("import { ModalForm } from '@ant-design/pro-components';");
        if (needsMessage) lines.Add("import { App } from 'antd';");
        lines.Add(needsRef
            ? "import AdminTable, { type AdminTableRef } from '@/components/AdminTable';"
            : "import AdminTable from '@/components/AdminTable';");
        if (needsForm) lines.Add("import SchemaFields from '@/components/SchemaFields';");
        lines.Add("import { api } from '@/api/client';");
        lines.Add(string.Empty);
        lines.Add($"export default function {plan.Entity}Page() {{");
        if (needsMessage) lines.Add("  const { message } = App.useApp();");
        if (needsRef) lines.Add("  const actionRef = useRef<AdminTableRef>(null);");
        if (needsForm)
        {
            lines.Add("  const [open, setOpen] = useState(false);");
            lines.Add("  const [current, setCurrent] = useState<any>();");
        }

        lines.Add("  return (");
        lines.Add("    <>");
        lines.Add("      <AdminTable");
        if (needsRef) lines.Add("        actionRef={actionRef}");
        lines.Add($"        pageCode=\"{plan.PageCode}\"");
        lines.Add($"        headerTitle=\"{Js(plan.Title)}\"");
        var permissions = new List<string>();
        if (plan.Buttons.Add) permissions.Add($"create: '{plan.PageCode}:add'");
        if (plan.Buttons.Edit) permissions.Add($"edit: '{plan.PageCode}:edit'");
        if (plan.Buttons.Delete) permissions.Add($"delete: '{plan.PageCode}:delete'");
        if (plan.Buttons.Import) permissions.Add($"import: '{plan.PageCode}:import'");
        if (plan.Buttons.Export) permissions.Add($"export: '{plan.PageCode}:export'");
        if (permissions.Count > 0)
        {
            lines.Add("        permissions={{");
            lines.Add("          " + string.Join(",\n          ", permissions) + ",");
            lines.Add("        }}");
        }

        if (plan.Buttons.Import)
        {
            lines.Add($"        importUrl=\"/{plan.Module}/{plan.Plural}/import\"");
            lines.Add($"        templateUrl=\"/{plan.Module}/{plan.Plural}/import-template\"");
        }

        if (plan.Buttons.Export) lines.Add($"        exportUrl=\"/{plan.Module}/{plan.Plural}/export\"");
        lines.Add($"        request={{(params) => api.get('/{plan.Module}/{plan.Plural}', {{ params }}).then((response) => response.data)}}");
        if (plan.Buttons.Add) lines.Add("        onCreate={() => { setCurrent(undefined); setOpen(true); }}");
        if (plan.Buttons.Edit) lines.Add("        onEdit={(row) => { setCurrent(row); setOpen(true); }}");
        if (plan.Buttons.Delete) lines.Add($"        onDelete={{async (row) => {{ await api.delete(`/{plan.Module}/{plan.Plural}/${{row.id}}`); message.success('已删除'); }}}}");
        if (plan.Buttons.Custom.Count > 0)
        {
            lines.Add("        actions={[");
            foreach (var button in plan.Buttons.Custom)
            {
                var name = Js(button.Name);
                lines.Add($"          {{ key: '{button.Code}', label: '{name}', permission: '{plan.PageCode}:{button.Code}', confirm: '确定执行{name}？', onClick: async (row) => {{ await api.post(`/{plan.Module}/{plan.Plural}/${{row.id}}/{button.Code}`); message.success('已提交'); actionRef.current?.reload(); }} }},");
            }

            lines.Add("        ]}");
        }

        lines.Add("      />");
        if (needsForm)
        {
            var title = Js(plan.Title);
            lines.Add("      <ModalForm");
            lines.Add($"        title={{current ? '编辑{title}' : '新建{title}'}}");
            lines.Add("        open={open}");
            lines.Add("        initialValues={current ?? {}}");
            lines.Add("        modalProps={{ destroyOnHidden: true, onCancel: () => setOpen(false) }}");
            lines.Add("        onFinish={async (values) => {");
            if (plan.Buttons.Edit) lines.Add($"          if (current?.id) await api.put(`/{plan.Module}/{plan.Plural}/${{current.id}}`, values);");
            if (plan.Buttons.Add && plan.Buttons.Edit) lines.Add($"          else await api.post('/{plan.Module}/{plan.Plural}', values);");
            else if (plan.Buttons.Add) lines.Add($"          await api.post('/{plan.Module}/{plan.Plural}', values);");
            lines.Add("          message.success('已保存');");
            lines.Add("          setOpen(false);");
            lines.Add("          actionRef.current?.reload();");
            lines.Add("          return true;");
            lines.Add("        }}");
            lines.Add("      >");
            lines.Add($"        <SchemaFields pageCode=\"{plan.PageCode}\" />");
            lines.Add("      </ModalForm>");
        }

        lines.Add("    </>");
        lines.Add("  );");
        lines.Add("}");
        return string.Join('\n', lines);
    }

    private static string RenderEndpoints(GenPlan plan)
    {
        var blocks = new List<string>();
        if (plan.Buttons.Add)
        {
            blocks.Add($$"""
                [HttpPost]
                [HasPermission("{{plan.PageCode}}:add")]
                public async Task<long> Create([FromBody] {{plan.Entity}}SaveDto dto) => await service.CreateAsync(dto);
""");
        }

        if (plan.Buttons.Edit)
        {
            blocks.Add($$"""
                [HttpPut("{id:long}")]
                [HasPermission("{{plan.PageCode}}:edit")]
                public Task Update(long id, [FromBody] {{plan.Entity}}SaveDto dto) => service.UpdateAsync(id, dto);
""");
        }

        if (plan.Buttons.Delete)
        {
            blocks.Add($$"""
                [HttpDelete("{id:long}")]
                [HasPermission("{{plan.PageCode}}:delete")]
                public Task Delete(long id) => service.DeleteAsync(id);
""");
        }

        if (plan.Buttons.Export)
        {
            blocks.Add($$"""
                [HttpGet("export")]
                [HasPermission("{{plan.PageCode}}:export")]
                public async Task<IActionResult> Export([FromQuery] {{plan.Entity}}QueryDto query) =>
                    File(await service.ExportAsync(query), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "{{plan.Camel}}.xlsx");
""");
        }

        if (plan.Buttons.Import)
        {
            blocks.Add($$"""
                [HttpGet("import-template")]
                [HasPermission("{{plan.PageCode}}:import")]
                public async Task<IActionResult> Template() =>
                    File(await service.TemplateAsync(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "{{plan.Camel}}-template.xlsx");

                [HttpPost("import")]
                [HasPermission("{{plan.PageCode}}:import")]
                public async Task<ImportResultDto> Import(IFormFile file)
                {
                    using var stream = file.OpenReadStream();
                    return await service.ImportAsync(stream);
                }
""");
        }

        foreach (var button in plan.Buttons.Custom)
        {
            blocks.Add($$"""
                [HttpPost("{id:long}/{{button.Code}}")]
                [HasPermission("{{plan.PageCode}}:{{button.Code}}")]
                public Task {{button.Method}}(long id) => service.{{button.Method}}Async(id);
""");
        }

        return blocks.Count == 0 ? string.Empty : "\n" + string.Join("\n", blocks.Select(IndentBlock));
    }

    private static string IndentBlock(string block)
    {
        var lines = block.Replace("\r\n", "\n").Split('\n');
        var content = lines.Where(line => line.Trim().Length > 0).ToList();
        if (content.Count == 0)
        {
            return string.Empty;
        }

        var min = content.Min(line => line.TakeWhile(char.IsWhiteSpace).Count());
        return string.Join('\n', lines.Select(line => line.Trim().Length == 0 ? string.Empty : "    " + (line.Length >= min ? line[min..] : line.Trim())));
    }

    private static string Js(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");

    private static string Cs(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private List<CodeGenFileDto> Render(GenPlan plan)
    {
        var opt = options.Value;
        var root = environment.ContentRootPath;
        var props = string.Join(Environment.NewLine, plan.Props.Select(Prop));
        var dtoProps = string.Join(Environment.NewLine + Environment.NewLine, plan.Props.Select(p => DtoProp(plan, p, export: true, import: false)));
        var saveProps = string.Join(Environment.NewLine + Environment.NewLine, plan.Props.Select(p => DtoProp(plan, p, export: false, import: true)));
        var deptField = plan.HasDept ? "\n    public long? DeptId { get; set; }\n" : string.Empty;
        var entity = $$"""
            using SqlSugar;

            namespace Dreamless.Core.Entities.Generated;

            /// <summary>由 Dreamless 低代码根据表 {{plan.Table}} 生成。</summary>
            [SugarTable("{{plan.Table}}")]
            public class {{plan.Entity}} : {{plan.BaseType}}
            {
            {{props}}
            }
            """;
        var dtos = $$"""
            using System.ComponentModel.DataAnnotations;
            using Dreamless.Application.Common;
            using Magicodes.ExporterAndImporter.Core;
            using Magicodes.ExporterAndImporter.Excel;

            namespace Dreamless.Application.Dtos.Generated;

            [ExcelExporter(Name = "{{plan.Title}}", ExporterHeadersFilter = typeof(Dreamless.Application.Dtos.InfrastructureHeaderFilter))]
            public class {{plan.Entity}}ListDto
            {
                [ExporterHeader(IsIgnore = true)]
                public long Id { get; set; }
            {{deptField}}
            {{dtoProps}}

                [ExporterHeader(DisplayName = "field.common.createTime", Format = "yyyy-MM-dd HH:mm:ss")]
                public DateTime CreateTime { get; set; }
            }

            public class {{plan.Entity}}SaveDto
            {
            {{deptField}}
            {{saveProps}}
            }

            [ExcelImporter(IsLabelingError = true, ImportHeaderFilter = typeof(Dreamless.Application.Dtos.InfrastructureHeaderFilter))]
            public class {{plan.Entity}}ImportDto
            {
            {{saveProps}}
            }

            public class {{plan.Entity}}QueryDto : PageQuery
            {
            }
            """;
        var customContract = string.Concat(plan.Buttons.Custom.Select(button => $"\n    Task {button.Method}Async(long id);"));
        var customService = string.Concat(plan.Buttons.Custom.Select(button =>
            $"\n    public Task {button.Method}Async(long id)\n    {{\n        _ = id;\n        throw new BusinessException(\"「{Cs(button.Name)}」已生成按钮和接口，请在 {plan.Entity}Service.{button.Method}Async 中实现业务逻辑。\");\n    }}"));
        var endpoints = RenderEndpoints(plan);
        var contract = $$"""
            using Dreamless.Application.Common;
            using Dreamless.Application.Dtos;
            using Dreamless.Application.Dtos.Generated;

            namespace Dreamless.Application.Abstractions.Generated;

            public interface I{{plan.Entity}}Service : IAppService
            {
                Task<PageResult<{{plan.Entity}}ListDto>> ListAsync({{plan.Entity}}QueryDto query);
                Task<long> CreateAsync({{plan.Entity}}SaveDto dto);
                Task UpdateAsync(long id, {{plan.Entity}}SaveDto dto);
                Task DeleteAsync(long id);
                Task<byte[]> ExportAsync({{plan.Entity}}QueryDto query);
                Task<byte[]> TemplateAsync();
                Task<ImportResultDto> ImportAsync(Stream stream);{{customContract}}
            }
            """;
        var service = $$"""
            using Dreamless.Application.Abstractions.Generated;
            using Dreamless.Application.Dtos.Generated;
            using Dreamless.Core.Entities.Generated;

            namespace Dreamless.Infrastructure.Services.Generated;

            public sealed class {{plan.Entity}}Service(ICrudEngine crud) : I{{plan.Entity}}Service
            {
                private const string Page = "{{plan.PageCode}}";

                public Task<PageResult<{{plan.Entity}}ListDto>> ListAsync({{plan.Entity}}QueryDto query) =>
                    crud.PageAsync<{{plan.Entity}}, {{plan.Entity}}ListDto>(Page, query);

                public Task<long> CreateAsync({{plan.Entity}}SaveDto dto) => crud.CreateAsync<{{plan.Entity}}>(Page, dto);

                public Task UpdateAsync(long id, {{plan.Entity}}SaveDto dto) => crud.UpdateAsync<{{plan.Entity}}>(Page, id, dto);

                public Task DeleteAsync(long id) => crud.DeleteAsync<{{plan.Entity}}>(id);

                public Task<byte[]> ExportAsync({{plan.Entity}}QueryDto query) =>
                    crud.ExportAsync<{{plan.Entity}}, {{plan.Entity}}ListDto>(Page, query);

                public Task<byte[]> TemplateAsync() => crud.TemplateAsync<{{plan.Entity}}ImportDto>(Page);

                public Task<ImportResultDto> ImportAsync(Stream stream) =>
                    crud.ImportAsync<{{plan.Entity}}, {{plan.Entity}}ImportDto>(Page, stream);{{customService}}
            }
            """;
        var controller = $$"""
            using Dreamless.Application.Abstractions.Generated;
            using Dreamless.Application.Common;
            using Dreamless.Application.Dtos;
            using Dreamless.Application.Dtos.Generated;
            using Dreamless.WebApi.Authorization;
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Mvc;

            namespace Dreamless.WebApi.Controllers.Generated;

            [ApiController]
            [Authorize]
            [Route("api/{{plan.Module}}/{{plan.Plural}}")]
            public class {{plan.Entity}}Controller(I{{plan.Entity}}Service service) : ControllerBase
            {
                [HttpGet]
                [HasPermission("{{plan.PageCode}}:list")]
                public Task<PageResult<{{plan.Entity}}ListDto>> List([FromQuery] {{plan.Entity}}QueryDto query) => service.ListAsync(query);
            {{endpoints}}
            }
            """;
        var page = RenderPage(plan);
        return
        [
            CodeFile(root, opt.EntityDir, $"{plan.Entity}.cs", "csharp", entity),
            CodeFile(root, opt.DtoDir, $"{plan.Entity}Dtos.cs", "csharp", dtos),
            CodeFile(root, opt.ContractDir, $"I{plan.Entity}Service.cs", "csharp", contract),
            CodeFile(root, opt.ServiceDir, $"{plan.Entity}Service.cs", "csharp", service),
            CodeFile(root, opt.ControllerDir, $"{plan.Entity}Controller.cs", "csharp", controller),
            CodeFile(root, opt.FrontendDir, Path.Combine(plan.Module, plan.Camel, "index.tsx"), "tsx", page)
        ];
    }

    private static CodeGenFileDto CodeFile(string root, string relativeDir, string name, string language, string content) => new()
    {
        Path = Path.GetFullPath(Path.Combine(root, relativeDir, name)),
        Language = language,
        Content = content.Trim() + Environment.NewLine
    };

    private void EnsureInside(string path)
    {
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."));
        if (!path.StartsWith(root, StringComparison.Ordinal))
        {
            throw new BusinessException("生成路径超出了项目目录");
        }
    }

    private static string Prop(GenProp prop)
    {
        var parts = new List<string> { $"IsNullable = {(prop.Nullable ? "true" : "false")}" };
        if (prop.CsType is "string" or "string?")
        {
            parts.Insert(0, $"Length = {(prop.Length > 0 && prop.Length < 4000 ? prop.Length : 200)}");
        }

        if (!string.IsNullOrWhiteSpace(prop.Display))
        {
            parts.Add($"ColumnDescription = \"{prop.Display.Replace("\"", "\\\"")}\"");
        }

        var suffix = prop.CsType == "string" ? " = string.Empty;" : string.Empty;
        return $"    [SugarColumn({string.Join(", ", parts)})]\n    public {prop.CsType} {prop.Name} {{ get; set; }}{suffix}";
    }

    private static string DtoProp(GenPlan plan, GenProp prop, bool export, bool import)
    {
        var key = $"field.{plan.PageCode.Replace(':', '.')}.{prop.Camel}";
        var lines = new List<string>();
        if (export)
        {
            lines.Add($"    [ExporterHeader(DisplayName = \"{key}\")]");
        }

        if (import)
        {
            lines.Add($"    [ImporterHeader(Name = \"{key}\")]");
            if (!prop.Nullable && prop.CsType == "string")
            {
                lines.Add("    [Required]");
            }
        }

        var suffix = prop.CsType == "string" ? " = string.Empty;" : string.Empty;
        lines.Add($"    public {prop.CsType} {prop.Name} {{ get; set; }}{suffix}");
        return string.Join(Environment.NewLine, lines);
    }

    private static (string Cs, string ValueType) MapType(string dbType, bool nullable, int length)
    {
        var core = dbType.ToLowerInvariant() switch
        {
            "bigint" => "long",
            "int" or "integer" => "int",
            "smallint" => "short",
            "tinyint" => "byte",
            "bit" => "bool",
            "decimal" or "numeric" or "money" or "smallmoney" => "decimal",
            "float" => "double",
            "real" => "float",
            "datetime" or "datetime2" or "date" or "datetimeoffset" or "smalldatetime" => "DateTime",
            "uniqueidentifier" => "Guid",
            "time" => "TimeSpan",
            _ => "string"
        };
        var valueType = core switch
        {
            "long" or "int" or "short" or "byte" or "decimal" or "double" or "float" => "digit",
            "bool" => "select",
            "DateTime" => "dateTime",
            "string" when length > 200 => "textarea",
            _ => "text"
        };
        var cs = core == "string"
            ? nullable ? "string?" : "string"
            : nullable ? core + "?" : core;
        return (cs, valueType);
    }

    private static string Pascal(string name)
    {
        var parts = name.Split(['_', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return char.ToUpperInvariant(parts[0][0]) + parts[0][1..];
        }

        return string.Concat(parts.Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
    }

    private static bool IsNumber(string csType)
    {
        var core = csType.TrimEnd('?');
        return core is "long" or "int" or "short" or "byte" or "decimal" or "double" or "float";
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static string Plural(string name)
    {
        if (name.EndsWith('y') && name.Length > 1 && !"aeiou".Contains(name[^2]))
        {
            return name[..^1] + "ies";
        }

        if (name.EndsWith('s') || name.EndsWith('x') || name.EndsWith("ch") || name.EndsWith("sh"))
        {
            return name + "es";
        }

        return name + "s";
    }

    private sealed record Inspected(List<ColumnInfoDto> Columns);

    private sealed record FieldChoice(string ValueType, string? DictCode, string? OptionJson, string? RelationTable, string? RelationValue, string? RelationLabel);

    private sealed record GenProp(string Name, string Camel, string CsType, bool Nullable, int Length, string Display, string ValueType, string? DictCode, string? OptionJson, string? RelationTable, string? RelationValue, string? RelationLabel);

    private sealed record GenPlan(
        string Table,
        string Entity,
        string Camel,
        string Module,
        string PageCode,
        string Plural,
        string Title,
        string Icon,
        string BaseType,
        bool HasDept,
        bool HasTenant,
        List<string> Warnings,
        List<GenProp> Props,
        GenProp? Dept,
        GenButtons Buttons);

    private sealed record GenButtons(bool Add, bool Edit, bool Delete, bool Import, bool Export, List<GenCustomButton> Custom);

    private sealed record GenCustomButton(string Code, string Name, string Method);
}
