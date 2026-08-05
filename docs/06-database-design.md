# データベース設計書

- ドキュメントID: DB-006
- ステータス: ドラフト
- 最終更新: 2026-08-05
- 前提: [05-domain-design.md](05-domain-design.md) / [04-analytics-spec.md](04-analytics-spec.md) / [02-glossary.md](02-glossary.md)
- DBMS: PostgreSQL 16

---

## 0. 設計方針

| 項目 | 方針 |
|---|---|
| 命名 | テーブルは snake_case・複数形。カラムは snake_case（[用語集 §7](02-glossary.md)） |
| 主キー | `bigint GENERATED ALWAYS AS IDENTITY` |
| 日時 | `timestamptz`。**アプリケーションは常にUTCで書き込む** |
| 日付 | `date`。**JST基準の論理日**を格納する |
| 列挙型 | `text` + CHECK制約。PostgreSQLのENUM型は使わない |
| 評価値 | `smallint` + CHECK制約（1〜5） |
| 削除 | 記録系は物理削除も論理削除もしない。マスタ系は無効化フラグ |
| 監査カラム | `created_at` / `updated_at` を持つ。記録系は加えて `recorded_at` |

### 0.1 列挙型に `text` を使う理由

PostgreSQLのENUM型は値の追加が `ALTER TYPE` を要し、削除・並べ替えができない。`smallint` はSQLでの可読性が低く、アドホックな分析クエリを書くたびに対応表を参照する必要がある。

本プロダクトは**手でSQLを叩いて分析する場面が多い**（[分析仕様 §8](04-analytics-spec.md) の将来分析は、まずSQLで試す）。可読性を優先し、`text` + CHECK とする。

CHECK制約により、不正値はDBレベルで弾かれる。

### 0.2 評価値に `smallint` を使う理由

1〜5の固定範囲であり、`text` にする利点がない。CHECK制約で範囲を保証する。

### 0.3 記録を削除しない理由

[PRD §8 原則2](01-product-requirements.md)。記録された事実は削除しない。誤記録は訂正（更新）で対応する。訂正できない項目（時刻、作業前状態）は、そもそも訂正させない設計になっている（[ドメイン設計 §4.7](05-domain-design.md)）。

論理削除フラグも持たない。「削除された記録」を分析から除外する処理が全クエリに必要になり、除外漏れのリスクが生じる。

---

## 1. ER図

```text
┌─────────────────┐
│  work_types     │
│─────────────────│
│ id (PK)         │◄──────────────────────┐
│ name (UQ)       │◄────────────┐         │
│ display_order   │◄───┐        │         │
│ is_active       │    │        │         │
└─────────────────┘    │        │         │
                       │        │         │
┌─────────────────┐    │        │         │
│  task_items     │    │        │         │
│─────────────────│    │        │         │
│ id (PK)         │◄───┼────┐   │         │
│ title           │    │    │   │         │
│ default_work_   │────┘    │   │         │
│   type_id (FK)  │         │   │         │
│ is_archived     │         │   │         │
└─────────────────┘         │   │         │
                            │   │         │
┌─────────────────────┐     │   │         │
│  planned_works      │     │   │         │
│─────────────────────│     │   │         │
│ id (PK)             │◄─┐  │   │         │
│ target_date         │  │  │   │         │
│ task_item_id (FK)   │──┼──┘   │         │
│ work_type_id (FK)   │──┼──────┘         │
│ planned_time_band   │  │                │
│ planned_minutes     │  │                │
└─────────────────────┘  │                │
         ▲               │                │
         │ 1:0..1        │ 1:0..1         │
┌────────┴──────────────┐│                │
│ non_execution_records ││                │
│───────────────────────││                │
│ id (PK)               ││                │
│ planned_work_id(FK,UQ)││                │
│ reason                ││                │
│ note                  ││                │
└───────────────────────┘│                │
                         │                │
┌────────────────────────┴──────┐         │
│  work_sessions                │         │
│───────────────────────────────│         │
│ id (PK)                       │         │
│ task_item_id (FK)             │─────────┼──► task_items
│ work_type_id (FK)             │─────────┘
│ planned_work_id (FK, UQ NULL) │
│ started_at                    │
│ finished_at                   │
│ status                        │
│ interruption_count            │
│ abandon_note                  │
└───┬───────────┬───────────┬───┘
    │ 1:1       │ 1:1       │ 1:0..1
    ▼           ▼           ▼
┌─────────┐ ┌─────────┐ ┌──────────────────┐
│pre_work_│ │work_    │ │performance_      │
│states   │ │contexts │ │results           │
│─────────│ │─────────│ │──────────────────│
│id (PK)  │ │id (PK)  │ │id (PK)           │
│session_ │ │session_ │ │session_id(FK,UQ) │
│ id(FK,UQ)│ │ id(FK,UQ)│ │focus_level       │
│fatigue_ │ │work_    │ │output_level      │
│ level   │ │ location│ │accuracy_level    │
│expected_│ │location_│ │satisfaction_level│
│ focus_  │ │ note    │ │fatigue_after     │
│ level   │ │meeting_ │ │note              │
│mood_    │ │ count   │ └──────────────────┘
│ level   │ │interrup-│
└─────────┘ │ tion_   │
            │ expected│
            └─────────┘

┌──────────────────────┐
│  daily_conditions    │   独立テーブル
│──────────────────────│   work_sessions とは
│ id (PK)              │   target_date で
│ target_date (UQ)     │   暗黙結合する
│ sleep_minutes        │   （FKなし）
│ physical_condition   │
│ mood_level           │
│ stress_level         │
└──────────────────────┘
```

