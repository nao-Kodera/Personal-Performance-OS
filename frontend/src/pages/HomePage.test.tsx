import { screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { WorkSessionDayResponse, WorkSessionResponse } from '../api/types';
import { get, getNoContent, mockApi } from '../test/mockApi';
import { renderWithProviders } from '../test/renderWithProviders';
import { HomePage } from './HomePage';

const session: WorkSessionResponse = {
  id: 301,
  taskItemId: 12,
  taskTitle: '認証方式の検討',
  workTypeId: 2,
  workTypeName: '設計',
  plannedWorkId: null,
  startedAt: '2026-08-04T00:12:00.000Z',
  finishedAt: null,
  status: 'InProgress',
  durationMinutes: null,
  interruptionCount: 0,
  abandonNote: null,
  timeBand: 'Morning',
  preWorkState: {
    fatigueLevel: 2,
    expectedFocusLevel: 4,
    moodLevel: 4,
    recordedAt: '2026-08-04T00:12:00.000Z',
  },
  workContext: {
    workLocation: 'Home',
    locationNote: null,
    meetingCount: 0,
    interruptionExpected: false,
    recordedAt: '2026-08-04T00:12:00.000Z',
  },
  result: null,
  fatigueDelta: null,
  focusGap: null,
  warnings: [],
};

const today: WorkSessionDayResponse = {
  date: '2026-08-04',
  dayOfWeek: 'Tuesday',
  sessions: [],
  summary: { completedCount: 2, abandonedCount: 0, totalMinutes: 183 },
};

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date('2026-08-04T00:54:00.000Z'));
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe('HomePage', () => {
  it('今日の日付を表示する', async () => {
    mockApi([getNoContent('/api/work-sessions/active'), get('/api/work-sessions', [])]);

    renderWithProviders(<HomePage />);

    expect(await screen.findByText('2026年8月4日(火)')).toBeInTheDocument();
  });

  it('進行中の作業と経過時間を表示する', async () => {
    mockApi([
      get('/api/work-sessions/active', session),
      get('/api/work-sessions', [today]),
    ]);

    renderWithProviders(<HomePage />);

    // 00:12 開始・現在 00:54 = 42 分。
    expect(await screen.findByText(/認証方式の検討/)).toBeInTheDocument();
    expect(screen.getByText('経過 42分')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '作業中の画面へ' })).toBeInTheDocument();
  });

  /**
   * docs/03-use-cases.md §4 の状態別制御。
   * 同時に InProgress は 1 件まで（WS-9）。並行作業を記録しても、どちらの成果か
   * 分離できない。サーバーも 409 で拒むが、画面でも開始させない。
   */
  it('進行中があれば作業を開始できない', async () => {
    mockApi([
      get('/api/work-sessions/active', session),
      get('/api/work-sessions', [today]),
    ]);

    renderWithProviders(<HomePage />);
    await screen.findByText(/認証方式の検討/);

    expect(screen.getByRole('button', { name: '作業を開始' })).toBeDisabled();
    expect(screen.queryByRole('link', { name: '作業を開始' })).toBeNull();
  });

  it('進行中がなければ作業を開始できる', async () => {
    mockApi([getNoContent('/api/work-sessions/active'), get('/api/work-sessions', [])]);

    renderWithProviders(<HomePage />);
    await screen.findByText('2026年8月4日(火)');

    const start = await screen.findByRole('link', { name: '作業を開始' });

    expect(start).toHaveAttribute('href', '/sessions/start');
    expect(screen.queryByRole('button', { name: '作業を開始' })).toBeNull();
  });

  it('今日の記録を表示する', async () => {
    mockApi([
      getNoContent('/api/work-sessions/active'),
      get('/api/work-sessions', [today]),
    ]);

    renderWithProviders(<HomePage />);

    expect(
      await screen.findByText('完了 2件 / 中断終了 0件 / 合計 3時間3分'),
    ).toBeInTheDocument();
  });

  /** 0 件であることも情報なので、枠ごと消さない。 */
  it('記録が無くても今日の記録の枠を出す', async () => {
    mockApi([getNoContent('/api/work-sessions/active'), get('/api/work-sessions', [])]);

    renderWithProviders(<HomePage />);

    expect(await screen.findByText('今日はまだ記録がありません。')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '今日の記録' })).toBeInTheDocument();
  });

  it('タスクと履歴への導線がある', async () => {
    mockApi([getNoContent('/api/work-sessions/active'), get('/api/work-sessions', [])]);

    renderWithProviders(<HomePage />);
    await screen.findByText('2026年8月4日(火)');

    expect(screen.getByRole('link', { name: 'タスク' })).toHaveAttribute('href', '/tasks');
    expect(screen.getByRole('link', { name: '履歴' })).toHaveAttribute('href', '/history');
  });
});
