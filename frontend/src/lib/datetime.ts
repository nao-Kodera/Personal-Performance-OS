/**
 * UTC → JST の表示変換。
 *
 * <b>変換をこのファイルに閉じ込める。</b>画面ごとに変換を書かないこと
 * （docs/08-technical-design.md §3.10）。
 *
 * API が返す日時はすべて UTC（末尾 Z）であり、表示は JST で行う
 * （docs/07-api-design.md §0.1）。ブラウザのタイムゾーン設定に依存させないため、
 * 常に timeZone: 'Asia/Tokyo' を明示する。端末が UTC でも JST 以外でも
 * 同じ表示になる。
 */

const JST = 'Asia/Tokyo';

const timeFormatter = new Intl.DateTimeFormat('ja-JP', {
  timeZone: JST,
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});

const dateFormatter = new Intl.DateTimeFormat('ja-JP', {
  timeZone: JST,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

const dayLabelFormatter = new Intl.DateTimeFormat('ja-JP', {
  timeZone: JST,
  month: 'long',
  day: 'numeric',
  weekday: 'short',
});

/** 「09:12」。 */
export function formatJstTime(isoUtc: string): string {
  return timeFormatter.format(new Date(isoUtc));
}

/** 「2026/08/04」。 */
export function formatJstDate(isoUtc: string): string {
  return dateFormatter.format(new Date(isoUtc));
}

/** 「2026/08/04 09:12」。 */
export function formatJstDateTime(isoUtc: string): string {
  return `${formatJstDate(isoUtc)} ${formatJstTime(isoUtc)}`;
}

/**
 * JST 基準の日付文字列（YYYY-MM-DD）。API のクエリパラメータに使う。
 * 日付のみの項目は JST の論理日である（docs/02-glossary.md §4）。
 */
export function toJstDateString(value: Date | string): string {
  const date = typeof value === 'string' ? new Date(value) : value;

  // en-CA ロケールは YYYY-MM-DD 形式を返す。
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: JST,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(date);
}

/** JST 基準の「今日」。 */
export function todayJst(now: Date = new Date()): string {
  return toJstDateString(now);
}

/**
 * 「8月4日(火)」。API が返す日付文字列（YYYY-MM-DD）を受け取る。
 */
export function formatDayLabel(jstDate: string): string {
  // 日付のみの文字列は JST の論理日である。UTC として解釈されると
  // 前日にずれるため、JST の正午を基準に組み立てる。
  const date = new Date(`${jstDate}T12:00:00+09:00`);

  return dayLabelFormatter.format(date);
}

/** 「93分」「1時間33分」。 */
export function formatDuration(minutes: number): string {
  if (minutes < 60) {
    return `${minutes}分`;
  }

  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;

  return rest === 0 ? `${hours}時間` : `${hours}時間${rest}分`;
}

/**
 * 経過時間を「00:42:15」で返す。
 *
 * 基準は必ずサーバーが記録した開始時刻である。クライアント側で
 * カウントアップした値を保持しないこと。リロードやタブ復帰で値が失われる
 * （docs/08-technical-design.md §3.11）。
 */
export function formatElapsed(startedAtUtc: string, now: Date = new Date()): string {
  const elapsedMs = now.getTime() - new Date(startedAtUtc).getTime();
  const totalSeconds = Math.max(0, Math.floor(elapsedMs / 1000));

  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  return [hours, minutes, seconds].map((x) => String(x).padStart(2, '0')).join(':');
}

/** 指定日数だけ前の JST 日付（YYYY-MM-DD）。分析・履歴の期間指定に使う。 */
export function jstDaysAgo(days: number, now: Date = new Date()): string {
  const shifted = new Date(now.getTime() - days * 24 * 60 * 60 * 1000);

  return toJstDateString(shifted);
}
