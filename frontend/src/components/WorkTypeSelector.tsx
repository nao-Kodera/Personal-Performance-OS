import type { WorkTypeResponse } from '../api/types';
import { ChoiceGroup } from './ChoiceGroup';

type Props = {
  workTypes: readonly WorkTypeResponse[];
  value: number | null;
  onChange: (workTypeId: number) => void;
  legend?: string;
};

/**
 * 作業タイプの選択。
 *
 * 有効な作業タイプは 6 個以内を前提とする。7 個以上に増やすと分析の
 * サンプル数が不足する（docs/04-analytics-spec.md §3.2）。
 */
export function WorkTypeSelector({
  workTypes,
  value,
  onChange,
  legend = '作業タイプ',
}: Props) {
  return (
    <ChoiceGroup
      legend={legend}
      name="workTypeId"
      choices={workTypes.map((workType) => ({
        value: workType.id,
        label: workType.name,
      }))}
      value={value}
      onChange={onChange}
      emptyMessage="作業タイプが登録されていません。"
    />
  );
}
