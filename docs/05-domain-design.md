# ドメイン設計書

- ドキュメントID: DOMAIN-005
- ステータス: ドラフト
- 最終更新: 2026-08-04
- 前提: [02-glossary.md](02-glossary.md) / [03-use-cases.md](03-use-cases.md) / [04-analytics-spec.md](04-analytics-spec.md)

---

## 0. この文書の役割

本プロダクトの概念モデルを固定する。**最も重要な設計書である。**

ここで決めた集約境界と不変条件が、DB設計・API設計・実装のすべてを規定する。

---

## 1. 設計の中心にある分離

```text
┌──────────────────────────────────────────────────────┐
│  開始前の状態          作業中の事実         作業後の結果   │
│  ─────────────        ──────────────      ──────────── │
│  DailyCondition       WorkSession         PerformanceResult
│  PreWorkState         （時刻・中断回数）
│  WorkContext
│                                                        │
│  結果を知らない        観測された事実        評価
│  時点で確定            （システム記録）      （主観）
│                                                        │
│  = 説明変数            = 事実               = 目的変数    │
└──────────────────────────────────────────────────────┘
```

**この3つを1つのエンティティに統合しないこと。**

統合すると次が失われる。

| 失われるもの | 具体的な影響 |
|---|---|
| 記録タイミングの区別 | 開始前の値が事後に書き換えられても検出できない |
| 説明変数と目的変数の独立性 | 分析が意味を持たなくなる |
| 部分的な状態の表現 | 「開始したが評価していない」が表現できない |
| 編集権限の分離 | PreWorkStateだけを編集不可にできない（[UC-08](03-use-cases.md)） |

物理的にも別テーブルとする（[DB設計](06-database-design.md)）。

---

## 2. 概念モデル全体図

```text
    ┌───────────┐
    │ WorkType  │ マスタ
    └─────┬─────┘
          │ 参照
     ┌────┴──────────────┬──────────────┐
     │                   │              │
┌────▼─────┐      ┌──────▼──────┐  ┌────▼────────┐
│ TaskItem │      │ PlannedWork │  │ WorkSession │
└────┬─────┘      └──────┬──────┘  └─────────────┘
     │  参照              │
     └───────────────────┘
                         │ 0..1
                         │
         ┌───────────────┴───────────────┐
         │                               │
    ┌────▼──────────┐        ┌───────────▼──────────┐
    │ WorkSession   │        │ NonExecutionRecord   │
    │ 【集約ルート】  │        │                      │
    └────┬──────────┘        └──────────────────────┘
         │ 集約内
    ┌────┼────────────────┬──────────────────┐
    │    │                │                  │
┌───▼────────┐  ┌─────────▼───┐  ┌───────────▼────────┐
│PreWorkState│  │ WorkContext │  │ PerformanceResult  │
│    1:1     │  │     1:1     │  │       1:0..1       │
└────────────┘  └─────────────┘  └────────────────────┘


    ┌────────────────┐
    │ DailyCondition │  独立。日付で暗黙結合
    └────────────────┘
```

---

## 3. 集約

### 3.1 集約の一覧

| # | 集約ルート | 集約内エンティティ | 境界の理由 |
|---|---|---|---|
| 1 | **WorkSession** | PreWorkState, WorkContext, PerformanceResult | これら3つはWorkSessionなしに存在意味がなく、常に同時に整合していなければならない |
| 2 | **PlannedWork** | NonExecutionRecord | 未実行記録は予定なしに存在しない |
| 3 | **TaskItem** | （なし） | 独立 |
| 4 | **DailyCondition** | （なし） | 独立。日単位で完結する |
| 5 | **WorkType** | （なし） | マスタ |

### 3.2 集約をまたぐ参照

**集約間はIDで参照する。** オブジェクト参照を持たない。

| 参照元 | 参照先 | 多重度 | 備考 |
|---|---|---|---|
| TaskItem | WorkType | N:1 | 既定値 |
| PlannedWork | TaskItem | N:1 | |
| PlannedWork | WorkType | N:1 | |
| WorkSession | TaskItem | N:1 | |
| WorkSession | WorkType | N:1 | 実績値 |
| WorkSession | PlannedWork | N:0..1 | 予定から開始した場合のみ |
| WorkSession | DailyCondition | 参照なし | **日付で暗黙結合する** |

