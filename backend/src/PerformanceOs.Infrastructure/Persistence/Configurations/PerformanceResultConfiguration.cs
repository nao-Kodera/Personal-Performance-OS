using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Infrastructure.Persistence.Configurations;

/// <summary>docs/06-database-design.md §2.8</summary>
public sealed class PerformanceResultConfiguration : IEntityTypeConfiguration<PerformanceResult>
{
    public void Configure(EntityTypeBuilder<PerformanceResult> builder)
    {
        builder.ToTable("performance_results", t =>
        {
            t.HasCheckConstraint("ck_performance_results_focus", "focus_level BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_performance_results_output", "output_level BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_performance_results_accuracy", "accuracy_level BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_performance_results_satisfaction", "satisfaction_level BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_performance_results_fatigue", "fatigue_after BETWEEN 1 AND 5");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.WorkSessionId).HasColumnName("work_session_id").IsRequired();

        builder.Property(x => x.FocusLevel).HasColumnName("focus_level").IsRequired();
        builder.Property(x => x.OutputLevel).HasColumnName("output_level").IsRequired();
        builder.Property(x => x.AccuracyLevel).HasColumnName("accuracy_level").IsRequired();
        builder.Property(x => x.SatisfactionLevel).HasColumnName("satisfaction_level").IsRequired();
        builder.Property(x => x.FatigueAfter).HasColumnName("fatigue_after").IsRequired();

        builder.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(PerformanceResult.MaxNoteLength);

        builder.Property(x => x.RecordedAt).HasColumnName("recorded_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // 導出値。合成指標は持たない（PR-4）。
        builder.Ignore(x => x.IsEdited);

        builder.HasIndex(x => x.WorkSessionId)
            .IsUnique()
            .HasDatabaseName("uq_performance_results_session");
    }
}