---

## 2. テーブル定義

### 2.1 work_types

作業タイプのマスタ。

| カラム | 型 | NULL | 既定 | 説明 |
|---|---|---|---|---|
| id | bigint | NO | IDENTITY | PK |
| name | varchar(50) | NO | | 名称 |
| display_order | integer | NO | 0 | 表示順（昇順） |
| is_active | boolean | NO | true | 有効フラグ |
| created_at | timestamptz | NO | | 作成日時（UTC） |
| updated_at | timestamptz | NO | | 更新日時（UTC） |

**制約**

```sql
PRIMARY KEY (id)
CONSTRAINT uq_work_types_name UNIQUE (lower(name))     -- 大文字小文字を区別しない
CONSTRAINT ck_work_types_name_not_blank CHECK (btrim(name) <> '')
```

`UNIQUE (lower(name))` は式インデックスで実現する。

```sql
CREATE UNIQUE INDEX uq_work_types_name ON work_types (lower(name));
```

**インデックス**

```sql
CREATE INDEX ix_work_types_active_order ON work_types (is_active, display_order);
```

**初期データ**

| id | name | display_order |
|---|---|---|
| 1 | 実装 | 10 |
| 2 | 設計 | 20 |
| 3 | ドキュメント | 30 |
| 4 | 調査 | 40 |
| 5 | 会議 | 50 |
| 6 | その他 | 90 |

`display_order` を10刻みにしているのは、後から間に挿入できるようにするため。

---

### 2.2 task_items

| カラム | 型 | NULL | 既定 | 説明 |
|---|---|---|---|---|
| id | bigint | NO | IDENTITY | PK |
| title | varchar(200) | NO | | タイトル |
| default_work_type_id | bigint | NO | | FK → work_types |
| note | varchar(2000) | YES | | メモ |
| is_archived | boolean | NO | false | アーカイブ |
| created_at | timestamptz | NO | | |
| updated_at | timestamptz | NO | | |

**制約**

```sql
PRIMARY KEY (id)
FOREIGN KEY (default_work_type_id) REFERENCES work_types (id) ON DELETE RESTRICT
CONSTRAINT ck_task_items_title_not_blank CHECK (btrim(title) <> '')
```

**title に一意制約を置かないこと。** 同名のタスクを別機会に登録するのは正常な操作である（[ドメイン設計 TI-3](05-domain-design.md)）。

**インデックス**

```sql
CREATE INDEX ix_task_items_active ON task_items (is_archived, updated_at DESC);
```

作業開始画面（S-04）のタスク選択で、有効なタスクを更新順に出すため。

**完了フラグを持たないことを確認すること。** `is_completed` / `completed_at` / `status` に相当するカラムは存在しない（[ドメイン設計 TI-4](05-domain-design.md)）。

---

### 2.3 daily_conditions

| カラム | 型 | NULL | 既定 | 説明 |
|---|---|---|---|---|
| id | bigint | NO | IDENTITY | PK |
| target_date | date | NO | | **JST基準の日付** |
| sleep_minutes | integer | NO | | 睡眠時間（分） |
| physical_condition | smallint | NO | | 体調 1〜5 |
| mood_level | smallint | NO | | 気分 1〜5 |
| stress_level | smallint | NO | | ストレス 1〜5 |
| note | varchar(1000) | YES | | |
| recorded_at | timestamptz | NO | | 初回記録日時（不変） |
| updated_at | timestamptz | NO | | |

