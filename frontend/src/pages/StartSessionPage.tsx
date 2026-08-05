import { useState } from 'react';
import { Link, useNavigate } from 'react-router';

import { ApiError } from '../api/client';
import { useTasks } from '../api/hooks/useTasks';
import { useActiveSession, useStartWorkSession } from '../api/hooks/useWorkSessions';
import { useWorkTypes } from '../api/hooks/useWorkTypes';
import type { Rating, WorkLocation } from '../api/types';
import { ChoiceGroup, type Choice } from '../components/ChoiceGroup';
import { CountStepper } from '../components/CountStepper';
import { LocationSelector } from '../components/LocationSelector';
import { RatingInput } from '../components/RatingInput';
import { TaskSelector } from '../components/TaskSelector';
import { WorkTypeSelector } from '../components/WorkTypeSelector';
import { describeError } from '../lib/errors';
import styles from './StartSessionPage.module.css';

/** 割り込み予想。boolean を ChoiceGroup で扱うための対応表。 */
const INTERRUPTION_CHOICES: readonly Choice<string>[] = [
  { value: 'no', label: 'いいえ' },
  { value: 'yes', label: 'はい' },
];

type FormState = {
  taskItemId: number | null;
  workTypeId: number | null;
  fatigueLevel: Rating | null;
  expectedFocusLevel: Rating | null;
  moodLevel: Rating | null;
  workLocation: WorkLocation | null;
  locationNote: string;
  meetingCount: number;
  interruptionExpected: boolean;
};

/**
 * 初期値。
 *
 * <b>評価 3 項目に既定値を置かないこと。</b>未選択のまま開始できると、
 * 観測していない値が記録される。既定値を使ってよいのは、事実として
 * 「0 件」「なし」が多数を占める会議件数と割り込み予想だけである
 * （docs/03-use-cases.md UC-04 補足）。
 */
const EMPTY_FORM: FormState = {
  taskItemId: null,
  workTypeId: null,
  fatigueLevel: null,
  expectedFocusLevel: null,
  moodLevel: null,
  workLocation: null,
  locationNote: '',
  meetingCount: 0,
  interruptionExpected: false,
};

/**
 * S-04 作業開始画面（docs/03-use-cases.md UC-04）。
 *
 * <b>本プロダクトで最も重要な画面である。</b>結果を知らない時点での状態・環境を
 * 確定させる。ここで記録する値が分析の説明変数になる。
 *
 * <b>入力項目を増やさないこと。</b>作業前 3 項目・作業環境 4 項目の計 7 項目で
 * 30 秒に収まる想定である。増やすと記録コストが上がり、記録自体が続かなくなる
 * （docs/08-technical-design.md §8 の禁止事項 11）。項目を足す場合は、既存項目を
 * 削るか、分析仕様上の必要性を証明すること。
 */