### 3.3 WorkSession と DailyCondition を FK で結ばない理由

分析A-05はこの2つを結合するが、FKは持たせない。

理由は次のとおり。

1. **記録順序が保証されない** — 日次コンディションを記録せずに作業を開始できる（[UC-04](03-use-cases.md)の例外）。FKにすると開始をブロックするか、NULLを許すことになる
2. **後から日次コンディションを記録した場合** — FKなら既存セッションを遡って更新する必要がある。日付結合なら自動的に反映される
3. **意味的に所有していない** — DailyConditionは「その日の状態」であり、特定のセッションに属さない

結合は分析時に `DATE(started_at AT TIME ZONE 'Asia/Tokyo') = target_date` で行う。

**この判断は、結合キーとしての「JST基準の日付」の定義を全レイヤーで統一することを要求する。** [用語集 §4](02-glossary.md) を厳守すること。

---

## 4. エンティティ定義

### 4.1 WorkType（マスタ）

```text
WorkType
  Id            WorkTypeId       識別子
  Name          string(50)       名称・一意
  DisplayOrder  int              表示順
  IsActive      bool             無効化フラグ
  CreatedAt     DateTimeOffset
  UpdatedAt     DateTimeOffset
```

**不変条件**

| # | 条件 |
|---|---|
| WT-1 | Name は1〜50文字、空白のみ不可 |
| WT-2 | Name は一意（大文字小文字を区別しない） |
| WT-3 | 削除しない。使わなくなったら `IsActive = false` |

**振る舞い**

```text
Rename(newName)      名称変更。過去のWorkSessionの分類も変わる
Deactivate()         無効化。新規選択肢から外れるが既存記録は保持
Activate()           再有効化
```

**設計判断: WorkTypeを削除不可にしている理由**

削除すると過去のWorkSessionの分類が失われ、分析A-01/A-02が破綻する。無効化のみを許す。

**設計判断: 名称変更を許す理由**

分類の呼び方は運用中に変わりうる。IDが同一なら分析の連続性は保たれる。ただし**意味を変える改名（「調査」→「レビュー」）は分析を汚染する**ため、その場合は新規作成して旧を無効化すること。この判断はユーザーに委ねる。

---

### 4.2 TaskItem（集約ルート）

```text
TaskItem
  Id                TaskItemId
  Title             string(200)
  DefaultWorkTypeId WorkTypeId
  Note              string(2000)?
  IsArchived        bool
  CreatedAt         DateTimeOffset
  UpdatedAt         DateTimeOffset
```

**不変条件**

| # | 条件 |
|---|---|
| TI-1 | Title は1〜200文字、空白のみ不可 |
| TI-2 | DefaultWorkTypeId は存在するWorkTypeを指す |
| TI-3 | Title の重複を**許す**（同名の作業を別機会に行うのは正常） |
| TI-4 | **完了状態を持たない** |

**振る舞い**

```text
Create(title, defaultWorkTypeId, note)
Update(title, defaultWorkTypeId, note)
Archive()
Unarchive()
```

**設計判断: 完了状態を持たない理由**

[PRD §12](01-product-requirements.md) の通り、本プロダクトはタスク消化を目的としない。完了フラグを持つと、UIに完了率が出て、タスク管理アプリの性格が入り込む。

「もう使わない」は `IsArchived` で表現する。これは完了ではなく**選択肢からの除外**である。

**設計判断: DefaultWorkTypeId を持つ理由**

[UC-04](03-use-cases.md) の30秒制約を満たすため。作業開始時にWorkTypeを毎回選ばせず、既定値を自動選択する。ただし変更可能であり、実績値はWorkSessionが持つ。

---

### 4.3 DailyCondition（集約ルート）

