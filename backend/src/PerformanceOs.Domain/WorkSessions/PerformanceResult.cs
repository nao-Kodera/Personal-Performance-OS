using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.ValueObjects;

namespace PerformanceOs.Domain.WorkSessions;

/// <summary>
/// <see cref="PerformanceResult"/> を記録・訂正するための入力値。
/// </summary>
public readonly record struct PerformanceResultInput(
    Rating FocusLevel,
    Rating OutputLevel,
    Rating AccuracyLevel,
    Rating SatisfactionLevel,
    Rating FatigueAfter,
    string? Note);

/// <summary>
/// 作業セッションに対する成果の評価。分析の目的変数となる。
/// </summary>
/// <remarks>
/// <para>
/// 5 指標すべてが必須である。1 つでも欠けると分析の一貫性が崩れる。
/// 評価尺度は全期間で固定する（docs/01-product-requirements.md §5.1）。
/// </para>
/// <para>
/// <see cref="PreWorkState"/> と違い編集を許す。成果は目的変数であり、
/// これを編集しても「説明変数を結果に合わせて捏造する」問題は起きないため。
/// ただし <see cref="RecordedAt"/> は変更せず、<see cref="UpdatedAt"/> との差で
/// 事後編集されたレコードを識別できるようにする（PR-2）。
/// </para>
/// <para>
/// <b>合成指標（総合スコア等）を持たない（PR-4）。</b>5 指標を合成すると、
/// どの要素が効いているか分からなくなり、条件の発見という目的に対して
/// 情報を失わせる（docs/02-glossary.md §6）。
/// </para>
/// </remarks>
public sealed class PerformanceResult : Entity
{
    public const int MaxNoteLength = 2000;

    /// <summary>EF Core 用。</summary>
    private PerformanceResult()
    {
    }

    internal PerformanceResult(PerformanceResultInput input, DateTimeOffset recordedAt)
    {
        Apply(input);
        RecordedAt = recordedAt;
        UpdatedAt = recordedAt;
    }

    public long WorkSessionId { get; private set; }

    /// <summary>集中度。分析 A-01 / A-03 の指標。</summary>
    public Rating FocusLevel { get; private set; }

    /// <summary>成果度。分析 A-02 / A-04 / A-05 の指標。</summary>
    public Rating OutputLevel { get; private set; }

    public Rating AccuracyLevel { get; private set; }

    public Rating SatisfactionLevel { get; private set; }

    /// <summary>終了時疲労度。<b>高いほど悪い</b>。</summary>
    public Rating FatigueAfter { get; private set; }

    public string? Note { get; private set; }

    /// <summary>初回記録時刻。訂正しても変更しない（PR-2）。</summary>
    public DateTimeOffset RecordedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// <see cref="RecordedAt"/> と <see cref="UpdatedAt"/> が異なる、
    /// すなわち事後に訂正されたか。
    /// </summary>
    public bool IsEdited => UpdatedAt != RecordedAt;

    internal void Update(PerformanceResultInput input, DateTimeOffset now)
    {
        Apply(input);
        UpdatedAt = now;
    }

    private void Apply(PerformanceResultInput input)
    {
        FocusLevel = input.FocusLevel;
        OutputLevel = input.OutputLevel;
        AccuracyLevel = input.AccuracyLevel;
        SatisfactionLevel = input.SatisfactionLevel;
        FatigueAfter = input.FatigueAfter;
        Note = NormalizeNote(input.Note);
    }

    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var trimmed = note.Trim();

        if (trimmed.Length > MaxNoteLength)
        {
            throw new DomainException(
                $"成果のメモは {MaxNoteLength} 文字以内である必要があります: {trimmed.Length} 文字");
        }

        return trimmed;
    }
}
