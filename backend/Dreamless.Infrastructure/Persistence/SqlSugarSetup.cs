using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dreamless.Infrastructure.Persistence;

public static class EntityTypes
{
    public static Type[] All => typeof(SysUser).Assembly
        .GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetCustomAttribute<SugarTable>() != null)
        .ToArray();
}

public static class SqlSugarSetup
{
    public static ISqlSugarClient Create(IServiceProvider services, IConfiguration configuration)
    {
        var current = services.GetRequiredService<ICurrentUser>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SqlSugar");
        var connection = Environment.GetEnvironmentVariable("DREAMLESS_CONNECTION")
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("缺少 ConnectionStrings:Default");

        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = connection,
            DbType = DbType.SqlServer,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (property, column) =>
                {
                    if (property.PropertyType == typeof(string))
                    {
                        column.DataType = "nvarchar";
                    }
                }
            }
        });

        db.Aop.OnLogExecuting = (sql, _) => logger.LogDebug("{Sql}", sql);
        AddFilters(db, current);
        BindAudit(db, current);
        return db;
    }

    private static void AddFilters(ISqlSugarClient db, ICurrentUser current)
    {
        var softDelete = typeof(SqlSugarSetup).GetMethod(nameof(AddSoftDelete), BindingFlags.NonPublic | BindingFlags.Static)!;
        var tenant = typeof(SqlSugarSetup).GetMethod(nameof(AddTenant), BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var type in EntityTypes.All)
        {
            if (typeof(ISoftDelete).IsAssignableFrom(type))
            {
                softDelete.MakeGenericMethod(type).Invoke(null, [db]);
            }

            if (typeof(ITenantEntity).IsAssignableFrom(type)
                && current.IsAuthenticated
                && current.TenantId > 0)
            {
                tenant.MakeGenericMethod(type).Invoke(null, [db, current.TenantId]);
            }
        }
    }

    private static void AddSoftDelete<T>(ISqlSugarClient db) where T : class, ISoftDelete, new()
    {
        db.QueryFilter.AddTableFilter<T>(it => it.IsDeleted == false);
    }

    private static void AddTenant<T>(ISqlSugarClient db, long tenantId) where T : class, ITenantEntity, new()
    {
        db.QueryFilter.AddTableFilter<T>(it => it.TenantId == tenantId);
    }

    private static void BindAudit(ISqlSugarClient db, ICurrentUser current)
    {
        db.Aop.DataExecuting = (_, entity) =>
        {
            if (entity.OperationType == DataFilterType.InsertByObject)
            {
                switch (entity.PropertyName)
                {
                    case nameof(BaseEntity.CreateTime) when entity.EntityValue is BaseEntity:
                        entity.SetValue(DateTime.Now);
                        break;
                    case nameof(BaseEntity.CreateUserId) when current.IsAuthenticated:
                        entity.SetValue(current.UserId);
                        break;
                    case nameof(BaseEntity.IsDeleted) when entity.EntityValue is BaseEntity:
                        entity.SetValue(false);
                        break;
                    case nameof(ITenantEntity.TenantId) when entity.EntityValue is ITenantEntity tenant && tenant.TenantId == 0 && current.TenantId > 0:
                        entity.SetValue(current.TenantId);
                        break;
                }
            }

            if (entity.OperationType == DataFilterType.UpdateByObject)
            {
                switch (entity.PropertyName)
                {
                    case nameof(BaseEntity.UpdateTime) when entity.EntityValue is BaseEntity:
                        entity.SetValue(DateTime.Now);
                        break;
                    case nameof(BaseEntity.UpdateUserId) when current.IsAuthenticated:
                        entity.SetValue(current.UserId);
                        break;
                }
            }
        };
    }
}