export function StartSessionPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState<FormState>(EMPTY_FORM);

  const activeSession = useActiveSession();
  const tasks = useTasks({ sort: 'recent' });
  const workTypes = useWorkTypes();
  const startSession = useStartWorkSession();

  /** 開始済みのセッションがある場合、この画面は使わせない（UC-04 例外）。 */
  function goToActiveSession() {
    void navigate('/sessions/active');
  }

  // 409 は S-05 への遷移で扱うため、メッセージを出さない。遷移によって画面が
  // 外れることに任せず、表示しない意図をここで明示する。
  const startError =
    startSession.error instanceof ApiError && startSession.error.isConflict
      ? null
      : startSession.error;

  const isComplete =
    form.taskItemId !== null &&
    form.workTypeId !== null &&
    form.fatigueLevel !== null &&
    form.expectedFocusLevel !== null &&
    form.moodLevel !== null &&
    form.workLocation !== null;

  function selectTask(taskItemId: number) {
    const selected = tasks.data?.find((task) => task.id === taskItemId);

    setForm((prev) => ({
      ...prev,
      taskItemId,
      // 作業タイプはタスクの既定値を継承する。既定値を積極的に使うことが
      // 30 秒制約を満たす主要な手段である（UC-04 補足）。
      workTypeId: selected?.defaultWorkTypeId ?? prev.workTypeId,
    }));
  }

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault();

    const {
      taskItemId,
      workTypeId,
      fatigueLevel,
      expectedFocusLevel,
      moodLevel,
      workLocation,
    } = form;

    if (
      taskItemId === null ||
      workTypeId === null ||
      fatigueLevel === null ||
      expectedFocusLevel === null ||
      moodLevel === null ||
      workLocation === null
    ) {
      return;
    }

    startSession.mutate(
      {
        taskItemId,
        workTypeId,
        preWorkState: { fatigueLevel, expectedFocusLevel, moodLevel },
        workContext: {
          workLocation,
          // 補足は「その他」のときだけ送る。他の場所で送ると 422 になる（WC-2）。
          locationNote:
            workLocation === 'Other' && form.locationNote.trim() !== ''
              ? form.locationNote.trim()
              : null,
          meetingCount: form.meetingCount,
          interruptionExpected: form.interruptionExpected,
        },
      },
      {
        onSuccess: goToActiveSession,
        onError: (error) => {
          // 409 は異常ではない。エラーを見せず進行中の画面へ送る
          // （docs/07-api-design.md §2.14）。
          if (error instanceof ApiError && error.isConflict) {
            goToActiveSession();
          }
        },
      },
    );
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

  if (activeSession.data) {
    return (
      <main>
        <h1>作業を開始</h1>
        <p className={styles.notice}>
          進行中の作業があります。同時に記録できる作業は 1 件までです。
        </p>
        <Link className={styles.primaryLink} to="/sessions/active">
          作業中の画面へ
        </Link>
      </main>
    );
  }

  return (
    <main>
      <h1>作業を開始</h1>

      <form onSubmit={handleSubmit}>
        <TaskSelector
          tasks={tasks.data ?? []}
          value={form.taskItemId}
          onChange={selectTask}
        />

        <WorkTypeSelector
          workTypes={workTypes.data ?? []}
          value={form.workTypeId}
          onChange={(workTypeId) => setForm((prev) => ({ ...prev, workTypeId }))}
        />

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>作業前の状態</h2>

          <RatingInput
            metric="fatigueLevel"
            value={form.fatigueLevel}
            onChange={(fatigueLevel) => setForm((prev) => ({ ...prev, fatigueLevel }))}
          />
          <RatingInput
            metric="expectedFocusLevel"
            value={form.expectedFocusLevel}
            onChange={(expectedFocusLevel) =>
              setForm((prev) => ({ ...prev, expectedFocusLevel }))
            }
          />
          <RatingInput
            metric="moodLevel"
            value={form.moodLevel}
            onChange={(moodLevel) => setForm((prev) => ({ ...prev, moodLevel }))}
          />
        </section>

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>作業環境</h2>

          <LocationSelector
            value={form.workLocation}
            onChange={(workLocation) => setForm((prev) => ({ ...prev, workLocation }))}
            locationNote={form.locationNote}
            onLocationNoteChange={(locationNote) =>
              setForm((prev) => ({ ...prev, locationNote }))
            }
          />

          <CountStepper
            label="今日の会議件数"
            value={form.meetingCount}
            onChange={(meetingCount) => setForm((prev) => ({ ...prev, meetingCount }))}
            unit="件"
          />

          <ChoiceGroup
            legend="割り込みが予想されるか"
            name="interruptionExpected"
            choices={INTERRUPTION_CHOICES}
            value={form.interruptionExpected ? 'yes' : 'no'}
            onChange={(next) =>
              setForm((prev) => ({ ...prev, interruptionExpected: next === 'yes' }))
            }
          />
        </section>

        {startError && (
          <p className={styles.error} role="alert">
            {describeError(startError)}
          </p>
        )}

        <button
          type="submit"
          className={styles.primary}
          disabled={!isComplete || startSession.isPending}
        >
          開始
        </button>
      </form>
    </main>
  );
}
