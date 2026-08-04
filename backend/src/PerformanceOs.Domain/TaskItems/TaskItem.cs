using PerformanceOs.Domain.Common;

namespace PerformanceOs.Domain.TaskItems;

/// <summary>
/// 観測対象となる作業の単位。「何をするか」を表す（docs/02-glossary.md §1）。
/// </summary>
/// <remarks>
/// <para>
/// <b>TaskItem は「完了」しない。</b>完了状態・期限・優先度・タグ・親子関係を
/// 持たせないこと。これらを追加すると、タスクの消化を目的とする一般的な
/// タスク管理アプリに性格が変わる
/// （docs/01-product-requirements.md §12、docs/08-technical-design.md §8 の禁止事項 2）。
/// </para>
/// <para>
/// 使わなくなったものは <see cref="Archive"/> する。これは完了ではなく
/// <b>選択肢からの除外</b>である。アーカイブ済みでも過去の WorkSession からは
/// 参照され続ける。
/// </para>
/// <para>
/// タイトルの重複を許す（TI-3）。同じ名前の作業を別の機会に行うことは正常である。
/// </para>
/// <para>
/// <see cref="DefaultWorkTypeId"/> は作業開始時の入力を減らすための既定値である。
/// 分析には WorkSession が持つ実績値を使う（docs/02-glossary.md §1）。
/// </para>
/// </remarks>
public sealed class TaskItem : Entity
{
    public const int MaxTitleLength = 200;
    public const int MaxNoteLength = 2000;

    /// <summary>EF Core 用。</summary>
    private TaskItem()
    {
        Title = string.Empty;
    }

    private TaskItem(string title, long defaultWorkTypeId, string? note, DateTimeOffset now)
    {
        Title = NormalizeTitle(title);
        DefaultWorkTypeId = defaultWorkTypeId;
        Note = NormalizeNote(note);
        IsArchived = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Title { get; private set; }

    /// <summary>
    /// 作業開始時に自動選択される既定の作業タイプ。
    /// 実在する有効な WorkType を指すことの検査は、集約をまたぐためアプリケーション層で行う。
    /// </summary>
    public long DefaultWorkTypeId { get; private set; }

    public string? Note { get; private set; }

    /// <summary>アーカイブ済みか。<b>完了フラグではない。</b></summary>
    public bool IsArchived { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static TaskItem Create(string title, long defaultWorkTypeId, string? note, DateTimeOffset now)
        => new(title, defaultWorkTypeId, note, now);

    public void Update(string title, long defaultWorkTypeId, string? note, DateTimeOffset now)
    {
        var normalizedTitle = NormalizeTitle(title);
        var normalizedNote = NormalizeNote(note);

        if (normalizedTitle == Title
            && defaultWorkTypeId == DefaultWorkTypeId
            && normalizedNote == Note)
        {
            return;
        }

        Title = normalizedTitle;
        DefaultWorkTypeId = defaultWorkTypeId;
        Note = normalizedNote;
        UpdatedAt = now;
    }

    /// <summary>
    /// アーカイブする。作業開始画面の選択肢から外れる。既にアーカイブ済みなら何もしない。
    /// </summary>
    /// <remarks>
    /// 進行中の WorkSession を持つ TaskItem をアーカイブできないという制約は、
    /// 集約をまたぐためアプリケーション層で担保する（docs/07-api-design.md §2.7）。
    /// </remarks>
    public void Archive(DateTimeOffset now)
    {
        if (IsArchived)
        {
            return;
        }

        IsArchived = true;
        UpdatedAt = now;
    }

    public void Unarchive(DateTimeOffset now)
    {
        if (!IsArchived)
        {
            return;
        }

        IsArchived = false;
        UpdatedAt = now;
    }

    private static string NormalizeTitle(string title)
    {
        var trimmed = title?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new DomainException("タスクのタイトルは空にできません。");
        }

        if (trimmed.Length > MaxTitleLength)
        {
            throw new DomainException(
                $"タスクのタイトルは {MaxTitleLength} 文字以内である必要があります: {trimmed.Length} 文字");
        }

        return trimmed;
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
                $"タスクのメモは {MaxNoteLength} 文字以内である必要があります: {trimmed.Length} 文字");
        }

        return trimmed;
    }
}
