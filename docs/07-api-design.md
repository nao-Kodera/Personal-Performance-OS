# API設計書

- ドキュメントID: API-007
- ステータス: ドラフト
- 最終更新: 2026-08-05
- 前提: [05-domain-design.md](05-domain-design.md) / [06-database-design.md](06-database-design.md) / [03-use-cases.md](03-use-cases.md)

---

## 0. 共通仕様

| 項目 | 内容 |
|---|---|
| ベースURL | `/api` |
| 形式 | JSON（`application/json; charset=utf-8`） |
| プロパティ命名 | camelCase |
| 日時の送受信 | ISO 8601・UTC・`Z` 付き（例: `2026-08-04T04:12:33Z`） |
| 日付の送受信 | `YYYY-MM-DD`（**JST基準の論理日**） |
| 認証 | なし（[PRD §7.3](01-product-requirements.md)） |
| ページング | MVPでは実装しない。件数が問題になる規模ではない |

### 0.1 日時に関する厳守事項

- **リクエストで開始時刻・終了時刻を受け取らない。** サーバー時刻を使う（[ドメイン設計 WS-8](05-domain-design.md)）
- **レスポンスの日時はUTC。** JST変換はクライアントで行う
- **例外は「日付」型のフィールド**（`targetDate` など）。これはJST基準の論理日であり、変換しない

この区別を誤ると、深夜作業の集計が1日ずれる。

### 0.2 エラーレスポンス

RFC 7807 (Problem Details) に準拠する。

```json
{
  "type": "https://performance-os.local/errors/validation",
  "title": "入力値が不正です",
  "status": 400,
  "detail": "focusLevel は 1 から 5 の範囲で指定してください",
  "errors": {
    "focusLevel": ["1 から 5 の範囲で指定してください"]
  }
}
```

**エラー種別**

| status | type | 用途 |
|---|---|---|
| 400 | `validation` | リクエスト形式・値の範囲エラー |
| 404 | `not-found` | 指定IDのリソースが存在しない |
| 409 | `conflict` | 状態遷移の不整合・一意制約違反 |
| 422 | `domain-rule` | ドメイン不変条件の違反 |
| 500 | `internal` | 想定外のエラー |

**409 と 422 の使い分け**

- **409 Conflict** — 現在の状態と操作が矛盾する。時間をおけば成功しうる、または他の操作が先に必要
  - 例: 進行中セッションが既に存在する状態で開始しようとした
  - 例: 終了済みセッションを再度終了しようとした
- **422 Unprocessable Entity** — 値そのものがドメイン規則に反する。何度試しても成功しない
  - 例: アーカイブ済みのTaskItemを指定した
  - 例: 過去日のDailyConditionを記録しようとした

### 0.3 共通の列挙値

レスポンスの列挙値は、[用語集](02-glossary.md)の英語名をそのまま返す。日本語表示名への変換はクライアントで行う。

```text
status        "InProgress" | "Completed" | "Abandoned"
workLocation  "Home" | "Office" | "Cafe" | "Other"
reason        "NoTime" | "Interrupted" | "PoorCondition"
              | "Deprioritized" | "Overplanned" | "Other"
timeBand      "EarlyMorning" | "Morning" | "Afternoon" | "Evening"
sleepBand     "Under6" | "From6To7" | "From7To8" | "Over8"
```

---

## 1. エンドポイント一覧

| メソッド | パス | ユースケース |
|---|---|---|
| GET | `/api/work-types` | 共通 |
| POST | `/api/work-types` | 共通 |
| PUT | `/api/work-types/{id}` | 共通 |
| GET | `/api/tasks` | UC-01 |
| POST | `/api/tasks` | UC-01 |
| PUT | `/api/tasks/{id}` | UC-01 |
| POST | `/api/tasks/{id}/archive` | UC-01 |
| POST | `/api/tasks/{id}/unarchive` | UC-01 |
| GET | `/api/daily-conditions/{date}` | UC-02 |
| PUT | `/api/daily-conditions/{date}` | UC-02 |
| GET | `/api/planned-works` | UC-03 |
| POST | `/api/planned-works` | UC-03 |
| PUT | `/api/planned-works/{id}/skip` | UC-07 |
| GET | `/api/work-sessions/active` | UC-04/05 |
| POST | `/api/work-sessions/start` | UC-04 |
| POST | `/api/work-sessions/{id}/finish` | UC-05 |
| POST | `/api/work-sessions/{id}/abandon` | UC-06 |
| PUT | `/api/work-sessions/{id}/result` | UC-08 |
| GET | `/api/work-sessions` | UC-08 |
| GET | `/api/home/today` | S-01 |
| GET | `/api/analytics/summary` | UC-09 |

### 1.1 意図的に用意しないエンドポイント

