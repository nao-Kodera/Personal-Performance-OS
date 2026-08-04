using System.ComponentModel.DataAnnotations;
using PerformanceOs.Domain.TaskItems;

namespace PerformanceOs.Api.Contracts.Requests;

/// <summary>
/// docs/07-api-design.md §2.5 / §2.6
/// </summary>
/// <remarks>
/// 完了フラグ・期限・優先度・タグ・親子関係を追加しないこと
/// （docs/08-technical-design.md §8 の禁止事項 2）。
/// </remarks>
public sealed class SaveTaskItemRequest
{
    [Required(ErrorMessage = "タイトルは必須です。")]
    [StringLength(TaskItem.MaxTitleLength, MinimumLength = 1,
        ErrorMessage = "タイトルは 1〜200 文字で指定してください。")]
    public string Title { get; init; } = string.Empty;

    [Required(ErrorMessage = "既定の作業タイプは必須です。")]
    [Range(1, long.MaxValue, ErrorMessage = "既定の作業タイプの指定が不正です。")]
    public long? DefaultWorkTypeId { get; init; }

    [StringLength(TaskItem.MaxNoteLength,
        ErrorMessage = "メモは 2000 文字以内で指定してください。")]
    public string? Note { get; init; }
}
