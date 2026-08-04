# 実装タスク分解

- ドキュメントID: TASKS-009
- ステータス: ドラフト
- 最終更新: 2026-08-04
- 前提: 設計書01〜08すべて

---

## 0. 分解方針

**縦切りで進める。** レイヤー単位（先に全テーブル、次に全API…）で進めない。

理由: レイヤー単位だと、最初に動くものが出るのが最後になる。本プロダクトは「記録が続くか」がMVPの成否を決める（[PRD §9.3](01-product-requirements.md)）ため、記録できる状態に最短で到達し、実際に使い始める必要がある。

```text
縦切り1  記録の中核（タスク → 作業前 → 開始 → 終了 → 評価 → 履歴）
縦切り2  日次コンディションと予定・未実行記録
縦切り3  分析
縦切り4  仕上げ
```

**縦切り1が完成した時点で、実運用を開始する。** 縦切り2・3の実装と並行して記録を貯める。分析は28日分のデータが必要であり、実装完了を待ってから記録を始めると、検証が1ヶ月遅れる。

---

## 1. 全体のタスク一覧

| # | タスク | 縦切り | 依存 | 目安 |
|---|---|---|---|---|
| T-00 | リポジトリ初期化・Docker Compose | 0 | — | S |
| T-01 | ソリューション骨格・プロジェクト参照 | 1 | T-00 | S |
| T-02 | Domain: 値オブジェクト・IClock・JstCalendar | 1 | T-01 | M |
| T-03 | Domain: WorkType / TaskItem | 1 | T-02 | S |
| T-04 | Domain: WorkSession集約 | 1 | T-02 | L |
| T-05 | Infrastructure: DbContext・マッピング・初期マイグレーション | 1 | T-03,04 | L |
| T-06 | Application: WorkTypeService / TaskItemService | 1 | T-05 | M |
| T-07 | Application: WorkSessionService | 1 | T-05 | L |
| T-08 | Api: 例外ミドルウェア・共通設定 | 1 | T-01 | M |
| T-09 | Api: WorkTypes / Tasks コントローラ | 1 | T-06,08 | M |
| T-10 | Api: WorkSessions コントローラ | 1 | T-07,08 | M |
| T-11 | Api統合テスト（縦切り1） | 1 | T-09,10 | L |
| T-12 | Frontend: 基盤（Vite・ルーティング・APIクライアント） | 1 | T-00 | M |
| T-13 | Frontend: 共通入力コンポーネント | 1 | T-12 | M |
| T-14 | Frontend: S-02 タスク画面 | 1 | T-13,09 | M |
| T-15 | Frontend: S-04 作業開始画面 | 1 | T-13,10 | M |
| T-16 | Frontend: S-05 作業中画面 | 1 | T-15 | M |
| T-17 | Frontend: S-06 成果評価画面 | 1 | T-16 | M |
| T-18 | Frontend: S-08 履歴画面 | 1 | T-13,10 | M |
| T-19 | Frontend: S-01 ホーム（縦切り1版） | 1 | T-15,18 | M |
| **—** | **縦切り1 完了 → 実運用開始** | | | |
| T-20 | Domain/Infra: DailyCondition | 2 | T-05 | M |
| T-21 | Domain/Infra: PlannedWork / NonExecutionRecord | 2 | T-05 | M |
| T-22 | Application: DailyConditionService / PlannedWorkService | 2 | T-20,21 | M |
| T-23 | Api: DailyConditions / PlannedWorks コントローラ | 2 | T-22 | M |
| T-24 | Application/Api: HomeService・`/api/home/today` | 2 | T-22 | M |
| T-25 | Api統合テスト（縦切り2） | 2 | T-23,24 | M |
| T-26 | Frontend: S-03 日次コンディション | 2 | T-23 | S |
| T-27 | Frontend: S-01 予定機能追加 | 2 | T-24 | M |
| T-28 | Frontend: S-07 未実行記録 | 2 | T-23 | S |
| T-29 | Frontend: S-04 に予定連携・未記録警告を追加 | 2 | T-24 | S |
| **—** | **縦切り2 完了** | | | |
| T-30 | Infrastructure: `v_completed_sessions` ビュー | 3 | T-20,21 | S |
| T-31 | Infrastructure: AnalyticsQuery（6種のSQL） | 3 | T-30 | L |
| T-32 | Application: AnalyticsService（サンプル数判定・並び順） | 3 | T-31 | M |
| T-33 | Api: `/api/analytics/summary` | 3 | T-32 | S |
| T-34 | 分析の統合テスト（C#/SQL整合含む） | 3 | T-33 | L |
| T-35 | Frontend: BarChart コンポーネント | 3 | T-12 | M |
| T-36 | Frontend: S-09 分析画面 | 3 | T-33,35 | L |
| **—** | **縦切り3 完了 → MVP機能完成** | | | |
| T-37 | 整合性検証クエリのテスト化 | 4 | T-34 | S |
| T-38 | CI（GitHub Actions） | 4 | T-11 | S |
| T-39 | README・起動手順 | 4 | T-36 | S |
| T-40 | 記録コストの実測と調整 | 4 | 実運用2週間 | M |