**制約**

```sql
PRIMARY KEY (id)
CONSTRAINT uq_daily_conditions_date UNIQUE (target_date)
CONSTRAINT ck_daily_conditions_sleep CHECK (
    sleep_minutes BETWEEN 15 AND 1440 AND sleep_minutes % 15 = 0
)
CONSTRAINT ck_daily_conditions_physical  CHECK (physical_condition BETWEEN 1 AND 5)
CONSTRAINT ck_daily_conditions_mood      CHECK (mood_level BETWEEN 1 AND 5)
CONSTRAINT ck_daily_conditions_stress    CHECK (stress_level BETWEEN 1 AND 5)
```

**インデックス**

`uq_daily_conditions_date` が範囲検索にも使われるため、追加不要。

**`target_date` が JST基準であることの厳守**

`timestamptz` から日付を取り出す際は必ず次を使う。

```sql
(started_at AT TIME ZONE 'Asia/Tokyo')::date
```

`started_at::date` としてはならない。サーバーのタイムゾーン設定に依存し、UTCなら9時間ずれる。

---

### 2.4 planned_works

| カラム | 型 | NULL | 既定 | 説明 |
|---|---|---|---|---|
| id | bigint | NO | IDENTITY | PK |
| target_date | date | NO | | **JST基準の日付** |
| task_item_id | bigint | NO | | FK → task_items |
| work_type_id | bigint | NO | | FK → work_types |
| planned_time_band | text | YES | | 予定時間帯 |
| planned_minutes | integer | YES | | 予定所要時間（分） |
| created_at | timestamptz | NO | | |

**制約**

```sql
PRIMARY KEY (id)
FOREIGN KEY (task_item_id) REFERENCES task_items (id) ON DELETE RESTRICT
FOREIGN KEY (work_type_id) REFERENCES work_types (id) ON DELETE RESTRICT
CONSTRAINT ck_planned_works_time_band CHECK (
    planned_time_band IS NULL OR
    planned_time_band IN ('EarlyMorning','Morning','Afternoon','Evening')
)
CONSTRAINT ck_planned_works_minutes CHECK (
    planned_minutes IS NULL OR
    (planned_minutes BETWEEN 15 AND 1440 AND planned_minutes % 15 = 0)
)
```

一意制約は置かない。同一タスク・同一日の重複を許す（[ドメイン設計 PW-3](05-domain-design.md)）。

**インデックス**

```sql
CREATE INDEX ix_planned_works_date ON planned_works (target_date);
```

分析A-06（実行率）と、ホーム画面の当日予定取得で使う。

---

### 2.5 work_sessions ★中心

| カラム | 型 | NULL | 既定 | 説明 |
|---|---|---|---|---|
| id | bigint | NO | IDENTITY | PK |
| task_item_id | bigint | NO | | FK → task_items |
| work_type_id | bigint | NO | | FK → work_types（**実績値**） |
| planned_work_id | bigint | YES | | FK → planned_works |
| started_at | timestamptz | NO | | 開始日時（UTC） |
| finished_at | timestamptz | YES | | 終了日時（UTC） |
| status | text | NO | | InProgress / Completed / Abandoned |
| interruption_count | integer | NO | 0 | 中断回数 |
| abandon_note | varchar(1000) | YES | | 中断終了の理由メモ |
| created_at | timestamptz | NO | | |
| updated_at | timestamptz | NO | | |

**制約**

```sql
PRIMARY KEY (id)
FOREIGN KEY (task_item_id)    REFERENCES task_items (id)    ON DELETE RESTRICT
FOREIGN KEY (work_type_id)    REFERENCES work_types (id)    ON DELETE RESTRICT
FOREIGN KEY (planned_work_id) REFERENCES planned_works (id) ON DELETE RESTRICT

CONSTRAINT uq_work_sessions_planned_work UNIQUE (planned_work_id)
    -- NULL は重複可。1つの予定に紐づくセッションは最大1件

CONSTRAINT ck_work_sessions_status CHECK (
    status IN ('InProgress','Completed','Abandoned')
)

CONSTRAINT ck_work_sessions_interruption CHECK (interruption_count >= 0)

-- WS-2/3/4: 状態と finished_at の整合
CONSTRAINT ck_work_sessions_status_finished CHECK (
    (status = 'InProgress' AND finished_at IS NULL) OR
    (status IN ('Completed','Abandoned') AND finished_at IS NOT NULL)
)

-- WS-5: 時系列の整合
CONSTRAINT ck_work_sessions_period CHECK (
    finished_at IS NULL OR finished_at > started_at
)

-- Abandoned 以外は abandon_note を持たない
CONSTRAINT ck_work_sessions_abandon_note CHECK (
    abandon_note IS NULL OR status = 'Abandoned'
)
```

