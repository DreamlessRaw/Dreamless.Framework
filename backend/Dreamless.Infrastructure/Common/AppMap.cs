using System.Collections;
using System.Reflection;

namespace Dreamless.Infrastructure.Common;

public static class AppMap
{
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "IsDeleted", "CreateTime", "CreateUserId", "UpdateTime", "UpdateUserId", "PasswordHash", "TenantId"
    };

    public static TTarget Map<TTarget>(object source) where TTarget : new()
    {
        var target = new TTarget();
        Copy(source, target, null);
        return target;
    }

    public static void Copy(object source, object target, IReadOnlySet<string>? include)
    {
        var targets = target.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var sourceProp in source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (sourceProp.GetIndexParameters().Length > 0 || !sourceProp.CanRead)
            {
                continue;
            }

            if (include != null && !include.Contains(sourceProp.Name))
            {
                continue;
            }

            if (Protected.Contains(sourceProp.Name) && (include == null || !include.Contains(sourceProp.Name)))
            {
                continue;
            }

            if (!targets.TryGetValue(sourceProp.Name, out var targetProp))
            {
                continue;
            }

            if (!TryConvert(sourceProp.GetValue(source), targetProp.PropertyType, out var value))
            {
                continue;
            }

            targetProp.SetValue(target, value);
        }
    }

    public static string DescribeError(object error)
    {
        var type = error.GetType();
        var row = type.GetProperty("RowIndex")?.GetValue(error);
        if (type.GetProperty("FieldErrors")?.GetValue(error) is IDictionary fields)
        {
            var parts = new List<string>();
            foreach (DictionaryEntry entry in fields)
            {
                parts.Add($"{entry.Key}:{entry.Value}");
            }

            return $"第 {row} 行 {string.Join("；", parts)}";
        }

        var message = type.GetProperty("Message")?.GetValue(error) as string
            ?? type.GetProperty("ErrorMessage")?.GetValue(error) as string
            ?? type.GetProperty("RequireColumnName")?.GetValue(error) as string;
        return string.IsNullOrWhiteSpace(message) ? error.ToString() ?? "导入错误" : message;
    }

    private static bool TryConvert(object? value, Type targetType, out object? converted)
    {
        var dest = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (value == null)
        {
            converted = null;
            return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
        }

        if (dest.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        if (dest.IsEnum)
        {
            if (value is string text && Enum.TryParse(dest, text, true, out var parsed))
            {
                converted = parsed;
                return true;
            }

            try
            {
                converted = Enum.ToObject(dest, value);
                return true;
            }
            catch
            {
                converted = null;
                return false;
            }
        }

        try
        {
            converted = Convert.ChangeType(value, dest);
            return true;
        }
        catch
        {
            converted = null;
            return false;
        }
    }
}

public static class ValueParser
{
    public static CommonStatus Status(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "1" or "启用" or "Enabled" or "enabled")
        {
            return CommonStatus.Enabled;
        }

        if (value is "0" or "停用" or "禁用" or "Disabled" or "disabled")
        {
            return CommonStatus.Disabled;
        }

        if (Enum.TryParse<CommonStatus>(value, true, out var parsed))
        {
            return parsed;
        }

        throw new BusinessException($"无法识别状态：{value}");
    }

    public static DataScope Scope(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DataScope.Self;
        }

        return value switch
        {
            "全部" or "All" => DataScope.All,
            "本部门及下级" or "DeptAndChild" => DataScope.DeptAndChild,
            "本部门" or "Dept" => DataScope.Dept,
            "仅本人" or "Self" => DataScope.Self,
            "自定义" or "Custom" => DataScope.Custom,
            _ when Enum.TryParse<DataScope>(value, true, out var parsed) => parsed,
            _ => throw new BusinessException($"无法识别数据范围：{value}")
        };
    }

    public static MenuType Menu(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return MenuType.Menu;
        }

        return value switch
        {
            "目录" or "Directory" => MenuType.Directory,
            "菜单" or "Menu" => MenuType.Menu,
            "按钮" or "Button" => MenuType.Button,
            _ when Enum.TryParse<MenuType>(value, true, out var parsed) => parsed,
            _ => throw new BusinessException($"无法识别菜单类型：{value}")
        };
    }
}