```text
DailyCondition
  Id                 DailyConditionId
  TargetDate         DateOnly          JST基準の日付
  SleepMinutes       int               15の倍数
  PhysicalCondition  Rating            1..5
  MoodLevel          Rating            1..5
  StressLevel        Rating            1..5
  Note               string(1000)?
  RecordedAt         DateTimeOffset    初回記録時刻
  UpdatedAt          DateTimeOffset
```

**不変条件**

| # | 条件 |
|---|---|
| DC-1 | TargetDate は一意（1日1件） |
| DC-2 | SleepMinutes は 15〜1440、かつ15の倍数 |
| DC-3 | 各Ratingは1〜5 |
| DC-4 | TargetDate は**当日のみ**設定可能（過去日の新規作成・編集を禁止） |
| DC-5 | RecordedAt は作成後に変更しない |

**振る舞い**

```text
Record(targetDate, sleepMinutes, physical, mood, stress, note)
Update(sleepMinutes, physical, mood, stress, note)   当日のみ
GetSleepBand()  → SleepBand
```

**設計判断: DC-4（当日のみ）の理由**

事後の記憶に基づく記録は精度が低い。特に睡眠時間は数日経つと思い出せない。不正確なデータが入ると分析A-05が汚染される。

記録し忘れた日は**欠損のまま**にする。欠損は分析時に除外され、除外件数が表示される（[分析仕様 §4 A-05](04-analytics-spec.md)）。これは推測で埋めるより健全である。

---

### 4.4 PlannedWork（集約ルート）

```text
PlannedWork
  Id                PlannedWorkId
  TargetDate        DateOnly          JST基準の日付
  TaskItemId        TaskItemId
  WorkTypeId        WorkTypeId
  PlannedTimeBand   TimeBand?
  PlannedMinutes    int?              15の倍数
  CreatedAt         DateTimeOffset

  ── 集約内 ──
  NonExecution      NonExecutionRecord?
```

**不変条件**

| # | 条件 |
|---|---|
| PW-1 | TargetDate は当日のみ |
| PW-2 | PlannedMinutes を指定する場合、15〜1440 かつ15の倍数 |
| PW-3 | 同一TaskItem・同一日の重複を**許す** |
| PW-4 | NonExecutionRecord が存在する場合、WorkSession が紐づいていてはならない |
| PW-5 | WorkSession が紐づいている場合、NonExecutionRecord を作成できない |

**振る舞い**

```text
Plan(targetDate, taskItemId, workTypeId, timeBand, minutes)
RecordNonExecution(reason, note)   PW-5 を検査
UpdateNonExecution(reason, note)
```

**PW-4 / PW-5 の実装上の注意**

「WorkSessionが紐づいているか」は別集約（WorkSession）の情報である。集約内で検査できない。

したがって、この不変条件は**アプリケーション層で担保する**。`RecordNonExecution` を呼ぶ前に、WorkSessionリポジトリで存在確認を行う。

**設計判断: PlannedWork を TaskItem と別に持つ理由**

TaskItemに「予定日」を持たせる案もあるが、次の理由で分ける。

1. 同じタスクを複数日・1日に複数回予定できる
2. 予定は「宣言」であり、タスクの属性ではない
3. 実行率の分母は「予定の件数」であって「タスクの件数」ではない

3が決定的である。TaskItemに予定日を持たせると、1タスク1予定に制限され、実行率の意味が変わる。

---

### 4.5 NonExecutionRecord（PlannedWork集約内）

```text
NonExecutionRecord
  Id             NonExecutionRecordId
  PlannedWorkId  PlannedWorkId
  Reason         NonExecutionReason
  Note           string(1000)?
  RecordedAt     DateTimeOffset
  UpdatedAt      DateTimeOffset
```

**不変条件**

| # | 条件 |
|---|---|
| NE-1 | 1つのPlannedWorkに対して最大1件 |
| NE-2 | Reason は必須 |
| NE-3 | RecordedAt は作成後に変更しない |

**設計判断: Reason を必須にしている理由**

理由なしの未実行記録は、実行率の分子にはなるが、[PRD §2 P4](01-product-requirements.md) が求める「計画の妥当性の検証」には使えない。

