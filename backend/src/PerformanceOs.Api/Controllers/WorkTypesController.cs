using Microsoft.AspNetCore.Mvc;
using PerformanceOs.Api.Contracts.Requests;
using PerformanceOs.Api.Contracts.Responses;
using PerformanceOs.Api.Mapping;
using PerformanceOs.Application.WorkTypes;

namespace PerformanceOs.Api.Controllers;

/// <summary>
/// 作業タイプ（docs/07-api-design.md §2.1〜2.3）。
/// </summary>
/// <remarks>
/// エラー応答は <c>ExceptionHandlingMiddleware</c> が組み立てる。
/// ここで try-catch しないこと（docs/08-technical-design.md §3.8）。
/// </remarks>
[ApiController]
[Route("api/work-types")]
public sealed class WorkTypesController : ControllerBase
{
    private readonly WorkTypeService _service;

    public WorkTypesController(WorkTypeService service)
    {
        _service = service;
    }

    /// <summary>表示順の昇順で返す。</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkTypeResponse>>> GetAsync(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var workTypes = await _service.GetAsync(includeInactive, cancellationToken);

        return Ok(workTypes.ToResponses());
    }

    [HttpPost]
    public async Task<ActionResult<WorkTypeResponse>> CreateAsync(
        [FromBody] CreateWorkTypeRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(
            request.Name, request.DisplayOrder, cancellationToken);

        // 単体取得のエンドポイントを持たないため Location ヘッダは付けない
        // （docs/07-api-design.md §1 のエンドポイント一覧）。
        return StatusCode(StatusCodes.Status201Created, created.ToResponse());
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<WorkTypeResponse>> UpdateAsync(
        long id,
        [FromBody] UpdateWorkTypeRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(
            id,
            request.Name,
            request.DisplayOrder!.Value,
            request.IsActive!.Value,
            cancellationToken);

        return Ok(updated.ToResponse());
    }
}