| 用意しないもの | 理由 |
|---|---|
| `PUT /api/work-sessions/{id}/pre-work-state` | PreWorkStateは不変（[ドメイン設計 PS-2](05-domain-design.md)） |
| `PUT /api/work-sessions/{id}/context` | WorkContextは不変（WC-3） |
| `DELETE /api/work-sessions/{id}` | 記録は削除しない |
| `DELETE /api/tasks/{id}` | アーカイブで代替 |
| `DELETE /api/work-types/{id}` | 無効化で代替 |
| `POST /api/work-sessions`（時刻指定での作成） | 後から記録を作れない（WS-8） |
| `GET /api/work-sessions/{id}` | 進行中は `/active`、過去は一覧から得られる。単体で引く用途がない |
| `PUT /api/daily-conditions`（過去日） | 当日のみ（DC-4）。パスの日付が当日でなければ422 |

**PreWorkStateの更新APIを用意しないことは、本プロダクトの根幹に関わる設計判断である。** 実装中に「編集できた方が便利」という理由で追加しないこと。理由は[ドメイン設計 §4.7](05-domain-design.md)を参照。

---

## 2. エンドポイント詳細

### 2.1 GET /api/work-types

作業タイプの一覧を取得する。

**クエリパラメータ**

| 名前 | 型 | 必須 | 既定 | 説明 |
|---|---|---|---|---|
| includeInactive | boolean | — | false | 無効なものを含めるか |

**レスポンス 200**

```json
[
  { "id": 1, "name": "実装",         "displayOrder": 10, "isActive": true },
  { "id": 2, "name": "設計",         "displayOrder": 20, "isActive": true },
  { "id": 3, "name": "ドキュメント",  "displayOrder": 30, "isActive": true }
]
```

`displayOrder` の昇順で返す。

---

### 2.2 POST /api/work-types

**リクエスト**

```json
{ "name": "レビュー", "displayOrder": 60 }
```

**バリデーション**

| 項目 | 規則 |
|---|---|
| name | 必須・1〜50文字・空白のみ不可・大文字小文字を区別せず一意 |
| displayOrder | 任意・0以上の整数。省略時は既存の最大値 + 10 |

**レスポンス 201** — 作成された WorkType

**エラー**

| status | 条件 |
|---|---|
| 400 | name が空、または50文字超 |
| 409 | 同名（大小無視）のWorkTypeが既に存在する |

**補足**

WorkTypeを7個以上に増やすと、分析のサンプル数が不足する（[分析仕様 §3.2](04-analytics-spec.md)）。APIでは制限しないが、クライアント側で有効なWorkTypeが6個を超える場合に警告を表示する。

---

### 2.3 PUT /api/work-types/{id}

**リクエスト**

```json
{ "name": "レビュー", "displayOrder": 60, "isActive": true }
```

**レスポンス 200** — 更新後の WorkType

**エラー**

| status | 条件 |
|---|---|
| 404 | 指定IDが存在しない |
| 409 | 同名のWorkTypeが既に存在する |

**補足**

`isActive: false` にしても、既存のWorkSessionの分類は変わらない。新規の選択肢から外れるだけである。

---

### 2.4 GET /api/tasks

**クエリパラメータ**

| 名前 | 型 | 必須 | 既定 | 説明 |
|---|---|---|---|---|
| includeArchived | boolean | — | false | アーカイブ済みを含むか |
| keyword | string | — | — | タイトルの部分一致 |
| sort | string | — | `recent` | `recent`（直近使用順） / `updated`（更新順） |

**レスポンス 200**

```json
[
  {
    "id": 12,
    "title": "認証方式の検討",
    "defaultWorkTypeId": 2,
    "defaultWorkTypeName": "設計",
    "note": null,
    "isArchived": false,
    "lastUsedAt": "2026-08-03T00:12:00Z",
    "sessionCount": 4,
    "createdAt": "2026-07-28T01:00:00Z",
    "updatedAt": "2026-07-28T01:00:00Z"
  }
]
```

**`sessionCount` と `lastUsedAt` を含める理由**

作業開始画面（S-04）のタスク選択で、直近使ったタスクを上に出すため。[UC-04](03-use-cases.md) の30秒制約を満たすには、タスク選択が速い必要がある。

`lastUsedAt` は、そのTaskItemに紐づくWorkSessionの最新 `started_at`。一度も使われていない場合は `null`。

---

### 2.5 POST /api/tasks

**リクエスト**

```json
{
  "title": "認証方式の検討",
  "defaultWorkTypeId": 2,
  "note": null
}
```

**バリデーション**

| 項目 | 規則 |
|---|---|
| title | 必須・1〜200文字・空白のみ不可 |
| defaultWorkTypeId | 必須・存在する有効なWorkType |
| note | 任意・2000文字以内 |

**レスポンス 201**

```json
{
  "id": 12,
  "title": "認証方式の検討",
  "defaultWorkTypeId": 2,
  "note": null,
  "isArchived": false,
  "createdAt": "2026-08-04T00:00:00Z",
  "updatedAt": "2026-08-04T00:00:00Z"
}
```

