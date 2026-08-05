import { Link } from 'react-router';

import { useActiveSession, useSessionHistory } from '../api/hooks/useWorkSessions';
import { formatDuration, formatFullDayLabel, todayJst } from '../lib/datetime';
import { describeError } from '../lib/errors';
import styles from './HomePage.module.css';

/**
 * S-01 ホーム（docs/03-use-cases.md §4）— 縦切り1 版。
 *
 * 「次に何を入力すべきか」を示す画面である。縦切り1 では
 * 「進行中」「作業を開始」「今日の記録」のみを扱う
 * （docs/09-implementation-tasks.md T-19）。
 *
 * 日次コンディション（②）と予定（③）は縦切り2 で追加する。
 *
 * <b>集約エンドポイント GET /api/home/today は T-24 で作る。</b>それまでは
 * 既存の 2 エンドポイントを組み合わせる。T-24 の時点でここを置き換えること
 * （docs/07-api-design.md §2.19）。
 */
export function HomePage() {
  const today = todayJst();

  const activeSession = useActiveSession();
  const todayHistory = useSessionHistory({ from: today, to: today });

  const session = activeSession.data;
  const summary = todayHistory.data?.[0]?.summary;

  const error = activeSession.error ?? todayHistory.error;

  return (
    <main>
      <h1 className={styles.date}>{formatFullDayLabel(today)}</h1>

      {error && (
        <p className={styles.error} role="alert">
          {describeError(error)}
        </p>
      )}

      {/* ① 進行中のセッション。ある場合のみ表示する。 */}
      {session && (
        <section className={styles.active}>
          <h2 className={styles.sectionTitle}>作業中</h2>
          <p className={styles.activeTask}>
            {session.taskTitle}
            <span className={styles.activeElapsed}>
              経過 {formatDuration(elapsedMinutes(session.startedAt))}
            </span>
          </p>
          <Link className={styles.primaryLink} to="/sessions/active">
            作業中の画面へ
          </Link>
        </section>
      )}

      {/* ④ 今日の記録。0 件であることも情報なので枠は残す。 */}
      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>今日の記録</h2>
        {summary === undefined ? (
          <p className={styles.muted}>今日はまだ記録がありません。</p>
        ) : (
          <p>
            完了 {summary.completedCount}件 / 中断終了 {summary.abandonedCount}件 / 合計{' '}
            {formatDuration(summary.totalMinutes)}
          </p>
        )}
      </section>

      {/* ⑤ 予定外の作業の開始。進行中があるときは無効化する（UC §4 状態別制御）。 */}
      <div className={styles.start}>
        {session ? (
          <button type="button" className={styles.primary} disabled>
            作業を開始
          </button>
        ) : (
          <Link className={styles.primaryLink} to="/sessions/start">
            作業を開始
          </Link>
        )}
        {session && (
          <p className={styles.muted}>
            同時に記録できる作業は 1 件までです。先に今の作業を終了してください。
          </p>
        )}
      </div>

      <nav className={styles.nav}>
        <Link to="/tasks">タスク</Link>
        <Link to="/history">履歴</Link>
      </nav>
    </main>
  );
}

/**
 * 開始からの経過分。
 *
 * ホームはタイマー画面ではないため、描画時点の値を出す。1 秒ごとに動かすのは
 * S-05 だけである（docs/08-technical-design.md §3.11）。
 */
function elapsedMinutes(startedAtUtc: string): number {
  const elapsedMs = Date.now() - new Date(startedAtUtc).getTime();

  return Math.max(0, Math.floor(elapsedMs / 60_000));
}
