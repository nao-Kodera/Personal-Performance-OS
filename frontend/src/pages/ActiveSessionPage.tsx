import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router';

import { useAbandonSession, useActiveSession } from '../api/hooks/useWorkSessions';
import { formatElapsed } from '../lib/datetime';
import { describeError } from '../lib/errors';
import { workLocationLabels } from '../lib/labels';
import styles from './ActiveSessionPage.module.css';

/** docs/06-database-design.md §2.4 の abandon_note の上限。 */
const MAX_ABANDON_NOTE_LENGTH = 1000;

/**
 * S-05 作業中画面（docs/03-use-cases.md §5）。
 *
 * <b>一時停止機能を設けないこと。</b>一時停止を許すと「実作業時間」の定義が
 * 曖昧になる。中断は回数でのみ記録する。
 *
 * <b>クライアント側でカウントアップした値を保持しないこと。</b>経過時間は
 * サーバーが記録した開始時刻から毎回再計算する。1 秒ごとのタイマーは再描画の
 * きっかけにのみ使う（docs/08-technical-design.md §3.11）。
 */
export function ActiveSessionPage() {
  const navigate = useNavigate();
  const activeSession = useActiveSession();
  const abandonSession = useAbandonSession();

  const [now, setNow] = useState(() => new Date());
  const [interruptionCount, setInterruptionCount] = useState(0);
  const [isConfirmingAbandon, setIsConfirmingAbandon] = useState(false);
  const [abandonNote, setAbandonNote] = useState('');

  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 1000);

    return () => clearInterval(timer);
  }, []);

  if (activeSession.isPending) {
    return (
      <main>
        <p>読み込み中…</p>
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

  if (!session) {
    return (
      <main>
        <h1>作業中</h1>
        <p className={styles.notice}>進行中の作業はありません。</p>
        <Link className={styles.primaryLink} to="/sessions/start">
          作業を開始する
        </Link>
      </main>
    );
  }

  const finish = () => {
    // 中断回数はこの画面ではサーバーに送らない。S-06 で確定時にまとめて送る
    // （docs/03-use-cases.md §5）。リロードで失われるが、S-06 で確認・修正できる。
    void navigate(`/sessions/${session.id}/finish`, { state: { interruptionCount } });
  };

  const abandon = () => {
    abandonSession.mutate(
      { id: session.id, note: abandonNote.trim() === '' ? null : abandonNote.trim() },
      { onSuccess: () => void navigate('/') },
    );
  };

  return (
    <main>
      <h1 className={styles.taskTitle}>{session.taskTitle}</h1>
      <p className={styles.meta}>
        {session.workTypeName} / {workLocationLabels[session.workContext.workLocation]}
      </p>

      <p className={styles.elapsed}>{formatElapsed(session.startedAt, now)}</p>

      <div className={styles.interruption}>
        <span>中断: {interruptionCount}回</span>
        <button
          type="button"
          className={styles.secondary}
          onClick={() => setInterruptionCount((count) => count + 1)}
        >
          +1
        </button>
      </div>

      {abandonSession.error && (
        <p className={styles.error} role="alert">
          {describeError(abandonSession.error)}
        </p>
      )}

      {isConfirmingAbandon ? (
        <section className={styles.confirm}>
          <p>成果は記録されず、分析の対象外になります。</p>

          <label className={styles.label}>
            理由メモ（任意）
            <textarea
              className={styles.textarea}
              value={abandonNote}
              maxLength={MAX_ABANDON_NOTE_LENGTH}
              onChange={(event) => setAbandonNote(event.target.value)}
            />
          </label>

          <div className={styles.actions}>
            <button
              type="button"
              className={styles.danger}
              disabled={abandonSession.isPending}
              onClick={abandon}
            >
              中断終了する
            </button>
            <button
              type="button"
              className={styles.secondary}
              onClick={() => setIsConfirmingAbandon(false)}
            >
              やめる
            </button>
          </div>
        </section>
      ) : (
        <div className={styles.actions}>
          <button type="button" className={styles.primary} onClick={finish}>
            終了
          </button>
          <button
            type="button"
            className={styles.secondary}
            onClick={() => setIsConfirmingAbandon(true)}
          >
            中断終了
          </button>
        </div>
      )}
    </main>
  );
}
