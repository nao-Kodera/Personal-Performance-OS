# 技術設計書

- ドキュメントID: TECH-008
- ステータス: ドラフト
- 最終更新: 2026-08-05
- 前提: [05-domain-design.md](05-domain-design.md) / [06-database-design.md](06-database-design.md) / [07-api-design.md](07-api-design.md)

---

## 0. 方針

**必要になるまで入れない。**

MVPの目的は観測と分析の成立であり、アーキテクチャの完成度ではない。以下は最初から導入しない。

| 導入しないもの | 理由 | 再検討の条件 |
|---|---|---|
| CQRS / MediatR | ハンドラの数だけファイルが増え、単純なCRUDに間接層が乗る | ユースケースが30を超えたら |
| リポジトリの汎用基底クラス | 集約ごとに必要な操作が違う。共通化の利益がない | — |
| AutoMapper | マッピングが明示的でなくなり、APIの変更影響が追えない | — |
| イベントソーシング | 記録の訂正が稀であり、状態のみで足りる | — |
| DDDの完全な戦術パターン一式 | 集約が5つしかない | — |
| 認証基盤 | [PRD §7.3](01-product-requirements.md) | 複数ユーザー化するとき |
| Redis等のキャッシュ | 想定データ規模で不要（[分析仕様 §7](04-analytics-spec.md)） | — |

**入れるもの**は、[ドメイン設計](05-domain-design.md)の不変条件を守るために必要なものに限る。

---

## 1. 技術スタック

### 1.1 バックエンド

| 項目 | 選定 | 備考 |
|---|---|---|
| ランタイム | .NET 10 (LTS) | 実装開始時に最新LTSを確認すること |
| フレームワーク | ASP.NET Core Web API | Minimal APIではなくControllerを使う（§3.2） |
| ORM | Entity Framework Core 10 | |
| DBドライバ | Npgsql.EntityFrameworkCore.PostgreSQL | |
| バリデーション | DataAnnotations（DTO）+ ドメイン層のガード | FluentValidationは導入しない |
| APIドキュメント | OpenAPI（`Microsoft.AspNetCore.OpenApi`）+ Scalar | 開発時のみ |
| テスト | xUnit + Testcontainers | アサーションは標準の `Assert`。FluentAssertions は 8.x 以降が商用ライセンスのため採用しない |

### 1.2 フロントエンド

| 項目 | 選定 | 備考 |
|---|---|---|
| 言語 | TypeScript 6.x | `strict: true` + `noUncheckedIndexedAccess: true` |
| ライブラリ | React 19 | |
| ビルド | Vite | |
| ルーティング | React Router | |
| サーバー状態 | TanStack Query v5 | |
| クライアント状態 | React標準（useState / Context） | 状態管理ライブラリは入れない |
| グラフ | Recharts | 横棒グラフのみ使用 |
| スタイル | CSS Modules | UIライブラリは入れない |
| テスト | Vitest + React Testing Library | |

**UIライブラリを入れない理由**

必要な画面が9つ、入力コンポーネントが6種類（[UC 6章](03-use-cases.md)）しかない。特に1〜5の評価ボタンは本プロダクト固有の形であり、既製品をそのまま使えない。依存を増やす利益がない。

**状態管理ライブラリを入れない理由**

アプリの状態のほぼ全てがサーバー由来である。TanStack Queryがサーバー状態を持てば、クライアント固有の状態は「作業中画面の中断カウンタ」程度しか残らない。

### 1.3 開発環境

| 項目 | 選定 |
|---|---|
| DB | PostgreSQL 16（Docker） |
| 起動 | Docker Compose |
| Node | 24.x LTS |

---

## 2. ソリューション構成

```text
Personal-Performance-OS/
├── docs/                               設計書
├── backend/
│   ├── PerformanceOs.sln
│   ├── src/
│   │   ├── PerformanceOs.Domain/
│   │   ├── PerformanceOs.Application/
│   │   ├── PerformanceOs.Infrastructure/
│   │   └── PerformanceOs.Api/
│   └── tests/
│       ├── PerformanceOs.Domain.Tests/
│       ├── PerformanceOs.Application.Tests/
│       └── PerformanceOs.Api.IntegrationTests/
├── frontend/
│   ├── src/
│   └── tests/
├── docker-compose.yml
└── README.md
```

