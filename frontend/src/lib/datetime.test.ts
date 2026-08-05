import { describe, expect, it } from 'vitest';

import {
  formatDayLabel,
  formatDuration,
  formatElapsed,
  formatFullDayLabel,
  formatJstDate,
  formatJstDateTime,
  formatJstTime,
  jstDaysAgo,
  todayJst,
  toJstDateString,
} from './datetime';

/**
 * テストは UTC 環境で実行される（src/test/setup.ts）。
 * 端末が JST でなくても JST 表示になることを確認する。
 */
describe('JST への表示変換', () => {
  it('UTC の時刻を JST の時刻で表示する', () => {
    // UTC 00:12 = JST 09:12
    expect(formatJstTime('2026-08-04T00:12:00.000Z')).toBe('09:12');
  });

  it('日付境界の直前は前日になる', () => {
    // UTC 2026-08-03 14:59 = JST 2026-08-03 23:59
    expect(formatJstDate('2026-08-03T14:59:00.000Z')).toBe('2026/08/03');
  });

  it('日付境界ちょうどは新しい日になる', () => {
    // UTC 2026-08-03 15:00 = JST 2026-08-04 00:00
    expect(formatJstDate('2026-08-03T15:00:00.000Z')).toBe('2026/08/04');
    expect(formatJstTime('2026-08-03T15:00:00.000Z')).toBe('00:00');
  });

  it('日時をまとめて表示できる', () => {
    expect(formatJstDateTime('2026-08-04T00:12:00.000Z')).toBe('2026/08/04 09:12');
  });

  it('JST 基準の日付文字列を返す', () => {
    expect(toJstDateString('2026-08-03T15:00:00.000Z')).toBe('2026-08-04');
    expect(toJstDateString('2026-08-03T14:59:59.000Z')).toBe('2026-08-03');
  });

  it('深夜零時過ぎの今日は UTC の日付と異なる', () => {
    // UTC 2026-08-03 15:15 = JST 2026-08-04 00:15
    expect(todayJst(new Date('2026-08-03T15:15:00.000Z'))).toBe('2026-08-04');
  });

  it('指定日数前の JST 日付を返す', () => {
    expect(jstDaysAgo(27, new Date('2026-08-04T00:00:00.000Z'))).toBe('2026-07-08');
  });
});

describe('日付ラベル', () => {
  it('日付文字列を曜日つきで表示する', () => {
    // 2026-08-04 は火曜日
    expect(formatDayLabel('2026-08-04')).toBe('8月4日(火)');
  });

  it('日付のみの文字列が UTC 解釈で前日にずれない', () => {
    // 素朴に new Date('2026-08-01') とすると UTC 解釈になり、
    // JST 表示では 7月31日 になってしまう。
    expect(formatDayLabel('2026-08-01')).toBe('8月1日(土)');
  });

  it('年つきの日付ラベルを返す', () => {
    expect(formatFullDayLabel('2026-08-04')).toBe('2026年8月4日(火)');
  });

  it('年つきでも UTC 解釈で前日にずれない', () => {
    expect(formatFullDayLabel('2026-08-01')).toBe('2026年8月1日(土)');
  });
});

describe('実作業時間', () => {
  it.each([
    [0, '0分'],
    [1, '1分'],
    [59, '59分'],
    [60, '1時間'],
    [93, '1時間33分'],
    [120, '2時間'],
    [481, '8時間1分'],
  ])('%i 分を %s と表示する', (minutes, expected) => {
    expect(formatDuration(minutes)).toBe(expected);
  });
});

describe('経過時間', () => {
  const startedAt = '2026-08-04T00:12:00.000Z';

  it.each([
    ['2026-08-04T00:12:00.000Z', '00:00:00'],
    ['2026-08-04T00:12:15.000Z', '00:00:15'],
    ['2026-08-04T00:54:15.000Z', '00:42:15'],
    ['2026-08-04T02:12:00.000Z', '02:00:00'],
  ])('現在時刻 %s で %s を返す', (now, expected) => {
    expect(formatElapsed(startedAt, new Date(now))).toBe(expected);
  });

  it('開始時刻より前でも負の値にならない', () => {
    expect(formatElapsed(startedAt, new Date('2026-08-04T00:11:00.000Z'))).toBe('00:00:00');
  });
});