**インデックス**

```sql
-- WS-9: InProgress は全体で1件まで（部分一意インデックス）
CREATE UNIQUE INDEX uq_work_sessions_single_active
    ON work_sessions ((true)) WHERE status = 'InProgress';

-- 分析・履歴の期間絞り込み
CREATE INDEX ix_work_sessions_started_at ON work_sessions (started_at DESC);

-- 分析の母集団絞り込み（status = 'Completed' が大半を占めるため部分インデックス）
CREATE INDEX ix_work_sessions_completed
    ON work_sessions (started_at DESC) WHERE status = 'Completed';

-- 分析 A-01/A-02 の集計
CREATE INDEX ix_work_sessions_work_type ON work_sessions (work_type_id, started_at);

-- タスクの直近使用順取得
CREATE INDEX ix_work_sessions_task_item ON work_sessions (task_item_id, started_at DESC);
```

**`uq_work_sessions_single_active` の説明**

```sql
CREATE UNIQUE INDEX uq_work_sessions_single_active
    ON work_sessions ((true)) WHERE status = 'InProgress';
```

`status = 'InProgress'` の行だけを対象に、定数 `true` に対する一意インデックスを張る。結果として、この条件を満たす行は**テーブル全体で最大1行**になる。

これが[ドメイン設計 WS-9](05-domain-design.md)（同時実行の禁止）のDBレベルの担保である。アプリケーション層のチェックだけでは、並行リクエストで2件作られうる。

将来ユーザーを追加する場合は、`ON work_sessions (user_id) WHERE status = 'InProgress'` に変更する。

**`uq_work_sessions_planned_work` が NULL を許す点**

PostgreSQLの UNIQUE 制約は NULL を重複とみなさない。したがって `planned_work_id IS NULL`（予定外の作業）は何件でも作成できる。意図した挙動である。

---

### 2.6 pre_work_states

| カラム | 型 | NULL | 既定 | 説明 |
|---|---|---|---|---|
| id | bigint | NO | IDENTITY | PK |
| work_session_id | bigint | NO | | FK → work_sessions（一意） |
| fatigue_level | smallint | NO | | 疲労度 1〜5 |
| expected_focus_level | smallint | NO | | 見込み集中度 1〜5 |
| mood_level | smallint | NO | | 気分 1〜5 |
| recorded_at | timestamptz | NO | | 記録日時（UTC） |

**制約**

```sql
PRIMARY KEY (id)
FOREIGN KEY (work_session_id) REFERENCES work_sessions (id) ON DELETE CASCADE
CONSTRAINT uq_pre_work_states_session UNIQUE (work_session_id)
CONSTRAINT ck_pre_work_states_fatigue  CHECK (fatigue_level BETWEEN 1 AND 5)
CONSTRAINT ck_pre_work_states_expected CHECK (expected_focus_level BETWEEN 1 AND 5)
CONSTRAINT ck_pre_work_states_mood     CHECK (mood_level BETWEEN 1 AND 5)
```

**`updated_at` を持たないことに注意。** このテーブルの行は生成後に更新されない（[ドメイン設計 PS-2](05-domain-design.md)）。更新カラムを持たないことで、「更新しない」という設計意図をスキーマで表現する。

**`ON DELETE CASCADE` を設定する理由**

work_sessions は削除しない設計だが、開発中のデータリセットやマイグレーションのやり直しで削除する場合がある。その際、子テーブルが残ると外部キー違反になる。集約内の関係なので CASCADE が正しい。

---

### 2.7 work_contexts

| カラム | 型 | NULL | 既定 | 説明 |
|---|---|---|---|---|
| id | bigint | NO | IDENTITY | PK |
| work_session_id | bigint | NO | | FK → work_sessions（一意） |
| work_location | text | NO | | Home / Office / Cafe / Other |
| location_note | varchar(200) | YES | | Other のときのみ |
| meeting_count | integer | NO | 0 | その日の会議件数 |
| interruption_expected | boolean | NO | false | 割り込み予想 |
| recorded_at | timestamptz | NO | | |