### 2.1 依存方向

```text
        Api
         │
         ▼
    Application ──────► Domain
         ▲                 ▲
         │                 │
    Infrastructure ────────┘
```

| プロジェクト | 依存先 |
|---|---|
| Domain | **なし**（外部パッケージも参照しない） |
| Application | Domain |
| Infrastructure | Domain, Application |
| Api | Application, Infrastructure（DI登録のみ） |

**Domain が何にも依存しないことを守ること。** EF Coreの属性、`DbContext`、ASP.NET Coreの型を Domain に持ち込まない。マッピングは Infrastructure の `IEntityTypeConfiguration` で行う。

理由: [ドメイン設計](05-domain-design.md)の不変条件が、永続化の都合で歪められることを防ぐ。特に PreWorkState のイミュータブル性は、EF Coreのために public setter を付けた瞬間に失われる。

### 2.2 Domain

```text
PerformanceOs.Domain/
├── Common/
│   ├── Entity.cs                    Id を持つ基底
│   └── DomainException.cs
├── WorkTypes/
│   └── WorkType.cs
├── TaskItems/
│   └── TaskItem.cs
├── DailyConditions/
│   └── DailyCondition.cs
├── PlannedWorks/
│   ├── PlannedWork.cs
│   ├── NonExecutionRecord.cs
│   └── NonExecutionReason.cs
├── WorkSessions/
│   ├── WorkSession.cs               集約ルート
│   ├── PreWorkState.cs
│   ├── WorkContext.cs
│   ├── PerformanceResult.cs
│   ├── SessionStatus.cs
│   └── WorkLocation.cs
├── ValueObjects/
│   ├── Rating.cs
│   ├── SleepDuration.cs
│   └── SessionPeriod.cs
├── Repositories/                    インターフェースのみ
│   ├── IWorkTypeRepository.cs
│   ├── ITaskItemRepository.cs
│   ├── IDailyConditionRepository.cs
│   ├── IPlannedWorkRepository.cs
│   └── IWorkSessionRepository.cs
└── Time/
    ├── IClock.cs
    └── JstCalendar.cs
```

### 2.3 Application

```text
PerformanceOs.Application/
├── WorkTypes/       WorkTypeService.cs
├── TaskItems/       TaskItemService.cs
├── DailyConditions/ DailyConditionService.cs
├── PlannedWorks/    PlannedWorkService.cs
├── WorkSessions/    WorkSessionService.cs
├── Home/            HomeService.cs
├── Analytics/
│   ├── AnalyticsService.cs
│   ├── IAnalyticsQuery.cs           リードモデル（実装はInfrastructure）
│   └── Models/                      集計結果のDTO
└── Common/
    ├── ApplicationException.cs
    ├── NotFoundException.cs
    ├── ConflictException.cs
    └── DomainRuleException.cs
```

**サービスクラスは集約ごとに1つ。** ユースケースごとにクラスを作らない（MediatRを入れない方針と対応）。

### 2.4 Infrastructure

```text
PerformanceOs.Infrastructure/
├── Persistence/
│   ├── PerformanceOsDbContext.cs
│   ├── Configurations/              IEntityTypeConfiguration<T> ×9
│   ├── Repositories/                Domain の I*Repository の実装
│   └── Migrations/
├── Analytics/
│   └── AnalyticsQuery.cs            v_completed_sessions を使う生SQL
└── Time/
    └── SystemClock.cs
```

### 2.5 Api

```text
PerformanceOs.Api/
├── Controllers/                     ×8
├── Contracts/
│   ├── Requests/                    リクエストDTO（DataAnnotations付き）
│   └── Responses/                   レスポンスDTO
├── Mapping/                         DTO ↔ ドメインの手書き変換
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs   例外 → ProblemDetails
├── Program.cs
└── appsettings.json
```

---

## 3. 実装方針

### 3.1 時刻の扱い

**すべての時刻取得を `IClock` 経由にする。**

```csharp
public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly TodayJst { get; }
}
```

`DateTime.Now` / `DateTime.UtcNow` をドメイン・アプリケーション層で直接呼ばない。

理由:
- テストで時刻を固定できる（深夜をまたぐセッション、当日判定のテストに必須）
- JST変換の実装が1箇所に集まる