目安: S = 半日以内 / M = 1〜2日 / L = 3日以上

---

## 2. 縦切り1: 記録の中核

**完了条件: タスクを登録し、状態を記録して作業を開始・終了・評価し、履歴で確認できる。**

### T-00 リポジトリ初期化・Docker Compose

- `docker-compose.yml`（[技術設計 §4](08-technical-design.md)）
- `.gitignore`、`.editorconfig`
- PostgreSQLコンテナが起動し、接続できることを確認

**確認**: `docker compose up db` で起動し、`psql` で接続できる。

---

### T-01 ソリューション骨格

- 4プロジェクトの作成と参照関係（[技術設計 §2.1](08-technical-design.md)）
- テストプロジェクト3つ

**確認**: `dotnet build` が通る。**Domain が他プロジェクト・外部パッケージを一切参照していない。**

---

### T-02 Domain: 値オブジェクトと時刻

| 対象 | 内容 |
|---|---|
| `Rating` | 1〜5。範囲外は `DomainException` |
| `SleepDuration` | 15〜1440分・15の倍数 |
| `IClock` / `SystemClock` | `UtcNow` / `TodayJst` |
| `JstCalendar` | `ToJstDate` / `ToTimeBand` |
| `TimeBand` / `SleepBand` | 列挙型 |

**テスト**: T-11、T-21〜T-24（[技術設計 §6](08-technical-design.md)）

**注意**: `TimeBand` の境界（04:59 → Evening、05:00 → EarlyMorning、16:59 → Afternoon、17:00 → Evening）を先にテストで固定する。Evening が日をまたぐため、`hour >= 17 || hour < 5` の条件を誤りやすい。

---

### T-03 Domain: WorkType / TaskItem

- `WorkType`: `Rename` / `Deactivate` / `Activate`
- `TaskItem`: `Create` / `Update` / `Archive` / `Unarchive`

**注意**: **TaskItem に完了状態を作らない**（[ドメイン設計 TI-4](05-domain-design.md)）。`IsCompleted` / `CompletedAt` / `Status` を追加しないこと。

---

### T-04 Domain: WorkSession集約 ★中核

| 対象 | 内容 |
|---|---|
| `WorkSession` | `Start` / `Finish` / `Abandon` / `UpdateResult` / `UpdateInterruptionCount` |
| `PreWorkState` | **setterなし・更新メソッドなし** |
| `WorkContext` | 同上。`location_note` は `Other` のときのみ |
| `PerformanceResult` | 更新可。`RecordedAt` は不変 |
| `SessionStatus` / `WorkLocation` | 列挙型 |
| `WorkSessionStarter` | 集約をまたぐ検証 |

**テスト**: T-03〜T-07、T-11、T-12（[技術設計 §6.2](08-technical-design.md)）

**注意**

- 状態遷移（WS-7）を確実に実装する。終端状態から遷移させない
- `Finish` は `PerformanceResult` を引数に取る。Resultなしで終了できるシグネチャにしない（WS-3）
- 時刻は引数で受け取る（`IClock` から渡す）が、**公開APIとしてクライアントから設定できる経路を作らない**（WS-8）
- **T-12（PreWorkState に public setter がないことをリフレクションで確認するテスト）をこの時点で書く**

---

### T-05 Infrastructure: 永続化 ★重要

| 対象 | 内容 |
|---|---|
| `PerformanceOsDbContext` | |
| `IEntityTypeConfiguration` ×5（この時点） | work_types / task_items / work_sessions / pre_work_states / work_contexts / performance_results |
| 初期マイグレーション | [DB設計 §2](06-database-design.md) |
| リポジトリ実装 | IWorkTypeRepository / ITaskItemRepository / IWorkSessionRepository |
| 初期データ | work_types 6件 |

