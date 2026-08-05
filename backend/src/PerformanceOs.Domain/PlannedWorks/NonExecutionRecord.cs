using PerformanceOs.Domain.Common;

namespace PerformanceOs.Domain.PlannedWorks;

/// <summary>
/// 予定を実行しなかったという記録（<see cref="PlannedWork"/> 集約内）。
/// </summary>
/// <remarks>
/// <para>
/// <b>これを記録しないと実行率は永久に算出できない。</b>後から遡って作ることが
/// できない唯一のデータである（docs/01-product-requirements.md §2 P4）。
/// </para>
/// <para>
/// 「やらなかった」は未完了の負債ではなく、記録すべき事実である。削除しない。
/// </para>
/// </remarks>
public sealed class NonExecutionRecord : Entity
{
    public const int MaxNoteLength = 1000;

    /// <summary>EF Core 用。</summary>
    private NonExecutionRecord()
    {
    }

    internal NonExecutionRecord(NonExecutionReason reason, string? note, DateTimeOffset recordedAt)
    {
        Apply(reason, note);
        RecordedAt = recordedAt;
        UpdatedAt = recordedAt;
    }

    public long PlannedWorkId { get; private set; }

    /// <summary>理由（NE-2 により必須）。</summary>
    public NonExecutionReason Reason { get; private set; }

    public string? Note { get; private set; }

    /// <summary>初回記録時刻。訂正しても変更しない（NE-3）。</summary>
    public DateTimeOffset RecordedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal void Update(NonExecutionReason reason, string? note, DateTimeOffset now)
    {
        Apply(reason, note);
        UpdatedAt = now;
    }

    private void Apply(NonExecutionReason reason, string? note)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new DomainException($"未実行の理由が不正です: {reason}");
        }

        Reason = reason;
        Note = NormalizeNote(note);
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
                $"未実行のメモは {MaxNoteLength} 文字以内である必要があります: {trimmed.Length} 文字");
        }

        return trimmed;
    }
}