**JST変換ロジックは `JstCalendar` に集約する。**

```csharp
public static class JstCalendar
{
    public static readonly TimeZoneInfo Jst =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

    public static DateOnly ToJstDate(DateTimeOffset utc);
    public static TimeBand ToTimeBand(DateTimeOffset utc);
}
```

**同じ変換がSQL側（`v_completed_sessions`ビュー）にも存在する。** 定義の出典は[用語集 §4](02-glossary.md)であり、C#とSQLの両方が一致していることをテストで確認すること（§6.3）。

**`TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo")` の可搬性**

Windowsでは従来 `"Tokyo Standard Time"` が必要だったが、.NET 6以降はWindows上でもIANA IDが解決される。Linuxコンテナ・Windows開発機の双方で `"Asia/Tokyo"` を使う。

### 3.2 Minimal API ではなく Controller を使う理由

エンドポイントが21あり、リクエスト/レスポンスDTOが多い。Minimal APIだと `Program.cs` が肥大するか、拡張メソッドへの分割規約を自前で決めることになる。Controllerの方が構造が既定されている。

### 3.3 エンティティの永続化とカプセル化

EF Coreがマッピングできる形にしつつ、不変条件を守る。

```csharp
public sealed class PreWorkState
{
    public long Id { get; private set; }
    public long WorkSessionId { get; private set; }
    public Rating FatigueLevel { get; private set; }
    public Rating ExpectedFocusLevel { get; private set; }
    public Rating MoodLevel { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    private PreWorkState() { }   // EF Core 用

    internal PreWorkState(Rating fatigue, Rating expectedFocus,
                          Rating mood, DateTimeOffset recordedAt)
    {
        FatigueLevel = fatigue;
        ExpectedFocusLevel = expectedFocus;
        MoodLevel = mood;
        RecordedAt = recordedAt;
    }
}
```

**要点**

| 項目 | 方針 |
|---|---|
| setter | すべて `private set`。public setter を作らない |
| コンストラクタ | EF Core用の `private` 引数なしコンストラクタを持つ |
| 生成 | `internal` コンストラクタ。集約ルート（WorkSession）からのみ生成できる |
| 更新メソッド | **PreWorkState / WorkContext には作らない**（[ドメイン設計 PS-2 / WC-3](05-domain-design.md)） |

**PreWorkState と WorkContext に更新メソッドを追加しないこと。** これらのイミュータブル性は、本プロダクトの分析が意味を持つための前提である。

### 3.4 Rating 値オブジェクトのマッピング

```csharp
public readonly record struct Rating
{
    public int Value { get; }

    public Rating(int value)
    {
        if (value is < 1 or > 5)
            throw new DomainException($"評価値は1〜5である必要があります: {value}");
        Value = value;
    }
}
```

EF Coreでは値変換を使う。

```csharp
builder.Property(x => x.FatigueLevel)
       .HasConversion(v => (short)v.Value, v => new Rating(v))
       .HasColumnType("smallint");
```

### 3.5 集約の読み込み

WorkSessionは常に集約全体を読む。

```csharp
public async Task<WorkSession?> GetByIdAsync(long id, CancellationToken ct)
    => await _db.WorkSessions
        .Include(s => s.PreWorkState)
        .Include(s => s.WorkContext)
        .Include(s => s.Result)
        .FirstOrDefaultAsync(s => s.Id == id, ct);
```

**部分的に読み込まない。** 状態遷移の検証（WS-2〜4）に子エンティティの有無が必要である。

読み取り専用の一覧・分析では、集約を読まずに射影クエリを使う（§3.7）。

### 3.6 同時実行制約（WS-9）の担保

二重に守る。

```csharp
// 1. アプリケーション層での事前チェック（親切なエラーを返すため）
var active = await _sessions.GetActiveAsync(ct);
if (active is not null)
    throw new ConflictException("進行中の作業セッションが既に存在します");

// 2. DBの部分一意インデックス（実際の担保）
//    uq_work_sessions_single_active
//    → 違反時 PostgresException(SqlState: 23505) を 409 に変換
```

**1だけでは守れない。** 複数タブからの同時操作や、リトライで並行リクエストが起きる。2が最終的な担保である。

