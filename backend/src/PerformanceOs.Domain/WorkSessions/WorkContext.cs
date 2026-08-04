using PerformanceOs.Domain.Common;

namespace PerformanceOs.Domain.WorkSessions;

/// <summary>
/// <see cref="WorkContext"/> を記録するための入力値。
/// </summary>
public readonly record struct WorkContextInput(
    WorkLocation WorkLocation,
    string? LocationNote,
    int MeetingCount,
    bool InterruptionExpected);

/// <summary>
/// 作業が行われる外部環境の情報。自分の外側にある条件を観測する。
/// </summary>
/// <remarks>
/// <para>
/// <b>このクラスは生成後に変更できない（WC-3）。</b>理由は
/// <see cref="PreWorkState"/> と同じである。
/// </para>
/// <para>
/// PreWorkState（内的状態）と分けているのは、docs/01-product-requirements.md §4 の
/// 4 層分離を構造として保持するためである。両者は同時に記録されるが、
/// 概念的に別であり、今後増える項目の性質も異なる。環境側は追加候補が多く
/// （騒音・天気・同席者・デバイス）、分離しておくと変更の影響範囲が限定される
/// （docs/05-domain-design.md §4.8）。
/// </para>
/// </remarks>
public sealed class WorkContext : Entity
{
    public const int MaxLocationNoteLength = 200;

    /// <summary>EF Core 用。</summary>
    private WorkContext()
    {
    }

    internal WorkContext(WorkContextInput input, DateTimeOffset recordedAt)
    {
        // WC-1
        if (input.MeetingCount < 0)
        {
            throw new DomainException($"会議件数は 0 以上である必要があります: {input.MeetingCount}");
        }

        WorkLocation = input.WorkLocation;
        LocationNote = NormalizeLocationNote(input.WorkLocation, input.LocationNote);
        MeetingCount = input.MeetingCount;
        InterruptionExpected = input.InterruptionExpected;
        RecordedAt = recordedAt;
    }

    public long WorkSessionId { get; private set; }

    public WorkLocation WorkLocation { get; private set; }

    /// <summary><see cref="WorkLocation.Other"/> のときのみ設定できる（WC-2）。</summary>
    public string? LocationNote { get; private set; }

    /// <summary>その日の会議件数。</summary>
    public int MeetingCount { get; private set; }

    /// <summary>割り込みが予想されるか。</summary>
    public bool InterruptionExpected { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    private static string? NormalizeLocationNote(WorkLocation location, string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        // WC-2
        if (location != WorkLocation.Other)
        {
            throw new DomainException(
                $"場所の補足は作業場所が {nameof(WorkLocation.Other)} のときのみ設定できます: {location}");
        }

        var trimmed = note.Trim();

        if (trimmed.Length > MaxLocationNoteLength)
        {
            throw new DomainException(
                $"場所の補足は {MaxLocationNoteLength} 文字以内である必要があります: {trimmed.Length} 文字");
        }

        return trimmed;
    }
}
