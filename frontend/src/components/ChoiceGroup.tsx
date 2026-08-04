import styles from './ChoiceGroup.module.css';

export type Choice<T extends string | number> = {
  value: T;
  label: string;
};

type Props<T extends string | number> = {
  legend: string;
  name: string;
  choices: readonly Choice<T>[];
  value: T | null;
  onChange: (value: T) => void;
  /** 選択肢が空のときの案内。 */
  emptyMessage?: string;
};

/**
 * ボタン列の単一選択。
 *
 * 作業タイプ・作業場所のように選択肢が少ない項目に使う。ドロップダウンより
 * 速く選べる（docs/03-use-cases.md §6）。
 *
 * 実体は radio。見た目だけをボタンにしている。
 */
export function ChoiceGroup<T extends string | number>({
  legend,
  name,
  choices,
  value,
  onChange,
  emptyMessage = '選択肢がありません。',
}: Props<T>) {
  return (
    <fieldset className={styles.group}>
      <legend className={styles.legend}>{legend}</legend>

      {choices.length === 0 ? (
        <p className={styles.empty}>{emptyMessage}</p>
      ) : (
        <div className={styles.options}>
          {choices.map((choice) => (
            <label key={choice.value} className={styles.option}>
              <input
                type="radio"
                className="visually-hidden"
                name={name}
                value={choice.value}
                checked={value === choice.value}
                onChange={() => onChange(choice.value)}
              />
              <span className={styles.button}>{choice.label}</span>
            </label>
          ))}
        </div>
      )}
    </fieldset>
  );
}