**手書きSQLが必要な箇所**（[DB設計 §8](06-database-design.md)）

```text
uq_work_types_name              式インデックス lower(name)
uq_work_sessions_single_active  部分一意インデックス ★
ix_work_sessions_completed      部分インデックス
```

**`uq_work_sessions_single_active` を必ず作ること。** これが同時実行禁止（WS-9）の実際の担保である。EF Coreは生成しないため、`migrationBuilder.Sql()` で手書きする。

```sql
CREATE UNIQUE INDEX uq_work_sessions_single_active
    ON work_sessions ((true)) WHERE status = 'InProgress';
```

**確認**: マイグレーション適用後、`\d work_sessions` で全CHECK制約とインデックスが存在すること。

---

### T-06 Application: WorkType / TaskItem サービス

- 存在確認・アーカイブ状態の検証
- 例外クラス（`NotFoundException` / `ConflictException` / `DomainRuleException`）

---

### T-07 Application: WorkSessionService ★中核

| メソッド | 対応API |
|---|---|
| `GetActiveAsync` | `GET /work-sessions/active` |
| `StartAsync` | `POST /work-sessions/start` |
| `FinishAsync` | `POST /work-sessions/{id}/finish` |
| `AbandonAsync` | `POST /work-sessions/{id}/abandon` |
| `UpdateResultAsync` | `PUT /work-sessions/{id}/result` |
| `GetHistoryAsync` | `GET /work-sessions` |

**注意**

- `StartAsync` は WorkSession / PreWorkState / WorkContext を**同一トランザクション**で保存する（[API設計 §4](07-api-design.md)）
- `FinishAsync` は WorkSession更新 + PerformanceResult挿入を同一トランザクションで行う
- `GetHistoryAsync` は日付グループ化を行う。グループキーは**JST変換後の日付**
- 一意制約違反（SqlState 23505）を捕捉して `ConflictException` に変換する

---

### T-08 Api: 共通基盤

- `ExceptionHandlingMiddleware`（[技術設計 §3.8](08-technical-design.md)）
- `UnmappedMemberHandling.Disallow` の設定（§3.9）
- CORS設定
- OpenAPI

---

### T-09 / T-10 Api: コントローラ

[API設計 §2](07-api-design.md) のとおり実装する。

**注意**

- **PreWorkState / WorkContext の更新エンドポイントを作らない**（[API設計 §1.1](07-api-design.md)）
- `DELETE` を作らない
- リクエストDTOに `startedAt` / `finishedAt` を持たせない

---

### T-11 Api統合テスト（縦切り1）

Testcontainers + WebApplicationFactory。

必須テスト: T-01〜T-07、T-11〜T-14、T-20〜T-25（[技術設計 §6.2/6.3](08-technical-design.md)）

**T-02（並行start）と T-25（C#/SQL整合）を省略しないこと。** 前者はDB制約の実効性、後者は分析の正しさに直結する。

---

### T-12 Frontend: 基盤

- Vite + React + TypeScript（`strict: true`）
- React Router
- TanStack Query
- `api/client.ts`: ProblemDetailsの解釈
- `lib/datetime.ts`: UTC → JST 表示変換
- `lib/labels.ts`: 列挙値 → 日本語表示名

**注意**: 日本語表示名は `labels.ts` に集約する。画面ごとにハードコードしない（[用語集 §0](02-glossary.md)）。

---

### T-13 Frontend: 共通入力コンポーネント

| コンポーネント | 仕様 |
|---|---|
| `RatingInput` | 5つのボタン横並び。1タップ選択 |
| `WorkTypeSelector` | ボタン列 |
| `TaskSelector` | 検索可能・直近使用順 |
| `LocationSelector` | 4ボタン + Other時のみテキスト |
| `CountStepper` | 会議件数・中断回数 |

**`RatingInput` をドロップダウンやスライダーにしないこと。** 作業後は5項目の評価がある。1項目5秒を超えると60秒制約を満たせない（[UC 6章](03-use-cases.md)）。

---

### T-14〜T-19 Frontend: 画面

