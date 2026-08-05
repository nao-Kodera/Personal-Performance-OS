import { useState } from 'react';
import { Link, useLocation, useParams } from 'react-router';

import { useActiveSession, useFinishSession } from '../api/hooks/useWorkSessions';
import type { Rating, SessionWarning, WorkSessionResponse } from '../api/types';
import { CountStepper } from '../components/CountStepper';
import { RatingInput } from '../components/RatingInput';
import { formatDuration, formatElapsed } from '../lib/datetime';
import { describeError } from '../lib/errors';
import { formatDelta, sessionWarningLabels, sessionWarningPrompts } from '../lib/labels';
import styles from './FinishSessionPage.module.css';

/** docs/06-database-design.md §2.8 の note の上限。 */
const MAX_NOTE_LENGTH = 2000;

const LONG_SESSION_MINUTES = 8 * 60;

type ResultState = {
  focusLevel: Rating | null;
  outputLevel: Rating | null;
  accuracyLevel: Rating | null;
  satisfactionLevel: Rating | null;
  fatigueAfter: Rating | null;
};

const EMPTY_RESULT: ResultState = {
  focusLevel: null,
  outputLevel: null,
  accuracyLevel: null,
  satisfactionLevel: null,
  fatigueAfter: null,
};

/** S-05 から navigate の state で運ばれてくる中断回数（docs/03-use-cases.md §5）。 */
function carriedInterruptionCount(state: unknown): number | null {
  if (state !== null && typeof state === 'object' && 'interruptionCount' in state) {
    const value = (state as { interruptionCount: unknown }).interruptionCount;

    if (typeof value === 'number') {
      return value;
    }
  }

  return null;
}

/**
 * S-06 作業終了・成果評価画面（docs/03-use-cases.md UC-05）。
 *
 * <b>スキップ導線を作らないこと。</b>5 指標すべてが必須である。部分的な評価を
 * 許すと、分析の母集団に欠損が混ざる（WS-3・技術設計 §8 の禁止事項 4）。
 * 評価が欠けたセッションは分析に使えず、記録した労力が無駄になる。
 *
 * <b>合成スコアを出さないこと。</b>5 指標を平均・加重した「総合点」は作らない
 * （禁止事項 5）。指標ごとに意味が違い、平均すると何を観測したのか分からなくなる。
 */
