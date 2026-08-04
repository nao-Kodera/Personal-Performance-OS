import { useId } from 'react';

import styles from './CountStepper.module.css';

type Props = {
  label: string;
  value: number;
  onChange: (value: number) => void;
  min?: number;
  max?: number;
  unit?: string;
};

/**
 * 件数の増減入力。
 *
 * 会議件数と中断回数に使う。既定値 0 のまま進められることが多いため、
 * 自由入力よりステッパーの方が速い（docs/03-use-cases.md §6）。
 */
export function CountStepper({
  label,
  value,
  onChange,
  min = 0,
  max = 99,
  unit = '回',
}: Props) {
  const valueId = useId();

  const clamp = (next: number) => Math.min(max, Math.max(min, next));

  return (
    <div className={styles.field}>
      <span className={styles.label} id={valueId}>
        {label}
      </span>

      <div className={styles.controls}>
        <button
          type="button"
          className={styles.step}
          onClick={() => onChange(clamp(value - 1))}
          disabled={value <= min}
          aria-label={`${label}を減らす`}
        >
          −
        </button>

        <output className={styles.value} aria-labelledby={valueId}>
          {value}
          {unit}
        </output>

        <button
          type="button"
          className={styles.step}
          onClick={() => onChange(clamp(value + 1))}
          disabled={value >= max}
          aria-label={`${label}を増やす`}
        >
          ＋
        </button>
      </div>
    </div>
  );
}
