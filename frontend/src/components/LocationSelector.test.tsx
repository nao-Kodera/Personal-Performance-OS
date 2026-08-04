import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { LocationSelector } from './LocationSelector';

describe('LocationSelector', () => {
  it('4つの場所をボタン列で出す', () => {
    render(
      <LocationSelector
        value={null}
        onChange={vi.fn()}
        locationNote=""
        onLocationNoteChange={vi.fn()}
      />,
    );

    expect(screen.getAllByRole('radio')).toHaveLength(4);
    expect(screen.getByRole('radio', { name: '自宅' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'オフィス' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'カフェ' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'その他' })).toBeInTheDocument();
  });

  /** WC-2: 補足は「その他」のときのみ設定できる。 */
  it('その他以外では補足欄を出さない', () => {
    render(
      <LocationSelector
        value="Home"
        onChange={vi.fn()}
        locationNote=""
        onLocationNoteChange={vi.fn()}
      />,
    );

    expect(screen.queryByLabelText('場所の補足')).not.toBeInTheDocument();
  });

  it('その他を選ぶと補足欄が出る', () => {
    render(
      <LocationSelector
        value="Other"
        onChange={vi.fn()}
        locationNote=""
        onLocationNoteChange={vi.fn()}
      />,
    );

    expect(screen.getByLabelText('場所の補足')).toBeInTheDocument();
  });

  /**
   * 補足を残したまま他の場所に切り替えると、サーバーが 422 を返す（WC-2）。
   * 切り替え時に捨てる。
   */
  it('その他以外に切り替えると補足を捨てる', async () => {
    const onLocationNoteChange = vi.fn();
    render(
      <LocationSelector
        value="Other"
        onChange={vi.fn()}
        locationNote="図書館"
        onLocationNoteChange={onLocationNoteChange}
      />,
    );

    await userEvent.click(screen.getByRole('radio', { name: '自宅' }));

    expect(onLocationNoteChange).toHaveBeenCalledWith('');
  });

  it('その他を選んでも補足は消さない', async () => {
    const onLocationNoteChange = vi.fn();
    render(
      <LocationSelector
        value="Home"
        onChange={vi.fn()}
        locationNote=""
        onLocationNoteChange={onLocationNoteChange}
      />,
    );

    await userEvent.click(screen.getByRole('radio', { name: 'その他' }));

    expect(onLocationNoteChange).not.toHaveBeenCalled();
  });

  it('選択を通知する', async () => {
    const onChange = vi.fn();
    render(
      <LocationSelector
        value={null}
        onChange={onChange}
        locationNote=""
        onLocationNoteChange={vi.fn()}
      />,
    );

    await userEvent.click(screen.getByRole('radio', { name: 'カフェ' }));

    expect(onChange).toHaveBeenCalledWith('Cafe');
  });
});
