using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PerformanceOs.Domain.TaskItems;
using PerformanceOs.Domain.WorkTypes;

namespace PerformanceOs.Infrastructure.Persistence.Configurations;

/// <summary>docs/06-database-design.md §2.2</summary>
public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("task_items", t =>
            t.HasCheckConstraint("ck_task_items_title_not_blank", "btrim(title) <> ''"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(TaskItem.MaxTitleLength)
            .IsRequired();

        builder.Property(x => x.DefaultWorkTypeId)
            .HasColumnName("default_work_type_id")
            .IsRequired();

        builder.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(TaskItem.MaxNoteLength);

        builder.Property(x => x.IsArchived)
            .HasColumnName("is_archived")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne<WorkType>()
            .WithMany()
            .HasForeignKey(x => x.DefaultWorkTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_task_items_default_work_type");

        // title に一意制約を置かない。同名のタスクを別機会に登録するのは
        // 正常な操作である（TI-3）。
        builder.HasIndex(x => new { x.IsArchived, x.UpdatedAt })
            .HasDatabaseName("ix_task_items_active")
            .IsDescending(false, true);
    }
}
