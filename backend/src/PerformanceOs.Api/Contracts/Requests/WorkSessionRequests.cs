using System.ComponentModel.DataAnnotations;
using PerformanceOs.Domain.ValueObjects;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Api.Contracts.Requests;

/// <summary>作業前状態（docs/07-api-design.md §2.14）。</summary>
/// <remarks>
/// <b>この 3 項目を増やさないこと。</b>作業開始の入力は 30 秒以内に
/// 収める必要がある（docs/01-product-requirements.md §8 原則 1）。
/// </remarks>
public sealed class PreWorkStateRequest
{
    [Required(ErrorMessage = "疲労度は必須です。")]
    [Range(Rating.Min, Rating.Max, ErrorMessage = "疲労度は 1〜5 で指定してください。")]
    public int? FatigueLevel { get; init; }

    [Required(ErrorMessage = "見込み集中度は必須です。")]
    [Range(Rating.Min, Rating.Max, ErrorMessage = "見込み集中度は 1〜5 で指定してください。")]
    public int? ExpectedFocusLevel { get; init; }

    [Required(ErrorMessage = "気分は必須です。")]
    [Range(Rating.Min, Rating.Max, ErrorMessage = "気分は 1〜5 で指定してください。")]
    public int? MoodLevel { get; init; }

    public PreWorkStateInput ToInput()
        => new(
            new Rating(FatigueLevel!.Value),
            new Rating(ExpectedFocusLevel!.Value),
            new Rating(MoodLevel!.Value));
}

/// <summary>作業環境（docs/07-api-design.md §2.14）。</summary>
public sealed class WorkContextRequest
{
    [Required(ErrorMessage = "作業場所は必須です。")]
    public WorkLocation? WorkLocation { get; init; }

    /// <summary>作業場所が Other のときのみ指定できる（WC-2）。</summary>
    [StringLength(WorkContext.MaxLocationNoteLength,
        ErrorMessage = "場所の補足は 200 文字以内で指定してください。")]
    public string? LocationNote { get; init; }

    [Required(ErrorMessage = "会議件数は必須です。")]
    [Range(0, int.MaxValue, ErrorMessage = "会議件数は 0 以上で指定してください。")]
    public int? MeetingCount { get; init; }

    [Required(ErrorMessage = "割り込みの予想は必須です。")]
    public bool? InterruptionExpected { get; init; }

    public WorkContextInput ToInput()
        => new(WorkLocation!.Value, LocationNote, MeetingCount!.Value, InterruptionExpected!.Value);
}

/// <summary>
/// 作業開始（docs/07-api-design.md §2.14）。
/// </summary>
/// <remarks>
/// <para>
/// <b>startedAt を受け取らない。</b>開始時刻はサーバーが決める（WS-8）。
/// クライアントから受け取ると、記憶に基づく後付けの記録が可能になり、
/// 実作業時間の分析が事実に基づかなくなる。
/// </para>
/// <para>
/// plannedWorkId も受け取らない。検証も外部キー制約も PlannedWork（T-21）が
/// 前提のため、未対応であることが 400 で明示されるようにしている。
/// </para>
/// </remarks>
public sealed class StartWorkSessionRequest
{
    [Required(ErrorMessage = "タスクは必須です。")]
    [Range(1, long.MaxValue, ErrorMessage = "タスクの指定が不正です。")]
    public long? TaskItemId { get; init; }

    [Required(ErrorMessage = "作業タイプは必須です。")]
    [Range(1, long.MaxValue, ErrorMessage = "作業タイプの指定が不正です。")]
    public long? WorkTypeId { get; init; }

    [Required(ErrorMessage = "作業前状態は必須です。")]
    public PreWorkStateRequest? PreWorkState { get; init; }

    [Required(ErrorMessage = "作業環境は必須です。")]
    public WorkContextRequest? WorkContext { get; init; }
}

/// <summary>
/// 成果評価（docs/07-api-design.md §2.15）。
/// </summary>
/// <remarks>
/// 5 指標すべてが必須。部分的な評価を許すと、分析の母集団に NULL が混ざる（WS-3）。
/// </remarks>
public sealed class PerformanceResultRequest
{
    [Required(ErrorMessage = "集中度は必須です。")]
    [Range(Rating.Min, Rating.Max, ErrorMessage = "集中度は 1〜5 で指定してください。")]
    public int? FocusLevel { get; init; }

    [Required(ErrorMessage = "成果度は必須です。")]
    [Range(Rating.Min, Rating.Max, ErrorMessage = "成果度は 1〜5 で指定してください。")]
    public int? OutputLevel { get; init; }

    [Required(ErrorMessage = "正確性は必須です。")]
    [Range(Rating.Min, Rating.Max, ErrorMessage = "正確性は 1〜5 で指定してください。")]
    public int? AccuracyLevel { get; init; }

    [Required(ErrorMessage = "満足度は必須です。")]
    [Range(Rating.Min, Rating.Max, ErrorMessage = "満足度は 1〜5 で指定してください。")]
    public int? SatisfactionLevel { get; init; }

    [Required(ErrorMessage = "終了時疲労度は必須です。")]
    [Range(Rating.Min, Rating.Max, ErrorMessage = "終了時疲労度は 1〜5 で指定してください。")]
    public int? FatigueAfter { get; init; }

    [StringLength(PerformanceResult.MaxNoteLength,
        ErrorMessage = "メモは 2000 文字以内で指定してください。")]
    public string? Note { get; init; }

    public PerformanceResultInput ToInput()
        => new(
            new Rating(FocusLevel!.Value),
            new Rating(OutputLevel!.Value),
            new Rating(AccuracyLevel!.Value),
            new Rating(SatisfactionLevel!.Value),
            new Rating(FatigueAfter!.Value),
            Note);
}

/// <summary>
/// 作業終了と成果評価（docs/07-api-design.md §2.15 / §2.17）。
/// </summary>
/// <remarks>
/// finishedAt を受け取らない。終了時刻はサーバーが決める（WS-8）。
/// </remarks>
public sealed class SaveResultRequest
{
    [Required(ErrorMessage = "中断回数は必須です。")]
    [Range(0, int.MaxValue, ErrorMessage = "中断回数は 0 以上で指定してください。")]
    public int? InterruptionCount { get; init; }

    [Required(ErrorMessage = "成果評価は必須です。")]
    public PerformanceResultRequest? Result { get; init; }
}

/// <summary>中断終了（docs/07-api-design.md §2.16）。</summary>
public sealed class AbandonWorkSessionRequest
{
    [StringLength(WorkSession.MaxAbandonNoteLength,
        ErrorMessage = "メモは 1000 文字以内で指定してください。")]
    public string? Note { get; init; }
}
