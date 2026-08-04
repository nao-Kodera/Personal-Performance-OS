using System.ComponentModel.DataAnnotations;
using PerformanceOs.Domain.WorkTypes;

namespace PerformanceOs.Api.Contracts.Requests;

/// <summary>docs/07-api-design.md §2.2</summary>
public sealed class CreateWorkTypeRequest
{
    [Required(ErrorMessage = "名称は必須です。")]
    [StringLength(WorkType.MaxNameLength, MinimumLength = 1,
        ErrorMessage = "名称は 1〜50 文字で指定してください。")]
    public string Name { get; init; } = string.Empty;

    /// <summary>省略時は既存の最大値 + 10。</summary>
    [Range(0, int.MaxValue, ErrorMessage = "表示順は 0 以上で指定してください。")]
    public int? DisplayOrder { get; init; }
}

/// <summary>docs/07-api-design.md §2.3</summary>
public sealed class UpdateWorkTypeRequest
{
    [Required(ErrorMessage = "名称は必須です。")]
    [StringLength(WorkType.MaxNameLength, MinimumLength = 1,
        ErrorMessage = "名称は 1〜50 文字で指定してください。")]
    public string Name { get; init; } = string.Empty;

    // 必須の値型は nullable + [Required] にする。非 nullable のままだと
    // プロパティが省略されたときに既定値（0 / false）で通過してしまう。
    [Required(ErrorMessage = "表示順は必須です。")]
    [Range(0, int.MaxValue, ErrorMessage = "表示順は 0 以上で指定してください。")]
    public int? DisplayOrder { get; init; }

    [Required(ErrorMessage = "有効フラグは必須です。")]
    public bool? IsActive { get; init; }
}