**制約**

```sql
PRIMARY KEY (id)
FOREIGN KEY (work_session_id) REFERENCES work_sessions (id) ON DELETE CASCADE
CONSTRAINT uq_work_contexts_session UNIQUE (work_session_id)
CONSTRAINT ck_work_contexts_location CHECK (
    work_location IN ('Home','Office','Cafe','Other')
)
CONSTRAINT ck_work_contexts_meeting CHECK (meeting_count >= 0)

-- WC-2: location_note は Other のときのみ
CONSTRAINT ck_work_contexts_location_note CHECK (
    location_note IS NULL OR work_location = 'Other'
)
```

`updated_at` を持たない。理由は pre_work_states と同じ。

---

### 2.8 performance_results

| カラム | 型 | NULL | 既定 | 説明 |
|---|---|---|---|---|
| id | bigint | NO | IDENTITY | PK |
| work_session_id | bigint | NO | | FK → work_sessions（一意） |
| focus_level | smallint | NO | | 集中度 1〜5 |
| output_level | smallint | NO | | 成果度 1〜5 |
| accuracy_level | smallint | NO | | 正確性 1〜5 |
| satisfaction_level | smallint | NO | | 満足度 1〜5 |
| fatigue_after | smallint | NO | | 終了時疲労度 1〜5 |
| note | varchar(2000) | YES | | |
| recorded_at | timestamptz | NO | | 初回記録日時（不変） |
| updated_at | timestamptz | NO | | 最終更新日時 |

**制約**

```sql
PRIMARY KEY (id)
FOREIGN KEY (work_session_id) REFERENCES work_sessions (id) ON DELETE CASCADE
CONSTRAINT uq_performance_results_session UNIQUE (work_session_id)
CONSTRAINT ck_performance_results_focus        CHECK (focus_level        BETWEEN 1 AND 5)
CONSTRAINT ck_performance_results_output       CHECK (output_level       BETWEEN 1 AND 5)
CONSTRAINT ck_performance_results_accuracy     CHECK (accuracy_level     BETWEEN 1 AND 5)
CONSTRAINT ck_performance_results_satisfaction CHECK (satisfaction_level BETWEEN 1 AND 5)
CONSTRAINT ck_performance_results_fatigue      CHECK (fatigue_after      BETWEEN 1 AND 5)
```

**合成スコアのカラムを持たないことを確認すること**（[ドメイン設計 PR-4](05-domain-design.md)）。

**インデックス**

`uq_performance_results_session` が結合に使われるため、追加不要。

---

### 2.9 non_execution_records

| カラム | 型 | NULL | 既定 | 説明 |
|---|---|---|---|---|
| id | bigint | NO | IDENTITY | PK |
| planned_work_id | bigint | NO | | FK → planned_works（一意） |
| reason | text | NO | | 理由区分 |
| note | varchar(1000) | YES | | |
| recorded_at | timestamptz | NO | | 初回記録日時（不変） |
| updated_at | timestamptz | NO | | |

**制約**

```sql
PRIMARY KEY (id)
FOREIGN KEY (planned_work_id) REFERENCES planned_works (id) ON DELETE CASCADE
CONSTRAINT uq_non_execution_records_planned UNIQUE (planned_work_id)
CONSTRAINT ck_non_execution_records_reason CHECK (
    reason IN ('NoTime','Interrupted','PoorCondition',
               'Deprioritized','Overplanned','Other')
)
```

---

## 3. 制約で担保できないもの

以下は**DBでは担保できない**。アプリケーション層とテストで守る。

| # | 条件 | 理由 | 担保方法 |
|---|---|---|---|
| WS-3 | Completed なら performance_results が必ず存在 | 別テーブルへの存在制約はCHECKで書けない | アプリ層で同一トランザクション。加えて §7 の整合性検証クエリで定期確認 |
| WS-4 | Abandoned なら performance_results が存在しない | 同上 | 同上 |
| PW-4/5 | 予定に対し「実行」と「未実行」が排他 | 別テーブル間の排他 | アプリケーション層（[ドメイン設計 §6.2](05-domain-design.md)） + §7 の検証クエリ |
| DC-4 | DailyCondition は当日のみ記録可 | 「現在日」に依存する条件はCHECKで書けない（IMMUTABLEでない） | アプリケーション層 |
| WS-8 | 時刻がシステム時刻であること | DBには判定材料がない | エンティティのAPI設計 |

