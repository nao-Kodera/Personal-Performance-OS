import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { CountStepper } from './CountStepper';

describe('CountStepper', () => {
  it('現在値を表示する', () => {
    render(<CountStepper label="会議件数" value={2} onChange={vi.fn()} unit="件" />);

    expect(screen.getByText('2件')).toBeInTheDocument();
  });

  it('増やせる', async () => {
    const onChange = vi.fn();
    render(<CountStepper label="中断回数" value={1} onChange={onChange} />);

    await userEvent.click(screen.getByRole('button', { name: '中断回数を増やす' }));

    expect(onChange).toHaveBeenCalledWith(2);
  });

  it('減らせる', async () => {
    const onChange = vi.fn();
    render(<CountStepper label="中断回数" value={1} onChange={onChange} />);

    await userEvent.click(screen.getByRole('button', { name: '中断回数を減らす' }));

    expect(onChange).toHaveBeenCalledWith(0);
  });

  /** 0 未満は API が 400 を返す。UI 側で到達させない。 */
  it('下限では減らすボタンを無効にする', () => {
    render(<CountStepper label="中断回数" value={0} onChange={vi.fn()} />);

    expect(screen.getByRole('button', { name: '中断回数を減らす' })).toBeDisabled();
  });

  it('上限では増やすボタンを無効にする', () => {
    render(<CountStepper label="中断回数" value={5} onChange={vi.fn()} max={5} />);

    expect(screen.getByRole('button', { name: '中断回数を増やす' })).toBeDisabled();
  });
});
