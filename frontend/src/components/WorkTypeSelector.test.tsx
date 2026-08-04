import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { WorkTypeResponse } from '../api/types';
import { WorkTypeSelector } from './WorkTypeSelector';

const workTypes: WorkTypeResponse[] = [
  { id: 1, name: '実装', displayOrder: 10, isActive: true },
  { id: 2, name: '設計', displayOrder: 20, isActive: true },
];

describe('WorkTypeSelector', () => {
  it('ボタン列で出す', () => {
    render(<WorkTypeSelector workTypes={workTypes} value={null} onChange={vi.fn()} />);

    expect(screen.getAllByRole('radio')).toHaveLength(2);
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
  });

  it('選ぶと ID を通知する', async () => {
    const onChange = vi.fn();
    render(<WorkTypeSelector workTypes={workTypes} value={null} onChange={onChange} />);

    await userEvent.click(screen.getByRole('radio', { name: '設計' }));

    expect(onChange).toHaveBeenCalledWith(2);
  });

  it('選択中の値が反映される', () => {
    render(<WorkTypeSelector workTypes={workTypes} value={1} onChange={vi.fn()} />);

    expect(screen.getByRole('radio', { name: '実装' })).toBeChecked();
  });

  it('渡された順に出す', () => {
    render(<WorkTypeSelector workTypes={workTypes} value={null} onChange={vi.fn()} />);

    const options = screen.getAllByRole('radio');

    expect(options[0]).toHaveAccessibleName('実装');
    expect(options[1]).toHaveAccessibleName('設計');
  });

  it('選択肢が無いときは案内を出す', () => {
    render(<WorkTypeSelector workTypes={[]} value={null} onChange={vi.fn()} />);

    expect(screen.getByText('作業タイプが登録されていません。')).toBeInTheDocument();
  });
});
