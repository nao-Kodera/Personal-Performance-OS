using Npgsql;
using PerformanceOs.Api.IntegrationTests.Infrastructure;

namespace PerformanceOs.Api.IntegrationTests;

/// <summary>
/// DB 制約そのものの実効性。
/// </summary>
/// <remarks>
/// <para>
/// アプリケーション層を迂回して直接 SQL を実行する。API 経由の並行テストは、
/// アプリケーション層の事前チェックだけでも通ってしまうため、制約が実際に
/// 存在することの証明にならない。制約を削除しても並行テストは大半のケースで
/// 通ってしまい、まれに落ちるだけになる。
/// </para>
/// <para>
/// ここでは制約違反を確実に発生させ、SqlState と制約名を検証する。
/// </para>
/// </remarks>
public sealed class DatabaseConstraintTests : IntegrationTestBase
{
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";

    public DatabaseConstraintTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<long> SeedTaskAsync() => await CreateTaskAsync();

    private static string InsertSessionSql(long taskItemId, string status, string finishedAt)
        => $"""
            INSERT INTO work_sessions
                (task_item_id, work_type_id, started_at, finished_at, status,
                 interruption_count, created_at, updated_at)
            VALUES ({taskItemId}, {DesignWorkTypeId}, now(), {finishedAt}, '{status}',
                    0, now(), now());
            """;

    /// <summary>
    /// docs/08-technical-design.md §6.2 T-02 / §3.6。
    /// WS-9 の最終的な担保である部分一意インデックスが実在し、機能すること。
    /// </summary>
    [Fact]
    public async Task 進行中の二重登録は部分一意インデックスで拒否される()
    {
        var taskId = await SeedTaskAsync();
        await Factory.ExecuteAsync(InsertSessionSql(taskId, "InProgress", "NULL"));

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => Factory.ExecuteAsync(InsertSessionSql(taskId, "InProgress", "NULL")));

        Assert.Equal(UniqueViolation, exception.SqlState);
        Assert.Equal("uq_work_sessions_single_active", exception.ConstraintName);
        Assert.Equal(1, await Factory.CountAsync(
            "SELECT count(*) FROM work_sessions WHERE status = 'InProgress'"));
    }

    /// <summary>
    /// 終了済みは何件でも共存できる。部分インデックスの条件が
    /// status = 'InProgress' に限定されていること。
    /// </summary>
    [Fact]
    public async Task 完了済みは複数存在できる()
    {
        var taskId = await SeedTaskAsync();

        await Factory.ExecuteAsync(InsertSessionSql(taskId, "Completed", "now() + interval '1 hour'"));
        await Factory.ExecuteAsync(InsertSessionSql(taskId, "Completed", "now() + interval '2 hour'"));

        Assert.Equal(2, await Factory.CountAsync("SELECT count(*) FROM work_sessions"));
    }

    /// <summary>WS-2: InProgress なのに finished_at がある。</summary>
    [Fact]
    public async Task 進行中に終了時刻があると拒否される()
    {
        var taskId = await SeedTaskAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => Factory.ExecuteAsync(
                InsertSessionSql(taskId, "InProgress", "now() + interval '1 hour'")));

        Assert.Equal(CheckViolation, exception.SqlState);
        Assert.Equal("ck_work_sessions_status_finished", exception.ConstraintName);
    }

    /// <summary>WS-5: 終了時刻が開始時刻以前。</summary>
    [Fact]
    public async Task 終了時刻が開始時刻以前なら拒否される()
    {
        var taskId = await SeedTaskAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => Factory.ExecuteAsync(
                InsertSessionSql(taskId, "Completed", "now() - interval '1 hour'")));

        Assert.Equal(CheckViolation, exception.SqlState);
        Assert.Equal("ck_work_sessions_period", exception.ConstraintName);
    }

    /// <summary>Rating の範囲。値オブジェクトと DB の二重の担保のうち DB 側。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task 評価値が範囲外なら拒否される(int value)
    {
        var taskId = await SeedTaskAsync();
        await Factory.ExecuteAsync(InsertSessionSql(taskId, "InProgress", "NULL"));

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => Factory.ExecuteAsync(
                $"""
                 INSERT INTO pre_work_states
                     (work_session_id, fatigue_level, expected_focus_level, mood_level, recorded_at)
                 SELECT id, {value}, 4, 4, now() FROM work_sessions LIMIT 1;
                 """));

        Assert.Equal(CheckViolation, exception.SqlState);
    }

    /// <summary>WC-2: 場所の補足は Other のときのみ。</summary>
    [Fact]
    public async Task その他以外の場所で補足があると拒否される()
    {
        var taskId = await SeedTaskAsync();
        await Factory.ExecuteAsync(InsertSessionSql(taskId, "InProgress", "NULL"));

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => Factory.ExecuteAsync(
                """
                INSERT INTO work_contexts
                    (work_session_id, work_location, location_note, meeting_count,
                     interruption_expected, recorded_at)
                SELECT id, 'Home', '図書館', 0, false, now() FROM work_sessions LIMIT 1;
                """));

        Assert.Equal(CheckViolation, exception.SqlState);
        Assert.Equal("ck_work_contexts_location_note", exception.ConstraintName);
    }

    /// <summary>WT-2: 作業タイプ名は大文字小文字を区別せず一意。</summary>
    [Fact]
    public async Task 大文字小文字違いの同名作業タイプは拒否される()
    {
        await Factory.ExecuteAsync(
            "INSERT INTO work_types (name, display_order, is_active, created_at, updated_at) " +
            "VALUES ('Review', 100, true, now(), now());");

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => Factory.ExecuteAsync(
                "INSERT INTO work_types (name, display_order, is_active, created_at, updated_at) " +
                "VALUES ('review', 110, true, now(), now());"));

        Assert.Equal(UniqueViolation, exception.SqlState);
        Assert.Equal("uq_work_types_name", exception.ConstraintName);
    }

    /// <summary>
    /// 初期データが投入されていること（docs/06-database-design.md §2.1）。
    /// </summary>
    [Fact]
    public async Task 作業タイプの初期データが六件ある()
    {
        Assert.Equal(6, await Factory.CountAsync("SELECT count(*) FROM work_types WHERE id <= 6"));
        Assert.Equal(1, await Factory.CountAsync(
            "SELECT count(*) FROM work_types WHERE name = '実装'"));
    }
}
