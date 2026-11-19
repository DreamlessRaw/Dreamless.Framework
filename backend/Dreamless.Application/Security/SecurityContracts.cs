namespace Dreamless.Application.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    long UserId { get; }

    long TenantId { get; }

    long? DeptId { get; }

    string UserName { get; }

    bool IsHostAdmin { get; }

    string Culture { get; }
}

public class FieldAccess
{
    public long FieldId { get; set; }

    public string Field { get; set; } = string.Empty;

    public bool CanView { get; set; }

    public bool CanEdit { get; set; }

    public bool CanExport { get; set; }
}

public class DataScopeFilter
{
    public bool All { get; init; }

    public bool IncludeSelf { get; init; }

    public List<long> DeptIds { get; init; } = [];
}

public interface IPermissionService : Common.IAppService
{
    Task<bool> HasAsync(long userId, string code);

    Task<HashSet<string>> GetCodesAsync(long userId, bool isHostAdmin);

    void Invalidate();
}

public interface IFieldAccessService : Common.IAppService
{
    Task<IReadOnlyList<FieldAccess>> GetAsync(string pageCode);

    void Mask<T>(T target, IReadOnlyList<FieldAccess> access);

    void CopyEditable<TSource, TTarget>(TSource source, TTarget target, IReadOnlyList<FieldAccess> access);
}

public interface IDataScopeService : Common.IAppService
{
    Task<DataScopeFilter> GetAsync();
}

public interface ILocalizationService : Common.IAppService
{
    Task<string> T(string key, string? fallback = null);

    Task<IReadOnlyDictionary<string, string>> GetMapAsync(string culture);

    void Invalidate(string? culture = null);
}
