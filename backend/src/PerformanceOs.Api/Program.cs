using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using PerformanceOs.Api.Middleware;
using PerformanceOs.Api.Serialization;
using PerformanceOs.Application.TaskItems;
using PerformanceOs.Application.WorkSessions;
using PerformanceOs.Application.WorkTypes;
using PerformanceOs.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- JSON
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        var json = options.JsonSerializerOptions;

        // 定義外のプロパティを 400 にする。無視しない。
        // PUT /work-sessions/{id}/result に startedAt を送っても静かに無視されると、
        // 変更不可の項目を変更しようとしていることに気づけない
        // （docs/07-api-design.md §3）。
        json.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;

        // 列挙値は英語名の文字列で返す。既定では数値になる
        // （docs/07-api-design.md §0.3）。
        json.Converters.Add(new JsonStringEnumConverter());

        // 日時は UTC の ISO 8601（末尾 Z）。既定では +00:00 表記になる。
        json.Converters.Add(new UtcDateTimeOffsetConverter());
    });

// バリデーション失敗時の応答を、他のエラーと同じ ProblemDetails の形に揃える
// （docs/07-api-design.md §0.2）。
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Type = ProblemTypes.Validation,
            Title = "入力値が不正です",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path,
        };

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" },
        };
    };
});

// ---------------------------------------------------------------- 依存関係
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "接続文字列 ConnectionStrings:Default が設定されていません。");

builder.Services.AddInfrastructure(connectionString);

// Application はパッケージ参照を持たないため、DI 登録はここで行う。
builder.Services.AddScoped<WorkTypeService>();
builder.Services.AddScoped<TaskItemService>();
builder.Services.AddScoped<WorkSessionService>();

// ---------------------------------------------------------------- CORS
const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddOpenApi();

var app = builder.Build();

// 例外変換は最も外側に置く。以降のすべての例外を捕捉する。
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// UseHttpsRedirection は入れない。docker-compose では HTTP の 8080 のみを
// 公開しており（docs/08-technical-design.md §4）、リダイレクトすると到達できない。
//
// UseAuthorization も入れない。MVP に認証は無い
// （docs/01-product-requirements.md §7.3）。

app.UseCors(FrontendCorsPolicy);

app.MapControllers();

app.Run();

/// <summary>統合テストから参照するためのエントリポイント公開。</summary>
public partial class Program;
