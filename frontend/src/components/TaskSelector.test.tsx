import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { TaskItemResponse } from '../api/types';
import { TaskSelector } from './TaskSelector';

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

const tasks = [
  task({ id: 1, title: '認証方式の検討', lastUsedAt: '2026-08-03T01:00:00.000Z', sessionCount: 4 }),
  task({ id: 2, title: 'ログイン画面の実装' }),
];

describe('TaskSelector', () => {
  it('渡された順にタスクを出す', () => {
    render(<TaskSelector tasks={tasks} value={null} onChange={vi.fn()} />);

    const options = screen.getAllByRole('radio');

    expect(options).toHaveLength(2);
    expect(options[0]).toHaveAccessibleName(/認証方式の検討/);
  });

  it('選ぶと ID を通知する', async () => {
    const onChange = vi.fn();
    render(<TaskSelector tasks={tasks} value={null} onChange={onChange} />);

    await userEvent.click(screen.getByRole('radio', { name: /ログイン画面の実装/ }));

    expect(onChange).toHaveBeenCalledWith(2);
  });

  it('キーワードで絞り込める', async () => {
    render(<TaskSelector tasks={tasks} value={null} onChange={vi.fn()} />);

    await userEvent.type(screen.getByRole('searchbox'), '認証');

    expect(screen.getAllByRole('radio')).toHaveLength(1);
  });

  it('該当が無いときは案内を出す', async () => {
    render(<TaskSelector tasks={tasks} value={null} onChange={vi.fn()} />);

    await userEvent.type(screen.getByRole('searchbox'), 'zzz');

    expect(screen.getByText('該当するタスクがありません。')).toBeInTheDocument();
  });

  it('タスクが1件も無いときは登録を促す', () => {
    render(<TaskSelector tasks={[]} value={null} onChange={vi.fn()} />);

    expect(screen.getByText('タスクが登録されていません。')).toBeInTheDocument();
  });

  it('未実施のタスクはその旨を示す', () => {
    render(<TaskSelector tasks={tasks} value={null} onChange={vi.fn()} />);

    expect(screen.getByText(/未実施/)).toBeInTheDocument();
  });

  it('使用済みのタスクは最終利用日と回数を示す', () => {
    render(<TaskSelector tasks={tasks} value={null} onChange={vi.fn()} />);

    // UTC 2026-08-03 01:00 = JST 2026-08-03 10:00
    expect(screen.getByText(/最終 2026\/08\/03（4回）/)).toBeInTheDocument();
  });
});
