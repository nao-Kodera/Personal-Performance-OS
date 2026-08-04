import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import type { TaskItemResponse, WorkSessionResponse, WorkTypeResponse } from '../api/types';
import { get, getNoContent, mockApi, post } from '../test/mockApi';
import { renderWithProviders } from '../test/renderWithProviders';
import { StartSessionPage } from './StartSessionPage';

const workTypes: WorkTypeResponse[] = [
  { id: 1, name: '実装', displayOrder: 10, isActive: true },
  { id: 2, name: '設計', displayOrder: 20, isActive: true },
];

const tasks: TaskItemResponse[] = [
  {
    id: 12,
    title: '認証方式の検討',
    defaultWorkTypeId: 2,
    defaultWorkTypeName: '設計',
    note: null,
    isArchived: false,
    lastUsedAt: null,
    sessionCount: 0,
    createdAt: '2026-08-01T00:00:00.000Z',
    updatedAt: '2026-08-01T00:00:00.000Z',
  },
];

const activeSession: WorkSessionResponse = {
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

/** 進行中セッションが無い、通常の初期状態。 */
function idleHandlers() {
  return [
    getNoContent('/api/work-sessions/active'),
    get('/api/work-types', workTypes),
    get('/api/tasks', tasks),
  ];
}

/** 必須 6 項目を埋める。既定値のまま進める項目には触れない。 */
async function fillRequiredInputs() {
  await userEvent.click(screen.getByRole('radio', { name: /認証方式の検討/ }));

  await userEvent.click(
    within(screen.getByRole('group', { name: '疲労度' })).getByRole('radio', { name: '2' }),
  );
  await userEvent.click(
    within(screen.getByRole('group', { name: '集中できそうか' })).getByRole('radio', {
      name: '4',
    }),
  );
  await userEvent.click(
    within(screen.getByRole('group', { name: '気分' })).getByRole('radio', { name: '4' }),
  );

  await userEvent.click(screen.getByRole('radio', { name: '自宅' }));
}

function currentPath() {
  return screen.getByTestId('location').textContent;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('StartSessionPage', () => {
  /**
   * docs/08-technical-design.md §8 の禁止事項 11。
   * 入力項目が増えると 30 秒に収まらず、記録自体が続かなくなる。
   * 項目を追加するとこのテストが落ちる。落ちたら設計書に戻ること。
   */
  it('入力項目を増やさない', async () => {
    mockApi(idleHandlers());

    renderWithProviders(<StartSessionPage />);
    await screen.findByRole('group', { name: '作業タイプ' });

    const legends = screen
      .getAllByRole('group')
      .map((group) => group.querySelector('legend')?.textContent);

    expect(legends).toEqual([
      '作業タイプ',
      '疲労度',
      '集中できそうか',
      '気分',
      '作業場所',
      '割り込みが予想されるか',
    ]);
  });

  it('タスクを選ぶと作業タイプが既定値で選択される', async () => {
    mockApi(idleHandlers());

    renderWithProviders(<StartSessionPage />);
    await screen.findByRole('group', { name: '作業タイプ' });

    expect(screen.getByRole('radio', { name: '設計' })).not.toBeChecked();

    await userEvent.click(screen.getByRole('radio', { name: /認証方式の検討/ }));

    expect(screen.getByRole('radio', { name: '設計' })).toBeChecked();
  });

  it('会議件数と割り込み予想に既定値を置く', async () => {
    mockApi(idleHandlers());

    renderWithProviders(<StartSessionPage />);
    await screen.findByRole('group', { name: '作業タイプ' });

    expect(screen.getByText('0件')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'いいえ' })).toBeChecked();
  });

  /**
   * 評価 3 項目に既定値を置くと、観測していない値が記録される。
   * 説明変数の信頼性が失われるため、未選択から始めること。
   */
  it('作業前の評価に既定値を置かない', async () => {
    mockApi(idleHandlers());

    renderWithProviders(<StartSessionPage />);
    await screen.findByRole('group', { name: '作業タイプ' });

    expect(screen.getAllByText('未選択')).toHaveLength(3);
    expect(screen.getByRole('button', { name: '開始' })).toBeDisabled();
  });

  it('必須が埋まるまで開始できない', async () => {
    mockApi(idleHandlers());

    renderWithProviders(<StartSessionPage />);
    await screen.findByRole('group', { name: '作業タイプ' });

    await userEvent.click(screen.getByRole('radio', { name: /認証方式の検討/ }));
    await userEvent.click(
      within(screen.getByRole('group', { name: '疲労度' })).getByRole('radio', { name: '2' }),
    );

    expect(screen.getByRole('button', { name: '開始' })).toBeDisabled();

    await fillRequiredInputs();

    expect(screen.getByRole('button', { name: '開始' })).toBeEnabled();
  });

  /**
   * docs/05-domain-design.md WS-8。
   * 開始時刻はサーバーが決める。クライアントから送ると、後付けの記録が可能になる。
   * 定義外のプロパティはサーバーが 400 にするため、完全一致で検証する。
   */
  it('開始時刻を送らない', async () => {
    const { calls } = mockApi([
      ...idleHandlers(),
      post('/api/work-sessions/start', 201, activeSession),
    ]);

    renderWithProviders(<StartSessionPage />);
    await screen.findByRole('group', { name: '作業タイプ' });

    await fillRequiredInputs();
    await userEvent.click(screen.getByRole('button', { name: '開始' }));

    await waitFor(() => {
      const started = calls.find((call) => call.method === 'POST');

      expect(started?.body).toEqual({
        taskItemId: 12,
        workTypeId: 2,
        preWorkState: { fatigueLevel: 2, expectedFocusLevel: 4, moodLevel: 4 },
        workContext: {
          workLocation: 'Home',
          locationNote: null,
          meetingCount: 0,
          interruptionExpected: false,
        },
      });
    });
  });

  /** WC-2。「その他」以外で補足を送ると 422 になる。 */
  it('その他を選んだときだけ場所の補足を送る', async () => {
    const { calls } = mockApi([
      ...idleHandlers(),
      post('/api/work-sessions/start', 201, activeSession),
    ]);

    renderWithProviders(<StartSessionPage />);
    await screen.findByRole('group', { name: '作業タイプ' });

    await fillRequiredInputs();

    await userEvent.click(screen.getByRole('radio', { name: 'その他' }));
    await userEvent.type(screen.getByLabelText('場所の補足'), '実家');
    await userEvent.click(screen.getByRole('button', { name: '開始' }));

    await waitFor(() => {
      const body = calls.find((call) => call.method === 'POST')?.body as
        | { workContext: { workLocation: string; locationNote: string | null } }
        | undefined;

      expect(body?.workContext.workLocation).toBe('Other');
      expect(body?.workContext.locationNote).toBe('実家');
    });
  });

  it('開始したら作業中の画面へ遷移する', async () => {
    mockApi([...idleHandlers(), post('/api/work-sessions/start', 201, activeSession)]);

    renderWithProviders(<StartSessionPage />, { route: '/sessions/start' });
    await screen.findByRole('group', { name: '作業タイプ' });

    await fillRequiredInputs();
    await userEvent.click(screen.getByRole('button', { name: '開始' }));

    await waitFor(() => {
      expect(currentPath()).toBe('/sessions/active');
    });
  });

  /**
   * docs/07-api-design.md §2.14。
   * 409 は異常ではない。複数タブや戻る操作で起きうる正常な競合であり、
   * エラーを見せずに進行中の画面へ送る。
   */
  it('409 ならエラーを出さずに作業中の画面へ送る', async () => {
    mockApi([
      ...idleHandlers(),
      post('/api/work-sessions/start', 409, {
        status: 409,
        detail: '進行中の作業セッションが既に存在します。',
      }),
    ]);

    renderWithProviders(<StartSessionPage />, { route: '/sessions/start' });
    await screen.findByRole('group', { name: '作業タイプ' });

    await fillRequiredInputs();
    await userEvent.click(screen.getByRole('button', { name: '開始' }));

    await waitFor(() => {
      expect(currentPath()).toBe('/sessions/active');
    });

    expect(screen.queryByRole('alert')).toBeNull();
  });

  /**
   * UC-04 の例外。進行中のセッションがあるときは、この画面を使わせない。
   * 同時に 2 件記録すると、どちらの成果か分離できない（WS-9）。
   */
  it('進行中のセッションがあるときは入力させない', async () => {
    mockApi([
      get('/api/work-sessions/active', activeSession),
      get('/api/work-types', workTypes),
      get('/api/tasks', tasks),
    ]);

    renderWithProviders(<StartSessionPage />);

    expect(await screen.findByText(/進行中の作業があります/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '作業中の画面へ' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '開始' })).toBeNull();
    expect(screen.queryByRole('group', { name: '疲労度' })).toBeNull();
  });
});
