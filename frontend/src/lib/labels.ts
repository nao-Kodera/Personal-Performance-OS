/**
 * 列挙値 → 日本語表示名。
 *
 * <b>画面ごとに日本語をハードコードしないこと。</b>同じ概念に別の語を使わない
 * ため、変換表をここに集約する（docs/02-glossary.md §0 のルール 3）。
 *
 * 表示名は docs/02-glossary.md の定義に従う。ここを変えるときは用語集も直す。
 */

import type {
  Rating,
  SessionStatus,
  SessionWarning,
  TimeBand,
  WorkLocation,
} from '../api/types';

export const sessionStatusLabels: Record<SessionStatus, string> = {
  InProgress: '進行中',
  Completed: '完了',
  Abandoned: '中断終了',
};

export const workLocationLabels: Record<WorkLocation, string> = {
  Home: '自宅',
  Office: 'オフィス',
  Cafe: 'カフェ',
  Other: 'その他',
};

/** docs/02-glossary.md §3.2。表示順は時系列順に固定する。 */
export const timeBandLabels: Record<TimeBand, string> = {
  EarlyMorning: '早朝',
  Morning: '午前',
  Afternoon: '午後',
  Evening: '夜',
};

/**
 * サーバーが返す warnings の文言（記録後に表示する）。
 * docs/07-api-design.md §2.15。
 */
export const sessionWarningLabels: Record<SessionWarning, string> = {
  LongSession: '8時間を超えています。記録し忘れの可能性があります。',
  VeryShortSession: '1分未満です。中断終了の方が適切かもしれません。',
  MissingDailyCondition:
    '日次コンディションが未記録です。睡眠に関する分析に反映されません。',
};

/**
 * 終了する前（S-06 の入力中）に出す警告の文言。
 *
 * サーバーが返す warnings と時制が違う。終了前はまだ中断終了に切り替えられる
 * ため、docs/03-use-cases.md UC-05 の例外表の文言を使う。記録後に同じ文で
 * 「記録しますか?」と問うと、もう選べない操作を促すことになる。
 */
export const sessionWarningPrompts: Record<SessionWarning, string> = {
  ...sessionWarningLabels,
  VeryShortSession: '1分未満です。中断終了として記録しますか?',
};

// ---------------------------------------------------------------- 評価尺度

/**
 * 評価指標。
 *
 * <b>尺度の向きが指標によって異なる。</b>疲労度は「高いほど悪い」、
 * それ以外は「高いほど良い」（docs/02-glossary.md §2.2）。
 * 表示時に値を反転しないこと。記録した値と表示する値を一致させる。
 */
export type RatingMetric =
  | 'fatigueLevel'
  | 'expectedFocusLevel'
  | 'moodLevel'
  | 'focusLevel'
  | 'outputLevel'
  | 'accuracyLevel'
  | 'satisfactionLevel'
  | 'fatigueAfter';

type RatingScale = {
  /** 指標の表示名。 */
  label: string;
  /** 高いほど良いか。疲労度のみ false。 */
  higherIsBetter: boolean;
  /** 1〜5 それぞれの意味（docs/02-glossary.md §2.1）。 */
  levels: Record<Rating, string>;
};

const focusLevels: Record<Rating, string> = {
  1: 'ほぼ集中できなかった',
  2: '何度も気が散った',
  3: '普通',
  4: 'よく集中できた',
  5: '完全に没入した',
};

const fatigueLevels: Record<Rating, string> = {
  1: '疲れていない',
  2: 'やや疲れている',
  3: '普通',
  4: '疲れている',
  5: '限界に近い',
};

const moodLevels: Record<Rating, string> = {
  1: '非常に悪い',
  2: '悪い',
  3: '普通',
  4: '良い',
  5: '非常に良い',
};

export const ratingScales: Record<RatingMetric, RatingScale> = {
  fatigueLevel: {
    label: '疲労度',
    higherIsBetter: false,
    levels: fatigueLevels,
  },
  expectedFocusLevel: {
    label: '集中できそうか',
    higherIsBetter: true,
    levels: {
      1: '集中できる気がしない',
      2: 'あまり期待できない',
      3: '普通',
      4: '集中できそう',
      5: '非常に集中できそう',
    },
  },
  moodLevel: {
    label: '気分',
    higherIsBetter: true,
    levels: moodLevels,
  },
  focusLevel: {
    label: '集中度',
    higherIsBetter: true,
    levels: focusLevels,
  },
  outputLevel: {
    label: '成果度',
    higherIsBetter: true,
    levels: {
      1: 'ほとんど進まなかった',
      2: '想定を下回った',
      3: '想定どおり',
      4: '想定を上回った',
      5: '大きく前進した',
    },
  },
  accuracyLevel: {
    label: '正確性',
    higherIsBetter: true,
    levels: {
      1: '手戻りが確実に必要',
      2: '手直しが多そう',
      3: '普通',
      4: '質は高い',
      5: 'やり直しの余地がない',
    },
  },
  satisfactionLevel: {
    label: '満足度',
    higherIsBetter: true,
    levels: {
      1: '時間を無駄にした',
      2: '納得できない',
      3: '普通',
      4: '納得できる',
      5: '非常に良い時間だった',
    },
  },
  fatigueAfter: {
    label: '終了時疲労度',
    higherIsBetter: false,
    levels: fatigueLevels,
  },
};

/** 「疲労 2→4 (+2)」のような差分表記。符号を必ず付ける。 */
export function formatDelta(delta: number): string {
  return delta > 0 ? `+${delta}` : String(delta);
}
