using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PerformanceOs.Domain.PlannedWorks;
using PerformanceOs.Domain.TaskItems;
using PerformanceOs.Domain.WorkSessions;
using PerformanceOs.Domain.WorkTypes;

namespace PerformanceOs.Infrastructure.Persistence.Configurations;

/// <summary>docs/06-database-design.md §2.5</summary>
public sealed class WorkSessionConfiguration : IEntityTypeConfiguration<WorkSession>
{
    public void Configure(EntityTypeBuilder<WorkSession> builder)
    {
        builder.ToTable("work_sessions", t =>
        {
            t.HasCheckConstraint(
                "ck_work_sessions_interruption",
                "interruption_count >= 0");

            // WS-2 / WS-3 / WS-4: 状態と finished_at の整合
            t.HasCheckConstraint(
                "ck_work_sessions_status_finished",
                """
                (status = 'InProgress' AND finished_at IS NULL)
                OR (status IN ('Completed','Abandoned') AND finished_at IS NOT NULL)
                """);

            // WS-5: 時系列の整合
            t.HasCheckConstraint(
                "ck_work_sessions_period",
                "finished_at IS NULL OR finished_at > started_at");

            t.HasCheckConstraint(
                "ck_work_sessions_abandon_note",
                "abandon_note IS NULL OR status = 'Abandoned'");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.TaskItemId).HasColumnName("task_item_id").IsRequired();
        builder.Property(x => x.WorkTypeId).HasColumnName("work_type_id").IsRequired();
        builder.Property(x => x.PlannedWorkId).HasColumnName("planned_work_id");

        builder.Property(x => x.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(x => x.FinishedAt).HasColumnName("finished_at");

        // 列挙値は text + CHECK 制約。SQL での可読性を優先する
        // （docs/06-database-design.md §0.1）。
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.InterruptionCount)
            .HasColumnName("interruption_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.AbandonNote)
            .HasColumnName("abandon_note")
            .HasMaxLength(WorkSession.MaxAbandonNoteLength);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // 導出値は保存しない（docs/04-analytics-spec.md §5）。
        builder.Ignore(x => x.Period);
        builder.Ignore(x => x.DurationMinutes);
        builder.Ignore(x => x.BelongingDate);
        builder.Ignore(x => x.TimeBand);
        builder.Ignore(x => x.FatigueDelta);
        builder.Ignore(x => x.FocusGap);

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.TaskItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_work_sessions_task_item");

        builder.HasOne<WorkType>()
            .WithMany()
            .HasForeignKey(x => x.WorkTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_work_sessions_work_type");

        // 予定から開始したセッションのみ値を持つ。予定を消してセッションだけを
        // 残せないよう Restrict にする。
        builder.HasOne<PlannedWork>()
            .WithMany()
            .HasForeignKey(x => x.PlannedWorkId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_work_sessions_planned_work");

        // 1 つの予定に紐づくセッションは最大 1 件。NULL は重複を許すため、
        // 予定外のセッションは何件でも作成できる。
        builder.HasIndex(x => x.PlannedWorkId)
            .IsUnique()
            .HasDatabaseName("uq_work_sessions_planned_work");

        builder.HasIndex(x => x.StartedAt)
            .IsDescending()
            .HasDatabaseName("ix_work_sessions_started_at");

        builder.HasIndex(x => new { x.WorkTypeId, x.StartedAt })
            .HasDatabaseName("ix_work_sessions_work_type");

        builder.HasIndex(x => new { x.TaskItemId, x.StartedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_work_sessions_task_item");

        // 集約内の子。常に一緒に読み込む。
        builder.HasOne(x => x.PreWorkState)
            .WithOne()
            .HasForeignKey<Domain.WorkSessions.PreWorkState>(x => x.WorkSessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pre_work_states_work_session");

        builder.HasOne(x => x.WorkContext)
            .WithOne()
            .HasForeignKey<Domain.WorkSessions.WorkContext>(x => x.WorkSessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_work_contexts_work_session");

        builder.HasOne(x => x.Result)
            .WithOne()
            .HasForeignKey<PerformanceResult>(x => x.WorkSessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_performance_results_work_session");

        builder.Navigation(x => x.PreWorkState).IsRequired();
        builder.Navigation(x => x.WorkContext).IsRequired();

        // uq_work_sessions_single_active（部分一意インデックス）と
        // ix_work_sessions_completed（部分インデックス）は EF Core が
        // 生成できないため、マイグレーション内で手書きする。
    }
}
