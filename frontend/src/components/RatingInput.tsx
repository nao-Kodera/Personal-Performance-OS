import type { Rating } from '../api/types';
import { type RatingMetric, ratingScales } from '../lib/labels';
import styles from './RatingInput.module.css';

const RATINGS = [1, 2, 3, 4, 5] as const satisfies readonly Rating[];

type Props = {
  metric: RatingMetric;
  value: Rating | null;
  onChange: (value: Rating) => void;
  /** radio のグループ名。同一画面で指標ごとに一意にする。 */
  name?: string;
};

/**
 * 1〜5 の評価入力。
 *
 * <b>ドロップダウンやスライダーにしないこと。</b>作業後は 5 項目の評価がある。
 * 1 項目 5 秒を超えると 60 秒制約を満たせない（docs/03-use-cases.md §6）。
 * 1 タップで選べる形を維持する。
 *
 * <b>値を反転して表示しないこと。</b>疲労度は「高いほど悪い」、それ以外は
 * 「高いほど良い」であり、向きは両端のラベルで示す
 * （docs/02-glossary.md §2.2）。
 *
 * 見た目はボタンだが実体は radio である。キーボードの矢印キーで選択でき、
 * 支援技術にもグループとして伝わる。
 */
export function RatingInput({ metric, value, onChange, name = metric }: Props) {
  const scale = ratingScales[metric];

  return (
    <fieldset className={styles.group}>
      <legend className={styles.legend}>{scale.label}</legend>

      <div className={styles.options}>
        {RATINGS.map((rating) => (
          <label key={rating} className={styles.option}>
            <input
              type="radio"
              className="visually-hidden"
              name={name}
              value={rating}
              checked={value === rating}
              onChange={() => onChange(rating)}
            />
            <span className={styles.button}>{rating}</span>
          </label>
        ))}
      </div>

      <div className={styles.ends}>
        <span>{scale.levels[1]}</span>
        <span>{scale.levels[5]}</span>
      </div>

      <p className={value === null ? `${styles.selected} ${styles.unselected}` : styles.selected}>
        {value === null ? '未選択' : scale.levels[value]}
      </p>
    </fieldset>
  );
}
