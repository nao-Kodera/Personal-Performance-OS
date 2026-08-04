using PerformanceOs.Domain.Common;

namespace PerformanceOs.Domain.WorkTypes;

/// <summary>
/// 作業タイプ。分析の主要な軸となるマスタ（docs/02-glossary.md §1）。
/// </summary>
/// <remarks>
/// <para>
/// <b>削除しない。</b>削除すると過去の WorkSession の分類が失われ、
/// 分析 A-01 / A-02 が破綻する。使わなくなったら <see cref="Deactivate"/> する。
/// </para>
/// <para>
/// <b>分類を細かくしすぎないこと。</b>区分が増えるとカテゴリあたりのサンプル数が
/// 減り、最小サンプル数 5 件を満たせなくなる。7 種以上に増やさない
/// （docs/04-analytics-spec.md §3.2）。
/// </para>
/// <para>
/// 名称の一意性（WT-2）は集約をまたぐ制約のため、ここでは検査できない。
/// アプリケーション層と DB の一意インデックスで担保する
/// （docs/05-domain-design.md §8）。
/// </para>
/// </remarks>
public sealed class WorkType : Entity
{
    public const int MaxNameLength = 50;

    /// <summary>EF Core 用。</summary>
    private WorkType()
    {
        Name = string.Empty;
    }

    private WorkType(string name, int displayOrder, DateTimeOffset now)
    {
        Name = NormalizeName(name);
        DisplayOrder = displayOrder;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Name { get; private set; }

    /// <summary>表示順。昇順に並べる。</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>
    /// 有効フラグ。false でも既存の WorkSession の分類は変わらず、
    /// 新規の選択肢から外れるだけである。
    /// </summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static WorkType Create(string name, int displayOrder, DateTimeOffset now)
        => new(name, displayOrder, now);

    /// <summary>
    /// 名称を変更する。ID が同一であれば分析の連続性は保たれる。
    /// </summary>
    /// <remarks>
    /// ただし<b>意味を変える改名</b>（「調査」→「レビュー」）は過去データの分類を
    /// 汚染する。その場合は新規作成して旧を無効化すること
    /// （docs/05-domain-design.md §4.1）。この判断はユーザーに委ねる。
    /// </remarks>
    public void Rename(string name, DateTimeOffset now)
    {
        var normalized = NormalizeName(name);
        if (normalized == Name)
        {
            return;
        }

        Name = normalized;
        UpdatedAt = now;
    }

    public void ChangeDisplayOrder(int displayOrder, DateTimeOffset now)
    {
        if (displayOrder == DisplayOrder)
        {
            return;
        }

        DisplayOrder = displayOrder;
        UpdatedAt = now;
    }

    /// <summary>無効化する。新規の選択肢から外れる。既に無効なら何もしない。</summary>
    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>再度有効にする。既に有効なら何もしない。</summary>
    public void Activate(DateTimeOffset now)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        UpdatedAt = now;
    }

    private static string NormalizeName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new DomainException("作業タイプの名称は空にできません。");
        }

        if (trimmed.Length > MaxNameLength)
        {
            throw new DomainException(
                $"作業タイプの名称は {MaxNameLength} 文字以内である必要があります: {trimmed.Length} 文字");
        }

        return trimmed;
    }
}
