using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PerformanceOs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyConditions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_conditions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    target_date = table.Column<DateOnly>(type: "date", nullable: false),
                    sleep_minutes = table.Column<int>(type: "integer", nullable: false),
                    physical_condition = table.Column<short>(type: "smallint", nullable: false),
                    mood_level = table.Column<short>(type: "smallint", nullable: false),
                    stress_level = table.Column<short>(type: "smallint", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_conditions", x => x.id);
                    table.CheckConstraint("ck_daily_conditions_mood", "mood_level BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_daily_conditions_physical", "physical_condition BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_daily_conditions_sleep", "sleep_minutes BETWEEN 15 AND 1440 AND sleep_minutes % 15 = 0");
                    table.CheckConstraint("ck_daily_conditions_stress", "stress_level BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateIndex(
                name: "uq_daily_conditions_date",
                table: "daily_conditions",
                column: "target_date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_conditions");
        }
    }
}