`SqlState = "23505"`（unique_violation）を捕捉し、インデックス名で判別して適切なメッセージの409に変換する。

### 3.7 分析クエリ

集約を経由せず、生SQLで直接読む。

```csharp
public interface IAnalyticsQuery
{
    Task<IReadOnlyList<WorkTypeAggregate>> GetByWorkTypeAsync(
        DateOnly from, DateOnly to, CancellationToken ct);
    // ... 他5種
}
```

実装は `Infrastructure/Analytics/AnalyticsQuery.cs` で、[DB設計 §4](06-database-design.md) のSQLをそのまま使う。

**LINQで書き直さない理由**

- `AT TIME ZONE 'Asia/Tokyo'` を含むJST変換をLINQで表現すると読めなくなる
- `v_completed_sessions` ビューに母集団の定義（`status = 'Completed'`）を集約している。LINQで再現すると定義が二重化する
- 設計書のSQLとコードのSQLが一致していれば、レビューが容易になる

**最小サンプル数の判定はSQLで行わない。** `HAVING COUNT(*) >= 5` としてはならない。件数ごと取得し、`AnalyticsService` で `sufficient` を判定して `avgXxx` を `null` にする（[API設計 §2.20](07-api-design.md)）。

### 3.8 例外からHTTPステータスへの変換

`ExceptionHandlingMiddleware` で一元的に変換する。

| 例外 | status | type |
|---|---|---|
| `NotFoundException` | 404 | `not-found` |
| `ConflictException` | 409 | `conflict` |
| `DomainRuleException` | 422 | `domain-rule` |
| `DomainException`（Domain層） | 422 | `domain-rule` |
| `PostgresException` SqlState 23505 | 409 | `conflict` |
| `PostgresException` SqlState 23514（CHECK違反） | 500 | `internal` |
| その他 | 500 | `internal` |

**CHECK制約違反（23514）を500にする理由**

CHECK制約は、アプリケーション層とドメイン層の検証をすり抜けた場合にのみ発火する。到達した時点でバグである。400ではなく500として扱い、ログに記録する。

### 3.9 未知のプロパティを400にする設定

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    });
```

[API設計 §3](07-api-design.md) の方針。変更不可の項目（`startedAt` など）を送ったとき、静かに無視されず400になる。

**`ConfigureHttpJsonOptions` を使わないこと。** これは Minimal API 用であり、Controller のモデルバインディングには適用されない（§3.2 の通り本プロジェクトは Controller を使う）。設定したつもりで未知のプロパティが素通りする。

### 3.10 フロントエンドの構成

```text
frontend/src/
├── api/
│   ├── client.ts                fetch ラッパー・ProblemDetails の解釈
│   ├── types.ts                 API の型定義（手書き）
│   └── hooks/                   TanStack Query のフック
├── components/
│   ├── RatingInput.tsx          1〜5のボタン列
│   ├── SleepDurationInput.tsx
│   ├── WorkTypeSelector.tsx
│   ├── TaskSelector.tsx
│   └── BarChart.tsx
├── pages/                       S-01 〜 S-09
├── lib/
│   ├── datetime.ts              UTC → JST 表示変換
│   └── labels.ts                列挙値 → 日本語表示名
└── App.tsx
```

**`lib/labels.ts` に日本語表示名を集約する。** APIは英語の列挙値を返す（[API設計 §0.3](07-api-design.md)）。画面ごとに違う日本語を使わないため、変換表を1箇所に置く（[用語集 §0](02-glossary.md)のルール3）。

**API型定義を手書きする理由**

OpenAPIからの自動生成も可能だが、エンドポイントが21・型が20程度であり、生成ツールの設定と型の癖に対処する手間の方が大きい。手書きし、統合テストで実際のレスポンスとの整合を確認する。

### 3.11 経過時間の表示

作業中画面（S-05）のタイマーは、サーバーの `startedAt` を基準に計算する。

```typescript
const elapsedMs = Date.now() - new Date(session.startedAt).getTime();
```

**クライアント側でカウントアップした値を保持しない。** リロード・タブ復帰で値が失われる。1秒ごとの `setInterval` は再レンダリングのトリガーにのみ使い、値は毎回 `startedAt` から再計算する。

---

## 4. Docker Compose

```yaml
services:
  db:
    image: postgres:16
    environment:
      POSTGRES_DB: performance_os
      POSTGRES_USER: performance_os
      POSTGRES_PASSWORD: dev_password
      TZ: UTC
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U performance_os"]
      interval: 5s
      retries: 10

  api:
    build:
      context: ./backend
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      TZ: UTC
      ConnectionStrings__Default: >-
        Host=db;Database=performance_os;Username=performance_os;Password=dev_password
    ports:
      - "5080:8080"
    depends_on:
      db:
        condition: service_healthy

  web:
    build:
      context: ./frontend
    environment:
      VITE_API_BASE_URL: http://localhost:5080
    ports:
      - "5173:5173"
    depends_on:
      - api

