using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PerformanceOs.Domain.DailyConditions;
using PerformanceOs.Infrastructure.Persistence.Converters;

namespace PerformanceOs.Infrastructure.Persistence.Configurations;

/// <summary>docs/06-database-design.md §2.3</summary>
public sealed class DailyConditionConfiguration : IEntityTypeConfiguration<DailyCondition>
{
    public void Configure(EntityTypeBuilder<DailyCondition> builder)
    {
        builder.ToTable("daily_conditions", t =>
        {
            t.HasCheckConstraint(
                "ck_daily_conditions_sleep",
                "sleep_minutes BETWEEN 15 AND 1440 AND sleep_minutes % 15 = 0");
            t.HasCheckConstraint(
                "ck_daily_conditions_physical", "physical_condition BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_daily_conditions_mood", "mood_level BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_daily_conditions_stress", "stress_level BETWEEN 1 AND 5");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        // JST 基準の論理日。timestamptz ではなく date で持つ
        // （docs/02-glossary.md §4）。
        builder.Property(x => x.TargetDate)
            .HasColumnName("target_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.Sleep)
            .HasColumnName("sleep_minutes")
            .HasConversion<SleepDurationConverter>()
            .IsRequired();

        builder.Property(x => x.PhysicalCondition).HasColumnName("physical_condition").IsRequired();
        builder.Property(x => x.MoodLevel).HasColumnName("mood_level").IsRequired();
        builder.Property(x => x.StressLevel).HasColumnName("stress_level").IsRequired();

        builder.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(DailyCondition.MaxNoteLength);

        builder.Property(x => x.RecordedAt).HasColumnName("recorded_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // 睡眠時間から都度導出する。保存すると区分の定義を変えたときに
        // 過去データへ新定義が適用されない（docs/05-domain-design.md §5）。
        builder.Ignore(x => x.SleepBand);

        // DC-1（1日1件）の担保。範囲検索にもこのインデックスが使われるため、
        // 追加のインデックスは作らない（docs/06-database-design.md §2.3）。
        builder.HasIndex(x => x.TargetDate)
            .IsUnique()
            .HasDatabaseName("uq_daily_conditions_date");
    }
}
