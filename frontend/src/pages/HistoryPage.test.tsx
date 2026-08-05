import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import type {
  PerformanceResultResponse,
  WorkSessionDayResponse,
  WorkSessionResponse,
} from '../api/types';
import { get, mockApi, put } from '../test/mockApi';
import { renderWithProviders } from '../test/renderWithProviders';
import { HistoryPage } from './HistoryPage';

const recordedResult: PerformanceResultResponse = {
  focusLevel: 4,
  outputLevel: 4,
  accuracyLevel: 3,
  satisfactionLevel: 4,
  fatigueAfter: 4,
  note: null,
  recordedAt: '2026-08-04T01:45:00.000Z',
  updatedAt: '2026-08-04T01:45:00.000Z',
  isEdited: false,
};

const completed: WorkSessionResponse = {
  id: 301,
  taskItemId: 12,
  taskTitle: '認証方式の検討',
  workTypeId: 2,
  workTypeName: '設計',
  plannedWorkId: null,
  startedAt: '2026-08-04T00:12:00.000Z',
  finishedAt: '2026-08-04T01:45:00.000Z',
  status: 'Completed',
  durationMinutes: 93,
  interruptionCount: 1,
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
    meetingCount: 1,
    interruptionExpected: false,
    recordedAt: '2026-08-04T00:12:00.000Z',
  },
  result: recordedResult,
  fatigueDelta: 2,
  focusGap: 0,
  warnings: [],
};

const abandoned: WorkSessionResponse = {
  ...completed,
  id: 302,
  taskTitle: '競合調査',
  workTypeName: '調査',
  startedAt: '2026-08-04T07:00:00.000Z',
  finishedAt: '2026-08-04T07:08:00.000Z',
  status: 'Abandoned',
  durationMinutes: 8,
  interruptionCount: 0,
  abandonNote: '会議に呼ばれて中断',
  result: null,
  fatigueDelta: null,
  focusGap: null,
};

const inProgress: WorkSessionResponse = {
  ...completed,
  id: 303,
  taskTitle: '実装中の作業',
  finishedAt: null,
  status: 'InProgress',
  durationMinutes: null,
  result: null,
  fatigueDelta: null,
  focusGap: null,
};

