using PerformanceOs.Api.Contracts.Responses;
using PerformanceOs.Application.TaskItems;
using PerformanceOs.Domain.TaskItems;
using PerformanceOs.Domain.WorkTypes;

namespace PerformanceOs.Api.Mapping;

/// <summary>
/// ドメイン／アプリケーションの型からレスポンス DTO への変換。
/// </summary>
/// <remarks>
/// 手書きで明示的に書く。マッピングライブラリを使うと、API の変更が
/// どこに影響するかを追えなくなる（docs/08-technical-design.md §0）。
/// </remarks>
public static class ContractMappings
{
    public static WorkTypeResponse ToResponse(this WorkType workType)
        => new(workType.Id, workType.Name, workType.DisplayOrder, workType.IsActive);

    public static IReadOnlyList<WorkTypeResponse> ToResponses(
        this IReadOnlyList<WorkType> workTypes)
        => workTypes.Select(ToResponse).ToList();

    public static TaskItemResponse ToResponse(this TaskItemSummary summary)
        => new(
            summary.Id,
            summary.Title,
            summary.DefaultWorkTypeId,
            summary.DefaultWorkTypeName,
            summary.Note,
            summary.IsArchived,
            summary.LastUsedAt,
            summary.SessionCount,
            summary.CreatedAt,
            summary.UpdatedAt);

    public static IReadOnlyList<TaskItemResponse> ToResponses(
        this IReadOnlyList<TaskItemSummary> summaries)
        => summaries.Select(ToResponse).ToList();

    public static TaskItemDetailResponse ToDetailResponse(this TaskItem taskItem)
        => new(
            taskItem.Id,
            taskItem.Title,
            taskItem.DefaultWorkTypeId,
            taskItem.Note,
            taskItem.IsArchived,
            taskItem.CreatedAt,
            taskItem.UpdatedAt);
}