**WS-3 は特に注意を要する。** アプリケーション層のバグでこれが破れると、分析の母集団に Result のない行が混ざり、集計が壊れる。§7の検証クエリを実装し、テストで実行すること。

---

## 4. 主要クエリ

分析仕様の各項目に対応するSQL。実装時はこれを基準とする。

### 4.1 共通: 分析用ビュー

集計クエリで繰り返す結合とJST変換を、ビューにまとめる。

```sql
CREATE VIEW v_completed_sessions AS
SELECT
    ws.id                AS session_id,
    ws.task_item_id,
    ws.work_type_id,
    ws.planned_work_id,
    ws.started_at,
    ws.finished_at,
    ws.interruption_count,
    (ws.started_at AT TIME ZONE 'Asia/Tokyo')          AS started_at_jst,
    (ws.started_at AT TIME ZONE 'Asia/Tokyo')::date    AS belonging_date,
    EXTRACT(HOUR FROM (ws.started_at AT TIME ZONE 'Asia/Tokyo'))::int AS started_hour_jst,
    EXTRACT(ISODOW FROM (ws.started_at AT TIME ZONE 'Asia/Tokyo'))::int AS iso_dow,
    EXTRACT(EPOCH FROM (ws.finished_at - ws.started_at))::int / 60 AS duration_minutes,
    pws.fatigue_level        AS pre_fatigue_level,
    pws.expected_focus_level,
    pws.mood_level           AS pre_mood_level,
    wc.work_location,
    wc.meeting_count,
    wc.interruption_expected,
    pr.focus_level,
    pr.output_level,
    pr.accuracy_level,
    pr.satisfaction_level,
    pr.fatigue_after,
    (pr.fatigue_after - pws.fatigue_level)          AS fatigue_delta,
    (pr.focus_level - pws.expected_focus_level)     AS focus_gap,
    CASE
        WHEN EXTRACT(HOUR FROM (ws.started_at AT TIME ZONE 'Asia/Tokyo')) BETWEEN 5 AND 8  THEN 'EarlyMorning'
        WHEN EXTRACT(HOUR FROM (ws.started_at AT TIME ZONE 'Asia/Tokyo')) BETWEEN 9 AND 11 THEN 'Morning'
        WHEN EXTRACT(HOUR FROM (ws.started_at AT TIME ZONE 'Asia/Tokyo')) BETWEEN 12 AND 16 THEN 'Afternoon'
        ELSE 'Evening'
    END AS time_band
FROM work_sessions ws
JOIN pre_work_states     pws ON pws.work_session_id = ws.id
JOIN work_contexts       wc  ON wc.work_session_id  = ws.id
JOIN performance_results pr  ON pr.work_session_id  = ws.id
WHERE ws.status = 'Completed';
```

**このビューが `status = 'Completed'` で絞っていることが重要である。** 分析の母集団定義（[分析仕様 §2.1](04-analytics-spec.md)）をここに集約し、各クエリで書き忘れることを防ぐ。

`JOIN performance_results` を INNER JOIN にしているため、WS-3 が破れている行は自動的に除外される。安全側に倒れる。

### 4.2 A-01 / A-02 作業タイプ別

```sql
SELECT
    wt.id   AS work_type_id,
    wt.name AS work_type_name,
    COUNT(*)                            AS n,
    ROUND(AVG(cs.focus_level),  2)      AS avg_focus,
    ROUND(AVG(cs.output_level), 2)      AS avg_output,
    SUM(cs.duration_minutes)            AS total_minutes
FROM v_completed_sessions cs
JOIN work_types wt ON wt.id = cs.work_type_id
WHERE cs.belonging_date BETWEEN @from AND @to
GROUP BY wt.id, wt.name
ORDER BY avg_focus DESC;
```

最小サンプル数（5件）の判定は**アプリケーション層で行う**。SQLで `HAVING COUNT(*) >= 5` としてはならない。サンプル不足の区分も「サンプル不足（n=3）」として表示する必要があるため、件数ごと返す。

### 4.3 A-03 時間帯別

```sql
SELECT
    cs.time_band,
    COUNT(*)                        AS n,
    ROUND(AVG(cs.focus_level), 2)   AS avg_focus
FROM v_completed_sessions cs
WHERE cs.belonging_date BETWEEN @from AND @to
GROUP BY cs.time_band;
```