volumes:
  pgdata:
```

**`TZ: UTC` を明示する理由**

コンテナのタイムゾーンがJSTだと、`DateTime.Now` を誤って使った箇所がローカルでは動き、本番で壊れる。UTC固定にすることで、JST変換の実装漏れが開発時に顕在化する。

**マイグレーションの適用**

`Program.cs` での自動適用は行わない。明示的に実行する。

```bash
dotnet ef database update --project backend/src/PerformanceOs.Infrastructure \
                          --startup-project backend/src/PerformanceOs.Api
```

---

## 5. 設定

| 設定 | 既定値 | 説明 |
|---|---|---|
| `ConnectionStrings:Default` | — | 接続文字列 |
| `Analytics:MinSampleSize` | 5 | 最小サンプル数 |
| `Analytics:DefaultPeriodDays` | 28 | 既定の分析期間 |
| `Cors:AllowedOrigins` | `http://localhost:5173` | 開発時のフロントエンド |

**`MinSampleSize` を設定可能にするが、下げないこと。** 下げると少数サンプルの平均が表示され、誤った確信を生む（[分析仕様 §2.3](04-analytics-spec.md)）。設定化しているのは、データ蓄積後に**上げる**ためである。

---

## 6. テスト方針

### 6.1 レイヤ別

| 対象 | 種別 | 重点 |
|---|---|---|
| Domain | 単体（xUnit） | 不変条件・状態遷移。DBもモックも使わない |
| Application | 単体 | サービスの分岐。リポジトリはインメモリ実装 |
| Infrastructure | 統合（Testcontainers） | SQL・制約・マイグレーション |
| Api | 統合（WebApplicationFactory + Testcontainers） | エンドポイントの入出力・ステータスコード |
| Frontend | 単体（Vitest + RTL） | 入力コンポーネント・バリデーション表示 |

**モックライブラリを使わない。** リポジトリのインメモリ実装を書く方が、モックの設定より読みやすく壊れにくい。

### 6.2 必ず書くテスト

[ドメイン設計 §8](05-domain-design.md) の不変条件のうち、DBで担保できないもの。

| # | テスト |
|---|---|
| T-01 | InProgress のセッションがある状態で start → 409 |
| T-02 | 並行に2件 start → 一方が409（DB制約の確認・統合テスト） |
| T-03 | Completed のセッションを再度 finish → 409 |
| T-04 | Abandoned のセッションを finish → 409 |
| T-05 | finish 時に result が欠けている → 400 |
| T-06 | finish 後、performance_results が必ず存在する（WS-3） |
| T-07 | abandon 後、performance_results が存在しない（WS-4） |
| T-08 | 過去日の daily-conditions PUT → 422 |
| T-09 | WorkSessionが紐づく PlannedWork に skip → 409 |
| T-10 | NonExecutionRecordがある PlannedWork で start → 409 |
| T-11 | Rating に0または6を渡すと例外 |
| T-12 | PreWorkState に更新手段が存在しない（コンパイル時に保証。リフレクションで public setter がないことを確認） |
| T-13 | start / finish のトランザクション途中で例外 → 部分的な行が残らない |
| T-14 | [DB設計 §7](06-database-design.md) の整合性検証クエリが全件0を返す |

**T-12 の意図**

将来の変更で PreWorkState に setter や更新メソッドが追加されることを防ぐ。テストが失敗したら、その変更が本プロダクトの根幹を壊していることを示す。

### 6.3 日時のテスト

`IClock` を固定して、境界を確認する。