特に `Overplanned`（計画が過大だった）を他の理由から分離していることが重要である。これが多い場合、対処は「頑張る」ではなく「計画を減らす」になる。

---

### 4.6 WorkSession（集約ルート）★中心

```text
WorkSession
  Id                 WorkSessionId
  TaskItemId         TaskItemId
  WorkTypeId         WorkTypeId        実績値
  PlannedWorkId      PlannedWorkId?
  StartedAt          DateTimeOffset    UTC
  FinishedAt         DateTimeOffset?   UTC
  Status             SessionStatus
  InterruptionCount  int
  AbandonNote        string(1000)?
  CreatedAt          DateTimeOffset
  UpdatedAt          DateTimeOffset

  ── 集約内 ──
  PreWorkState       PreWorkState      必須・1:1
  WorkContext        WorkContext       必須・1:1
  Result             PerformanceResult?    0..1
```

**状態遷移**

```text
                  Start()
                     │
                     ▼
              ┌─────────────┐
              │ InProgress  │
              └──┬───────┬──┘
       Finish()  │       │  Abandon()
                 ▼       ▼
        ┌─────────────┐ ┌────────────┐
        │  Completed  │ │ Abandoned  │
        └─────────────┘ └────────────┘
              （終端）        （終端）
```

**不変条件**

| # | 条件 |
|---|---|
| WS-1 | 生成時、PreWorkState と WorkContext が必ず同時に生成される |
| WS-2 | `Status = InProgress` のとき `FinishedAt` は NULL、`Result` は存在しない |
| WS-3 | `Status = Completed` のとき `FinishedAt` は非NULL、`Result` が**必ず存在する** |
| WS-4 | `Status = Abandoned` のとき `FinishedAt` は非NULL、`Result` は存在しない |
| WS-5 | `FinishedAt > StartedAt` |
| WS-6 | InterruptionCount >= 0 |
| WS-7 | 終端状態（Completed / Abandoned）から他の状態へ遷移しない |
| WS-8 | `StartedAt` / `FinishedAt` は**外部から設定できない**。システム時刻のみ |
| WS-9 | **InProgress のWorkSessionは全体で最大1件**（グローバル制約） |

**振る舞い**

```text
Start(taskItemId, workTypeId, plannedWorkId?, preWorkState, workContext, now)
    → Status = InProgress, StartedAt = now

Finish(interruptionCount, result, now)
    → WS-2 を検査（InProgress でなければ例外）
    → Status = Completed, FinishedAt = now, Result 設定

Abandon(note, now)
    → WS-2 を検査
    → Status = Abandoned, FinishedAt = now

UpdateResult(result)          Completed のみ。Result を差し替え、UpdatedAt 更新
UpdateInterruptionCount(n)    Completed のみ

Duration()                    → FinishedAt - StartedAt（未終了なら null）
FatigueDelta()                → Result.FatigueAfter - PreWorkState.FatigueLevel
FocusGap()                    → Result.FocusLevel - PreWorkState.ExpectedFocusLevel
BelongingDate()               → StartedAt を JST に変換した日付
TimeBand()                    → StartedAt を JST に変換した時刻から判定
```

**設計判断: WS-3（Completed には必ずResultがある）の理由**

「終了したが評価していない」状態を許すと、分析の母集団が不安定になる。`status = Completed` で絞ったのに Result が NULL のレコードが混ざり、集計側で毎回NULLチェックが要る。

そのため、[UC-05](03-use-cases.md) では成果評価のスキップ導線を設けていない。終了操作と評価入力は**1つのトランザクション**である。

**設計判断: WS-8（時刻を外部設定できない）の理由**

ユーザーが時刻を手入力できると、記録の事実性が失われる。「だいたい2時間くらいやった」という記憶ベースの値が入り、実作業時間の分析が無意味になる。

副作用として、記録し忘れたセッションは**後から作成できない**。これは意図的である。記録されなかったものは存在しなかったものとして扱う。

**設計判断: WS-9（同時1件）の理由**

