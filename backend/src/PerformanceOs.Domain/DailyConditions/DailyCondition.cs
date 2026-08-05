using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.Time;
using PerformanceOs.Domain.ValueObjects;

namespace PerformanceOs.Domain.DailyConditions;

/// <summary>
/// <see cref="DailyCondition"/> を記録・訂正するための入力値。
/// </summary>
/// <remarks>
/// 対象日を含めない。対象日は記録後に変更できないため、訂正の入力に現れない
/// （docs/05-domain-design.md §4.3 の Update のシグネチャ）。
/// </remarks>
public readonly record struct DailyConditionInput(
    SleepDuration Sleep,
    Rating PhysicalCondition,
    Rating MoodLevel,
    Rating StressLevel,
    string? Note);

/// <summary>
/// その日の土台となる状態。日単位の説明変数となる（docs/02-glossary.md §1）。
/// </summary>
/// <remarks>
/// <para>
/// 対象日は <b>JST 基準の論理日</b>である。UTC の日付ではない
/// （docs/02-glossary.md §4）。
/// </para>
/// <para>
/// <b>当日のみ記録・訂正できる（DC-4）。</b>事後の記憶に基づく記録は精度が低く、
/// 特に睡眠時間は数日経つと思い出せない。不正確なデータが入ると分析 A-05 が
/// 汚染される。<b>この判定はここでは行わない。</b>「当日」の判断には現在時刻が
/// 必要であり、Domain は時刻を自分で取らないため、アプリケーション層が担う
/// （docs/05-domain-design.md §8）。DB 制約もないため、テストで担保すること。
/// </para>
/// <para>
/// 記録し忘れた日は<b>欠損のまま</b>にする。推測で埋めない
/// （docs/01-product-requirements.md §8 原則 4）。
/// </para>
/// </remarks>
public sealed class DailyCondition : Entity
{
    public const int MaxNoteLength = 1000;

    /// <summary>EF Core 用。</summary>
    private DailyCondition()
    {
    }

    private DailyCondition(DateOnly targetDate, DailyConditionInput input, DateTimeOffset recordedAt)
    {
        TargetDate = targetDate;
        Apply(input);
        RecordedAt = recordedAt;
        UpdatedAt = recordedAt;
    }

    /// <summary>
    /// 対象日（JST 基準の論理日）。1 日 1 件（DC-1）。
    /// </summary>
    /// <remarks>
    /// <b>変更する手段を作らないこと。</b>記録後に対象日を移せると、当日のみ
    /// 記録可という制約（DC-4）を迂回して過去日の記録を作れてしまう。
    /// </remarks>
    public DateOnly TargetDate { get; private set; }

    /// <summary>睡眠時間。15 分単位（DC-2）。</summary>
    public SleepDuration Sleep { get; private set; }

    public Rating PhysicalCondition { get; private set; }

    public Rating MoodLevel { get; private set; }

    /// <summary>ストレス。<b>高いほど悪い</b>（docs/02-glossary.md §2.2）。</summary>
    public Rating StressLevel { get; private set; }

    public string? Note { get; private set; }

    /// <summary>初回記録時刻。訂正しても変更しない（DC-5）。</summary>
    public DateTimeOffset RecordedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>分析 A-05 で使う睡眠時間区分。保存せず都度導出する。</summary>
    public SleepBand SleepBand => Sleep.ToBand();

    /// <summary>
    /// その日のコンディションを記録する。
    /// </summary>
    /// <remarks>
    /// <paramref name="targetDate"/> が当日であることの確認は呼び出し側で行う
    /// （DC-4）。時刻は引数で受け取り、ここでは取得しない。
    /// </remarks>
    public static DailyCondition Record(
        DateOnly targetDate, DailyConditionInput input, DateTimeOffset recordedAt)
        => new(targetDate, input, recordedAt);

    /// <summary>
    /// 記録済みのコンディションを訂正する。対象日は変わらない。
    /// </summary>
    public void Update(DailyConditionInput input, DateTimeOffset now)
    {
        Apply(input);
        UpdatedAt = now;
    }

    private void Apply(DailyConditionInput input)
    {
        Sleep = input.Sleep;
        PhysicalCondition = input.PhysicalCondition;
        MoodLevel = input.MoodLevel;
        StressLevel = input.StressLevel;
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
                $"日次コンディションのメモは {MaxNoteLength} 文字以内である必要があります: {trimmed.Length} 文字");
        }

        return trimmed;
    }
}
