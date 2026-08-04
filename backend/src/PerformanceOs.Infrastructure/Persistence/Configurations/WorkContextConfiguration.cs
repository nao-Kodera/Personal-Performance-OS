using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Infrastructure.Persistence.Configurations;

/// <summary>
/// docs/06-database-design.md §2.7
/// </summary>
/// <remarks><b>updated_at を持たない。</b>理由は pre_work_states と同じ（WC-3）。</remarks>
public sealed class WorkContextConfiguration : IEntityTypeConfiguration<WorkContext>
{
    public void Configure(EntityTypeBuilder<WorkContext> builder)
    {
        builder.ToTable("work_contexts", t =>
        {
            t.HasCheckConstraint(
                "ck_work_contexts_location",
                "work_location IN ('Home','Office','Cafe','Other')");

            t.HasCheckConstraint("ck_work_contexts_meeting", "meeting_count >= 0");

            // WC-2
            t.HasCheckConstraint(
                "ck_work_contexts_location_note",
                "location_note IS NULL OR work_location = 'Other'");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.WorkSessionId).HasColumnName("work_session_id").IsRequired();

        builder.Property(x => x.WorkLocation)
            .HasColumnName("work_location")
            .HasConversion<string>()
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.LocationNote)
            .HasColumnName("location_note")
            .HasMaxLength(WorkContext.MaxLocationNoteLength);

        builder.Property(x => x.MeetingCount)
            .HasColumnName("meeting_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.InterruptionExpected)
            .HasColumnName("interruption_expected")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.RecordedAt).HasColumnName("recorded_at").IsRequired();

        builder.HasIndex(x => x.WorkSessionId)
            .IsUnique()
            .HasDatabaseName("uq_work_contexts_session");
    }
}