**一覧（§2.4）と形が違う。** 集計値（`lastUsedAt` / `sessionCount`）と `defaultWorkTypeName` を含まない。集計値は射影クエリでしか得られず、更新のたびに追加の問い合わせが必要になる。作業タイプ名は、クライアントが選択肢として保持している一覧から ID で解決できる。

**エラー**

| status | 条件 |
|---|---|
| 400 | title が空/200文字超、note が2000文字超 |
| 422 | defaultWorkTypeId が存在しない、または `isActive = false` |

**補足**

**同名のTaskItemが存在してもエラーにしない**（[ドメイン設計 TI-3](05-domain-design.md)）。同じ名前の作業を別機会に行うのは正常である。

---

### 2.6 PUT /api/tasks/{id}

**リクエスト** — POST と同じ形

**レスポンス 200** — POST と同じ形

**エラー**

| status | 条件 |
|---|---|
| 404 | 指定IDが存在しない |
| 400/422 | POST と同じ |

---

### 2.7 POST /api/tasks/{id}/archive ／ /unarchive

**リクエスト** — ボディなし

**レスポンス 200** — §2.5 と同じ形

**エラー**

| status | 条件 |
|---|---|
| 404 | 指定IDが存在しない |
| 409 | archive時、そのTaskItemで進行中のWorkSessionが存在する |

**補足**

これは「完了」ではない。TaskItemに完了の概念はない（[ドメイン設計 TI-4](05-domain-design.md)）。パス名に `complete` を使わないこと。

---

### 2.8 GET /api/daily-conditions/{date}

**パスパラメータ**

| 名前 | 形式 | 説明 |
|---|---|---|
| date | `YYYY-MM-DD` | **JST基準の日付** |

**レスポンス 200**

```json
{
  "targetDate": "2026-08-04",
  "sleepMinutes": 435,
  "sleepBand": "From7To8",
  "physicalCondition": 4,
  "moodLevel": 4,
  "stressLevel": 2,
  "note": null,
  "recordedAt": "2026-08-03T23:10:00Z",
  "updatedAt": "2026-08-03T23:10:00Z"
}
```

**レスポンス 404** — その日の記録が存在しない

**補足**

- `sleepBand` はサーバーで導出して返す。クライアントで区分判定を実装しない（区分定義の重複を防ぐため）
- 404 は異常ではない。未記録は正常な状態である。クライアントは404を「未記録」として扱い、エラー表示しない

---

### 2.9 PUT /api/daily-conditions/{date}

記録と更新を兼ねる（upsert）。

**リクエスト**

```json
{
  "sleepMinutes": 435,
  "physicalCondition": 4,
  "moodLevel": 4,
  "stressLevel": 2,
  "note": null
}
```

**バリデーション**

| 項目 | 規則 |
|---|---|
| date（パス） | **当日（JST）であること** |
| sleepMinutes | 必須・15〜1440・15の倍数 |
| physicalCondition / moodLevel / stressLevel | 必須・1〜5 |
| note | 任意・1000文字以内 |

**レスポンス**

| status | 条件 |
|---|---|
| 200 | 既存レコードを更新した |
| 201 | 新規作成した |

**エラー**

| status | 条件 |
|---|---|
| 400 | sleepMinutes が範囲外/15の倍数でない、評価値が1〜5でない |
| 422 | パスの日付が当日（JST）でない |

**PUT（upsert）にした理由**

`target_date` が自然キーであり、1日1件（[ドメイン設計 DC-1](05-domain-design.md)）。POST/PUTを分けると、クライアントが「既に記録済みか」を先に問い合わせる必要があり、[UC-02](03-use-cases.md) の30秒制約を圧迫する。

**当日以外を422にする理由**

過去日の遡り入力は記憶に基づくため精度が低く、分析A-05を汚染する（[ドメイン設計 DC-4](05-domain-design.md)）。**「今日の分を昨日入れ忘れた」場合は記録しない**のが正しい。欠損として扱う。

「JSTの当日」の判定はサーバー時刻で行う。クライアントの時計を信用しない。

---

### 2.10 GET /api/planned-works

**クエリパラメータ**

| 名前 | 型 | 必須 | 説明 |
|---|---|---|---|
| date | `YYYY-MM-DD` | いずれか必須 | 単日指定（JST） |
| from / to | `YYYY-MM-DD` | いずれか必須 | 期間指定（JST） |

**レスポンス 200**