export function FinishSessionPage() {
  const { id } = useParams();
  const location = useLocation();

  const activeSession = useActiveSession();
  const finishSession = useFinishSession();

  const [result, setResult] = useState<ResultState>(EMPTY_RESULT);
  const [note, setNote] = useState('');
  const [editedInterruptionCount, setEditedInterruptionCount] = useState<number | null>(
    null,
  );

  /**
   * 経過時間は画面を開いた時点で固定する。
   *
   * 実際の作業時間は終了時にサーバーが確定させる。ここで 1 秒ごとに動かしても
   * 情報は増えず、60 秒で入力する画面の注意を奪うだけである。
   */
  const [openedAt] = useState(() => new Date());

  if (finishSession.data) {
    return <CompletedView session={finishSession.data} />;
  }

  if (activeSession.isPending) {
    return (
      <main>
        <p className={styles.loading}>読み込み中…</p>
      </main>
    );
  }

  if (activeSession.error) {
    return (
      <main>
        <p className={styles.error} role="alert">
          {describeError(activeSession.error)}
        </p>
      </main>
    );
  }

  const session = activeSession.data;

  // 単体取得のエンドポイントが無いため、進行中のセッションとしてのみ扱える
  // （docs/07-api-design.md §1）。
  if (!session || String(session.id) !== id) {
    return (
      <main>
        <h1>成果を記録</h1>
        <p className={styles.notice}>このセッションは進行中ではありません。</p>
        <Link className={styles.primaryLink} to="/">
          ホームへ
        </Link>
      </main>
    );
  }

  const interruptionCount =
    editedInterruptionCount ??
    carriedInterruptionCount(location.state) ??
    session.interruptionCount;

  const elapsedMinutes =
    (openedAt.getTime() - new Date(session.startedAt).getTime()) / 60_000;

  const warnings: SessionWarning[] = [];

  if (elapsedMinutes < 1) {
    warnings.push('VeryShortSession');
  }

  if (elapsedMinutes > LONG_SESSION_MINUTES) {
    warnings.push('LongSession');
  }

  const isComplete =
    result.focusLevel !== null &&
    result.outputLevel !== null &&
    result.accuracyLevel !== null &&
    result.satisfactionLevel !== null &&
    result.fatigueAfter !== null;

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault();

    const { focusLevel, outputLevel, accuracyLevel, satisfactionLevel, fatigueAfter } =
      result;

    if (
      !session ||
      focusLevel === null ||
      outputLevel === null ||
      accuracyLevel === null ||
      satisfactionLevel === null ||
      fatigueAfter === null
    ) {
      return;
    }

    finishSession.mutate({
      id: session.id,
      interruptionCount,
      result: {
        focusLevel,
        outputLevel,
        accuracyLevel,
        satisfactionLevel,
        fatigueAfter,
        note: note.trim() === '' ? null : note.trim(),
      },
    });
  }

  return (
    <main>
      <h1 className={styles.taskTitle}>{session.taskTitle}</h1>
      <p className={styles.meta}>
        {session.workTypeName} / {formatElapsed(session.startedAt, openedAt)}
      </p>

      {warnings.map((warning) => (
        <p key={warning} className={styles.warning}>
          {sessionWarningPrompts[warning]}
          {warning === 'VeryShortSession' && (
            <Link className={styles.warningLink} to="/sessions/active">
              中断終了に切り替える
            </Link>
          )}
        </p>
      ))}

      <form onSubmit={handleSubmit}>
        <CountStepper
          label="中断回数"
          value={interruptionCount}
          onChange={setEditedInterruptionCount}
        />

        <section className={styles.section}>
          <RatingInput
            metric="focusLevel"
            value={result.focusLevel}
            onChange={(focusLevel) => setResult((prev) => ({ ...prev, focusLevel }))}
          />
          <RatingInput
            metric="outputLevel"
            value={result.outputLevel}
            onChange={(outputLevel) => setResult((prev) => ({ ...prev, outputLevel }))}
          />
          <RatingInput
            metric="accuracyLevel"
            value={result.accuracyLevel}
            onChange={(accuracyLevel) =>
              setResult((prev) => ({ ...prev, accuracyLevel }))
            }
          />
          <RatingInput
            metric="satisfactionLevel"
            value={result.satisfactionLevel}
            onChange={(satisfactionLevel) =>
              setResult((prev) => ({ ...prev, satisfactionLevel }))
            }
          />
          <RatingInput
            metric="fatigueAfter"
            value={result.fatigueAfter}
            onChange={(fatigueAfter) => setResult((prev) => ({ ...prev, fatigueAfter }))}
          />
        </section>

        <label className={styles.label}>
          メモ（任意）
          <textarea
            className={styles.textarea}
            value={note}
            maxLength={MAX_NOTE_LENGTH}
            onChange={(event) => setNote(event.target.value)}
          />
        </label>

        {finishSession.error && (
          <p className={styles.error} role="alert">
            {describeError(finishSession.error)}
          </p>
        )}

        <button
          type="submit"
          className={styles.primary}
          disabled={!isComplete || finishSession.isPending}
        >
          記録して終了
        </button>
      </form>
    </main>
  );
}

/**
 * 記録直後の表示。
 *
 * 疲労増加量と集中の差は保存されない導出値であり、サーバーが返した値を出す。
 * クライアントで計算すると PreWorkState を保持する必要があり、リロードで
 * 失われる（docs/07-api-design.md §2.15）。
 *
 * <b>指標を合成した総合点を出さないこと</b>（技術設計 §8 の禁止事項 5）。
 */
function CompletedView({ session }: { session: WorkSessionResponse }) {
  return (
    <main>
      <h1>記録しました</h1>

      <p className={styles.meta}>
        {session.taskTitle}
        {session.durationMinutes !== null && ` / ${formatDuration(session.durationMinutes)}`}
      </p>

      <dl className={styles.summary}>
        <dt>疲労</dt>
        <dd>
          {session.preWorkState.fatigueLevel} → {session.result?.fatigueAfter}
          {session.fatigueDelta !== null && `（${formatDelta(session.fatigueDelta)}）`}
        </dd>

        <dt>集中</dt>
        <dd>
          見込み {session.preWorkState.expectedFocusLevel} → 実際{' '}
          {session.result?.focusLevel}
          {session.focusGap !== null && `（${formatDelta(session.focusGap)}）`}
        </dd>
      </dl>

      {session.warnings.map((warning) => (
        <p key={warning} className={styles.warning}>
          {sessionWarningLabels[warning]}
        </p>
      ))}

      <Link className={styles.primaryLink} to="/">
        ホームへ
      </Link>
    </main>
  );
}
