import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { RatingInput } from './RatingInput';

describe('RatingInput', () => {
  /**
   * docs/03-use-cases.md §6。
   * ドロップダウンやスライダーにすると 1 項目 5 秒を超え、
   * 60 秒制約を満たせなくなる。
   */
  it('5つの選択肢を1タップで選べる形で出す', () => {
    render(<RatingInput metric="focusLevel" value={null} onChange={vi.fn()} />);

    const options = screen.getAllByRole('radio');

    expect(options).toHaveLength(5);
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    expect(screen.queryByRole('slider')).not.toBeInTheDocument();
  });

  it('選ぶと値を通知する', async () => {
    const onChange = vi.fn();
    render(<RatingInput metric="focusLevel" value={null} onChange={onChange} />);

    await userEvent.click(screen.getByRole('radio', { name: '4' }));

    expect(onChange).toHaveBeenCalledWith(4);
  });

  it('選択中の値が反映される', () => {
    render(<RatingInput metric="focusLevel" value={3} onChange={vi.fn()} />);

    expect(screen.getByRole('radio', { name: '3' })).toBeChecked();
    expect(screen.getByRole('radio', { name: '4' })).not.toBeChecked();
  });

  it('指標の表示名を出す', () => {
    render(<RatingInput metric="satisfactionLevel" value={null} onChange={vi.fn()} />);

    expect(screen.getByRole('group', { name: '満足度' })).toBeInTheDocument();
  });

  it('未選択のときはその旨を示す', () => {
    render(<RatingInput metric="focusLevel" value={null} onChange={vi.fn()} />);

    expect(screen.getByText('未選択')).toBeInTheDocument();
  });

  it('選択中の段階の意味を表示する', () => {
    // 両端（1 と 5）の意味は常に表示されるため、中間の値で確認する。
    render(<RatingInput metric="focusLevel" value={4} onChange={vi.fn()} />);

    expect(screen.getByText('よく集中できた')).toBeInTheDocument();
  });

  it('両端の意味は選択に関わらず常に表示する', () => {
    render(<RatingInput metric="focusLevel" value={3} onChange={vi.fn()} />);

    expect(screen.getByText('ほぼ集中できなかった')).toBeInTheDocument();
    expect(screen.getByText('完全に没入した')).toBeInTheDocument();
  });

  /**
   * docs/02-glossary.md §2.2。
   * 疲労度は「高いほど悪い」。値を反転せず、両端のラベルで向きを示す。
   */
  it('疲労度は値を反転せず両端の意味で向きを示す', async () => {
    const onChange = vi.fn();
    render(<RatingInput metric="fatigueLevel" value={null} onChange={onChange} />);

    expect(screen.getByText('疲れていない')).toBeInTheDocument();
    expect(screen.getByText('限界に近い')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('radio', { name: '5' }));

    // 5 を選んだら 5 が通知される。1 に反転しない。
    expect(onChange).toHaveBeenCalledWith(5);
  });

  it('キーボードで選択できる', async () => {
    const onChange = vi.fn();
    render(<RatingInput metric="focusLevel" value={2} onChange={onChange} />);

    await userEvent.tab();
    await userEvent.keyboard('{ArrowRight}');

    expect(onChange).toHaveBeenCalledWith(3);
  });
});