| タスク | 画面 | 注意点 |
|---|---|---|
| T-14 | S-02 タスク | 完了ボタンを作らない。アーカイブのみ |
| T-15 | S-04 作業開始 | 入力7項目。既定値を積極的に使う。30秒を計測する |
| T-16 | S-05 作業中 | 経過時間は `startedAt` から毎回再計算（[技術設計 §3.11](08-technical-design.md)） |
| T-17 | S-06 成果評価 | **スキップ導線を作らない**。5指標すべて必須 |
| T-18 | S-08 履歴 | PerformanceResultのみ編集可。時刻・PreWorkStateは編集不可 |
| T-19 | S-01 ホーム | 縦切り1では「進行中」「作業を開始」「今日の記録」のみ |

**T-15 完了時に、実際に自分で入力して30秒以内に収まるか計測すること。** 超える場合は項目を減らす。これはMVP成功条件S3（[PRD §9.1](01-product-requirements.md)）に直結する。

---

## 3. 縦切り2: 日次コンディションと予定

**完了条件: 日次コンディションを記録でき、予定を立て、実行または未実行を記録できる。**

### T-20 DailyCondition

**注意**: **当日のみ記録可**（DC-4）。過去日は422。この検証はアプリケーション層でのみ担保され、DB制約がない（[DB設計 §3](06-database-design.md)）。テストを必ず書く（T-08）。

### T-21 PlannedWork / NonExecutionRecord

**注意**: 実行と未実行の排他（PW-4/PW-5）。`NonExecutionRecorder` ドメインサービスで担保する。テスト T-09 / T-10。

### T-22〜T-25 Application / Api

- `PUT /api/daily-conditions/{date}` は upsert（200 or 201）
- `PUT /api/planned-works/{id}/skip` も upsert。WorkSession紐づき時は409
- `GET /api/home/today` は集約エンドポイント。`prompts` をサーバーで判定

### T-26〜T-29 Frontend

**T-29 の注意**: 作業開始時に当日のDailyConditionが未記録なら警告を出すが、**開始を妨げない**（[UC-04](03-use-cases.md)の例外表）。記録の摩擦を増やすと、記録自体が続かなくなる。

---

## 4. 縦切り3: 分析

**完了条件: 6種の分析が表示され、サンプル不足が正しく扱われる。**

### T-30 `v_completed_sessions` ビュー

[DB設計 §4.1](06-database-design.md) のSQLをマイグレーションに手書きする。

**このビューが母集団定義（`status = 'Completed'`）と JST変換を集約している。** 各クエリで個別に書かない。

### T-31 AnalyticsQuery

[DB設計 §4.2〜4.6](06-database-design.md) のSQLをそのまま実装する。

**`HAVING COUNT(*) >= 5` を書かないこと。** 件数ごと返し、判定はApplication層で行う（[技術設計 §3.7](08-technical-design.md)）。

### T-32 AnalyticsService

| 責務 | 内容 |
|---|---|
| サンプル数判定 | `n >= MinSampleSize` → `sufficient` |
| 平均値のNULL化 | `sufficient = false` なら `avgXxx = null` |
| 並び順 | byWorkType: 平均降順 / byTimeBand: 時系列 / byDayOfWeek: 月〜日 / bySleepBand: 睡眠昇順 |
| 丸め | 平均は小数第2位、割合は小数第1位 |

**`sufficient = false` のとき平均値を `null` にすること。** 数値を返すと、クライアントの実装ミスで表示される（[API設計 §2.20](07-api-design.md)）。

### T-34 分析の統合テスト

| # | テスト |
|---|---|
| 1 | 既知のデータセットを投入し、6種の集計結果が期待値と一致する |
| 2 | サンプル4件の区分は `sufficient = false`、`avg = null` |
| 3 | サンプル5件の区分は `sufficient = true` |
| 4 | Abandoned のセッションが A-01〜A-05 の母集団から除外される |
| 5 | Abandoned のセッションが A-06 の「実行済み」には含まれる |
| 6 | DailyCondition未記録の日のセッションが A-05 から除外され、`excludedSessionCount` に計上される |
| 7 | 22:00開始・翌01:00終了のセッションが開始日側に集計される |
| 8 | **C#の `JstCalendar.ToTimeBand` と SQLの `time_band` が24時間分一致する** |
| 9 | A-06 の `executed + nonExecuted + unprocessed = totalPlanned` |

**9 が破れる場合、PW-4/PW-5の排他が守られていない。** 実行率の合計が100%を超えるバグとして表面化する。

### T-36 S-09 分析画面

**注意**（[技術設計 §8](08-technical-design.md)）

