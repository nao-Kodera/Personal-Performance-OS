using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Infrastructure.Persistence.Configurations;

/// <summary>
/// docs/06-database-design.md §2.6
/// </summary>
/// <remarks>
/// <b>updated_at を持たない。</b>この行は生成後に更新されない（PS-2）。
/// 更新カラムを持たないことで、設計意図をスキーマ側でも表現する。
/// </remarks>
public sealed class PreWorkStateConfiguration : IEntityTypeConfiguration<PreWorkState>
{
    public void Configure(EntityTypeBuilder<PreWorkState> builder)
    {
        builder.ToTable("pre_work_states", t =>
        {
            t.HasCheckConstraint("ck_pre_work_states_fatigue", "fatigue_level BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_pre_work_states_expected", "expected_focus_level BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_pre_work_states_mood", "mood_level BETWEEN 1 AND 5");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.WorkSessionId).HasColumnName("work_session_id").IsRequired();
        builder.Property(x => x.FatigueLevel).HasColumnName("fatigue_level").IsRequired();
        builder.Property(x => x.ExpectedFocusLevel).HasColumnName("expected_focus_level").IsRequired();
        builder.Property(x => x.MoodLevel).HasColumnName("mood_level").IsRequired();
        builder.Property(x => x.RecordedAt).HasColumnName("recorded_at").IsRequired();

        builder.HasIndex(x => x.WorkSessionId)
            .IsUnique()
            .HasDatabaseName("uq_pre_work_states_session");
    }
}
