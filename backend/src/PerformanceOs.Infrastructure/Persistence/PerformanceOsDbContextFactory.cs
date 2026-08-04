using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PerformanceOs.Infrastructure.Persistence;

/// <summary>
/// <c>dotnet ef</c> がマイグレーションを生成・適用する際に使う設計時ファクトリ。
/// </summary>
/// <remarks>
/// <para>
/// これを置くことで、Api の Program.cs に DbContext を登録していない段階でも
/// マイグレーションを操作できる。DI 登録は T-08 で行う。
/// </para>
/// <para>
/// 接続先は環境変数 <c>PERFORMANCE_OS_CONNECTION</c> で上書きできる。
/// 既定値は docker-compose.yml の db サービス。
/// <b>本番の接続文字列をここに書かないこと。</b>
/// </para>
/// </remarks>
public sealed class PerformanceOsDbContextFactory : IDesignTimeDbContextFactory<PerformanceOsDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=performance_os;Username=performance_os;Password=dev_password";

    public PerformanceOsDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("PERFORMANCE_OS_CONNECTION")
            ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<PerformanceOsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PerformanceOsDbContext(options);
    }
}
