import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { WorkSessionResponse } from '../api/types';
import { get, getNoContent, mockApi, post } from '../test/mockApi';
import { renderWithProviders } from '../test/renderWithProviders';
import { ActiveSessionPage } from './ActiveSessionPage';

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

/**
 * Date だけを固定し、タイマーは実物のまま動かす。
 * 1 秒ごとの再描画も React Query の解決も本来の経路を通る。
 */
beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date('2026-08-04T00:42:15.000Z'));
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe('ActiveSessionPage', () => {
  it('作業中のタスクと環境を表示する', async () => {
    mockApi([get('/api/work-sessions/active', session)]);

    renderWithProviders(<ActiveSessionPage />);

    expect(await screen.findByText('認証方式の検討')).toBeInTheDocument();
    expect(screen.getByText('設計 / 自宅')).toBeInTheDocument();
  });

  it('経過時間を開始時刻から計算する', async () => {
    mockApi([get('/api/work-sessions/active', session)]);

    renderWithProviders(<ActiveSessionPage />);

    // 00:12:00 開始・現在 00:42:15。
    expect(await screen.findByText('00:30:15')).toBeInTheDocument();
  });

  /**
   * docs/08-technical-design.md §3.11。
   * クライアント側でカウントアップした値を保持していれば、時計を 10 分進めても
   * 表示は 1 秒しか進まない。毎回 startedAt から再計算していることの確認。
   */
  it('時計が進んだ分だけ経過時間が進む', async () => {
    mockApi([get('/api/work-sessions/active', session)]);

    renderWithProviders(<ActiveSessionPage />);
    await screen.findByText('00:30:15');

    vi.setSystemTime(new Date('2026-08-04T00:52:15.000Z'));

    await waitFor(
      () => {
        expect(screen.getByText('00:40:15')).toBeInTheDocument();
      },
      { timeout: 3000 },
    );
  });

  /**
   * docs/03-use-cases.md §5。
   * 一時停止を許すと「実作業時間」の定義が曖昧になる。中断は回数でのみ記録する。
   */
  it('一時停止の操作を持たない', async () => {
    mockApi([get('/api/work-sessions/active', session)]);

    renderWithProviders(<ActiveSessionPage />);
    await screen.findByText('認証方式の検討');

    for (const forbidden of [/一時停止/, /停止/, /ポーズ/, /pause/i, /再開/]) {
      expect(screen.queryByRole('button', { name: forbidden })).toBeNull();
    }
  });

  it('中断を加算してもサーバーに送らない', async () => {
    const { calls } = mockApi([get('/api/work-sessions/active', session)]);
    const user = userEvent.setup();

    renderWithProviders(<ActiveSessionPage />);
    await screen.findByText('認証方式の検討');

    expect(screen.getByText('中断: 0回')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: '+1' }));
    await user.click(screen.getByRole('button', { name: '+1' }));

    expect(screen.getByText('中断: 2回')).toBeInTheDocument();
    expect(calls.filter((call) => call.method !== 'GET')).toHaveLength(0);
  });

  /** 減算は S-06 で行う（誤操作の訂正）。この画面には置かない。 */
  it('中断の減算を持たない', async () => {
    mockApi([get('/api/work-sessions/active', session)]);

    renderWithProviders(<ActiveSessionPage />);
    await screen.findByText('認証方式の検討');

    expect(screen.queryByRole('button', { name: '-1' })).toBeNull();
    expect(screen.queryByRole('button', { name: '−1' })).toBeNull();
  });

  it('終了すると中断回数を持って成果評価へ進む', async () => {
    mockApi([get('/api/work-sessions/active', session)]);
    const user = userEvent.setup();

    renderWithProviders(<ActiveSessionPage />, { route: '/sessions/active' });
    await screen.findByText('認証方式の検討');

    await user.click(screen.getByRole('button', { name: '+1' }));
    await user.click(screen.getByRole('button', { name: '終了' }));

    await waitFor(() => {
      expect(screen.getByTestId('location').textContent).toBe('/sessions/301/finish');
    });

    expect(screen.getByTestId('location-state').textContent).toBe(
      JSON.stringify({ interruptionCount: 1 }),
    );
  });

  /** UC-06 手順2。確認なしに中断終了しない。 */
  it('中断終了は確認してから実行する', async () => {
    const { calls } = mockApi([
      get('/api/work-sessions/active', session),
      post('/api/work-sessions/301/abandon', 200, {
        ...session,
        status: 'Abandoned',
        finishedAt: '2026-08-04T00:42:15.000Z',
      }),
    ]);
    const user = userEvent.setup();

    renderWithProviders(<ActiveSessionPage />, { route: '/sessions/active' });
    await screen.findByText('認証方式の検討');

    await user.click(screen.getByRole('button', { name: '中断終了' }));

    expect(
      screen.getByText('成果は記録されず、分析の対象外になります。'),
    ).toBeInTheDocument();
    expect(calls.filter((call) => call.method === 'POST')).toHaveLength(0);

    await user.type(screen.getByLabelText(/理由メモ/), '会議に呼ばれて中断');
    await user.click(screen.getByRole('button', { name: '中断終了する' }));

    await waitFor(() => {
      const abandoned = calls.find((call) => call.method === 'POST');

      expect(abandoned?.url).toContain('/api/work-sessions/301/abandon');
      expect(abandoned?.body).toEqual({ note: '会議に呼ばれて中断' });
    });

    await waitFor(() => {
      expect(screen.getByTestId('location').textContent).toBe('/');
    });
  });

  it('確認をやめると元に戻る', async () => {
    mockApi([get('/api/work-sessions/active', session)]);
    const user = userEvent.setup();

    renderWithProviders(<ActiveSessionPage />);
    await screen.findByText('認証方式の検討');

    await user.click(screen.getByRole('button', { name: '中断終了' }));
    await user.click(screen.getByRole('button', { name: 'やめる' }));

    expect(screen.getByRole('button', { name: '終了' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '中断終了する' })).toBeNull();
  });

  it('進行中の作業が無ければ開始画面へ誘導する', async () => {
    mockApi([getNoContent('/api/work-sessions/active')]);

    renderWithProviders(<ActiveSessionPage />);

    expect(await screen.findByText('進行中の作業はありません。')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '作業を開始する' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '終了' })).toBeNull();
  });
});