```json
[
  {
    "id": 88,
    "targetDate": "2026-08-04",
    "taskItemId": 12,
    "taskTitle": "認証方式の検討",
    "workTypeId": 2,
    "workTypeName": "設計",
    "plannedTimeBand": "Morning",
    "plannedMinutes": 90,
    "executionState": "Executed",
    "workSessionId": 301,
    "nonExecution": null
  },
  {
    "id": 89,
    "targetDate": "2026-08-04",
    "taskItemId": 15,
    "taskTitle": "議事録の作成",
    "workTypeId": 3,
    "workTypeName": "ドキュメント",
    "plannedTimeBand": "Afternoon",
    "plannedMinutes": 30,
    "executionState": "NotExecuted",
    "workSessionId": null,
    "nonExecution": { "reason": "NoTime", "note": null }
  },
  {
    "id": 90,
    "targetDate": "2026-08-04",
    "taskItemId": 20,
    "taskTitle": "APIの実装",
    "workTypeId": 1,
    "workTypeName": "実装",
    "plannedTimeBand": null,
    "plannedMinutes": null,
    "executionState": "Unprocessed",
    "workSessionId": null,
    "nonExecution": null
  }
]
```

**`executionState`**

| 値 | 意味 |
|---|---|
| `Executed` | WorkSessionが紐づいている |
| `NotExecuted` | NonExecutionRecordが存在する |
| `Unprocessed` | どちらもない |

**`executionState` をサーバーで判定して返す理由**

クライアントが `workSessionId` と `nonExecution` の有無から判定すると、判定ロジックが分析側（[分析仕様 A-06](04-analytics-spec.md)）と重複し、ずれる。判定はサーバーの1箇所に置く。

---

### 2.11 POST /api/planned-works

**リクエスト**

```json
{
  "targetDate": "2026-08-04",
  "taskItemId": 12,
  "workTypeId": 2,
  "plannedTimeBand": "Morning",
  "plannedMinutes": 90
}
```

**バリデーション**

| 項目 | 規則 |
|---|---|
| targetDate | 必須・**当日（JST）であること** |
| taskItemId | 必須・存在し、アーカイブされていないTaskItem |
| workTypeId | 必須・存在する有効なWorkType |
| plannedTimeBand | 任意・列挙値 |
| plannedMinutes | 任意・15〜1440・15の倍数 |

**レスポンス 201** — 作成された PlannedWork

**エラー**

| status | 条件 |
|---|---|
| 400 | plannedMinutes が範囲外/15の倍数でない |
| 422 | targetDate が当日でない、taskItemId がアーカイブ済み、workTypeId が無効 |

**補足**

同一TaskItem・同一日の重複を許す（[ドメイン設計 PW-3](05-domain-design.md)）。409を返さない。

---

### 2.12 PUT /api/planned-works/{id}/skip

未実行を記録する（UC-07）。記録と更新を兼ねる。

**リクエスト**

```json
{ "reason": "NoTime", "note": "会議が長引いた" }
```

**バリデーション**

| 項目 | 規則 |
|---|---|
| reason | 必須・列挙値6種のいずれか |
| note | 任意・1000文字以内 |

**レスポンス**

| status | 条件 |
|---|---|
| 200 | 既存のNonExecutionRecordを更新した |
| 201 | 新規作成した |

**エラー**

| status | 条件 |
|---|---|
| 404 | 指定IDのPlannedWorkが存在しない |
| 400 | reason が列挙値でない |
| **409** | **そのPlannedWorkに既にWorkSessionが紐づいている** |

**409を返す条件が重要である。** [ドメイン設計 PW-4/PW-5](05-domain-design.md) の排他制約であり、これが破れると実行率の分子と未実行件数が二重計上され、A-06 の合計が100%を超える。

---

### 2.13 GET /api/work-sessions/active

進行中のセッションを取得する。

**レスポンス 200**

```json
{
  "id": 301,
  "taskItemId": 12,
  "taskTitle": "認証方式の検討",
  "workTypeId": 2,
  "workTypeName": "設計",
  "plannedWorkId": 88,
  "startedAt": "2026-08-04T00:12:00Z",
  "finishedAt": null,
  "status": "InProgress",
  "durationMinutes": null,
  "interruptionCount": 0,
  "abandonNote": null,
  "timeBand": "Morning",
  "preWorkState": {
    "fatigueLevel": 2,
    "expectedFocusLevel": 4,
    "moodLevel": 4,
    "recordedAt": "2026-08-04T00:12:00Z"
  },
  "workContext": {
    "workLocation": "Home",
    "locationNote": null,
    "meetingCount": 1,
    "interruptionExpected": false,
    "recordedAt": "2026-08-04T00:12:00Z"
  },
  "result": null,
  "fatigueDelta": null,
  "focusGap": null,
  "warnings": []
}
```

**これが WorkSession を返すすべてのエンドポイント（§2.14〜§2.17）の共通形である。** 未終了・未評価の項目は省略せず `null` を返す。クライアントが項目の有無で分岐せずに済むため。

**レスポンス 204** — 進行中のセッションが存在しない

**補足**

- `startedAt` はUTC。クライアントは現在時刻との差から経過時間を算出する
- **クライアント側のタイマーを信用源にしないこと。** ブラウザを閉じても正しい経過時間を表示する必要がある（[UC 5章](03-use-cases.md)）
- 進行中は最大1件（[ドメイン設計 WS-9](05-domain-design.md)）のため、配列ではなく単一オブジェクトを返す

---

