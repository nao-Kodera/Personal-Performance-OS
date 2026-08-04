using Microsoft.EntityFrameworkCore;
using Npgsql;
using PerformanceOs.Api.Middleware;
using PerformanceOs.Application.Common;
using PerformanceOs.Domain.Common;

namespace PerformanceOs.Api.IntegrationTests.Middleware;

/// <summary>
/// 例外 → HTTP ステータスの変換（docs/08-technical-design.md §3.8）。
/// </summary>
/// <remarks>
/// 純粋な写像であり DB を必要としないため、単体テストとして書く。
/// </remarks>
public class ExceptionProblemMapperTests
{
    [Fact]
    public void 見つからない例外は404になる()
    {
        var problem = ExceptionProblemMapper.Map(NotFoundException.For("タスク", 12));

        Assert.Equal(404, problem.Status);
        Assert.Equal(ProblemTypes.NotFound, problem.Type);
        Assert.Contains("タスク", problem.Detail);
        Assert.False(problem.IsUnexpected);
    }

    [Fact]
    public void 競合例外は409になる()
    {
        var problem = ExceptionProblemMapper.Map(
            new ConflictException("進行中の作業セッションが既に存在します。"));

        Assert.Equal(409, problem.Status);
        Assert.Equal(ProblemTypes.Conflict, problem.Type);
        Assert.Equal("進行中の作業セッションが既に存在します。", problem.Detail);
    }

    [Fact]
    public void ドメイン規則違反は422になる()
    {
        var problem = ExceptionProblemMapper.Map(new DomainRuleException("アーカイブ済みです"));

        Assert.Equal(422, problem.Status);
        Assert.Equal(ProblemTypes.DomainRule, problem.Type);
    }

    /// <summary>
    /// ドメイン層の例外もアプリケーション層と同じ 422 に写像する。
    /// アプリケーション層の検証をすり抜けた値であり、利用者にとっては同じ意味。
    /// </summary>
    [Fact]
    public void ドメイン層の例外も422になる()
    {
        var problem = ExceptionProblemMapper.Map(new DomainException("評価値は 1〜5 の範囲です"));

        Assert.Equal(422, problem.Status);
        Assert.Equal(ProblemTypes.DomainRule, problem.Type);
        Assert.Contains("1〜5", problem.Detail);
    }

    [Fact]
    public void 未知の例外は500になり詳細を返さない()
    {
        var problem = ExceptionProblemMapper.Map(
            new InvalidOperationException("内部の実装詳細が含まれるメッセージ"));

        Assert.Equal(500, problem.Status);
        Assert.Equal(ProblemTypes.Internal, problem.Type);
        Assert.DoesNotContain("実装詳細", problem.Detail);
        Assert.True(problem.IsUnexpected);
    }

    // ------------------------------------------------------------------
    // DB 由来の例外
    // ------------------------------------------------------------------

    private static PostgresException Postgres(string sqlState, string? constraintName)
        => new(
            messageText: "violation",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: sqlState,
            constraintName: constraintName);

    /// <summary>
    /// docs/08-technical-design.md §3.6。
    /// 進行中セッションの同時実行禁止（WS-9）は、並行リクエストではこの経路で
    /// 弾かれる。アプリケーション層の事前チェックだけでは防げない。
    /// </summary>
    [Fact]
    public void 進行中セッションの一意制約違反は409になる()
    {
        var exception = new DbUpdateException(
            "update failed",
            Postgres("23505", "uq_work_sessions_single_active"));

        var problem = ExceptionProblemMapper.Map(exception);

        Assert.Equal(409, problem.Status);
        Assert.Equal(ProblemTypes.Conflict, problem.Type);
        Assert.Contains("進行中", problem.Detail);
        Assert.False(problem.IsUnexpected);
    }

    [Theory]
    [InlineData("uq_work_types_name", "作業タイプ")]
    [InlineData("uq_work_sessions_planned_work", "予定")]
    [InlineData("uq_daily_conditions_date", "その日")]
    public void 制約名ごとに説明が変わる(string constraintName, string expectedFragment)
    {
        var problem = ExceptionProblemMapper.Map(
            new DbUpdateException("update failed", Postgres("23505", constraintName)));

        Assert.Equal(409, problem.Status);
        Assert.Contains(expectedFragment, problem.Detail);
    }

    [Fact]
    public void 未知の制約名でも409になる()
    {
        var problem = ExceptionProblemMapper.Map(
            new DbUpdateException("update failed", Postgres("23505", "uq_unknown")));

        Assert.Equal(409, problem.Status);
    }

    /// <summary>
    /// CHECK 制約違反は 400 ではなく 500。アプリケーション層とドメイン層の
    /// 検証をすり抜けており、到達した時点でバグであるため
    /// （docs/08-technical-design.md §3.8）。
    /// </summary>
    [Fact]
    public void CHECK制約違反は500になる()
    {
        var problem = ExceptionProblemMapper.Map(
            new DbUpdateException("update failed", Postgres("23514", "ck_pre_work_states_fatigue")));

        Assert.Equal(500, problem.Status);
        Assert.Equal(ProblemTypes.Internal, problem.Type);
        Assert.True(problem.IsUnexpected);
    }

    [Fact]
    public void 想定していないSqlStateは500になる()
    {
        var problem = ExceptionProblemMapper.Map(
            new DbUpdateException("update failed", Postgres("23503", "fk_work_sessions_task_item")));

        Assert.Equal(500, problem.Status);
        Assert.True(problem.IsUnexpected);
    }

    /// <summary>
    /// EF Core は PostgresException を DbUpdateException で包む。
    /// さらに入れ子になっていても展開できること。
    /// </summary>
    [Fact]
    public void 入れ子になった例外からも取り出せる()
    {
        var exception = new InvalidOperationException(
            "outer",
            new DbUpdateException("update failed", Postgres("23505", "uq_work_types_name")));

        var problem = ExceptionProblemMapper.Map(exception);

        Assert.Equal(409, problem.Status);
    }
}
