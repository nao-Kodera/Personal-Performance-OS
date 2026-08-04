using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.ValueObjects;

namespace PerformanceOs.Domain.WorkSessions;

/// <summary>
/// <see cref="PreWorkState"/> を記録するための入力値。
/// </summary>
/// <remarks>
/// 記録時刻はここに含めない。集約ルートが開始時刻と同一の値を設定することで
/// PS-3 を保証する（docs/05-domain-design.md §4.7）。
/// </remarks>
public readonly record struct PreWorkStateInput(
    Rating FatigueLevel,
    Rating ExpectedFocusLevel,
    Rating MoodLevel);

/// <summary>
/// 作業開始の直前に記録される、本人の状態。説明変数となる。
/// </summary>
/// <remarks>
/// <para>
/// <b>このクラスは生成後に変更できない（PS-2）。これは本プロダクトで最も重要な
/// 不変条件である。</b>
/// </para>
/// <para>
/// 作業前状態を事後に編集できると、次が起きる。
/// 悪い結果が出た → 「あの日は疲れていた」と後付けで疲労度を上げる →
/// 「疲労が高いと成果が下がる」という相関が人工的に作られる →
/// 分析結果が自分の思い込みの写像になる。
/// </para>
/// <para>
/// これは本プロダクトの目的を完全に破壊する。したがって
/// <b>setter も更新メソッドも持たせない。</b>更新用の API も用意しない
/// （docs/05-domain-design.md §4.7、docs/07-api-design.md §1.1）。
/// この制約はリフレクションによるテストで固定している。
/// </para>
/// </remarks>
public sealed class PreWorkState : Entity
{
    /// <summary>EF Core 用。</summary>
    private PreWorkState()
    {
    }

    internal PreWorkState(PreWorkStateInput input, DateTimeOffset recordedAt)
    {
        FatigueLevel = input.FatigueLevel;
        ExpectedFocusLevel = input.ExpectedFocusLevel;
        MoodLevel = input.MoodLevel;
        RecordedAt = recordedAt;
    }

    public long WorkSessionId { get; private set; }

    /// <summary>疲労度。<b>高いほど悪い</b>（docs/02-glossary.md §2.2）。</summary>
    public Rating FatigueLevel { get; private set; }

    /// <summary>作業前に感じた「集中できそう」の度合い。</summary>
    public Rating ExpectedFocusLevel { get; private set; }

    public Rating MoodLevel { get; private set; }

    /// <summary>記録時刻。集約ルートの開始時刻と同一である（PS-3）。</summary>
    public DateTimeOffset RecordedAt { get; private set; }
}
