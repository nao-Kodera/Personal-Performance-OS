import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import type { TaskItemResponse, WorkTypeResponse } from '../api/types';
import { get, mockApi, post } from '../test/mockApi';
import { renderWithProviders } from '../test/renderWithProviders';
import { TasksPage } from './TasksPage';

const workTypes: WorkTypeResponse[] = [
  { id: 1, name: '実装', displayOrder: 10, isActive: true },
  { id: 2, name: '設計', displayOrder: 20, isActive: true },
];

function task(overrides: Partial<TaskItemResponse> & { id: number; title: string }): TaskItemResponse {
  return {
    defaultWorkTypeId: 2,
    defaultWorkTypeName: '設計',
    note: null,
    isArchived: false,
    lastUsedAt: null,
    sessionCount: 0,
    createdAt: '2026-08-01T00:00:00.000Z',
    updatedAt: '2026-08-01T00:00:00.000Z',
    ...overrides,
  };
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('TasksPage', () => {
  it('タスクの一覧を表示する', async () => {
    mockApi([
      get('/api/work-types', workTypes),
      get('/api/tasks', [
        task({ id: 1, title: '認証方式の検討', sessionCount: 4, lastUsedAt: '2026-08-03T01:00:00.000Z' }),
      ]),
    ]);

    renderWithProviders(<TasksPage />);

    expect(await screen.findByText('認証方式の検討')).toBeInTheDocument();
    expect(screen.getByText(/設計・最終 2026\/08\/03（4回）/)).toBeInTheDocument();
  });

  /**
   * docs/08-technical-design.md §8 の禁止事項 2。
   * TaskItem に完了の概念はない。アーカイブは完了ではなく選択肢からの除外である。
   */
  it('完了操作を持たない', async () => {
    mockApi([
      get('/api/work-types', workTypes),
      get('/api/tasks', [task({ id: 1, title: '認証方式の検討' })]),
    ]);

    renderWithProviders(<TasksPage />);
    await screen.findByText('認証方式の検討');

    for (const forbidden of ['完了', 'done', 'Done', '済み']) {
      expect(screen.queryByRole('button', { name: new RegExp(forbidden) })).toBeNull();
    }

    expect(screen.queryByRole('checkbox', { name: /完了/ })).toBeNull();
    expect(screen.getByRole('button', { name: 'アーカイブ' })).toBeInTheDocument();
  });

  it('期限や優先度の入力を持たない', async () => {
    mockApi([get('/api/work-types', workTypes), get('/api/tasks', [])]);

    renderWithProviders(<TasksPage />);
    await screen.findByText('まだタスクがありません。');

    for (const forbidden of [/期限/, /締切/, /優先度/, /タグ/]) {
      expect(screen.queryByLabelText(forbidden)).toBeNull();
    }
  });

  it('タイトルが空のうちは登録できない', async () => {
    mockApi([get('/api/work-types', workTypes), get('/api/tasks', [])]);

    renderWithProviders(<TasksPage />);
    await screen.findByText('まだタスクがありません。');

    expect(screen.getByRole('button', { name: '登録' })).toBeDisabled();
  });

  it('作業タイプが未選択のうちは登録できない', async () => {
    mockApi([get('/api/work-types', workTypes), get('/api/tasks', [])]);

    renderWithProviders(<TasksPage />);
    await screen.findByText('まだタスクがありません。');

    await userEvent.type(screen.getByLabelText('タイトル'), '新しいタスク');

    expect(screen.getByRole('button', { name: '登録' })).toBeDisabled();
  });

  it('タイトルと作業タイプを入れると登録できる', async () => {
    const { calls } = mockApi([
      get('/api/work-types', workTypes),
      get('/api/tasks', []),
      post('/api/tasks', 201, { id: 1 }),
    ]);

    renderWithProviders(<TasksPage />);
    await screen.findByText('まだタスクがありません。');

    await userEvent.type(screen.getByLabelText('タイトル'), '新しいタスク');
    await userEvent.click(screen.getByRole('radio', { name: '実装' }));
    await userEvent.click(screen.getByRole('button', { name: '登録' }));

    await waitFor(() => {
      const created = calls.find((call) => call.method === 'POST');
      expect(created?.body).toEqual({
        title: '新しいタスク',
        defaultWorkTypeId: 1,
        note: null,
      });
    });
  });

  it('前後の空白を除いて送る', async () => {
    const { calls } = mockApi([
      get('/api/work-types', workTypes),
      get('/api/tasks', []),
      post('/api/tasks', 201, { id: 1 }),
    ]);

    renderWithProviders(<TasksPage />);
    await screen.findByText('まだタスクがありません。');

    await userEvent.type(screen.getByLabelText('タイトル'), '  余白つき  ');
    await userEvent.click(screen.getByRole('radio', { name: '実装' }));
    await userEvent.click(screen.getByRole('button', { name: '登録' }));

    await waitFor(() => {
      const body = calls.find((call) => call.method === 'POST')?.body as
        | { title: string }
        | undefined;
      expect(body?.title).toBe('余白つき');
    });
  });

  it('アーカイブできる', async () => {
    const { calls } = mockApi([
      get('/api/work-types', workTypes),
      get('/api/tasks', [task({ id: 7, title: '終わった作業' })]),
      post('/api/tasks/7/archive', 200, { id: 7 }),
    ]);

    renderWithProviders(<TasksPage />);
    await screen.findByText('終わった作業');

    await userEvent.click(screen.getByRole('button', { name: 'アーカイブ' }));

    await waitFor(() => {
      expect(calls.some((call) => call.url.endsWith('/api/tasks/7/archive'))).toBe(true);
    });
  });

  it('アーカイブ済みには解除ボタンを出す', async () => {
    mockApi([
      get('/api/work-types', workTypes),
      get('/api/tasks', [task({ id: 7, title: '終わった作業', isArchived: true })]),
    ]);

    renderWithProviders(<TasksPage />);
    await screen.findByText('終わった作業');

    expect(screen.getByRole('button', { name: 'アーカイブ解除' })).toBeInTheDocument();
  });

  /**
   * 進行中のセッションを持つタスクは 409 になる（docs/07-api-design.md §2.7）。
   * 利用者に理由が伝わること。
   */
  it('アーカイブが競合したら理由を表示する', async () => {
    mockApi([
      get('/api/work-types', workTypes),
      get('/api/tasks', [task({ id: 7, title: '作業中のタスク' })]),
      post('/api/tasks/7/archive', 409, {
        status: 409,
        detail: '進行中の作業セッションがあるタスクはアーカイブできません。',
      }),
    ]);

    renderWithProviders(<TasksPage />);
    await screen.findByText('作業中のタスク');

    await userEvent.click(screen.getByRole('button', { name: 'アーカイブ' }));

    expect(
      await screen.findByText('進行中の作業セッションがあるタスクはアーカイブできません。'),
    ).toBeInTheDocument();
  });

  it('編集を開始するとフォームに値が入る', async () => {
    mockApi([
      get('/api/work-types', workTypes),
      get('/api/tasks', [task({ id: 1, title: '認証方式の検討', note: '下書きあり' })]),
    ]);

    renderWithProviders(<TasksPage />);
    await screen.findByText('認証方式の検討');

    await userEvent.click(screen.getByRole('button', { name: '編集' }));

    expect(screen.getByLabelText('タイトル')).toHaveValue('認証方式の検討');
    expect(screen.getByLabelText('メモ')).toHaveValue('下書きあり');
    expect(screen.getByRole('radio', { name: '設計' })).toBeChecked();
    expect(screen.getByRole('button', { name: '更新' })).toBeInTheDocument();
  });

  it('編集をキャンセルするとフォームが空に戻る', async () => {
    mockApi([
      get('/api/work-types', workTypes),
      get('/api/tasks', [task({ id: 1, title: '認証方式の検討' })]),
    ]);

    renderWithProviders(<TasksPage />);
    await screen.findByText('認証方式の検討');

    await userEvent.click(screen.getByRole('button', { name: '編集' }));
    await userEvent.click(screen.getByRole('button', { name: 'キャンセル' }));

    expect(screen.getByLabelText('タイトル')).toHaveValue('');
    expect(screen.getByRole('button', { name: '登録' })).toBeInTheDocument();
  });

  it('アーカイブ済みの表示を切り替えられる', async () => {
    const { calls } = mockApi([
      get('/api/work-types', workTypes),
      get('/api/tasks', []),
    ]);

    renderWithProviders(<TasksPage />);
    await screen.findByText('まだタスクがありません。');

    await userEvent.click(screen.getByLabelText('アーカイブ済みも表示する'));

    await waitFor(() => {
      expect(
        calls.some((call) => call.url.includes('includeArchived=true')),
      ).toBe(true);
    });
  });
});
