/**
 * API の型定義。
 *
 * OpenAPI からの自動生成は行わない。エンドポイントが 21・型が 20 程度であり、
 * 生成ツールの設定と型の癖に対処する手間の方が大きい
 * （docs/08-technical-design.md §3.10）。
 * 実際のレスポンスとの整合は統合テストで確認する。
 *
 * 日時はすべて UTC の ISO 8601 文字列（末尾 Z）。表示時に JST へ変換する
 * （docs/07-api-design.md §0.1）。lib/datetime.ts を使うこと。
 */

// ---------------------------------------------------------------- 列挙値

/** docs/02-glossary.md §1 */
export type SessionStatus = 'InProgress' | 'Completed' | 'Abandoned';

/** docs/02-glossary.md §3.1 */
export type WorkLocation = 'Home' | 'Office' | 'Cafe' | 'Other';

/** docs/02-glossary.md §3.2。開始時刻（JST）から導出される。 */
export type TimeBand = 'EarlyMorning' | 'Morning' | 'Afternoon' | 'Evening';

/** docs/07-api-design.md §2.15。保存を妨げない情報。 */
export type SessionWarning =
  | 'LongSession'
  | 'VeryShortSession'
  | 'MissingDailyCondition';

export type DayOfWeek =
  | 'Sunday'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday';

/** 1〜5 の評価値（docs/02-glossary.md §2）。 */
export type Rating = 1 | 2 | 3 | 4 | 5;

// ---------------------------------------------------------------- 作業タイプ

export type WorkTypeResponse = {
  id: number;
  name: string;
  displayOrder: number;
  isActive: boolean;
};

export type CreateWorkTypeRequest = {
  name: string;
  displayOrder?: number;
};

export type UpdateWorkTypeRequest = {
  name: string;
  displayOrder: number;
  isActive: boolean;
};

// ---------------------------------------------------------------- タスク

export type TaskItemSort = 'recent' | 'updated';

/** 一覧の 1 行。集計値を含む。 */
export type TaskItemResponse = {
  id: number;
  title: string;
  defaultWorkTypeId: number;
  defaultWorkTypeName: string;
  note: string | null;
  isArchived: boolean;
  /** 最新セッションの開始時刻。未使用なら null。 */
  lastUsedAt: string | null;
  sessionCount: number;
  createdAt: string;
  updatedAt: string;
};

/**
 * 作成・更新・アーカイブの結果。集計値と作業タイプ名を含まない。
 * 作業タイプ名は保持している一覧から ID で解決する。
 */
export type TaskItemDetailResponse = {
  id: number;
  title: string;
  defaultWorkTypeId: number;
  note: string | null;
  isArchived: boolean;
  createdAt: string;
  updatedAt: string;
};

export type SaveTaskItemRequest = {
  title: string;
  defaultWorkTypeId: number;
  note?: string | null;
};

// ---------------------------------------------------------------- 作業セッション

/** 作業前状態。記録後は変更できない（PS-2）。 */
export type PreWorkStateResponse = {
  fatigueLevel: Rating;
  expectedFocusLevel: Rating;
  moodLevel: Rating;
  recordedAt: string;
};

/** 作業環境。記録後は変更できない（WC-3）。 */
export type WorkContextResponse = {
  workLocation: WorkLocation;
  locationNote: string | null;
  meetingCount: number;
  interruptionExpected: boolean;
  recordedAt: string;
};

export type PerformanceResultResponse = {
  focusLevel: Rating;
  outputLevel: Rating;
  accuracyLevel: Rating;
  satisfactionLevel: Rating;
  fatigueAfter: Rating;
  note: string | null;
  recordedAt: string;
  updatedAt: string;
  /** 事後に訂正されたか。 */
  isEdited: boolean;
};

export type WorkSessionResponse = {
  id: number;
  taskItemId: number;
  taskTitle: string;
  workTypeId: number;
  workTypeName: string;
  plannedWorkId: number | null;
  startedAt: string;
  finishedAt: string | null;
  status: SessionStatus;
  /** 未終了なら null。保存されない導出値。 */
  durationMinutes: number | null;
  interruptionCount: number;
  abandonNote: string | null;
  timeBand: TimeBand;
  preWorkState: PreWorkStateResponse;
  workContext: WorkContextResponse;
  result: PerformanceResultResponse | null;
  /** 終了時疲労度 − 作業前疲労度。未評価なら null。 */
  fatigueDelta: number | null;
  /** 実際の集中度 − 見込み集中度。未評価なら null。 */
  focusGap: number | null;
  /** 終了時のみ内容を持つ。 */
  warnings: SessionWarning[];
};

/**
 * 作業開始のリクエスト。
 *
 * startedAt を送らない。開始時刻はサーバーが決める（WS-8）。
 * 定義外のプロパティを送ると 400 になる。
 */
export type StartWorkSessionRequest = {
  taskItemId: number;
  workTypeId: number;
  preWorkState: {
    fatigueLevel: Rating;
    expectedFocusLevel: Rating;
    moodLevel: Rating;
  };
  workContext: {
    workLocation: WorkLocation;
    locationNote?: string | null;
    meetingCount: number;
    interruptionExpected: boolean;
  };
};

/**
 * 終了と成果評価。5 指標すべてが必須である。
 * 部分的な評価を許すと分析の母集団に欠損が混ざる（WS-3）。
 */
export type SaveResultRequest = {
  interruptionCount: number;
  result: {
    focusLevel: Rating;
    outputLevel: Rating;
    accuracyLevel: Rating;
    satisfactionLevel: Rating;
    fatigueAfter: Rating;
    note?: string | null;
  };
};

export type AbandonWorkSessionRequest = {
  note?: string | null;
};

export type WorkSessionDaySummaryResponse = {
  completedCount: number;
  abandonedCount: number;
  totalMinutes: number;
};

/** 履歴の 1 日分。日付は JST 基準。 */
export type WorkSessionDayResponse = {
  date: string;
  dayOfWeek: DayOfWeek;
  sessions: WorkSessionResponse[];
  summary: WorkSessionDaySummaryResponse;
};

export type WorkSessionHistoryQuery = {
  from?: string;
  to?: string;
  status?: SessionStatus;
  taskItemId?: number;
};
