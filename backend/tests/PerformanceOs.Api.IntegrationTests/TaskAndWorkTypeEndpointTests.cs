using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PerformanceOs.Api.IntegrationTests.Infrastructure;

namespace PerformanceOs.Api.IntegrationTests;

/// <summary>タスクと作業タイプのエンドポイント。</summary>
public sealed class TaskAndWorkTypeEndpointTests : IntegrationTestBase
{
    public TaskAndWorkTypeEndpointTests(ApiFactory factory) : base(factory)
    {
    }

    // ------------------------------------------------------------------
    // 作業タイプ
    // ------------------------------------------------------------------

    [Fact]
    public async Task 初期データが表示順で返る()
    {
        var response = await Client.GetAsync("/api/work-types");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(6, body.GetArrayLength());
        Assert.Equal("実装", body[0].GetProperty("name").GetString());
        Assert.Equal("その他", body[5].GetProperty("name").GetString());
    }

    [Fact]
    public async Task 表示順を省略すると最大値に十を足した値になる()
    {
        var response = await Client.PostAsJsonAsync("/api/work-types", new { name = "レビュー" }, Json);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(100, body.GetProperty("displayOrder").GetInt32());
    }

    [Fact]
    public async Task 大文字小文字違いの同名は409になる()
    {
        await Client.PostAsJsonAsync("/api/work-types", new { name = "Review" }, Json);

        var response = await Client.PostAsJsonAsync("/api/work-types", new { name = "review" }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// ExistsByNameAsync の excludeId が効いていること。設計書 §7 の署名のままだと
    /// この操作が誤って 409 になる。
    /// </summary>
    [Fact]
    public async Task 名称を変えずに表示順だけ更新できる()
    {
        var created = await Client.PostAsJsonAsync("/api/work-types", new { name = "レビュー" }, Json);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = body.GetProperty("id").GetInt64();

        var response = await Client.PutAsJsonAsync(
            $"/api/work-types/{id}",
            new { name = "レビュー", displayOrder = 105, isActive = true },
            Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(105, updated.GetProperty("displayOrder").GetInt32());
    }

    [Fact]
    public async Task 無効化すると既定の一覧から消える()
    {
        var created = await Client.PostAsJsonAsync("/api/work-types", new { name = "レビュー" }, Json);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("id").GetInt64();

        await Client.PutAsJsonAsync(
            $"/api/work-types/{id}",
            new { name = "レビュー", displayOrder = 100, isActive = false },
            Json);

        var active = await (await Client.GetAsync("/api/work-types"))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var all = await (await Client.GetAsync("/api/work-types?includeInactive=true"))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(6, active.GetArrayLength());
        Assert.Equal(7, all.GetArrayLength());
    }

    [Fact]
    public async Task 存在しない作業タイプの更新は404になる()
    {
        var response = await Client.PutAsJsonAsync(
            "/api/work-types/9999",
            new { name = "x", displayOrder = 10, isActive = true },
            Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // タスク
    // ------------------------------------------------------------------

    [Fact]
    public async Task タスクを登録できる()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/tasks",
            new { title = "認証方式の検討", defaultWorkTypeId = DesignWorkTypeId, note = "メモ" },
            Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("認証方式の検討", body.GetProperty("title").GetString());
        Assert.False(body.GetProperty("isArchived").GetBoolean());
    }

    /// <summary>TI-3: 同名のタスクを複数登録できる。</summary>
    [Fact]
    public async Task 同名のタスクを複数登録できる()
    {
        await CreateTaskAsync("週次レビュー");
        await CreateTaskAsync("週次レビュー");

        var body = await (await Client.GetAsync("/api/tasks"))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(2, body.GetArrayLength());
    }

    /// <summary>
    /// docs/08-technical-design.md §8 の禁止事項 2。
    /// TaskItem に完了の概念が入り込んでいないこと。
    /// </summary>
    [Fact]
    public async Task 完了フラグを送ると400になる()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/tasks",
            new { title = "x", defaultWorkTypeId = DesignWorkTypeId, isCompleted = true },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 存在しない作業タイプでは登録できない()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/tasks", new { title = "x", defaultWorkTypeId = 9999 }, Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task タイトルが空なら400になる()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/tasks", new { title = "", defaultWorkTypeId = DesignWorkTypeId }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task アーカイブすると既定の一覧から消える()
    {
        var taskId = await CreateTaskAsync();

        var response = await Client.PostAsync($"/api/tasks/{taskId}/archive", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var active = await (await Client.GetAsync("/api/tasks"))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var all = await (await Client.GetAsync("/api/tasks?includeArchived=true"))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(0, active.GetArrayLength());
        Assert.Equal(1, all.GetArrayLength());
    }

    [Fact]
    public async Task 進行中のセッションがあるタスクはアーカイブできない()
    {
        var taskId = await CreateTaskAsync();
        await StartSessionAsync(taskId);

        var response = await Client.PostAsync($"/api/tasks/{taskId}/archive", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task アーカイブを解除できる()
    {
        var taskId = await CreateTaskAsync();
        await Client.PostAsync($"/api/tasks/{taskId}/archive", null);

        var response = await Client.PostAsync($"/api/tasks/{taskId}/unarchive", null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.GetProperty("isArchived").GetBoolean());
    }

    /// <summary>
    /// 一覧の射影が働き、セッション数と最終利用時刻が入ること。
    /// </summary>
    [Fact]
    public async Task 一覧にセッション数と最終利用時刻が入る()
    {
        var used = await CreateTaskAsync("使用済み");
        var unused = await CreateTaskAsync("未使用");
        var sessionId = await StartSessionAsync(used);
        await Client.PostAsJsonAsync($"/api/work-sessions/{sessionId}/finish", ResultPayload(), Json);

        var body = await (await Client.GetAsync("/api/tasks"))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        var usedItem = body.EnumerateArray().Single(x => x.GetProperty("id").GetInt64() == used);
        var unusedItem = body.EnumerateArray().Single(x => x.GetProperty("id").GetInt64() == unused);

        Assert.Equal(1, usedItem.GetProperty("sessionCount").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, usedItem.GetProperty("lastUsedAt").ValueKind);
        Assert.Equal("設計", usedItem.GetProperty("defaultWorkTypeName").GetString());

        Assert.Equal(0, unusedItem.GetProperty("sessionCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, unusedItem.GetProperty("lastUsedAt").ValueKind);
    }

    /// <summary>Recent では未使用のタスクが後ろに来る。</summary>
    [Fact]
    public async Task 直近使用順では未使用タスクが後ろに来る()
    {
        var unused = await CreateTaskAsync("未使用");
        var used = await CreateTaskAsync("使用済み");
        var sessionId = await StartSessionAsync(used);
        await Client.PostAsJsonAsync($"/api/work-sessions/{sessionId}/finish", ResultPayload(), Json);

        var body = await (await Client.GetAsync("/api/tasks?sort=recent"))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(used, body[0].GetProperty("id").GetInt64());
        Assert.Equal(unused, body[1].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task 並び順の指定が不正なら400になる()
    {
        var response = await Client.GetAsync("/api/tasks?sort=invalid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task キーワードで絞り込める()
    {
        await CreateTaskAsync("認証方式の検討");
        await CreateTaskAsync("別の作業");

        var body = await (await Client.GetAsync("/api/tasks?keyword=認証"))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal("認証方式の検討", body[0].GetProperty("title").GetString());
    }
}
