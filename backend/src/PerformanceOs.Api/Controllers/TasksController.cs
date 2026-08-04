using Microsoft.AspNetCore.Mvc;
using PerformanceOs.Api.Contracts.Requests;
using PerformanceOs.Api.Contracts.Responses;
using PerformanceOs.Api.Mapping;
using PerformanceOs.Application.TaskItems;

namespace PerformanceOs.Api.Controllers;

/// <summary>
/// タスク（docs/07-api-design.md §2.4〜2.7）。
/// </summary>
/// <remarks>
/// <b>完了エンドポイントを作らないこと。</b>TaskItem に完了の概念はない。
/// 使わなくなったものは archive する。これは完了ではなく選択肢からの除外である
/// （docs/08-technical-design.md §8 の禁止事項 2）。
/// <para>
/// 削除エンドポイントも作らない。記録は削除しない
/// （docs/07-api-design.md §1.1）。
/// </para>
/// </remarks>
[ApiController]
[Route("api/tasks")]
public sealed class TasksController : ControllerBase
{
    private readonly TaskItemService _service;

    public TasksController(TaskItemService service)
    {
        _service = service;
    }

    /// <param name="sort">
    /// <c>recent</c>（直近使用順・既定）または <c>updated</c>（更新順）。
    /// </param>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskItemResponse>>> GetAsync(
        [FromQuery] bool includeArchived = false,
        [FromQuery] string? keyword = null,
        [FromQuery] TaskItemSort sort = TaskItemSort.Recent,
        CancellationToken cancellationToken = default)
    {
        var summaries = await _service.GetAsync(includeArchived, keyword, sort, cancellationToken);

        return Ok(summaries.ToResponses());
    }

    [HttpPost]
    public async Task<ActionResult<TaskItemDetailResponse>> CreateAsync(
        [FromBody] SaveTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(
            request.Title, request.DefaultWorkTypeId!.Value, request.Note, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, created.ToDetailResponse());
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<TaskItemDetailResponse>> UpdateAsync(
        long id,
        [FromBody] SaveTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(
            id, request.Title, request.DefaultWorkTypeId!.Value, request.Note, cancellationToken);

        return Ok(updated.ToDetailResponse());
    }

    /// <summary>
    /// アーカイブする。作業開始画面の選択肢から外れるが、過去のセッションからは
    /// 参照され続ける。
    /// </summary>
    [HttpPost("{id:long}/archive")]
    public async Task<ActionResult<TaskItemDetailResponse>> ArchiveAsync(
        long id, CancellationToken cancellationToken)
    {
        var archived = await _service.ArchiveAsync(id, cancellationToken);

        return Ok(archived.ToDetailResponse());
    }

    [HttpPost("{id:long}/unarchive")]
    public async Task<ActionResult<TaskItemDetailResponse>> UnarchiveAsync(
        long id, CancellationToken cancellationToken)
    {
        var unarchived = await _service.UnarchiveAsync(id, cancellationToken);

        return Ok(unarchived.ToDetailResponse());
    }
}