| # | テスト |
|---|---|
| T-20 | 22:00(JST)開始・翌01:00(JST)終了 → 所属日は開始日 |
| T-21 | 04:59(JST)開始 → TimeBand は Evening |
| T-22 | 05:00(JST)開始 → TimeBand は EarlyMorning |
| T-23 | 16:59 → Afternoon、17:00 → Evening |
| T-24 | JST 00:15 に「今日」を問い合わせると、UTCでは前日だが JST の当日が返る |
| T-25 | **C#の `JstCalendar.ToTimeBand` と SQLの `v_completed_sessions.time_band` が全時刻帯で一致する**（統合テスト） |

**T-25 が重要である。** JST変換ロジックがC#とSQLの2箇所に存在するため、片方だけ変更されるとAPIの表示と分析結果がずれる。0時〜23時の24パターンで一致を確認する。

### 6.4 書かないテスト

| 対象 | 理由 |
|---|---|
| コントローラの単体テスト | 統合テストで同じ範囲を、より実態に近く確認できる |
| DTOマッピングの網羅テスト | 統合テストのレスポンス検証でカバーされる |
| E2E（Playwright等） | MVPの規模に対して維持コストが見合わない |
| カバレッジ目標 | 数値目標を置くと、意味のないテストが増える |

---

## 7. 開発の進め方

| 項目 | 方針 |
|---|---|
| ブランチ | `main` + 作業ブランチ。縦切り単位でマージ |
| コミット粒度 | 動作する単位 |
| CI | GitHub Actions。ビルド + テスト。MVP期間中はデプロイなし |
| デプロイ | MVPではローカル実行のみ |

**MVPをデプロイしない理由**

対象ユーザーは開発者本人1人であり（[PRD §6](01-product-requirements.md)）、認証もない。デプロイすると認証が必要になり、[PRD §7.3](01-product-requirements.md) の除外項目に反する。ローカルのDocker Composeで運用する。

**ただし、記録が続かないとMVPは失敗する**（[PRD §9.3](01-product-requirements.md)）。ローカル実行が記録の障壁になるようなら、この方針を見直すこと。その場合の判断材料は「PCを開いていない時間に作業を始めることがあるか」である。

---

## 8. 実装時の禁止事項

以下は設計書に反する。実装中に気づいたら止めること。

| # | 禁止事項 | 出典 |
|---|---|---|
| 1 | PreWorkState / WorkContext に setter や更新メソッドを追加する | [ドメイン設計 PS-2 / WC-3](05-domain-design.md) |
| 2 | TaskItem に完了フラグ・期限・優先度・タグ・親子関係を追加する | [ドメイン設計 §9](05-domain-design.md) |
| 3 | 開始時刻・終了時刻をリクエストから受け取る | [ドメイン設計 WS-8](05-domain-design.md) |
| 4 | 成果評価をスキップして終了できるようにする | [ドメイン設計 WS-3](05-domain-design.md) |
| 5 | 合成スコア（総合評価）を計算・保存・表示する | [用語集 §6](02-glossary.md) |
| 6 | 分析に相関係数・p値を追加する | [分析仕様 §1.2](04-analytics-spec.md) |
| 7 | 分析結果に結論文（「午前が最も集中できています」等）を出す | [UC-09](03-use-cases.md) |
| 8 | サンプル数5件未満の平均値を返す・表示する | [分析仕様 §2.3](04-analytics-spec.md) |
| 9 | 欠損値を推測で補完する | [PRD §8 原則4](01-product-requirements.md) |
| 10 | 記録を削除できるようにする | [PRD §8 原則2](01-product-requirements.md) |
| 11 | 作業前の入力項目を増やして30秒を超える | [PRD §8 原則1](01-product-requirements.md) |
| 12 | `started_at::date` のようにJST変換なしで日付を取り出す | [用語集 §4](02-glossary.md) |
| 13 | 設計書にない分析軸・集計を追加する | [PRD §8 原則5](01-product-requirements.md) |

**11 が最も破られやすい。** 「この項目も記録しておけば後で分析できる」という理由で入力項目が増えると、記録が続かなくなり、すべてのデータが失われる。項目を足す場合は、既存項目を削るか、[分析仕様書](04-analytics-spec.md)に必要性を明記してから行う。
