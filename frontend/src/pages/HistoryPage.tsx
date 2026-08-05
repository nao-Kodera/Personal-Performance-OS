import { useState } from 'react';
import { Link } from 'react-router';

import { useSessionHistory, useUpdateResult } from '../api/hooks/useWorkSessions';
import type {
  PerformanceResultResponse,
  Rating,
  WorkSessionResponse,
} from '../api/types';
import { CountStepper } from '../components/CountStepper';
import { RatingInput } from '../components/RatingInput';
import {
  formatDayLabel,
  formatDuration,
  formatJstDateTime,
  formatJstTime,
} from '../lib/datetime';
import { describeError } from '../lib/errors';
import {
  formatDelta,
  sessionStatusLabels,
  workLocationLabels,
} from '../lib/labels';
import styles from './HistoryPage.module.css';

const MAX_NOTE_LENGTH = 2000;

/**
 * S-08 履歴画面（docs/03-use-cases.md UC-08）。
 *
 * <b>訂正できるのは成果評価と中断回数だけである。</b>開始/終了時刻・
 * PreWorkState・WorkContext を編集する手段を作らないこと。結果を知った後に
 * 作業前の状態を書き換えられると、説明変数と目的変数の独立性が失われ、分析が
 * 意味を持たなくなる。これは本プロダクトの根幹である（UC-08・禁止事項 1）。
 *
 * <b>削除の手段も作らない。</b>記録は削除しない（禁止事項 10）。
 */
export function HistoryPage() {
  const history = useSessionHistory();

  if (history.isPending) {
    return (
      <main>
        <p className={styles.loading}>読み込み中…</p>
      </main>
    );
  }

  if (history.error) {
    return (
      <main>
        <p className={styles.error} role="alert">
          {describeError(history.error)}
        </p>
      </main>
    );
  }

  const days = history.data ?? [];

  if (days.length === 0) {
    return (
      <main>
        <PageHeader />
        <p className={styles.notice}>まだ記録がありません。</p>
        <Link className={styles.primaryLink} to="/sessions/start">
          作業を開始する
        </Link>
      </main>
    );
  }

  return (
    <main>
      <PageHeader />

      {days.map((day) => (
        <section key={day.date} className={styles.day}>
          <h2 className={styles.dayLabel}>{formatDayLabel(day.date)}</h2>
          <p className={styles.summary}>
            完了 {day.summary.completedCount}件 / 中断終了{' '}
            {day.summary.abandonedCount}件 / 合計{' '}
            {formatDuration(day.summary.totalMinutes)}
          </p>

          <ul className={styles.list}>
            {day.sessions.map((session) => (
              <li key={session.id} className={styles.item}>
                <SessionRow session={session} />
              </li>
            ))}
          </ul>
        </section>
      ))}
    </main>
  );
}

/**
 * 見出しとホームへの復帰導線。
 *
 * 画面遷移は S-01 を起点に戻る（docs/03-use-cases.md §2.1）。
 */
function PageHeader() {
  return (
    <div className={styles.pageHeader}>
      <h1>履歴</h1>
      <Link className={styles.backLink} to="/">
        ホームへ
      </Link>
    </div>
  );
}

function SessionRow({ session }: { session: WorkSessionResponse }) {
  const [isOpen, setIsOpen] = useState(false);

  const period =
    session.finishedAt === null
      ? `${formatJstTime(session.startedAt)}–`
      : `${formatJstTime(session.startedAt)}–${formatJstTime(session.finishedAt)}`;

  return (
    <>
      <button
        type="button"
        className={styles.rowToggle}
        aria-expanded={isOpen}
        onClick={() => setIsOpen((open) => !open)}
      >
        <span className={styles.rowHead}>
          <span className={styles.headMeta}>
            <span className={styles.period}>{period}</span>
            {session.durationMinutes !== null && (
              <span className={styles.duration}>
                （{formatDuration(session.durationMinutes)}）
              </span>
            )}{' '}
            <span className={styles.workType}>{session.workTypeName}</span>
          </span>{' '}
          <span className={styles.headTitle}>
            {session.taskTitle}
            {session.status !== 'Completed' && (
              <span className={styles.status}>
                [{sessionStatusLabels[session.status]}]
              </span>
            )}
          </span>
        </span>

        {session.result && (
          <span className={styles.rowResult}>
            <Reading label="集中" value={session.result.focusLevel} />{' '}
            <Reading label="成果" value={session.result.outputLevel} />{' '}
            <Reading label="正確" value={session.result.accuracyLevel} />{' '}
            <Reading label="満足" value={session.result.satisfactionLevel} />
            {session.fatigueDelta !== null && (
              <>
                {' '}
                <Reading
                  label="疲労 "
                  value={`${session.preWorkState.fatigueLevel}→${session.result.fatigueAfter}（${formatDelta(session.fatigueDelta)}）`}
                />
              </>
            )}{' '}
            <Reading label="中断" value={session.interruptionCount} unit="回" />{' '}
            <Reading
              label="@"
              value={workLocationLabels[session.workContext.workLocation]}
            />
          </span>
        )}
      </button>

      {isOpen && <SessionDetail session={session} />}
    </>
  );
}

/**
 * 一覧行に並ぶ読み取り値の 1 つ（「集中4」など）。
 *
 * 項目名と値を分けるのは表示のためだけであり、示す内容は変えない。
 * <b>ここで指標を合成した値を作らないこと</b>（技術設計 §8 の禁止事項 5）。
 */
