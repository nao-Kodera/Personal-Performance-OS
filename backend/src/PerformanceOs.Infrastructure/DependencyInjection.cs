using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PerformanceOs.Application.TaskItems;
using PerformanceOs.Domain.Repositories;
using PerformanceOs.Domain.Time;
using PerformanceOs.Infrastructure.Persistence;
using PerformanceOs.Infrastructure.Persistence.Queries;
using PerformanceOs.Infrastructure.Persistence.Repositories;
using PerformanceOs.Infrastructure.Time;

namespace PerformanceOs.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Infrastructure の実装を登録する。呼び出しは Api の Program.cs（T-08）で行う。
    /// </summary>
    /// <remarks>
    /// マイグレーションの自動適用は行わない。明示的に
    /// <c>dotnet ef database update</c> を実行する（docs/08-technical-design.md §4）。
    /// </remarks>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PerformanceOsDbContext>(options => options.UseNpgsql(connectionString));

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IWorkTypeRepository, WorkTypeRepository>();
        services.AddScoped<ITaskItemRepository, TaskItemRepository>();
        services.AddScoped<IWorkSessionRepository, WorkSessionRepository>();
        services.AddScoped<IDailyConditionRepository, DailyConditionRepository>();

        // 読み取りモデル。集約を経由しない射影の実装。
        services.AddScoped<ITaskItemQuery, TaskItemQuery>();

        return services;
    }
}
