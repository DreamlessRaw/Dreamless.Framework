using SqlSugar;

namespace Dreamless.Core.Entities;

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}

public interface ITenantEntity
{
    long TenantId { get; set; }
}

public interface IDept
{
    long? DeptId { get; set; }
}

/// <summary>审计字段与软删除。所有持久化实体的公共根。</summary>
public abstract class BaseEntity : ISoftDelete
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    public DateTime CreateTime { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? CreateUserId { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? UpdateTime { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? UpdateUserId { get; set; }

    public bool IsDeleted { get; set; }
}

/// <summary>
/// 租户是默认且必须的隔离维度。业务表继承本类后，非宿主账号的查询会自动加上 TenantId。
/// </summary>
public abstract class BaseTenantEntity : BaseEntity, ITenantEntity
{
    public long TenantId { get; set; }
}

/// <summary>
/// 部门是可选继承。需要按部门做数据权限时继承本类，DeptId 允许为空。
/// </summary>
public abstract class BaseDeptEntity : BaseTenantEntity, IDept
{
    [SugarColumn(IsNullable = true)]
    public long? DeptId { get; set; }
}
