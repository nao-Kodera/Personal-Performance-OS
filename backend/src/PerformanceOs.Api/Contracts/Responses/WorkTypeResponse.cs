namespace PerformanceOs.Api.Contracts.Responses;

/// <summary>docs/07-api-design.md §2.1</summary>
public sealed record WorkTypeResponse(
    long Id,
    string Name,
    int DisplayOrder,
    bool IsActive);
