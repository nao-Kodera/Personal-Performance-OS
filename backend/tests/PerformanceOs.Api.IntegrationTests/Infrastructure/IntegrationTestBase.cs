using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PerformanceOs.Api.IntegrationTests.Infrastructure;

/// <summary>
/// 統合テストの共通処理。各テストの前にデータを初期化する。
/// </summary>
[Collection(ApiCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    /// <summary>初期データの作業タイプ「設計」。</summary>
    protected const long DesignWorkTypeId = 2;

    /// <summary>初期データの作業タイプ「実装」。</summary>
    protected const long ImplementationWorkTypeId = 1;

    protected IntegrationTestBase(ApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected ApiFactory Factory { get; }

    protected HttpClient Client { get; }

    protected static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task InitializeAsync() => Factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ------------------------------------------------------------------
    // 準備用のヘルパー
    // ------------------------------------------------------------------

    protected async Task<long> CreateTaskAsync(
        string title = "検証タスク", long workTypeId = DesignWorkTypeId)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/tasks",
            new { title, defaultWorkTypeId = workTypeId },
            Json);

        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        return created.GetProperty("id").GetInt64();
    }

    protected static object StartPayload(
        long taskItemId,
        long workTypeId = DesignWorkTypeId,
        int fatigueLevel = 2,
        string workLocation = "Home",
        string? locationNote = null)
        => new
        {
            taskItemId,
            workTypeId,
            preWorkState = new { fatigueLevel, expectedFocusLevel = 4, moodLevel = 4 },
            workContext = new
            {
                workLocation,
                locationNote,
                meetingCount = 1,
                interruptionExpected = false,
            },
        };

    protected static object ResultPayload(
        int interruptionCount = 1,
        int focusLevel = 4,
        int fatigueAfter = 4)
        => new
        {
            interruptionCount,
            result = new
            {
                focusLevel,
                outputLevel = 4,
                accuracyLevel = 3,
                satisfactionLevel = 4,
                fatigueAfter,
                note = (string?)null,
            },
        };

    protected async Task<long> StartSessionAsync(long taskItemId)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/work-sessions/start", StartPayload(taskItemId), Json);

        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        return created.GetProperty("id").GetInt64();
    }
}