並び順（早朝→午前→午後→夜）はアプリケーション層で固定する。SQLの `ORDER BY` では文字列順になり、意図した順にならない。

### 4.4 A-04 曜日別

```sql
SELECT
    cs.iso_dow,
    COUNT(*)                         AS n,
    ROUND(AVG(cs.output_level), 2)   AS avg_output
FROM v_completed_sessions cs
WHERE cs.belonging_date BETWEEN @from AND @to
GROUP BY cs.iso_dow
ORDER BY cs.iso_dow;
```

`ISODOW` は月曜=1、日曜=7。`DOW` は日曜=0であり、月曜始まりにするには変換が要る。`ISODOW` を使う。

### 4.5 A-05 睡眠時間区分別

```sql
SELECT
    CASE
        WHEN dc.sleep_minutes <  360 THEN 'Under6'
        WHEN dc.sleep_minutes <  420 THEN 'From6To7'
        WHEN dc.sleep_minutes <  480 THEN 'From7To8'
        ELSE 'Over8'
    END                                 AS sleep_band,
    COUNT(*)                            AS n,
    COUNT(DISTINCT cs.belonging_date)   AS day_count,
    ROUND(AVG(cs.output_level), 2)      AS avg_output
FROM v_completed_sessions cs
JOIN daily_conditions dc ON dc.target_date = cs.belonging_date
WHERE cs.belonging_date BETWEEN @from AND @to
GROUP BY 1;
```

除外件数は別クエリで取得する。

```sql
SELECT COUNT(*) AS excluded_count
FROM v_completed_sessions cs
LEFT JOIN daily_conditions dc ON dc.target_date = cs.belonging_date
WHERE cs.belonging_date BETWEEN @from AND @to
  AND dc.id IS NULL;
```

**`JOIN daily_conditions` が INNER JOIN であるため、日次コンディション未記録の日のセッションは自動的に除外される。** これは[分析仕様 §2.4](04-analytics-spec.md) の定義どおりだが、除外件数を必ず表示すること。除外を黙って行うと、結果の信頼度をユーザーが判断できない。

### 4.6 A-06 実行率

```sql
SELECT
    COUNT(*)                                                        AS total_planned,
    COUNT(ws.id)                                                    AS executed,
    COUNT(ner.id)                                                   AS non_executed,
    COUNT(*) - COUNT(ws.id) - COUNT(ner.id)                         AS unprocessed,
    COUNT(*) FILTER (WHERE ws.status = 'Abandoned')                 AS abandoned
FROM planned_works pw
LEFT JOIN work_sessions          ws  ON ws.planned_work_id  = pw.id
LEFT JOIN non_execution_records  ner ON ner.planned_work_id = pw.id
WHERE pw.target_date BETWEEN @from AND @to;
```

**この LEFT JOIN の `work_sessions` は `status` で絞っていない。** [分析仕様 A-06](04-analytics-spec.md) の定義どおり、Abandoned も「着手した」として実行済みに数える。内訳として `abandoned` を別に返す。

理由別の内訳。

```sql
SELECT ner.reason, COUNT(*) AS n
FROM planned_works pw
JOIN non_execution_records ner ON ner.planned_work_id = pw.id
WHERE pw.target_date BETWEEN @from AND @to
GROUP BY ner.reason;
```

予定外セッション数。

```sql
SELECT COUNT(*) AS unplanned_sessions
FROM v_completed_sessions cs
WHERE cs.belonging_date BETWEEN @from AND @to
  AND cs.planned_work_id IS NULL;
```

---

## 5. インデックス一覧

| テーブル | インデックス | 種別 | 用途 |
|---|---|---|---|
| work_types | uq_work_types_name | UNIQUE(式) | 名称の一意性（大小無視） |
| work_types | ix_work_types_active_order | 通常 | 有効なタイプの表示順取得 |
| task_items | ix_task_items_active | 通常 | タスク選択リスト |
| daily_conditions | uq_daily_conditions_date | UNIQUE | 1日1件・日付検索 |
| planned_works | ix_planned_works_date | 通常 | 当日予定・A-06 |
| work_sessions | uq_work_sessions_single_active | UNIQUE(部分) | **同時実行の禁止（WS-9）** |
| work_sessions | uq_work_sessions_planned_work | UNIQUE | 予定とセッションの1:1 |
| work_sessions | ix_work_sessions_started_at | 通常 | 履歴・期間絞り込み |
| work_sessions | ix_work_sessions_completed | 通常(部分) | 分析の母集団 |
| work_sessions | ix_work_sessions_work_type | 通常 | A-01/A-02 |
| work_sessions | ix_work_sessions_task_item | 通常 | タスクの直近使用順 |
| pre_work_states | uq_pre_work_states_session | UNIQUE | 1:1・結合 |
| work_contexts | uq_work_contexts_session | UNIQUE | 1:1・結合 |
| performance_results | uq_performance_results_session | UNIQUE | 1:1・結合 |
| non_execution_records | uq_non_execution_records_planned | UNIQUE | 1:1・結合 |

