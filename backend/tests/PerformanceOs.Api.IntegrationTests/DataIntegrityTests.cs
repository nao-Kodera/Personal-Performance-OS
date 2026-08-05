using System.Net.Http.Json;
using PerformanceOs.Api.IntegrationTests.Infrastructure;

namespace PerformanceOs.Api.IntegrationTests;

/// <summary>
/// docs/06-database-design.md §7 の整合性検証クエリ。
/// docs/08-technical-design.md §6.2 T-13 / T-14 に対応する。
/// </summary>
/// <remarks>
/// DB の制約では担保できない不変条件（WS-1 / WS-3 / WS-4 / PW-4 / PW-5）は
/// アプリケーション層でしか守られていない。実際の操作を行った後に
/// 検証クエリが 0 件であることを確認する。
/// </remarks>
public sealed class DataIntegrityTests : IntegrationTestBase
{
    public DataIntegrityTests(ApiFactory factory) : base(factory)
    {
    }

    /// <summary>WS-3: Completed なのに成果が無い行。</summary>
    private const string CompletedWithoutResult =
        """
        SELECT count(*) FROM work_sessions ws
        WHERE ws.status = 'Completed'
          AND NOT EXISTS (
              SELECT 1 FROM performance_results pr WHERE pr.work_session_id = ws.id)
        """;

    /// <summary>WS-4: Abandoned なのに成果がある行。</summary>
    private const string AbandonedWithResult =
        """
        SELECT count(*) FROM work_sessions ws
        WHERE ws.status = 'Abandoned'
          AND EXISTS (
              SELECT 1 FROM performance_results pr WHERE pr.work_session_id = ws.id)
        """;

    /// <summary>WS-1: 子が欠けている行。</summary>
    private const string MissingChildren =
        """
        SELECT count(*) FROM work_sessions ws
        WHERE NOT EXISTS (SELECT 1 FROM pre_work_states p WHERE p.work_session_id = ws.id)
           OR NOT EXISTS (SELECT 1 FROM work_contexts   c WHERE c.work_session_id = ws.id)
        """;

    /// <summary>WS-9: 進行中が複数存在する。</summary>
    private const string MultipleActive =
        "SELECT count(*) FROM work_sessions WHERE status = 'InProgress'";

    private async Task AssertIntegrityAsync()
    {
        Assert.Equal(0, await Factory.CountAsync(CompletedWithoutResult));
        Assert.Equal(0, await Factory.CountAsync(AbandonedWithResult));
        Assert.Equal(0, await Factory.CountAsync(MissingChildren));
        Assert.True(await Factory.CountAsync(MultipleActive) <= 1);
    }

    [Fact]
    public async Task 何も記録していない状態で整合している()
    {
        await AssertIntegrityAsync();
    }

    /// <summary>
    /// 一連の操作を行った後も整合していること。
    /// </summary>
    [Fact]
    public async Task 通常の操作後も整合している()
    {
        var taskId = await CreateTaskAsync();

        // 完了させる
        var first = await StartSessionAsync(taskId);
        await Client.PostAsJsonAsync($"/api/work-sessions/{first}/finish", ResultPayload(), Json);

        // 中断終了させる
        var second = await StartSessionAsync(taskId);
        await Client.PostAsJsonAsync(
            $"/api/work-sessions/{second}/abandon", new { note = "中断" }, Json);

        // 進行中を残す
        await StartSessionAsync(taskId);

        await AssertIntegrityAsync();

        Assert.Equal(3, await Factory.CountAsync("SELECT count(*) FROM work_sessions"));
        Assert.Equal(3, await Factory.CountAsync("SELECT count(*) FROM pre_work_states"));
        Assert.Equal(3, await Factory.CountAsync("SELECT count(*) FROM work_contexts"));
        Assert.Equal(1, await Factory.CountAsync("SELECT count(*) FROM performance_results"));
    }

    /// <summary>
    /// docs/08-technical-design.md §6.2 T-13。
    /// 失敗した操作が部分的な行を残さないこと。
    /// </summary>
    [Fact]
    public async Task 失敗した開始は部分的な行を残さない()
    {
        var taskId = await CreateTaskAsync();

        // 422（WC-2 違反）で失敗させる
        var invalidLocation = await Client.PostAsJsonAsync(
            "/api/work-sessions/start",
            StartPayload(taskId, workLocation: "Home", locationNote: "図書館"),
            Json);
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, invalidLocation.StatusCode);

        // 422（存在しない作業タイプ）で失敗させる
        var invalidWorkType = await Client.PostAsJsonAsync(
            "/api/work-sessions/start", StartPayload(taskId, workTypeId: 9999), Json);
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, invalidWorkType.StatusCode);

        Assert.Equal(0, await Factory.CountAsync("SELECT count(*) FROM work_sessions"));
        Assert.Equal(0, await Factory.CountAsync("SELECT count(*) FROM pre_work_states"));
        Assert.Equal(0, await Factory.CountAsync("SELECT count(*) FROM work_contexts"));

        await AssertIntegrityAsync();
    }

    /// <summary>
    /// 失敗した終了が成果を残さないこと。WS-3 の裏返し。
    /// </summary>
    [Fact]
    public async Task 失敗した終了は成果を残さない()
    {
        var taskId = await CreateTaskAsync();
        var sessionId = await StartSessionAsync(taskId);

        var response = await Client.PostAsJsonAsync(
            $"/api/work-sessions/{sessionId}/finish", ResultPayload(focusLevel: 99), Json);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await Factory.CountAsync("SELECT count(*) FROM performance_results"));
        Assert.Equal(1, await Factory.CountAsync(
            "SELECT count(*) FROM work_sessions WHERE status = 'InProgress'"));

        await AssertIntegrityAsync();
    }

    /// <summary>
    /// 並行して開始しても、進行中は 1 件を超えない。
    /// </summary>
    [Fact]
    public async Task 並行開始後も進行中は一件を超えない()
    {
        var taskId = await CreateTaskAsync();
        var payload = StartPayload(taskId);

        await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ =>
                Factory.CreateClient().PostAsJsonAsync(
                    "/api/work-sessions/start", payload, Json)));

        Assert.Equal(1, await Factory.CountAsync(MultipleActive));

        await AssertIntegrityAsync();
    }
}