function day(sessions: WorkSessionResponse[]): WorkSessionDayResponse {
  return {
    date: '2026-08-04',
    dayOfWeek: 'Tuesday',
    sessions,
    summary: {
      completedCount: sessions.filter((x) => x.status === 'Completed').length,
      abandonedCount: sessions.filter((x) => x.status === 'Abandoned').length,
      totalMinutes: 93,
    },
  };
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('HistoryPage', () => {
  it('日付ごとにセッションを表示する', async () => {
    mockApi([get('/api/work-sessions', [day([completed, abandoned])])]);

    renderWithProviders(<HistoryPage />);

    expect(await screen.findByText('8月4日(火)')).toBeInTheDocument();
    expect(screen.getByText(/完了 1件 \/ 中断終了 1件 \/ 合計 1時間33分/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /認証方式の検討/ })).toBeInTheDocument();
    expect(screen.getByText('[中断終了]')).toBeInTheDocument();
  });

  /**
   * docs/03-use-cases.md UC-08「編集で許可する範囲」。
   *
   * <b>本プロダクトの根幹。</b>結果を知った後に作業前の状態を書き換えられると、
   * 説明変数と目的変数の独立性が失われ、分析が意味を持たなくなる
   * （技術設計 §8 の禁止事項 1）。
   */
  it('作業前の状態・環境・時刻を編集する手段を持たない', async () => {
    mockApi([get('/api/work-sessions', [day([completed])])]);
    const user = userEvent.setup();

    renderWithProviders(<HistoryPage />);
    await screen.findByText('8月4日(火)');

    await user.click(screen.getByRole('button', { name: /認証方式の検討/ }));
    await user.click(screen.getByRole('button', { name: '成果を訂正する' }));

    // 訂正フォームで編集できるのは成果評価と中断回数だけ。
    for (const forbidden of ['疲労度', '見込み集中', '気分', '作業場所', '会議']) {
      expect(screen.queryByRole('group', { name: forbidden })).toBeNull();
    }

    for (const forbidden of [/開始時刻/, /終了時刻/, /作業場所/, /会議件数/]) {
      expect(screen.queryByLabelText(forbidden)).toBeNull();
    }
  });

  /** 記録は削除しない（技術設計 §8 の禁止事項 10）。 */
  it('削除の手段を持たない', async () => {
    mockApi([get('/api/work-sessions', [day([completed])])]);
    const user = userEvent.setup();

    renderWithProviders(<HistoryPage />);
    await screen.findByText('8月4日(火)');

    await user.click(screen.getByRole('button', { name: /認証方式の検討/ }));

    for (const forbidden of [/削除/, /消す/, /取り消/]) {
      expect(screen.queryByRole('button', { name: forbidden })).toBeNull();
    }
  });

  it('進行中のセッションは訂正させない', async () => {
    mockApi([get('/api/work-sessions', [day([inProgress])])]);
    const user = userEvent.setup();

    renderWithProviders(<HistoryPage />);
    await screen.findByText('8月4日(火)');

    await user.click(screen.getByRole('button', { name: /実装中の作業/ }));

    expect(screen.getByText(/進行中/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '成果を訂正する' })).toBeNull();
  });

  it('中断終了の理由を表示する', async () => {
    mockApi([get('/api/work-sessions', [day([abandoned])])]);
    const user = userEvent.setup();

    renderWithProviders(<HistoryPage />);
    await screen.findByText('8月4日(火)');

    await user.click(screen.getByRole('button', { name: /競合調査/ }));

    expect(screen.getByText('会議に呼ばれて中断')).toBeInTheDocument();
  });

  /**
   * docs/07-api-design.md §2.17。
   * 時刻や PreWorkState を送るとサーバーが未知のプロパティとして 400 を返す。
   * 完全一致で検証し、余分なキーが混ざらないことを固定する。
   */
  it('訂正では5指標・メモ・中断回数だけを送る', async () => {
    const { calls } = mockApi([
      get('/api/work-sessions', [day([completed])]),
      put('/api/work-sessions/301/result', 200, completed),
    ]);
    const user = userEvent.setup();

    renderWithProviders(<HistoryPage />);
    await screen.findByText('8月4日(火)');

    await user.click(screen.getByRole('button', { name: /認証方式の検討/ }));
    await user.click(screen.getByRole('button', { name: '成果を訂正する' }));

    await user.click(
      within(screen.getByRole('group', { name: '集中度' })).getByRole('radio', {
        name: '3',
      }),
    );
    await user.click(screen.getByRole('button', { name: '中断回数を増やす' }));
    await user.click(screen.getByRole('button', { name: '保存' }));

    await waitFor(() => {
      const updated = calls.find((call) => call.method === 'PUT');

      expect(updated?.body).toEqual({
        interruptionCount: 2,
        result: {
          focusLevel: 3,
          outputLevel: 4,
          accuracyLevel: 3,
          satisfactionLevel: 4,
          fatigueAfter: 4,
          note: null,
        },
      });
    });
  });

  it('訂正フォームには記録済みの値が入っている', async () => {
    mockApi([get('/api/work-sessions', [day([completed])])]);
    const user = userEvent.setup();

    renderWithProviders(<HistoryPage />);
    await screen.findByText('8月4日(火)');

    await user.click(screen.getByRole('button', { name: /認証方式の検討/ }));
    await user.click(screen.getByRole('button', { name: '成果を訂正する' }));

    expect(
      within(screen.getByRole('group', { name: '集中度' })).getByRole('radio', {
        name: '4',
      }),
    ).toBeChecked();
    expect(screen.getByText('1回')).toBeInTheDocument();
  });

  /** recordedAt と updatedAt の差で事後編集を識別する（PR-2）。 */
  it('訂正済みであることが分かる', async () => {
    const edited: WorkSessionResponse = {
      ...completed,
      result: {
        ...recordedResult,
        updatedAt: '2026-08-05T02:00:00.000Z',
        isEdited: true,
      },
    };
    mockApi([get('/api/work-sessions', [day([edited])])]);
    const user = userEvent.setup();

    renderWithProviders(<HistoryPage />);
    await screen.findByText('8月4日(火)');

    await user.click(screen.getByRole('button', { name: /認証方式の検討/ }));

    expect(screen.getByText('2026/08/05 11:00 に訂正')).toBeInTheDocument();
  });

  it('記録が無ければ作業開始へ誘導する', async () => {
    mockApi([get('/api/work-sessions', [])]);

    renderWithProviders(<HistoryPage />);

    expect(await screen.findByText('まだ記録がありません。')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '作業を開始する' })).toBeInTheDocument();
  });
});