function Reading({
  label,
  value,
  unit,
}: {
  label: string;
  value: React.ReactNode;
  unit?: string;
}) {
  return (
    <span className={styles.reading}>
      <span className={styles.readingKey}>{label}</span>
      <span className={styles.readingValue}>{value}</span>
      {unit !== undefined && <span className={styles.readingUnit}>{unit}</span>}
    </span>
  );
}

/**
 * 詳細表示。
 *
 * 作業前の状態と作業環境は読み取り専用で表示する。<b>入力欄にしないこと。</b>
 */
function SessionDetail({ session }: { session: WorkSessionResponse }) {
  const [isEditing, setIsEditing] = useState(false);
  const result = session.result;

  return (
    <div className={styles.detail}>
      <dl className={styles.facts}>
        <dt>作業前</dt>
        <dd>
          疲労{session.preWorkState.fatigueLevel} 見込み集中
          {session.preWorkState.expectedFocusLevel} 気分{session.preWorkState.moodLevel}
        </dd>

        <dt>作業環境</dt>
        <dd>
          {workLocationLabels[session.workContext.workLocation]}
          {session.workContext.locationNote !== null &&
            `（${session.workContext.locationNote}）`}{' '}
          会議{session.workContext.meetingCount}件{' '}
          {session.workContext.interruptionExpected
            ? '割り込み予想あり'
            : '割り込み予想なし'}
        </dd>

        {result !== null && result.note !== null && (
          <>
            <dt>メモ</dt>
            <dd className={styles.note}>{result.note}</dd>
          </>
        )}

        {session.abandonNote !== null && (
          <>
            <dt>中断の理由</dt>
            <dd className={styles.note}>{session.abandonNote}</dd>
          </>
        )}

        {result?.isEdited && (
          <>
            <dt>訂正</dt>
            <dd>{formatJstDateTime(result.updatedAt)} に訂正</dd>
          </>
        )}
      </dl>

      {/* 訂正できるのは完了したセッションのみ。進行中は編集させない（UC-08）。 */}
      {session.status === 'Completed' &&
        result !== null &&
        (isEditing ? (
          <ResultEditor
            session={session}
            result={result}
            onClose={() => setIsEditing(false)}
          />
        ) : (
          <button
            type="button"
            className={styles.secondary}
            onClick={() => setIsEditing(true)}
          >
            成果を訂正する
          </button>
        ))}
    </div>
  );
}

type ResultState = {
  focusLevel: Rating;
  outputLevel: Rating;
  accuracyLevel: Rating;
  satisfactionLevel: Rating;
  fatigueAfter: Rating;
};

/**
 * 成果評価の訂正フォーム。
 *
 * 送るのは 5 指標・メモ・中断回数だけである。時刻や PreWorkState を含めると
 * サーバーが未知のプロパティとして 400 を返す（docs/07-api-design.md §2.17）。
 */
function ResultEditor({
  session,
  result: recorded,
  onClose,
}: {
  session: WorkSessionResponse;
  result: PerformanceResultResponse;
  onClose: () => void;
}) {
  const updateResult = useUpdateResult();

  const [result, setResult] = useState<ResultState>({
    focusLevel: recorded.focusLevel,
    outputLevel: recorded.outputLevel,
    accuracyLevel: recorded.accuracyLevel,
    satisfactionLevel: recorded.satisfactionLevel,
    fatigueAfter: recorded.fatigueAfter,
  });
  const [note, setNote] = useState(recorded.note ?? '');
  const [interruptionCount, setInterruptionCount] = useState(session.interruptionCount);

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault();

    updateResult.mutate(
      {
        id: session.id,
        interruptionCount,
        result: {
          ...result,
          note: note.trim() === '' ? null : note.trim(),
        },
      },
      { onSuccess: onClose },
    );
  }

  return (
    <form className={styles.editor} onSubmit={handleSubmit}>
      <CountStepper
        label="中断回数"
        value={interruptionCount}
        onChange={setInterruptionCount}
      />

      <RatingInput
        metric="focusLevel"
        name={`focusLevel-${session.id}`}
        value={result.focusLevel}
        onChange={(focusLevel) => setResult((prev) => ({ ...prev, focusLevel }))}
      />
      <RatingInput
        metric="outputLevel"
        name={`outputLevel-${session.id}`}
        value={result.outputLevel}
        onChange={(outputLevel) => setResult((prev) => ({ ...prev, outputLevel }))}
      />
      <RatingInput
        metric="accuracyLevel"
        name={`accuracyLevel-${session.id}`}
        value={result.accuracyLevel}
        onChange={(accuracyLevel) => setResult((prev) => ({ ...prev, accuracyLevel }))}
      />
      <RatingInput
        metric="satisfactionLevel"
        name={`satisfactionLevel-${session.id}`}
        value={result.satisfactionLevel}
        onChange={(satisfactionLevel) =>
          setResult((prev) => ({ ...prev, satisfactionLevel }))
        }
      />
      <RatingInput
        metric="fatigueAfter"
        name={`fatigueAfter-${session.id}`}
        value={result.fatigueAfter}
        onChange={(fatigueAfter) => setResult((prev) => ({ ...prev, fatigueAfter }))}
      />

      <label className={styles.label}>
        メモ（任意）
        <textarea
          className={styles.textarea}
          value={note}
          maxLength={MAX_NOTE_LENGTH}
          onChange={(event) => setNote(event.target.value)}
        />
      </label>

      {updateResult.error && (
        <p className={styles.error} role="alert">
          {describeError(updateResult.error)}
        </p>
      )}

      <div className={styles.actions}>
        <button type="submit" className={styles.primary} disabled={updateResult.isPending}>
          保存
        </button>
        <button type="button" className={styles.secondary} onClick={onClose}>
          キャンセル
        </button>
      </div>
    </form>
  );
}
