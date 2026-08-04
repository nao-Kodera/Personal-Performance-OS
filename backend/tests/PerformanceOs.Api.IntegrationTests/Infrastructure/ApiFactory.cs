using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PerformanceOs.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PerformanceOs.Api.IntegrationTests.Infrastructure;

/// <summary>
/// 実際の PostgreSQL コンテナ上で API を起動するテスト基盤。
/// </summary>
/// <remarks>
/// <para>
/// インメモリ DB を使わない。本プロダクトの不変条件のうち重要なものは
/// DB 制約で担保されており（部分一意インデックス・CHECK 制約）、
/// インメモリ実装ではそれらが検証できないため
/// （docs/06-database-design.md §3、docs/08-technical-design.md §6.1）。
/// </para>
/// <para>
/// コンテナはテスト全体で 1 つだけ起動し、テストごとにデータを初期化する。
/// </para>
/// </remarks>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // 本番と同じ PostgreSQL 16 を使う（docs/08-technical-design.md §1.3）。
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("performance_os_test")
        .WithUsername("performance_os")
        .WithPassword("test_password")
        // コンテナのタイムゾーンを UTC に固定する。JST だと日付境界の
        // 扱いを誤った実装がテストで露見しなくなる
        // （docs/08-technical-design.md §4）。
        .WithEnvironment("TZ", "UTC")
        .Build();

    private string ConnectionString => _container.GetConnectionString();

    // WebApplicationFactory が IAsyncDisposable.DisposeAsync（ValueTask）を
    // 持つため、xUnit の IAsyncLifetime（Task）とはシグネチャが衝突する。
    // 明示的実装で分離する。
    async Task IAsyncLifetime.InitializeAsync()
    {
        await _container.StartAsync();

        // マイグレーションを明示的に適用する。アプリ側では自動適用しない
        // （docs/08-technical-design.md §4）。
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
    }

    public PerformanceOsDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<PerformanceOsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    /// <summary>
    /// 記録データを消して初期状態に戻す。work_types の初期データ 6 件は残す。
    /// </summary>
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            TRUNCATE work_sessions, task_items RESTART IDENTITY CASCADE;
            DELETE FROM work_types WHERE id > 6;
            """;

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// アプリケーション層を迂回して生 SQL を実行する。
    /// DB 制約そのものの実効性を確認するために使う。
    /// </summary>
    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 生 SQL を 1 行 1 値で読む。整合性検証クエリ（docs/06-database-design.md §7）に使う。
    /// </summary>
    public async Task<long> CountAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var result = await command.ExecuteScalarAsync();

        return Convert.ToInt64(result);
    }
}

/// <summary>コンテナの起動を 1 回に抑えるためのコレクション定義。</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