並行作業を記録すると、どちらの成果か分離できない。「実装しながら会議に出た」場合、集中度2という評価がどちらに帰属するか決められない。

観測単位を成立させるため、同時実行を禁止する。これは集約をまたぐグローバル制約であり、**アプリケーション層 + DBの部分ユニークインデックス**で二重に担保する（[DB設計 §5](06-database-design.md)）。

**設計判断: Abandoned を持つ理由**

InProgress のまま放置されたセッションは、分析の母集団判定を汚し、[UC-04](03-use-cases.md) の「同時1件」制約により次の作業を開始できなくする。

終端状態を与えることで、記録として保持しつつ分析から除外できる。削除ではなく状態で表現するのは[PRD §8 原則2](01-product-requirements.md)に従う。

---

### 4.7 PreWorkState（WorkSession集約内）

```text
PreWorkState
  Id                  PreWorkStateId
  WorkSessionId       WorkSessionId
  FatigueLevel        Rating   1..5
  ExpectedFocusLevel  Rating   1..5
  MoodLevel           Rating   1..5
  RecordedAt          DateTimeOffset
```

**不変条件**

| # | 条件 |
|---|---|
| PS-1 | 各Ratingは1〜5、すべて必須 |
| PS-2 | **生成後、値を変更できない（イミュータブル）** |
| PS-3 | RecordedAt は WorkSession.StartedAt と同一トランザクション内の時刻 |

**PS-2 が本プロダクトで最も重要な不変条件である。**

作業前状態を事後に編集できると、次が起きる。

```text
悪い結果が出た
  → 「あの日は疲れていた」と後付けで疲労度を上げる
  → 「疲労が高いと成果が下がる」という相関が人工的に作られる
  → 分析結果が自分の思い込みの写像になる
```

これは本プロダクトの目的を完全に破壊する。したがって、**エンティティレベルでsetterを持たせない**。更新用のAPIも用意しない（[API設計](07-api-design.md)）。[UC-08](03-use-cases.md) の編集可否表と一致する。

---

### 4.8 WorkContext（WorkSession集約内）

```text
WorkContext
  Id                    WorkContextId
  WorkSessionId         WorkSessionId
  WorkLocation          WorkLocation
  LocationNote          string(200)?
  MeetingCount          int
  InterruptionExpected  bool
  RecordedAt            DateTimeOffset
```

**不変条件**

| # | 条件 |
|---|---|
| WC-1 | MeetingCount >= 0 |
| WC-2 | LocationNote は `WorkLocation = Other` のときのみ設定可能 |
| WC-3 | **生成後、値を変更できない（イミュータブル）** |

WC-3 の理由は PS-2 と同じ。

**設計判断: PreWorkState と WorkContext を分ける理由**

両者は同時に記録され、同じライフサイクルを持つ。統合しても機能上は動く。

分ける理由は次の2点である。

1. **[PRD §4](01-product-requirements.md) の4層分離を構造として保持する** — 「状態（自分）」と「環境（外部）」は概念的に別である。統合すると、この区別が実装から消え、時間が経つと誰も意識しなくなる
2. **今後の拡張の性質が異なる** — 環境側は追加候補が多い（騒音、天気、同席者、デバイス）。状態側は少ない。増える側を分離しておくと、変更の影響範囲が限定される

---

### 4.9 PerformanceResult（WorkSession集約内）

```text
PerformanceResult
  Id                  PerformanceResultId
  WorkSessionId       WorkSessionId
  FocusLevel          Rating   1..5
  OutputLevel         Rating   1..5
  AccuracyLevel       Rating   1..5
  SatisfactionLevel   Rating   1..5
  FatigueAfter        Rating   1..5
  Note                string(2000)?
  RecordedAt          DateTimeOffset   初回記録時刻・不変
  UpdatedAt           DateTimeOffset
```

**不変条件**

| # | 条件 |
|---|---|
| PR-1 | 5指標すべて必須。1〜5 |
| PR-2 | RecordedAt は作成後に変更しない |
| PR-3 | 更新時は UpdatedAt のみ変わる |
| PR-4 | 合成指標（総合スコア等）を持たない |

