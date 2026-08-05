using Microsoft.AspNetCore.Mvc;

namespace PerformanceOs.Api.Middleware;

/// <summary>
/// 例外を RFC 7807 の ProblemDetails に変換する
/// （docs/07-api-design.md §0.2、docs/08-technical-design.md §3.8）。
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var problem = ExceptionProblemMapper.Map(exception);

        if (problem.IsUnexpected)
        {
            // 想定外の例外は必ずログに残す。握り潰さない。
            _logger.LogError(
                exception,
                "未処理の例外が発生しました: {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
        }
        else
        {
            _logger.LogInformation(
                "リクエストが拒否されました: {Status} {Method} {Path} — {Detail}",
                problem.Status,
                context.Request.Method,
                context.Request.Path,
                problem.Detail);
        }

        if (context.Response.HasStarted)
        {
            // 応答の書き込みが始まっていると上書きできない。
            // 握り潰さず、そのまま伝播させる。
            _logger.LogWarning("応答が開始済みのため ProblemDetails を書き込めません。");
            throw exception;
        }

        var details = new ProblemDetails
        {
            Type = problem.Type,
            Title = problem.Title,
            Status = problem.Status,
            Detail = problem.Detail,
            Instance = context.Request.Path,
        };

        context.Response.Clear();
        context.Response.StatusCode = problem.Status;
        context.Response.ContentType = "application/problem+json; charset=utf-8";

        await context.Response.WriteAsJsonAsync(details, context.RequestAborted);
    }
}