### 2.14 POST /api/work-sessions/start ★中心

**リクエスト**

```json
{
  "taskItemId": 12,
  "workTypeId": 2,
  "plannedWorkId": 88,
  "preWorkState": {
    "fatigueLevel": 2,
    "expectedFocusLevel": 4,
    "moodLevel": 4
  },
  "workContext": {
    "workLocation": "Home",
    "locationNote": null,
    "meetingCount": 1,
    "interruptionExpected": false
  }
}
```

**バリデーション**

| 項目 | 規則 |
|---|---|
| taskItemId | 必須・存在し、アーカイブされていない |
| workTypeId | 必須・存在し、有効 |
| plannedWorkId | 任意・存在し、まだ実行も未実行記録もされていない |
| preWorkState.* | 3項目すべて必須・1〜5 |
| workContext.workLocation | 必須・列挙値 |
| workContext.locationNote | `workLocation = "Other"` のときのみ設定可・200文字以内 |
| workContext.meetingCount | 必須・0以上 |
| workContext.interruptionExpected | 必須・boolean |

**処理概要**

```text
1. 進行中のWorkSessionが存在しないことを確認     → 存在すれば 409
2. TaskItem / WorkType の妥当性を確認            → 不正なら 422
3. plannedWorkId 指定時、その予定が未処理であることを確認 → 処理済みなら 409
4. startedAt = サーバー現在時刻（UTC）
5. WorkSession / PreWorkState / WorkContext を
   同一トランザクションで挿入
6. 作成されたセッションを返す
```

**レスポンス 201** — `/api/work-sessions/active` と同じ形

**エラー**

| status | 条件 |
|---|---|
| 400 | 評価値が1〜5でない、locationNote が Other 以外で設定されている |
| **409** | **進行中のWorkSessionが既に存在する** |
| 409 | plannedWorkId が既に実行済み、または未実行記録がある |
| 422 | taskItemId がアーカイブ済み、workTypeId が無効 |

**リクエストに `startedAt` を含めない理由**

時刻はサーバーが決める（[ドメイン設計 WS-8](05-domain-design.md)）。クライアントから受け取ると、記憶に基づく後付けの記録が可能になり、実作業時間の分析が事実に基づかなくなる。

**PreWorkState / WorkContext を同一リクエストで受け取る理由**

分けると、状態だけ記録して開始しなかったケース、開始したが状態がないケースが生じ、[ドメイン設計 WS-1](05-domain-design.md) が破れる。1トランザクションで3テーブルに挿入する。

**409（進行中が既に存在）が返った場合のクライアント挙動**

エラー表示ではなく、進行中セッション画面（S-05）へ誘導する。これは異常ではなく、複数タブや戻る操作で起きうる正常な競合である。

---

### 2.15 POST /api/work-sessions/{id}/finish

**リクエスト**

```json
{
  "interruptionCount": 1,
  "result": {
    "focusLevel": 4,
    "outputLevel": 4,
    "accuracyLevel": 3,
    "satisfactionLevel": 4,
    "fatigueAfter": 4,
    "note": null
  }
}
```

**バリデーション**

| 項目 | 規則 |
|---|---|
| interruptionCount | 必須・0以上の整数 |
| result.* の5指標 | **すべて必須**・1〜5 |
| result.note | 任意・2000文字以内 |

**処理概要**

```text
1. 対象セッションを取得                      → なければ 404
2. status = InProgress であることを確認       → 違えば 409
3. finishedAt = サーバー現在時刻（UTC）
4. finishedAt > startedAt を確認             → 違えば 409（時刻の巻き戻り）
5. status = Completed に更新、PerformanceResult を挿入
   （同一トランザクション）
6. 更新後のセッションを返す
```

**レスポンス 200** — `/api/work-sessions/active`（§2.13）と同じ形に `finishedAt` / `durationMinutes` / `result` / `fatigueDelta` / `focusGap` / `warnings` が入る。**以下は差分の抜粋であり、`taskTitle` / `workTypeName` / `timeBand` / `preWorkState` / `workContext` も同様に含まれる。**

```json
{
  "id": 301,
  "status": "Completed",
  "startedAt": "2026-08-04T00:12:00Z",
  "finishedAt": "2026-08-04T01:45:00Z",
  "durationMinutes": 93,
  "interruptionCount": 1,
  "result": {
    "focusLevel": 4,
    "outputLevel": 4,
    "accuracyLevel": 3,
    "satisfactionLevel": 4,
    "fatigueAfter": 4,
    "note": null,
    "recordedAt": "2026-08-04T01:45:00Z",
    "updatedAt": "2026-08-04T01:45:00Z"
  },
  "fatigueDelta": 2,
  "focusGap": 0,
  "warnings": []
}
```

**`warnings`**

保存を妨げないが、クライアントに表示させる情報。

| 値 | 条件 |
|---|---|
| `LongSession` | 実作業時間が8時間を超えている |
| `VeryShortSession` | 実作業時間が1分未満 |
| `MissingDailyCondition` | 所属日のDailyConditionが未記録 |

