using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PerformanceOs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateCoreRecordingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_types", x => x.id);
                    table.CheckConstraint("ck_work_types_name_not_blank", "btrim(name) <> ''");
                });

            migrationBuilder.CreateTable(
                name: "task_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    default_work_type_id = table.Column<long>(type: "bigint", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_items", x => x.id);
                    table.CheckConstraint("ck_task_items_title_not_blank", "btrim(title) <> ''");
                    table.ForeignKey(
                        name: "fk_task_items_default_work_type",
                        column: x => x.default_work_type_id,
                        principalTable: "work_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_sessions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    task_item_id = table.Column<long>(type: "bigint", nullable: false),
                    work_type_id = table.Column<long>(type: "bigint", nullable: false),
                    planned_work_id = table.Column<long>(type: "bigint", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    interruption_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    abandon_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_sessions", x => x.id);
                    table.CheckConstraint("ck_work_sessions_abandon_note", "abandon_note IS NULL OR status = 'Abandoned'");
                    table.CheckConstraint("ck_work_sessions_interruption", "interruption_count >= 0");
                    table.CheckConstraint("ck_work_sessions_period", "finished_at IS NULL OR finished_at > started_at");
                    table.CheckConstraint("ck_work_sessions_status_finished", "(status = 'InProgress' AND finished_at IS NULL)\nOR (status IN ('Completed','Abandoned') AND finished_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_work_sessions_task_item",
                        column: x => x.task_item_id,
                        principalTable: "task_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_sessions_work_type",
                        column: x => x.work_type_id,
                        principalTable: "work_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "performance_results",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    work_session_id = table.Column<long>(type: "bigint", nullable: false),
                    focus_level = table.Column<short>(type: "smallint", nullable: false),
                    output_level = table.Column<short>(type: "smallint", nullable: false),
                    accuracy_level = table.Column<short>(type: "smallint", nullable: false),
                    satisfaction_level = table.Column<short>(type: "smallint", nullable: false),
                    fatigue_after = table.Column<short>(type: "smallint", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_performance_results", x => x.id);
                    table.CheckConstraint("ck_performance_results_accuracy", "accuracy_level BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_performance_results_fatigue", "fatigue_after BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_performance_results_focus", "focus_level BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_performance_results_output", "output_level BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_performance_results_satisfaction", "satisfaction_level BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_performance_results_work_session",
                        column: x => x.work_session_id,
                        principalTable: "work_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pre_work_states",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    work_session_id = table.Column<long>(type: "bigint", nullable: false),
                    fatigue_level = table.Column<short>(type: "smallint", nullable: false),
                    expected_focus_level = table.Column<short>(type: "smallint", nullable: false),
                    mood_level = table.Column<short>(type: "smallint", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pre_work_states", x => x.id);
                    table.CheckConstraint("ck_pre_work_states_expected", "expected_focus_level BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_pre_work_states_fatigue", "fatigue_level BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_pre_work_states_mood", "mood_level BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_pre_work_states_work_session",
                        column: x => x.work_session_id,
                        principalTable: "work_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_contexts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    work_session_id = table.Column<long>(type: "bigint", nullable: false),
                    work_location = table.Column<string>(type: "text", nullable: false),
                    location_note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    meeting_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    interruption_expected = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_contexts", x => x.id);
                    table.CheckConstraint("ck_work_contexts_location", "work_location IN ('Home','Office','Cafe','Other')");
                    table.CheckConstraint("ck_work_contexts_location_note", "location_note IS NULL OR work_location = 'Other'");
                    table.CheckConstraint("ck_work_contexts_meeting", "meeting_count >= 0");
                    table.ForeignKey(
                        name: "fk_work_contexts_work_session",
                        column: x => x.work_session_id,
                        principalTable: "work_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_performance_results_session",
                table: "performance_results",
                column: "work_session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_pre_work_states_session",
                table: "pre_work_states",
                column: "work_session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_items_active",
                table: "task_items",
                columns: new[] { "is_archived", "updated_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_task_items_default_work_type_id",
                table: "task_items",
                column: "default_work_type_id");

            migrationBuilder.CreateIndex(
                name: "uq_work_contexts_session",
                table: "work_contexts",
                column: "work_session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_sessions_started_at",
                table: "work_sessions",
                column: "started_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_work_sessions_task_item",
                table: "work_sessions",
                columns: new[] { "task_item_id", "started_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_work_sessions_work_type",
                table: "work_sessions",
                columns: new[] { "work_type_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "uq_work_sessions_planned_work",
                table: "work_sessions",
                column: "planned_work_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_types_active_order",
                table: "work_types",
                columns: new[] { "is_active", "display_order" });

            // ----------------------------------------------------------------
            // 以下は EF Core のモデル定義から生成されないため手書きする
            // （docs/06-database-design.md §8）。
            // ----------------------------------------------------------------

            // WT-2: 名称の一意性。大文字小文字を区別しない式インデックス。
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX uq_work_types_name ON work_types (lower(name));
                """);

            // WS-9: 進行中のセッションは全体で 1 件まで。
            //
            // status = 'InProgress' の行だけを対象に、定数 true に対する一意
            // インデックスを張ることで、この条件を満たす行がテーブル全体で
            // 最大 1 行になる。アプリケーション層のチェックだけでは並行
            // リクエストで 2 件作られうるため、これが最終的な担保である。
            //
            // 将来ユーザーを追加する場合は (user_id) WHERE ... に変更すること。
            // このままだとシステム全体で 1 人しか作業できない制約になる
            // （docs/06-database-design.md §6 手順 5）。
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX uq_work_sessions_single_active
                    ON work_sessions ((true)) WHERE status = 'InProgress';
                """);

            // 分析の母集団は status = 'Completed' が大半を占めるため部分インデックス。
            migrationBuilder.Sql(
                """
                CREATE INDEX ix_work_sessions_completed
                    ON work_sessions (started_at DESC) WHERE status = 'Completed';
                """);

            // ----------------------------------------------------------------
            // 初期データ（docs/06-database-design.md §2.1）
            //
            // id は GENERATED ALWAYS AS IDENTITY のため明示的に指定できない。
            // 列を省略して DB に採番させる。display_order を 10 刻みにして
            // いるのは、後から間に挿入できるようにするため。
            // ----------------------------------------------------------------
            migrationBuilder.Sql(
                """
                INSERT INTO work_types (name, display_order, is_active, created_at, updated_at)
                VALUES
                    ('実装',         10, true, now(), now()),
                    ('設計',         20, true, now(), now()),
                    ('ドキュメント', 30, true, now(), now()),
                    ('調査',         40, true, now(), now()),
                    ('会議',         50, true, now(), now()),
                    ('その他',       90, true, now(), now());
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_work_sessions_completed;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS uq_work_sessions_single_active;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS uq_work_types_name;");

            migrationBuilder.DropTable(
                name: "performance_results");

            migrationBuilder.DropTable(
                name: "pre_work_states");

            migrationBuilder.DropTable(
                name: "work_contexts");

            migrationBuilder.DropTable(
                name: "work_sessions");

            migrationBuilder.DropTable(
                name: "task_items");

            migrationBuilder.DropTable(
                name: "work_types");
        }
    }
}
