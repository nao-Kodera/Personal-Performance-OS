import type { WorkLocation } from '../api/types';
import { workLocationLabels } from '../lib/labels';
import { ChoiceGroup, type Choice } from './ChoiceGroup';
import styles from './ChoiceGroup.module.css';

/** 表示順は docs/02-glossary.md §3.1 の定義順に固定する。 */
const CHOICES: readonly Choice<WorkLocation>[] = (
  ['Home', 'Office', 'Cafe', 'Other'] as const
).map((value) => ({ value, label: workLocationLabels[value] }));

/** docs/06-database-design.md §2.7 の location_note の上限。 */
const MAX_NOTE_LENGTH = 200;

type Props = {
  value: WorkLocation | null;
  onChange: (value: WorkLocation) => void;
  locationNote: string;
  onLocationNoteChange: (value: string) => void;
};

/**
 * 作業場所の選択。
 *
 * 補足テキストは「その他」を選んだときのみ表示する。他の場所で補足を送ると
 * サーバーが 422 を返す（WC-2）。
 */
export function LocationSelector({
  value,
  onChange,
  locationNote,
  onLocationNoteChange,
}: Props) {
  return (
    <div>
      <ChoiceGroup
        legend="作業場所"
        name="workLocation"
        choices={CHOICES}
        value={value}
        onChange={(next) => {
          onChange(next);

          // 「その他」以外に切り替えたら補足を捨てる。残したまま送ると
          // 422 になる。
          if (next !== 'Other') {
            onLocationNoteChange('');
          }
        }}
      />

      {value === 'Other' && (
        <div className={styles.note}>
          <label>
            場所の補足
            <input
              type="text"
              className={styles.noteInput}
              value={locationNote}
              maxLength={MAX_NOTE_LENGTH}
              onChange={(event) => onLocationNoteChange(event.target.value)}
            />
          </label>
        </div>
      )}
    </div>
  );
}
