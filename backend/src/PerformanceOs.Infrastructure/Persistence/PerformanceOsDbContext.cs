using Microsoft.EntityFrameworkCore;
using PerformanceOs.Domain.DailyConditions;
using PerformanceOs.Domain.PlannedWorks;
using PerformanceOs.Domain.TaskItems;
using PerformanceOs.Domain.ValueObjects;
using PerformanceOs.Domain.WorkSessions;
using PerformanceOs.Domain.WorkTypes;
using PerformanceOs.Infrastructure.Persistence.Converters;

namespace PerformanceOs.Infrastructure.Persistence;

public sealed class PerformanceOsDbContext : DbContext
{
    public PerformanceOsDbContext(DbContextOptions<PerformanceOsDbContext> options)
        : base(options)
    {
    }

    public DbSet<WorkType> WorkTypes => Set<WorkType>();

    public DbSet<TaskItem> TaskItems => Set<TaskItem>();

    /// <summary>
    /// 集約ルートのみ DbSet を公開する。PreWorkState / WorkContext /
    /// PerformanceResult はナビゲーション経由で扱う。
    /// </summary>
    public DbSet<WorkSession> WorkSessions => Set<WorkSession>();

    /// <summary>
    /// 日次コンディション。WorkSession とは FK を持たず、JST の日付で
    /// 暗黙に結合する（docs/06-database-design.md §1）。
    /// </summary>
    public DbSet<DailyCondition> DailyConditions => Set<DailyCondition>();

    /// <summary>
    /// 作業予定。NonExecutionRecord は集約内のため DbSet を公開しない。
    /// </summary>
    public DbSet<PlannedWork> PlannedWorks => Set<PlannedWork>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // すべての Rating プロパティに同じ変換を適用する。
        // 個別指定にすると、新しい評価指標を追加したときの設定漏れが起きる。
        configurationBuilder.Properties<Rating>()
            .HaveConversion<RatingConverter>()
            .HaveColumnType("smallint");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 主キーは GENERATED ALWAYS AS IDENTITY（docs/06-database-design.md §0）。
        // 初期データは HasData ではなくマイグレーション内の InsertData で
        // Id 列を省略して投入する。ALWAYS では明示的な Id 指定ができないため。
        modelBuilder.UseIdentityAlwaysColumns();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PerformanceOsDbContext).Assembly);
    }
}