**エラー**

| status | 条件 |
|---|---|
| 400 | 5指標のいずれかが欠落/範囲外 |
| 404 | 指定IDが存在しない |
| 409 | status が InProgress でない（既に終了済み） |

**5指標を必須にしている理由**

`status = Completed` なら PerformanceResult が必ず存在する（[ドメイン設計 WS-3](05-domain-design.md)）。部分的な評価を許すと、分析の母集団にNULLが混ざり、集計側で毎回NULLチェックが必要になる。

**終了と評価を1エンドポイントにしている理由**

分けると「終了したが評価していない」状態が生じ、WS-3が破れる。[UC-05](03-use-cases.md) でスキップ導線を設けていないのと同じ理由である。

**`fatigueDelta` / `focusGap` を返す理由**

保存はしないが（[分析仕様 §5](04-analytics-spec.md)）、終了直後に表示する。クライアントで計算させると、PreWorkStateの値をクライアントが保持する必要があり、リロードで失われる。

---

### 2.16 POST /api/work-sessions/{id}/abandon

**リクエスト**

```json
{ "note": "会議に呼ばれて中断" }
```

**バリデーション**

| 項目 | 規則 |
|---|---|
| note | 任意・1000文字以内 |

**処理概要**

```text
1. 対象セッションを取得                 → なければ 404
2. status = InProgress を確認           → 違えば 409
3. finishedAt = サーバー現在時刻
4. status = Abandoned に更新
   PerformanceResult は作成しない
```

**レスポンス 200** — §2.13 と同じ形。以下は差分の抜粋。

```json
{
  "id": 302,
  "status": "Abandoned",
  "startedAt": "2026-08-04T05:00:00Z",
  "finishedAt": "2026-08-04T05:08:00Z",
  "durationMinutes": 8,
  "abandonNote": "会議に呼ばれて中断",
  "result": null
}
```

**エラー**

| status | 条件 |
|---|---|
| 404 | 指定IDが存在しない |
| 409 | status が InProgress でない |

**補足**

Abandonedは分析の母集団から除外されるが（[分析仕様 §2.1](04-analytics-spec.md)）、実行率A-06では「着手した」として扱われる。この非対称は意図的である。

---

### 2.17 PUT /api/work-sessions/{id}/result

成果評価を訂正する（UC-08）。

**リクエスト**

```json
{
  "interruptionCount": 2,
  "result": {
    "focusLevel": 3,
    "outputLevel": 4,
    "accuracyLevel": 3,
    "satisfactionLevel": 3,
    "fatigueAfter": 4,
    "note": "後から見ると集中はしていなかった"
  }
}
```

**処理概要**

```text
1. 対象セッションを取得              → なければ 404
2. status = Completed を確認         → 違えば 409
3. PerformanceResult を更新
   recorded_at は変更しない
   updated_at のみ更新
4. interruptionCount を更新
```

**レスポンス 200** — finish と同じ形

**エラー**

| status | 条件 |
|---|---|
| 404 | 指定IDが存在しない |
| 409 | status が Completed でない |
| 400 | 5指標のいずれかが欠落/範囲外 |

**`recordedAt` を変更しない理由**

`recordedAt` と `updatedAt` の差により、事後編集されたレコードを識別できる（[ドメイン設計 PR-2](05-domain-design.md)）。分析の信頼性を評価する材料になる。

**このAPIで開始時刻・終了時刻・PreWorkState・WorkContext を更新できないことを確認すること。** リクエストボディに含まれていても無視するのではなく、**未知のプロパティとして400を返す**設定にする（[技術設計](08-technical-design.md)）。

---

### 2.18 GET /api/work-sessions

履歴を取得する（UC-08）。

**クエリパラメータ**

| 名前 | 型 | 必須 | 既定 | 説明 |
|---|---|---|---|---|
| from | `YYYY-MM-DD` | — | 28日前 | 開始日（JST・含む） |
| to | `YYYY-MM-DD` | — | 当日 | 終了日（JST・含む） |
| status | string | — | 全件 | `InProgress` / `Completed` / `Abandoned` |
| taskItemId | number | — | — | タスクで絞り込む |

**レスポンス 200**

日付でグループ化して返す。

