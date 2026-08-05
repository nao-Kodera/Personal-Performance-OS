using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PerformanceOs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannedWorks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "planned_works",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    target_date = table.Column<DateOnly>(type: "date", nullable: false),
                    task_item_id = table.Column<long>(type: "bigint", nullable: false),
                    work_type_id = table.Column<long>(type: "bigint", nullable: false),
                    planned_time_band = table.Column<string>(type: "text", nullable: true),
                    planned_minutes = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planned_works", x => x.id);
                    table.CheckConstraint("ck_planned_works_minutes", "planned_minutes IS NULL OR\n(planned_minutes BETWEEN 15 AND 1440 AND planned_minutes % 15 = 0)");
                    table.CheckConstraint("ck_planned_works_time_band", "planned_time_band IS NULL OR\nplanned_time_band IN ('EarlyMorning','Morning','Afternoon','Evening')");
                    table.ForeignKey(
                        name: "fk_planned_works_task_item",
                        column: x => x.task_item_id,
                        principalTable: "task_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_planned_works_work_type",
                        column: x => x.work_type_id,
                        principalTable: "work_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "non_execution_records",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    planned_work_id = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_non_execution_records", x => x.id);
                    table.CheckConstraint("ck_non_execution_records_reason", "reason IN ('NoTime','Interrupted','PoorCondition',\n           'Deprioritized','Overplanned','Other')");
                    table.ForeignKey(
                        name: "fk_non_execution_records_planned_work",
                        column: x => x.planned_work_id,
                        principalTable: "planned_works",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_non_execution_records_planned",
                table: "non_execution_records",
                column: "planned_work_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_planned_works_date",
                table: "planned_works",
                column: "target_date");

            migrationBuilder.CreateIndex(
                name: "IX_planned_works_task_item_id",
                table: "planned_works",
                column: "task_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_planned_works_work_type_id",
                table: "planned_works",
                column: "work_type_id");

            migrationBuilder.AddForeignKey(
                name: "fk_work_sessions_planned_work",
                table: "work_sessions",
                column: "planned_work_id",
                principalTable: "planned_works",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_work_sessions_planned_work",
                table: "work_sessions");

            migrationBuilder.DropTable(
                name: "non_execution_records");

            migrationBuilder.DropTable(
                name: "planned_works");
        }
    }
}
