using Npgsql;
using PerformanceOs.Application.Common;
using PerformanceOs.Domain.Common;
using ApplicationException = PerformanceOs.Application.Common.ApplicationException;

namespace PerformanceOs.Api.Middleware;

/// <summary>
/// 例外を HTTP レスポンスの内容へ写像した結果。
/// </summary>
/// <param name="IsUnexpected">
/// 想定外の例外か。true の場合、詳細を利用者に返さずログに記録する。
/// </param>
public sealed record ProblemDescriptor(
    int Status,
    string Type,
    string Title,
    string Detail,
    bool IsUnexpected);

/// <summary>
/// 例外 → HTTP ステータスの変換（docs/08-technical-design.md §3.8）。
/// </summary>
/// <remarks>
/// <b>変換はここに一元化する。</b>コントローラで個別に try-catch して
/// 独自の ProblemDetails を組み立てないこと。
/// </remarks>
public static class ExceptionProblemMapper
{
    /// <summary>一意制約違反（23505）。</summary>
    private const string UniqueViolation = "23505";

    /// <summary>CHECK 制約違反（23514）。</summary>
    private const string CheckViolation = "23514";

    public static ProblemDescriptor Map(Exception exception) => exception switch
    {
        NotFoundException ex => new ProblemDescriptor(
            StatusCodes.Status404NotFound, ProblemTypes.NotFound,
            "リソースが見つかりません", ex.Message, false),

        ConflictException ex => new ProblemDescriptor(
            StatusCodes.Status409Conflict, ProblemTypes.Conflict,
            "現在の状態では実行できません", ex.Message, false),

        DomainRuleException ex => new ProblemDescriptor(
            StatusCodes.Status422UnprocessableEntity, ProblemTypes.DomainRule,
            "指定された値が規則に反しています", ex.Message, false),

        // ドメイン層の不変条件違反。アプリケーション層の検証をすり抜けた値。
        DomainException ex => new ProblemDescriptor(
            StatusCodes.Status422UnprocessableEntity, ProblemTypes.DomainRule,
            "指定された値が規則に反しています", ex.Message, false),

        // 将来 ApplicationException を継承した型が増えたときの受け皿。
        ApplicationException ex => new ProblemDescriptor(
            StatusCodes.Status422UnprocessableEntity, ProblemTypes.DomainRule,
            "指定された値が規則に反しています", ex.Message, false),

        _ => MapDatabaseException(exception) ?? Unexpected(exception),
    };

    /// <summary>
    /// DB 由来の例外を写像する。該当しなければ null。
    /// </summary>
    /// <remarks>
    /// EF Core は <see cref="Npgsql.PostgresException"/> を DbUpdateException で
    /// 包むため、内側を展開して調べる。
    /// </remarks>
    private static ProblemDescriptor? MapDatabaseException(Exception exception)
    {
        var postgres = FindPostgresException(exception);

        if (postgres is null)
        {
            return null;
        }

        return postgres.SqlState switch
        {
            UniqueViolation => new ProblemDescriptor(
                StatusCodes.Status409Conflict,
                ProblemTypes.Conflict,
                "現在の状態では実行できません",
                DescribeUniqueViolation(postgres.ConstraintName),
                false),

            // CHECK 制約はアプリケーション層とドメイン層の検証をすり抜けた場合に
            // のみ発火する。到達した時点でバグであるため 400 ではなく 500 とし、
            // ログに記録する（docs/08-technical-design.md §3.8）。
            CheckViolation => new ProblemDescriptor(
                StatusCodes.Status500InternalServerError,
                ProblemTypes.Internal,
                "サーバー内部エラー",
                "サーバー内部でエラーが発生しました。",
                true),

            _ => null,
        };
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }
        }

        return null;
    }

    /// <summary>
    /// 制約名から利用者向けの説明を作る。
    /// </summary>
    /// <remarks>
    /// アプリケーション層の事前チェックをすり抜けた並行リクエストで到達する。
    /// 特に <c>uq_work_sessions_single_active</c> は同時実行禁止（WS-9）の
    /// 最終的な担保であり、ここが実際に効く経路である
    /// （docs/08-technical-design.md §3.6）。
    /// </remarks>
    private static string DescribeUniqueViolation(string? constraintName) => constraintName switch
    {
        "uq_work_sessions_single_active" => "進行中の作業セッションが既に存在します。",
        "uq_work_sessions_planned_work" => "この予定には既に作業セッションが紐づいています。",
        "uq_work_types_name" => "同名の作業タイプが既に存在します。",
        "uq_daily_conditions_date" => "その日の記録は既に存在します。",
        "uq_non_execution_records_planned" => "この予定には既に未実行記録があります。",
        _ => "既に存在する記録と重複しています。",
    };

    private static ProblemDescriptor Unexpected(Exception exception) => new(
        StatusCodes.Status500InternalServerError,
        ProblemTypes.Internal,
        "サーバー内部エラー",
        // 例外メッセージを利用者に返さない。内部構造が漏れるため。
        "サーバー内部でエラーが発生しました。",
        true);
}