```json
[
  {
    "date": "2026-08-04",
    "dayOfWeek": "Tuesday",
    "dailyCondition": {
      "sleepMinutes": 435,
      "sleepBand": "From7To8",
      "physicalCondition": 4,
      "moodLevel": 4,
      "stressLevel": 2,
      "note": null
    },
    "sessions": [
      {
        "id": 301,
        "taskTitle": "認証方式の検討",
        "workTypeName": "設計",
        "startedAt": "2026-08-04T00:12:00Z",
        "finishedAt": "2026-08-04T01:45:00Z",
        "durationMinutes": 93,
        "status": "Completed",
        "interruptionCount": 1,
        "timeBand": "Morning",
        "preWorkState": {
          "fatigueLevel": 2,
          "expectedFocusLevel": 4,
          "moodLevel": 4
        },
        "workContext": {
          "workLocation": "Home",
          "locationNote": null,
          "meetingCount": 1,
          "interruptionExpected": false
        },
        "result": {
          "focusLevel": 4,
          "outputLevel": 4,
          "accuracyLevel": 3,
          "satisfactionLevel": 4,
          "fatigueAfter": 4,
          "note": null,
          "recordedAt": "2026-08-04T01:45:00Z",
          "updatedAt": "2026-08-04T01:45:00Z",
          "isEdited": false
        },
        "fatigueDelta": 2,
        "focusGap": 0
      }
    ],
    "summary": {
      "completedCount": 1,
      "abandonedCount": 0,
      "totalMinutes": 93
    }
  }
]
```

**日付グループのキーは、セッションの `startedAt` をJSTに変換した日付**（[用語集 §4](02-glossary.md)）。深夜をまたぐセッションは開始日側に入る。

**`dailyCondition` が `null` の日がありうる。** 未記録は正常な状態である。

**`isEdited`** は `recordedAt != updatedAt` で判定した値。サーバーで判定して返す。

---

### 2.19 GET /api/home/today

ホーム画面（S-01）用の集約エンドポイント。

**レスポンス 200**

```json
{
  "date": "2026-08-04",
  "dayOfWeek": "Tuesday",
  "activeSession": null,
  "dailyCondition": {
    "sleepMinutes": 435,
    "sleepBand": "From7To8",
    "physicalCondition": 4,
    "moodLevel": 4,
    "stressLevel": 2
  },
  "plannedWorks": [],
  "todaySummary": {
    "completedCount": 2,
    "abandonedCount": 0,
    "totalMinutes": 183
  },
  "prompts": {
    "needsDailyCondition": false,
    "unprocessedPlannedWorkCount": 1
  }
}
```

**`prompts`**

クライアントが表示すべき促しの判定結果。

| 項目 | 意味 |
|---|---|
| `needsDailyCondition` | 当日のDailyConditionが未記録 |
| `unprocessedPlannedWorkCount` | `executionState = Unprocessed` の予定件数 |

**集約エンドポイントにする理由**

ホーム画面は起動時に必ず開かれる。個別に4〜5回リクエストすると表示が遅く、[UC-04](03-use-cases.md) の30秒制約に影響する。1回で返す。

**`prompts` をサーバーで判定する理由**

「18時以降なら未処理予定を促す」といった判定を、日付境界の扱いを含めてサーバー側に置く。クライアントの時計とタイムゾーン設定に依存させない。

---

### 2.20 GET /api/analytics/summary

分析6種をまとめて返す（UC-09）。

**クエリパラメータ**

| 名前 | 型 | 必須 | 既定 | 説明 |
|---|---|---|---|---|
| period | string | — | `28d` | `7d` / `28d` / `90d` / `all` |

**レスポンス 200**

```json
{
  "period": {
    "kind": "28d",
    "from": "2026-07-08",
    "to": "2026-08-04",
    "totalSessions": 52
  },
  "minSampleSize": 5,

  "byWorkType": {
    "overallAvgFocus": 3.42,
    "overallAvgOutput": 3.31,
    "items": [
      { "workTypeId": 2, "workTypeName": "設計",
        "n": 14, "avgFocus": 4.07, "avgOutput": 3.86,
        "totalMinutes": 1240, "sufficient": true },
      { "workTypeId": 1, "workTypeName": "実装",
        "n": 22, "avgFocus": 3.45, "avgOutput": 3.50,
        "totalMinutes": 2010, "sufficient": true },
      { "workTypeId": 5, "workTypeName": "会議",
        "n": 3, "avgFocus": null, "avgOutput": null,
        "totalMinutes": 180, "sufficient": false }
    ]
  },

  "byTimeBand": {
    "items": [
      { "timeBand": "EarlyMorning", "n": 6,  "avgFocus": 4.17, "sufficient": true },
      { "timeBand": "Morning",      "n": 21, "avgFocus": 3.95, "sufficient": true },
      { "timeBand": "Afternoon",    "n": 19, "avgFocus": 2.89, "sufficient": true },
      { "timeBand": "Evening",      "n": 6,  "avgFocus": 3.17, "sufficient": true }
    ]
  },

  "byDayOfWeek": {
    "items": [
      { "dayOfWeek": "Monday",    "n": 11, "avgOutput": 3.55, "sufficient": true },
      { "dayOfWeek": "Saturday",  "n": 0,  "avgOutput": null, "sufficient": false }
    ]
  },

  "bySleepBand": {
    "excludedSessionCount": 12,
    "excludedReason": "MissingDailyCondition",
    "items": [
      { "sleepBand": "Under6",   "n": 8,  "dayCount": 4, "avgOutput": 2.75, "sufficient": true },
      { "sleepBand": "From6To7", "n": 12, "dayCount": 6, "avgOutput": 3.25, "sufficient": true },
      { "sleepBand": "From7To8", "n": 15, "dayCount": 7, "avgOutput": 3.87, "sufficient": true },
      { "sleepBand": "Over8",    "n": 5,  "dayCount": 3, "avgOutput": 3.60, "sufficient": true }
    ]
  },

  "execution": {
    "totalPlanned": 38,
    "executed": 26,
    "nonExecuted": 9,
    "unprocessed": 3,
    "abandonedAmongExecuted": 2,
    "executionRate": 0.684,
    "nonExecutionRate": 0.237,
    "unprocessedRate": 0.079,
    "sufficient": true,
    "plannedDayCount": 19,
    "avgPlannedPerDay": 2.0,
    "unplannedSessionCount": 14,
    "reasons": [
      { "reason": "NoTime",       "n": 4, "rate": 0.444 },
      { "reason": "Overplanned",  "n": 3, "rate": 0.333 },
      { "reason": "PoorCondition","n": 2, "rate": 0.222 }
    ]
  }
}
```