**インデックスをこれ以上増やさないこと。** 想定データ規模は3年で約2,000セッションであり（[分析仕様 §7](04-analytics-spec.md)）、この規模では全表走査でも十分速い。上記は結合と一意性のために必要な最小限である。

---

## 6. 将来の複数ユーザー化

MVPでは `user_id` を持たない（[ドメイン設計 §10](05-domain-design.md)）。追加する場合の手順を記す。

| # | 変更 |
|---|---|
| 1 | `users` テーブルを追加 |
| 2 | 各テーブルに `user_id bigint NOT NULL` を追加（既存行は固定値で埋める） |
| 3 | `uq_daily_conditions_date` を `(user_id, target_date)` に変更 |
| 4 | `uq_work_types_name` を `(user_id, lower(name))` に変更（またはWorkTypeを共通マスタのままとする） |
| 5 | `uq_work_sessions_single_active` を `ON work_sessions (user_id) WHERE status = 'InProgress'` に変更 |
| 6 | 各インデックスの先頭に `user_id` を追加 |

**手順5が最も重要である。** 現在の定義 `ON work_sessions ((true))` はテーブル全体で1件という意味であり、複数ユーザー化した瞬間に「システム全体で1人しか作業できない」という致命的な制約になる。変更を忘れないこと。

---

## 7. 整合性検証クエリ

DBで担保できない不変条件（§3）を検証する。テストおよび運用時の点検に使う。

```sql
-- WS-3: Completed なのに Result がない
SELECT id, started_at FROM work_sessions ws
WHERE ws.status = 'Completed'
  AND NOT EXISTS (SELECT 1 FROM performance_results pr WHERE pr.work_session_id = ws.id);

-- WS-4: Abandoned なのに Result がある
SELECT id FROM work_sessions ws
WHERE ws.status = 'Abandoned'
  AND EXISTS (SELECT 1 FROM performance_results pr WHERE pr.work_session_id = ws.id);

-- WS-1: PreWorkState または WorkContext が欠けている
SELECT id FROM work_sessions ws
WHERE NOT EXISTS (SELECT 1 FROM pre_work_states  p WHERE p.work_session_id = ws.id)
   OR NOT EXISTS (SELECT 1 FROM work_contexts    c WHERE c.work_session_id = ws.id);

-- PW-4/5: 予定に対し実行と未実行が両立している
SELECT pw.id FROM planned_works pw
WHERE EXISTS (SELECT 1 FROM work_sessions         ws  WHERE ws.planned_work_id  = pw.id)
  AND EXISTS (SELECT 1 FROM non_execution_records ner WHERE ner.planned_work_id = pw.id);

-- WS-9: InProgress が複数存在（部分一意インデックスがあれば0件のはず）
SELECT COUNT(*) FROM work_sessions WHERE status = 'InProgress';
```

**すべて0件（最後は0または1）であること。** 統合テストのアサーションに含める。

---

## 8. マイグレーション方針

| 項目 | 方針 |
|---|---|
| ツール | EF Core Migrations |
| 命名 | `YYYYMMDDHHmmss_動詞_対象`（例: `20260804120000_CreateWorkSessionTables`） |
| ビュー・部分インデックス | EF Coreが自動生成できないため、マイグレーション内で `migrationBuilder.Sql()` を使って手書きする |
| 初期データ | `work_types` の6件は、マイグレーション内の `InsertData` で投入する |
| ロールバック | MVP期間中は `Down` を書く。運用開始後は前方向のみ |

**手書きSQLが必要な箇所**

```text
uq_work_types_name              式インデックス（lower(name)）
uq_work_sessions_single_active  部分一意インデックス
ix_work_sessions_completed      部分インデックス
v_completed_sessions            ビュー
```

これらは EF Core のモデル定義から生成されない。マイグレーションに手書きし、`Down` で `DROP` すること。書き忘れると、DB制約による担保（特にWS-9）が効かなくなる。
