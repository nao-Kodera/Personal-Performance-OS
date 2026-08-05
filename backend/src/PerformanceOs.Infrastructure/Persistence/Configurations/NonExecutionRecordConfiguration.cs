using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PerformanceOs.Domain.PlannedWorks;

namespace PerformanceOs.Infrastructure.Persistence.Configurations;

/// <summary>docs/06-database-design.md §2.9</summary>
public sealed class NonExecutionRecordConfiguration
    : IEntityTypeConfiguration<NonExecutionRecord>
{
    public void Configure(EntityTypeBuilder<NonExecutionRecord> builder)
    {
        builder.ToTable("non_execution_records", t =>
            t.HasCheckConstraint(
                "ck_non_execution_records_reason",
                """
                reason IN ('NoTime','Interrupted','PoorCondition',
                           'Deprioritized','Overplanned','Other')
                """));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.PlannedWorkId).HasColumnName("planned_work_id").IsRequired();

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasConversion<string>()
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(NonExecutionRecord.MaxNoteLength);

        builder.Property(x => x.RecordedAt).HasColumnName("recorded_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // NE-1: 1 つの予定に対して最大 1 件。
        builder.HasIndex(x => x.PlannedWorkId)
            .IsUnique()
            .HasDatabaseName("uq_non_execution_records_planned");
    }
}
