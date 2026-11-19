using System.Text.Json;

namespace Dreamless.Application.Common;

public class PageQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }

    /// <summary>点击顺序即优先级，例如 deptName:asc,createTime:desc。</summary>
    public string? Sorts { get; set; }

    /// <summary>列搜索条件，JSON 对象。文本为包含匹配，日期数组为范围。</summary>
    public string? Filters { get; set; }

    public int NormalizedPage => Math.Max(Page, 1);

    public int NormalizedSize => Math.Clamp(PageSize, 1, 200);

    public IReadOnlyList<SortClause> SortClauses => SortClause.Parse(Sorts);

    public IReadOnlyList<FilterClause> FilterClauses => FilterClause.Parse(Filters);
}

public readonly record struct SortClause(string Field, bool Descending)
{
    public static IReadOnlyList<SortClause> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var list = new List<SortClause>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bits = part.Split(':', 2, StringSplitOptions.TrimEntries);
            if (bits.Length == 0 || bits[0].Length is < 1 or > 40 || !char.IsLetter(bits[0][0]))
            {
                continue;
            }

            if (!bits[0].All(char.IsLetterOrDigit))
            {
                continue;
            }

            var descending = bits.Length > 1 && bits[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
            list.Add(new SortClause(bits[0], descending));
            if (list.Count == 5)
            {
                break;
            }
        }

        return list;
    }
}

public readonly record struct FilterClause(string Field, string? Value, string? ValueTo)
{
    public static IReadOnlyList<FilterClause> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var list = new List<FilterClause>();
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Name.Length is < 1 or > 40 || !char.IsLetter(property.Name[0]) || !property.Name.All(char.IsLetterOrDigit))
                {
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    var values = property.Value.EnumerateArray().Select(ReadScalar).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()).Take(2).ToList();
                    if (values.Count == 0)
                    {
                        continue;
                    }

                    list.Add(new FilterClause(property.Name, Clip(values[0]), values.Count > 1 ? Clip(values[1]) : null));
                }
                else
                {
                    var text = ReadScalar(property.Value);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    list.Add(new FilterClause(property.Name, Clip(text.Trim()), null));
                }

                if (list.Count == 20)
                {
                    break;
                }
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ReadScalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null
    };

    private static string Clip(string value) => value.Length <= 200 ? value : value[..200];
}

public class PageResult<T>
{
    public long Total { get; set; }

    public List<T> Items { get; set; } = [];
}

public class BusinessException : Exception
{
    public int StatusCode { get; }

    public BusinessException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}

public static class Permissions
{
    public const string Dashboard = "dashboard:view";

    public const string TenantList = "system:tenant:list";
    public const string TenantAdd = "system:tenant:add";
    public const string TenantEdit = "system:tenant:edit";
    public const string TenantDelete = "system:tenant:delete";
    public const string TenantImport = "system:tenant:import";
    public const string TenantExport = "system:tenant:export";

    public const string DeptList = "system:dept:list";
    public const string DeptAdd = "system:dept:add";
    public const string DeptEdit = "system:dept:edit";
    public const string DeptDelete = "system:dept:delete";
    public const string DeptImport = "system:dept:import";
    public const string DeptExport = "system:dept:export";

    public const string PositionList = "system:position:list";
    public const string PositionAdd = "system:position:add";
    public const string PositionEdit = "system:position:edit";
    public const string PositionDelete = "system:position:delete";
    public const string PositionImport = "system:position:import";
    public const string PositionExport = "system:position:export";

    public const string UserList = "system:user:list";
    public const string UserAdd = "system:user:add";
    public const string UserEdit = "system:user:edit";
    public const string UserDelete = "system:user:delete";
    public const string UserImport = "system:user:import";
    public const string UserExport = "system:user:export";

    public const string RoleList = "system:role:list";
    public const string RoleAdd = "system:role:add";
    public const string RoleEdit = "system:role:edit";
    public const string RoleDelete = "system:role:delete";
    public const string RoleImport = "system:role:import";
    public const string RoleExport = "system:role:export";

    public const string MenuList = "system:menu:list";
    public const string MenuAdd = "system:menu:add";
    public const string MenuEdit = "system:menu:edit";
    public const string MenuDelete = "system:menu:delete";
    public const string MenuImport = "system:menu:import";
    public const string MenuExport = "system:menu:export";

    public const string I18nList = "system:i18n:list";
    public const string I18nAdd = "system:i18n:add";
    public const string I18nEdit = "system:i18n:edit";
    public const string I18nDelete = "system:i18n:delete";
    public const string I18nImport = "system:i18n:import";
    public const string I18nExport = "system:i18n:export";

    public const string CodegenList = "dev:codegen:list";
    public const string CodegenPreview = "dev:codegen:preview";
    public const string CodegenGenerate = "dev:codegen:generate";

    public const string MessageList = "message:notice:list";
    public const string MessageSend = "message:notice:send";
    public const string MessageDelete = "message:notice:delete";
}

public interface IAppService
{
}
