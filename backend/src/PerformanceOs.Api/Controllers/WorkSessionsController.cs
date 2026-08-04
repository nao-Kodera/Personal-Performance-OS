using Microsoft.AspNetCore.Mvc;
using PerformanceOs.Api.Contracts.Requests;
using PerformanceOs.Api.Contracts.Responses;
using PerformanceOs.Api.Mapping;
using PerformanceOs.Application.WorkSessions;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Api.Controllers;

/// <summary>
/// 作業セッション（docs/07-api-design.md §2.13〜2.18）。
/// </summary>
/// <remarks>
/// <b>意図的に用意しないエンドポイント</b>（docs/07-api-design.md §1.1）:
/// <list type="bullet">
///   <item>PreWorkState / WorkContext の更新（PS-2 / WC-3）</item>
///   <item>セッションの削除（記録は削除しない）</item>
///   <item>時刻を指定しての作成（WS-8）</item>
/// </list>
/// </remarks>
[ApiController]
[Route("api/work-sessions")]
public sealed class WorkSessionsController : ControllerBase
{
    private readonly WorkSessionService _service;

    public WorkSessionsController(WorkSessionService service)
    {
        _service = service;
    }

    /// <summary>
    /// 進行中のセッションを返す。存在しなければ 204。
    /// </summary>
    /// <remarks>
    /// 進行中は全体で最大 1 件のため、配列ではなく単一オブジェクトを返す（WS-9）。
    /// クライアントは startedAt と現在時刻の差から経過時間を算出する。
    /// クライアント側のタイマーを信用源にしないこと。
    /// </remarks>
    [HttpGet("active")]
    public async Task<ActionResult<WorkSessionResponse>> GetActiveAsync(
        CancellationToken cancellationToken)
    {
        var session = await _service.GetActiveAsync(cancellationToken);

        if (session is null)
        {
            return NoContent();
        }

        var labels = await _service.GetLabelsAsync(cancellationToken);

        return Ok(session.ToResponse(labels));
    }

    /// <summary>
    /// 作業を開始する。WorkSession / PreWorkState / WorkContext が
    /// 同一トランザクションで保存される（WS-1）。
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult<WorkSessionResponse>> StartAsync(
        [FromBody] StartWorkSessionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _service.StartAsync(
            request.TaskItemId!.Value,
            request.WorkTypeId!.Value,
            request.PreWorkState!.ToInput(),
            request.WorkContext!.ToInput(),
            cancellationToken);

        var labels = await _service.GetLabelsAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, session.ToResponse(labels));
    }

    /// <summary>
    /// 作業を終了し、成果を記録する。
    /// </summary>
    /// <remarks>
    /// 終了と評価は分離できない。<b>成果評価のスキップ導線を作らないこと</b>（WS-3）。
    /// </remarks>
    [HttpPost("{id:long}/finish")]
    public async Task<ActionResult<WorkSessionResponse>> FinishAsync(
        long id,
        [FromBody] SaveResultRequest request,
        CancellationToken cancellationToken)
    {
        var finished = await _service.FinishAsync(
            id, request.InterruptionCount!.Value, request.Result!.ToInput(), cancellationToken);

        var labels = await _service.GetLabelsAsync(cancellationToken);

        return Ok(finished.Session.ToResponse(labels, finished.Warnings));
    }

    /// <summary>
    /// 作業として成立しなかったセッションを終了する。成果は記録されない（WS-4）。
    /// </summary>
    [HttpPost("{id:long}/abandon")]
    public async Task<ActionResult<WorkSessionResponse>> AbandonAsync(
        long id,
        [FromBody] AbandonWorkSessionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _service.AbandonAsync(id, request.Note, cancellationToken);

        var labels = await _service.GetLabelsAsync(cancellationToken);

        return Ok(session.ToResponse(labels));
    }

    /// <summary>
    /// 成果評価を訂正する。初回記録時刻は変更されない（PR-2）。
    /// </summary>
    /// <remarks>
    /// 開始・終了時刻、PreWorkState、WorkContext は変更できない。
    /// これらをリクエストに含めると、定義外プロパティとして 400 になる。
    /// </remarks>
    [HttpPut("{id:long}/result")]
    public async Task<ActionResult<WorkSessionResponse>> UpdateResultAsync(
        long id,
        [FromBody] SaveResultRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _service.UpdateResultAsync(
            id, request.InterruptionCount!.Value, request.Result!.ToInput(), cancellationToken);

        var labels = await _service.GetLabelsAsync(cancellationToken);

        return Ok(session.ToResponse(labels));
    }

    /// <summary>
    /// 履歴を JST の日付単位で返す。
    /// </summary>
    /// <param name="from">開始日（JST・この日を含む）。省略時は 27 日前。</param>
    /// <param name="to">終了日（JST・この日を含む）。省略時は当日。</param>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkSessionDayResponse>>> GetHistoryAsync(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] SessionStatus? status = null,
        [FromQuery] long? taskItemId = null,
        CancellationToken cancellationToken = default)
    {
        var days = await _service.GetHistoryAsync(from, to, status, taskItemId, cancellationToken);
        var labels = await _service.GetLabelsAsync(cancellationToken);

        return Ok(days.ToResponses(labels));
    }
}
