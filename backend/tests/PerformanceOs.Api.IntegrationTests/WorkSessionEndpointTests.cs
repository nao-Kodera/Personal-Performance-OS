using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PerformanceOs.Api.IntegrationTests.Infrastructure;

namespace PerformanceOs.Api.IntegrationTests;

/// <summary>
/// 作業セッションのエンドポイント。docs/08-technical-design.md §6.2 の
/// T-01 / T-03 / T-04 / T-05 / T-06 / T-07 に対応する。
/// </summary>
public sealed class WorkSessionEndpointTests : IntegrationTestBase
{
    public WorkSessionEndpointTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task 進行中が無ければ204を返す()
    {
        var response = await Client.GetAsync("/api/work-sessions/active");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task 開始すると201と集約が返る()
    {
        var taskId = await CreateTaskAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/work-sessions/start", StartPayload(taskId), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal("InProgress", body.GetProperty("status").GetString());
        Assert.Equal("検証タスク", body.GetProperty("taskTitle").GetString());
        Assert.Equal("設計", body.GetProperty("workTypeName").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("result").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("preWorkState").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("workContext").ValueKind);
    }

    /// <summary>docs/08-technical-design.md §6.2 T-01。</summary>
    [Fact]
    public async Task 進行中がある状態で開始すると409になる()
    {
        var taskId = await CreateTaskAsync();
        await StartSessionAsync(taskId);

        var response = await Client.PostAsJsonAsync(
            "/api/work-sessions/start", StartPayload(taskId), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// docs/08-technical-design.md §6.2 T-02。
    /// </summary>
    /// <remarks>
    /// <b>DB 制約の実効性を確認する唯一のテストである。</b>
    /// アプリケーション層の事前チェックは、両方のリクエストが「進行中なし」を
    /// 観測した後に両方が INSERT する競合を防げない。部分一意インデックス
    /// <c>uq_work_sessions_single_active</c> だけが最終的な担保になる
    /// （docs/08-technical-design.md §3.6）。
    /// </remarks>
    [Fact]
    public async Task 並行して開始すると一方だけが成功する()
    {
        var taskId = await CreateTaskAsync();

        const int attempts = 8;
        var payload = StartPayload(taskId);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, attempts).Select(_ =>
                Factory.CreateClient().PostAsJsonAsync(
                    "/api/work-sessions/start", payload, Json)));

        var created = responses.Count(x => x.StatusCode == HttpStatusCode.Created);
        var conflicted = responses.Count(x => x.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, created);
        Assert.Equal(attempts - 1, conflicted);

        // DB 上も 1 件だけであること。
        Assert.Equal(1, await Factory.CountAsync(
            "SELECT count(*) FROM work_sessions WHERE status = 'InProgress'"));
    }

    /// <summary>docs/08-technical-design.md §6.2 T-06。</summary>
    [Fact]
    public async Task 終了すると成果が必ず保存される()
    {
        var taskId = await CreateTaskAsync();
        var sessionId = await StartSessionAsync(taskId);

        var response = await Client.PostAsJsonAsync(
            $"/api/work-sessions/{sessionId}/finish", ResultPayload(), Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Completed", body.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("result").ValueKind);

        Assert.Equal(1, await Factory.CountAsync(
            $"SELECT count(*) FROM performance_results WHERE work_session_id = {sessionId}"));
    }

    /// <summary>docs/08-technical-design.md §6.2 T-03。</summary>
    [Fact]
    public async Task 完了済みを再度終了すると409になる()
    {
        var taskId = await CreateTaskAsync();
        var sessionId = await StartSessionAsync(taskId);
        await Client.PostAsJsonAsync($"/api/work-sessions/{sessionId}/finish", ResultPayload(), Json);

        var response = await Client.PostAsJsonAsync(
            $"/api/work-sessions/{sessionId}/finish", ResultPayload(), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>docs/08-technical-design.md §6.2 T-04。</summary>
    [Fact]
    public async Task 中断終了したものを終了すると409になる()
    {
        var taskId = await CreateTaskAsync();
        var sessionId = await StartSessionAsync(taskId);
        await Client.PostAsJsonAsync(
            $"/api/work-sessions/{sessionId}/abandon", new { note = "会議" }, Json);

        var response = await Client.PostAsJsonAsync(
            $"/api/work-sessions/{sessionId}/finish", ResultPayload(), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>docs/08-technical-design.md §6.2 T-07。</summary>
    [Fact]
    public async Task 中断終了すると成果が保存されない()
    {
        var taskId = await CreateTaskAsync();
        var sessionId = await StartSessionAsync(taskId);

        var response = await Client.PostAsJsonAsync(
            $"/api/work-sessions/{sessionId}/abandon", new { note = "会議に呼ばれた" }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Abandoned", body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("result").ValueKind);

        Assert.Equal(0, await Factory.CountAsync(
            $"SELECT count(*) FROM performance_results WHERE work_session_id = {sessionId}"));
    }

    /// <summary>
    /// docs/08-technical-design.md §6.2 T-05。
    /// 成果評価のスキップ導線を作らないという方針が API 層で守られていること。
    /// </summary>
    [Fact]
    public async Task 成果評価が欠けていると400になる()
    {
        var taskId = await CreateTaskAsync();
        var sessionId = await StartSessionAsync(taskId);

        var response = await Client.PostAsJsonAsync(
            $"/api/work-sessions/{sessionId}/finish",
            new { interruptionCount = 0, result = new { focusLevel = 4, outputLevel = 4 } },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await Factory.CountAsync("SELECT count(*) FROM performance_results"));
    }

    [Fact]
    public async Task 成果評価そのものが無いと400になる()
    {
        var taskId = await CreateTaskAsync();
        var sessionId = await StartSessionAsync(taskId);

        var response = await Client.PostAsJsonAsync(
            $"/api/work-sessions/{sessionId}/finish", new { interruptionCount = 0 }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// WS-8: 開始時刻をリクエストで受け取らない。定義外プロパティとして拒否される。
    /// </summary>
    [Fact]
    public async Task 開始時刻を送ると400になる()
    {
        var taskId = await CreateTaskAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/work-sessions/start",
            new
            {
                taskItemId = taskId,
                workTypeId = DesignWorkTypeId,
                startedAt = "2026-08-01T00:00:00Z",
                preWorkState = new { fatigueLevel = 2, expectedFocusLevel = 4, moodLevel = 4 },
                workContext = new
                {
                    workLocation = "Home",
                    meetingCount = 0,
                    interruptionExpected = false,
                },
            },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>WC-2: 場所の補足は Other のときのみ。</summary>
    [Fact]
    public async Task その他以外の場所で補足を送ると422になる()
    {
        var taskId = await CreateTaskAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/work-sessions/start",
            StartPayload(taskId, workLocation: "Home", locationNote: "図書館"),
            Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task 評価値が範囲外なら400になる()
    {
        var taskId = await CreateTaskAsync();
        var sessionId = await StartSessionAsync(taskId);

        var response = await Client.PostAsJsonAsync(
            $"/api/work-sessions/{sessionId}/finish", ResultPayload(focusLevel: 6), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task アーカイブ済みのタスクでは開始できない()
    {
        var taskId = await CreateTaskAsync();
        await Client.PostAsync($"/api/tasks/{taskId}/archive", null);

        var response = await Client.PostAsJsonAsync(
            "/api/work-sessions/start", StartPayload(taskId), Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, await Factory.CountAsync("SELECT count(*) FROM work_sessions"));
    }

    /// <summary>PR-2: 訂正しても初回記録時刻は変わらない。</summary>
    [Fact]
    public async Task 成果を訂正しても初回記録時刻は変わらない()
    {
        var taskId = await CreateTaskAsync();
        var sessionId = await StartSessionAsync(taskId);

        var finished = await Client.PostAsJsonAsync(
            $"/api/work-sessions/{sessionId}/finish", ResultPayload(), Json);
        var finishedBody = await finished.Content.ReadFromJsonAsync<JsonElement>(Json);
        var recordedAt = finishedBody.GetProperty("result").GetProperty("recordedAt").GetString();

        var updated = await Client.PutAsJsonAsync(
            $"/api/work-sessions/{sessionId}/result", ResultPayload(focusLevel: 2), Json);
        var updatedBody = await updated.Content.ReadFromJsonAsync<JsonElement>(Json);
        var result = updatedBody.GetProperty("result");

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(recordedAt, result.GetProperty("recordedAt").GetString());
        Assert.True(result.GetProperty("isEdited").GetBoolean());
        Assert.Equal(2, result.GetProperty("focusLevel").GetInt32());
    }

    [Fact]
    public async Task 進行中の成果は訂正できない()
    {
        var taskId = await CreateTaskAsync();
        var sessionId = await StartSessionAsync(taskId);

        var response = await Client.PutAsJsonAsync(
            $"/api/work-sessions/{sessionId}/result", ResultPayload(), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task 存在しないセッションの操作は404になる()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/work-sessions/99999/finish", ResultPayload(), Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // 履歴
    // ------------------------------------------------------------------

    [Fact]
    public async Task 履歴が日付単位でまとまる()
    {
        var taskId = await CreateTaskAsync();
        var sessionId = await StartSessionAsync(taskId);
        await Client.PostAsJsonAsync($"/api/work-sessions/{sessionId}/finish", ResultPayload(), Json);

        var response = await Client.GetAsync("/api/work-sessions");
        var days = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, days.GetArrayLength());

        var day = days[0];
        Assert.Equal(1, day.GetProperty("sessions").GetArrayLength());
        Assert.Equal(1, day.GetProperty("summary").GetProperty("completedCount").GetInt32());
        Assert.Equal("検証タスク", day.GetProperty("sessions")[0].GetProperty("taskTitle").GetString());
    }

    [Fact]
    public async Task 履歴はアーカイブ済みタスクの名称も解決する()
    {
        var taskId = await CreateTaskAsync();
        var sessionId = await StartSessionAsync(taskId);
        await Client.PostAsJsonAsync($"/api/work-sessions/{sessionId}/finish", ResultPayload(), Json);
        await Client.PostAsync($"/api/tasks/{taskId}/archive", null);

        var response = await Client.GetAsync("/api/work-sessions");
        var days = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(
            "検証タスク",
            days[0].GetProperty("sessions")[0].GetProperty("taskTitle").GetString());
    }

    [Fact]
    public async Task 開始日が終了日より後なら422になる()
    {
        var response = await Client.GetAsync("/api/work-sessions?from=2026-08-05&to=2026-08-04");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