**設計判断: PerformanceResult は編集可能にする理由**

PreWorkStateと違い、成果評価は編集を許す。理由は次のとおり。

- 評価の訂正は正当な行為である（押し間違い、直後の判断ミス）
- 成果は目的変数であり、これを編集しても「説明変数を結果に合わせて捏造する」という問題は起きない
- `RecordedAt` と `UpdatedAt` の差で、事後編集されたレコードを識別できる

ただし**編集を推奨しない**。時間が経ってからの再評価は記憶に基づくため精度が落ちる。

**設計判断: PR-4（合成指標を持たない）の理由**

[用語集 §6](02-glossary.md) の通り。5指標を合成すると、どの要素が効いているか分からなくなる。合成が必要になった場合でも、保存はせず表示時に計算する。

---

## 5. 値オブジェクト

### 5.1 Rating

```text
Rating
  Value : int   1..5
```

1〜5の評価値。範囲外は生成時に例外。すべての主観評価はこの型を使う。

**プリミティブ型（int）を直接使わない理由**

`FocusLevel = 7` のような値が、DBに到達するまで検出されない事態を防ぐ。生成時点で弾く。

### 5.2 SleepDuration

```text
SleepDuration
  Minutes : int   15..1440、15の倍数

  ToBand() → SleepBand
```

### 5.3 SessionPeriod

```text
SessionPeriod
  StartedAt  : DateTimeOffset   UTC
  FinishedAt : DateTimeOffset?  UTC

  Duration()      → TimeSpan?
  BelongingDate() → DateOnly    JST基準
  TimeBand()      → TimeBand    JST基準
```

**JST変換ロジックをこの1箇所に閉じ込める。** [用語集 §4](02-glossary.md) の日付境界の定義を、複数箇所に散らさないため。分析クエリ側にも同じロジックが必要になるが、定義の出典はこことする。

### 5.4 列挙型

```text
SessionStatus        InProgress | Completed | Abandoned
WorkLocation         Home | Office | Cafe | Other
NonExecutionReason   NoTime | Interrupted | PoorCondition
                     | Deprioritized | Overplanned | Other
TimeBand             EarlyMorning | Morning | Afternoon | Evening   （導出値）
SleepBand            Under6 | From6To7 | From7To8 | Over8           （導出値）
```

TimeBand と SleepBand は**保存しない**。StartedAt / SleepMinutes から都度導出する。区分の定義を変更したとき、過去データにも新定義が適用される必要があるため。

---

## 6. ドメインサービス

集約をまたぐ処理を担う。

### 6.1 WorkSessionStarter

```text
Start(taskItemId, workTypeId, plannedWorkId?, preWorkStateData, workContextData)
```

**責務**

1. WS-9 の検査（InProgressのセッションが存在しないこと）
2. TaskItem が存在し、アーカイブされていないことの検査
3. WorkType が存在し、有効であることの検査
4. plannedWorkId が指定された場合、その PlannedWork が存在し、まだ実行されておらず、NonExecutionRecord も持たないことの検査（PW-5）
5. WorkSession の生成

**集約単体で担保できない不変条件をここに集約する。** エンティティのコンストラクタでは検査できないものだけを置く。

### 6.2 NonExecutionRecorder

```text
Record(plannedWorkId, reason, note)
```

**責務**

1. PlannedWork が存在することの検査
2. その PlannedWork に紐づく WorkSession が存在しないことの検査（PW-4）
3. NonExecutionRecord の生成または更新

---

## 7. リポジトリ

集約ルートごとに1つ。

