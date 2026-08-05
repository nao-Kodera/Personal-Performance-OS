import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { WorkSessionResponse } from '../api/types';
import { get, getNoContent, mockApi, post } from '../test/mockApi';
import { renderWithProviders } from '../test/renderWithProviders';
import { FinishSessionPage } from './FinishSessionPage';

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

const completed: WorkSessionResponse = {
  ...session,
  status: 'Completed',
  finishedAt: '2026-08-04T01:45:00.000Z',
  durationMinutes: 93,
  interruptionCount: 1,
  result: {
    focusLevel: 4,
    outputLevel: 4,
    accuracyLevel: 3,
    satisfactionLevel: 4,
    fatigueAfter: 4,
    note: null,
    recordedAt: '2026-08-04T01:45:00.000Z',
    updatedAt: '2026-08-04T01:45:00.000Z',
    isEdited: false,
  },
  fatigueDelta: 2,
  focusGap: 0,
  warnings: [],
};

const ROUTE = { route: '/sessions/301/finish', path: '/sessions/:id/finish' };

beforeEach(() => {
  // 00:12:00 開始・現在 01:45:00 = 93 分。警告の閾値には掛からない。
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date('2026-08-04T01:45:00.000Z'));
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

/** 5 指標すべてを選ぶ。 */
async function fillAllMetrics(user: ReturnType<typeof userEvent.setup>) {
  const select = async (metric: string, rating: string) => {
    await user.click(
      within(screen.getByRole('group', { name: metric })).getByRole('radio', {
        name: rating,
      }),
    );
  };

  await select('集中度', '4');
  await select('成果度', '4');
  await select('正確性', '3');
  await select('満足度', '4');
  await select('終了時疲労度', '4');
}

describe('FinishSessionPage', () => {
  /**
   * docs/08-technical-design.md §8 の禁止事項 4。
   * 評価が欠けたセッションは分析に使えず、記録した労力が無駄になる。
   * Completed なら PerformanceResult が必ず存在する（WS-3）。
   */
  it('スキップ導線を持たない', async () => {
    mockApi([get('/api/work-sessions/active', session)]);

    renderWithProviders(<FinishSessionPage />, ROUTE);
    await screen.findByRole('group', { name: '集中度' });

    for (const forbidden of [/スキップ/, /あとで/, /後で/, /保存せず/, /評価しない/]) {
      expect(screen.queryByRole('button', { name: forbidden })).toBeNull();
      expect(screen.queryByRole('link', { name: forbidden })).toBeNull();
    }
  });

  it('5指標がすべて揃うまで記録できない', async () => {
    mockApi([get('/api/work-sessions/active', session)]);
    const user = userEvent.setup();

    renderWithProviders(<FinishSessionPage />, ROUTE);
    await screen.findByRole('group', { name: '集中度' });

    expect(screen.getByRole('button', { name: '記録して終了' })).toBeDisabled();

    // 4 項目だけ選んでも押せない。
    await user.click(
      within(screen.getByRole('group', { name: '集中度' })).getByRole('radio', {
        name: '4',
      }),
    );
    await user.click(
      within(screen.getByRole('group', { name: '成果度' })).getByRole('radio', {
        name: '4',
      }),
    );
    await user.click(
      within(screen.getByRole('group', { name: '正確性' })).getByRole('radio', {
        name: '3',
      }),
    );
    await user.click(
      within(screen.getByRole('group', { name: '満足度' })).getByRole('radio', {
        name: '4',
      }),
    );

    expect(screen.getByRole('button', { name: '記録して終了' })).toBeDisabled();

    await user.click(
      within(screen.getByRole('group', { name: '終了時疲労度' })).getByRole('radio', {
        name: '4',
      }),
    );

    expect(screen.getByRole('button', { name: '記録して終了' })).toBeEnabled();
  });

  it('S-05 から運ばれた中断回数を初期表示する', async () => {
    mockApi([get('/api/work-sessions/active', session)]);

    renderWithProviders(<FinishSessionPage />, { ...ROUTE, state: { interruptionCount: 3 } });
    await screen.findByRole('group', { name: '集中度' });

    expect(screen.getByText('3回')).toBeInTheDocument();
  });

  /** 減算はこの画面で行う（誤操作の訂正・docs/03-use-cases.md §5）。 */
  it('中断回数を減らせる', async () => {
    mockApi([get('/api/work-sessions/active', session)]);
    const user = userEvent.setup();

    renderWithProviders(<FinishSessionPage />, { ...ROUTE, state: { interruptionCount: 3 } });
    await screen.findByRole('group', { name: '集中度' });

    await user.click(screen.getByRole('button', { name: '中断回数を減らす' }));

    expect(screen.getByText('2回')).toBeInTheDocument();
  });

  it('終了時刻を送らず、5指標と中断回数だけを送る', async () => {
    const { calls } = mockApi([
      get('/api/work-sessions/active', session),
      post('/api/work-sessions/301/finish', 200, completed),
    ]);
    const user = userEvent.setup();

    renderWithProviders(<FinishSessionPage />, { ...ROUTE, state: { interruptionCount: 1 } });
    await screen.findByRole('group', { name: '集中度' });

    await fillAllMetrics(user);
    await user.click(screen.getByRole('button', { name: '記録して終了' }));

    await waitFor(() => {
      const finished = calls.find((call) => call.method === 'POST');

      expect(finished?.body).toEqual({
        interruptionCount: 1,
        result: {
          focusLevel: 4,
          outputLevel: 4,
          accuracyLevel: 3,
          satisfactionLevel: 4,
          fatigueAfter: 4,
          note: null,
        },
      });
    });
  });

  /**
   * 疲労増加量と集中の差はサーバーが返した値を出す。クライアントで計算すると
   * PreWorkState を保持する必要があり、リロードで失われる（API §2.15）。
   */
  it('記録後に疲労増加量を表示し、合成スコアを出さない', async () => {
    mockApi([
      get('/api/work-sessions/active', session),
      post('/api/work-sessions/301/finish', 200, completed),
    ]);
    const user = userEvent.setup();

    renderWithProviders(<FinishSessionPage />, ROUTE);
    await screen.findByRole('group', { name: '集中度' });

    await fillAllMetrics(user);
    await user.click(screen.getByRole('button', { name: '記録して終了' }));

    expect(await screen.findByText('記録しました')).toBeInTheDocument();

    const values = screen.getAllByRole('definition').map((node) => node.textContent);

    expect(values[0]).toContain('2 → 4');
    expect(values[0]).toContain('+2');

    // 禁止事項 5。指標ごとに意味が違い、平均すると何を観測したのか分からなくなる。
    for (const forbidden of [/総合/, /スコア/, /点数/, /平均/]) {
      expect(screen.queryByText(forbidden)).toBeNull();
    }
  });

  it('1分未満なら警告して中断終了への導線を出す', async () => {
    vi.setSystemTime(new Date('2026-08-04T00:12:30.000Z'));
    mockApi([get('/api/work-sessions/active', session)]);

    renderWithProviders(<FinishSessionPage />, ROUTE);
    await screen.findByRole('group', { name: '集中度' });

    expect(screen.getByText(/1分未満です/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '中断終了に切り替える' })).toBeInTheDocument();

    // 警告は保存を妨げない。
    expect(screen.getByRole('button', { name: '記録して終了' })).toBeInTheDocument();
  });

  it('8時間を超えていたら警告する', async () => {
    vi.setSystemTime(new Date('2026-08-04T08:13:00.000Z'));
    mockApi([get('/api/work-sessions/active', session)]);

    renderWithProviders(<FinishSessionPage />, ROUTE);
    await screen.findByRole('group', { name: '集中度' });

    expect(screen.getByText(/8時間を超えています/)).toBeInTheDocument();
  });

  it('進行中でなければ入力させない', async () => {
    mockApi([getNoContent('/api/work-sessions/active')]);

    renderWithProviders(<FinishSessionPage />, ROUTE);

    expect(
      await screen.findByText('このセッションは進行中ではありません。'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('group', { name: '集中度' })).toBeNull();
  });

  it('進行中のセッションと URL の ID が食い違えば入力させない', async () => {
    mockApi([get('/api/work-sessions/active', { ...session, id: 999 })]);

    renderWithProviders(<FinishSessionPage />, ROUTE);

    expect(
      await screen.findByText('このセッションは進行中ではありません。'),
    ).toBeInTheDocument();
  });
});