- 件数（n）を必ず併記する
- サンプル不足の区分は「サンプル不足（n=3）」と表示し、バーを描画しない
- **結論文を出さない**
- 合成スコアを表示しない
- 疲労度・ストレスは「高いほど悪い」。値を反転して表示しない（[用語集 §2.2](02-glossary.md)）

---

## 5. 縦切り4: 仕上げ

### T-37 整合性検証クエリのテスト化

[DB設計 §7](06-database-design.md) の5クエリを統合テストのアサーションにする。

### T-40 記録コストの実測と調整

実運用2週間後に行う。

| 計測項目 | 目標 | 未達時の対応 |
|---|---|---|
| 作業開始の入力時間 | 30秒以内 | **入力項目を減らす**。分析仕様から不要な項目を特定する |
| 作業終了の入力時間 | 60秒以内 | 同上 |
| 日次コンディションの入力時間 | 30秒以内 | 同上 |
| 記録の継続率 | 欠測4日以内/28日 | 記録フローそのものを見直す |

**未達時に「頑張って記録する」で対処しないこと。** [PRD §9.3](01-product-requirements.md) の通り、記録が続かないのは設計の失敗である。

---

## 6. Claudeへの指示テンプレート

各タスクを実装させる際のプロンプト。

```text
添付した設計書（docs/01〜09）を前提に、以下を実装してください。

対象: T-04 Domain: WorkSession集約

参照すべき設計書:
- docs/05-domain-design.md §4.6〜4.9（エンティティ定義・不変条件）
- docs/02-glossary.md §2（評価尺度）
- docs/08-technical-design.md §3.3（カプセル化の方針）

対象外:
- 永続化（T-05で行う）
- API（T-10で行う）
- 分析

制約:
- 設計書にない仕様を独断で追加しないでください
- PreWorkState / WorkContext に setter や更新メソッドを作らないでください
- docs/08-technical-design.md §8 の禁止事項を確認してください

最初に実装計画と変更対象ファイルを提示してください。
設計書間に不整合がある場合は、実装前に指摘してください。
```

**「対象外」を明示すること。** 指定しないと、関連する層まで一度に実装され、レビューが困難になる。

**「設計書にない仕様を独断で追加しない」を毎回入れること。** 特に、一般的なタスク管理アプリの機能（完了フラグ、優先度、期限）は「あって当然」と判断されて追加されやすい。

---

## 7. 各縦切り完了時の確認

### 縦切り1

| # | 確認項目 |
|---|---|
| 1 | タスク登録 → 作業開始 → 終了 → 評価 → 履歴表示 が通しで動く |
| 2 | 作業開始の入力が30秒以内で完了する |
| 3 | 進行中セッションがある状態で開始しようとすると409 |
| 4 | ブラウザをリロードしても経過時間が正しい |
| 5 | TaskItem に完了の概念が存在しない |
| 6 | PreWorkState を更新するAPIが存在しない |
| 7 | **実運用を開始できる** |

### 縦切り2

| # | 確認項目 |
|---|---|
| 1 | 日次コンディションが記録でき、過去日は422 |
| 2 | 予定を立て、実行または未実行を記録できる |
| 3 | 実行済みの予定に未実行記録を付けようとすると409 |
| 4 | [DB設計 §7](06-database-design.md) の検証クエリが全件0 |

### 縦切り3

| # | 確認項目 |
|---|---|
| 1 | 6種の分析が表示される |
| 2 | サンプル不足が正しく表示され、数値が出ない |
| 3 | 結論文が表示されない |
| 4 | C#とSQLのTimeBand判定が一致する |
| 5 | A-06 の3状態の合計が予定件数と一致する |

### MVP全体（実運用4週間後）

[PRD §9](01-product-requirements.md) の成功条件で判定する。

| # | 条件 |
|---|---|
| S1 | 記録が28日継続（欠測4日以内） |
| S2 | WorkSession 50件以上 |
| S3 | 作業開始の入力が平均30秒以内 |
| S4 | 分析6種すべてがサンプル不足なく表示される |
| S5 | パフォーマンスに影響する条件を3つ以上言語化できる |
| S6 | S5のうち1つ以上で実際に行動を変える判断ができる |
| S7 | 体感と分析結果が食い違う点が1つ以上見つかる |

**S5〜S7が満たされない場合、機能追加ではなく観測対象の見直しを行う**（[PRD §9.3](01-product-requirements.md)）。
