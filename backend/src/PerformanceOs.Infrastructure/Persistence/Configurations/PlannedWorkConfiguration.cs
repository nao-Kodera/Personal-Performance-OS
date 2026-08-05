using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PerformanceOs.Domain.PlannedWorks;
using PerformanceOs.Domain.TaskItems;
using PerformanceOs.Domain.WorkTypes;

namespace PerformanceOs.Infrastructure.Persistence.Configurations;

/// <summary>docs/06-database-design.md §2.4</summary>
public sealed class PlannedWorkConfiguration : IEntityTypeConfiguration<PlannedWork>
{
    public void Configure(EntityTypeBuilder<PlannedWork> builder)
    {
        builder.ToTable("planned_works", t =>
        {
            t.HasCheckConstraint(
                "ck_planned_works_time_band",
                """
                planned_time_band IS NULL OR
                planned_time_band IN ('EarlyMorning','Morning','Afternoon','Evening')
                """);

            t.HasCheckConstraint(
                "ck_planned_works_minutes",
                """
                planned_minutes IS NULL OR
                (planned_minutes BETWEEN 15 AND 1440 AND planned_minutes % 15 = 0)
                """);
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        // JST 基準の論理日（docs/02-glossary.md §4）。
        builder.Property(x => x.TargetDate)
            .HasColumnName("target_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.TaskItemId).HasColumnName("task_item_id").IsRequired();
        builder.Property(x => x.WorkTypeId).HasColumnName("work_type_id").IsRequired();

        // 列挙値は text + CHECK 制約（docs/06-database-design.md §0.1）。
        builder.Property(x => x.PlannedTimeBand)
            .HasColumnName("planned_time_band")
            .HasConversion<string>()
            .HasColumnType("text");

        builder.Property(x => x.PlannedMinutes).HasColumnName("planned_minutes");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Ignore(x => x.IsUnexecuted);

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.TaskItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_planned_works_task_item");

        builder.HasOne<WorkType>()
            .WithMany()
            .HasForeignKey(x => x.WorkTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_planned_works_work_type");

        // 集約内の未実行記録。予定を読むときは常に一緒に読む。
        builder.HasOne(x => x.NonExecution)
            .WithOne()
            .HasForeignKey<NonExecutionRecord>(x => x.PlannedWorkId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_non_execution_records_planned_work");

        builder.Navigation(x => x.NonExecution).AutoInclude();

        // 一意制約を置かない。同一タスク・同一日の重複を許す（PW-3）。
        builder.HasIndex(x => x.TargetDate).HasDatabaseName("ix_planned_works_date");
    }
}