```text
IWorkTypeRepository
    GetAllAsync(includeInactive)
    GetByIdAsync(id)
    ExistsByNameAsync(name)
    AddAsync / UpdateAsync

ITaskItemRepository
    GetAsync(includeArchived, keyword)
    GetByIdAsync(id)
    GetRecentlyUsedAsync(limit)     直近のWorkSessionでの使用順
    AddAsync / UpdateAsync

IDailyConditionRepository
    GetByDateAsync(jstDate)
    GetByDateRangeAsync(from, to)
    AddAsync / UpdateAsync

IPlannedWorkRepository
    GetByDateAsync(jstDate)
    GetByIdAsync(id)
    GetByDateRangeAsync(from, to)
    AddAsync / UpdateAsync

IWorkSessionRepository
    GetActiveAsync()                InProgress のもの（0または1件）
    GetByIdAsync(id)                集約全体を読み込む
    GetByDateRangeAsync(from, to)
    ExistsByPlannedWorkIdAsync(plannedWorkId)
    AddAsync / UpdateAsync
```

**分析用のクエリはリポジトリに置かない。** 集約を返す必要がなく、集計結果を返すため。[技術設計](08-technical-design.md) で定めるリードモデル（`IAnalyticsQuery`）を別に用意する。

---

## 8. 不変条件の担保箇所まとめ

| # | 不変条件 | 担保箇所 |
|---|---|---|
| WT-2 | WorkType名の一意性 | DB一意制約 + アプリ層で事前確認 |
| DC-1 | DailyCondition 1日1件 | DB一意制約 |
| DC-4 | 当日のみ | アプリケーション層 |
| PW-4/5 | 予定の排他（実行 or 未実行） | ドメインサービス + DB部分一意制約 |
| WS-2〜4 | 状態とResultの整合 | エンティティ内部 + DB CHECK制約 |
| WS-5 | FinishedAt > StartedAt | エンティティ内部 + DB CHECK制約 |
| WS-8 | 時刻の外部設定禁止 | エンティティのシグネチャ（時刻を引数に取らない公開API） |
| WS-9 | InProgress は1件 | ドメインサービス + DB部分一意インデックス |
| PS-2 | PreWorkState 不変 | エンティティにsetterなし + 更新APIなし |
| WC-3 | WorkContext 不変 | 同上 |
| Rating | 1〜5 | 値オブジェクト + DB CHECK制約 |

**アプリケーション層のみで担保している条件（DC-4）は、DBレベルの保証がない。** 実装時にテストで担保すること。

---

## 9. 意図的に持たない概念

| 概念 | 持たない理由 |
|---|---|
| User / Account | MVPは単一ユーザー（[PRD §6](01-product-requirements.md)）。ただし将来の追加を妨げない構造にしてある |
| Project / Category（TaskItemの上位） | 階層はタスク管理アプリ化を招く |
| Tag | 同上。分類軸はWorkTypeのみ |
| Priority | 優先度は消化の概念。観測に不要 |
| DueDate | 同上 |
| RecurrenceRule | 同上 |
| Estimate（TaskItemの見積） | PlannedWork.PlannedMinutes が予定所要時間を持つ。TaskItem側には持たせない |
| Goal / KPI | 目標達成の管理は目的でない |
| Score（合成指標） | [用語集 §6](02-glossary.md) |
| SessionPause（一時停止） | 実作業時間の定義が曖昧になる（[UC 5章](03-use-cases.md)） |

**この表は実装中の判断基準として使うこと。** 「あると便利では」という理由でこれらを追加しない。追加が必要になった場合は、まず[PRD §0](01-product-requirements.md)の目的に照らして議論する。

---

## 10. 将来の複数ユーザー化への配慮

MVPでは実装しないが、不可能にはしない。

| 項目 | 対応 |
|---|---|
| テーブル設計 | 将来 `user_id` を追加できるよう、一意制約は後から複合化できる形にする（[DB設計 §6](06-database-design.md)） |
| DailyCondition | 現在は `target_date` 単独一意。将来は `(user_id, target_date)` に変更 |
| WorkSession の同時1件制約 | 現在はグローバル。将来はユーザー単位 |
| WorkType | 現在はグローバルマスタ。将来はユーザー別かシステム共通かを選ぶ判断が必要 |

**先回りして `user_id` を入れない。** 単一ユーザーのMVPで意味のないカラムを持つと、すべてのクエリに無意味な条件が付き、認証もないため常に固定値になる。マイグレーションで追加する方が安全である。