**設計上の要点**

| 項目 | 内容 |
|---|---|
| `sufficient` | 各区分に必ず含める。`n >= minSampleSize` の判定結果 |
| `avgXxx` | `sufficient = false` のとき **`null`** を返す。0や実際の平均値を返さない |
| `n` | `sufficient` に関わらず必ず返す。「サンプル不足（n=3）」を表示するため |
| 並び順 | 配列の順序で表示順を表す。`byTimeBand` は時系列順、`byDayOfWeek` は月〜日、`bySleepBand` は睡眠時間昇順、`byWorkType` は平均値降順 |
| 丸め | サーバーで丸めた値を返す（[分析仕様 §2.5](04-analytics-spec.md)） |

**`sufficient = false` のとき平均値を `null` にする理由**

数値を返すと、クライアント実装のミスで表示されてしまう。少数サンプルの平均が表示されれば、ユーザーは誤った確信を持つ（[分析仕様 §2.3](04-analytics-spec.md)）。データとして存在させない。

**含めてはならないもの**

| 含めないもの | 理由 |
|---|---|
| 相関係数・p値・信頼区間 | [分析仕様 §1.2](04-analytics-spec.md) |
| 合成スコア | [用語集 §6](02-glossary.md) |
| 結論文・推薦文 | 解釈は人間が行う（[UC-09](03-use-cases.md)） |
| 「前期間比」の増減 | MVPの範囲外。サンプル数が足りない |

**1エンドポイントにまとめる理由**

分析画面は6種を同時に表示する。分割すると6リクエストになり、期間変更のたびに全て投げ直すことになる。全体で数十msの集計であり、まとめる方が単純である。

---

## 3. バリデーションの実装方針

| 層 | 担当する検証 | 失敗時 |
|---|---|---|
| リクエストDTO | 型、必須、文字数、数値範囲、列挙値 | 400 |
| アプリケーション層 | 存在確認、状態遷移の可否、当日判定 | 404 / 409 / 422 |
| ドメイン層 | 不変条件（Rating範囲、状態遷移） | 例外 → 422 |
| DB | CHECK制約、一意制約 | 例外 → 409 / 500 |

**同じ検証を複数層で行うことを許容する。** 特にRatingの1〜5は、DTO・値オブジェクト・CHECK制約の3箇所で検証する。冗長だが、どの経路からでも不正値が入らないことを保証する。

**未知のプロパティの扱い**

リクエストボディに定義外のプロパティが含まれる場合、**400を返す**。無視しない。

理由: `PUT /api/work-sessions/{id}/result` に `startedAt` を送っても静かに無視されると、クライアント側の実装ミスに気づけない。エラーにすることで、変更不可の項目を変更しようとしていることが即座に分かる。

---

## 4. トランザクション境界

| 操作 | 同一トランザクションで行うこと |
|---|---|
| `POST /work-sessions/start` | work_sessions + pre_work_states + work_contexts の挿入 |
| `POST /work-sessions/{id}/finish` | work_sessions の更新 + performance_results の挿入 |
| `PUT /daily-conditions/{date}` | daily_conditions の挿入または更新 |
| `PUT /planned-works/{id}/skip` | WorkSession存在確認 + non_execution_records の挿入/更新 |

**start と finish のトランザクションが分割されると、[ドメイン設計 WS-1 / WS-3](05-domain-design.md) が破れる。** 実装時に必ず確認すること。

**`skip` の存在確認をトランザクション内で行う理由**

確認とINSERTの間に、別リクエストがWorkSessionを作成しうる。分離レベルはPostgreSQLの既定（Read Committed）で十分だが、確認とINSERTを同一トランザクションに入れること。厳密には競合しうるが、単一ユーザーのMVPでは実害がない。将来的には `planned_works` の行ロック（`SELECT ... FOR UPDATE`）で対応する。
