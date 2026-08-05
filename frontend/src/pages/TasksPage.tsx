import { useId, useState } from 'react';

import {
  useArchiveTask,
  useCreateTask,
  useTasks,
  useUpdateTask,
} from '../api/hooks/useTasks';
import { useWorkTypes } from '../api/hooks/useWorkTypes';
import type { TaskItemResponse } from '../api/types';
import { WorkTypeSelector } from '../components/WorkTypeSelector';
import { formatJstDate } from '../lib/datetime';
import { describeError } from '../lib/errors';
import styles from './TasksPage.module.css';

const MAX_TITLE_LENGTH = 200;
const MAX_NOTE_LENGTH = 2000;

type FormState = {
  editingId: number | null;
  title: string;
  defaultWorkTypeId: number | null;
  note: string;
};

const EMPTY_FORM: FormState = {
  editingId: null,
  title: '',
  defaultWorkTypeId: null,
  note: '',
};

/**
 * S-02 タスク画面（docs/03-use-cases.md UC-01）。
 *
 * <b>完了操作を作らないこと。</b>TaskItem に完了の概念はない。使わなくなった
 * ものはアーカイブする。これは完了ではなく選択肢からの除外である
 * （docs/08-technical-design.md §8 の禁止事項 2）。
 *
 * 期限・優先度・タグ・親子関係も追加しない。
 */
export function TasksPage() {
  const [includeArchived, setIncludeArchived] = useState(false);
  const [form, setForm] = useState<FormState>(EMPTY_FORM);

  const titleId = useId();
  const noteId = useId();
  const archivedToggleId = useId();

  const workTypes = useWorkTypes();
  const tasks = useTasks({ includeArchived, sort: 'updated' });

  const createTask = useCreateTask();
  const updateTask = useUpdateTask();
  const archiveTask = useArchiveTask();

  const mutationError =
    createTask.error ?? updateTask.error ?? archiveTask.error ?? null;

  const isEditing = form.editingId !== null;
  const canSubmit =
    form.title.trim().length > 0 &&
    form.defaultWorkTypeId !== null &&
    !createTask.isPending &&
    !updateTask.isPending;

  function startEditing(task: TaskItemResponse) {
    setForm({
      editingId: task.id,
      title: task.title,
      defaultWorkTypeId: task.defaultWorkTypeId,
      note: task.note ?? '',
    });
  }

  function resetForm() {
    setForm(EMPTY_FORM);
    createTask.reset();
    updateTask.reset();
  }

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault();

    if (!canSubmit || form.defaultWorkTypeId === null) {
      return;
    }

    const request = {
      title: form.title.trim(),
      defaultWorkTypeId: form.defaultWorkTypeId,
      note: form.note.trim() === '' ? null : form.note.trim(),
    };

    const onSuccess = () => setForm(EMPTY_FORM);

    if (form.editingId === null) {
      createTask.mutate(request, { onSuccess });
    } else {
      updateTask.mutate({ id: form.editingId, ...request }, { onSuccess });
    }
  }

  return (
    <main>
      <div className={styles.header}>
        <h1>タスク</h1>
      </div>

      <form className={styles.form} onSubmit={handleSubmit}>
        <h2 className={styles.formTitle}>
          {isEditing ? 'タスクを編集' : 'タスクを登録'}
        </h2>

        <div className={styles.field}>
          <label className={styles.label} htmlFor={titleId}>
            タイトル
          </label>
          <input
            id={titleId}
            className={styles.input}
            type="text"
            value={form.title}
            maxLength={MAX_TITLE_LENGTH}
            onChange={(event) =>
              setForm((prev) => ({ ...prev, title: event.target.value }))
            }
          />
        </div>

        <WorkTypeSelector
          legend="既定の作業タイプ"
          workTypes={workTypes.data ?? []}
          value={form.defaultWorkTypeId}
          onChange={(defaultWorkTypeId) =>
            setForm((prev) => ({ ...prev, defaultWorkTypeId }))
          }
        />

        <div className={styles.field}>
          <label className={styles.label} htmlFor={noteId}>
            メモ
          </label>
          <textarea
            id={noteId}
            className={styles.textarea}
            value={form.note}
            maxLength={MAX_NOTE_LENGTH}
            onChange={(event) =>
              setForm((prev) => ({ ...prev, note: event.target.value }))
            }
          />
        </div>

        <div className={styles.actions}>
          <button type="submit" className={styles.primary} disabled={!canSubmit}>
            {isEditing ? '更新' : '登録'}
          </button>

          {isEditing && (
            <button type="button" className={styles.secondary} onClick={resetForm}>
              キャンセル
            </button>
          )}
        </div>
      </form>

      {mutationError && (
        <p className={styles.error} role="alert">
          {describeError(mutationError)}
        </p>
      )}

      <div className={styles.filters}>
        <input
          id={archivedToggleId}
          type="checkbox"
          checked={includeArchived}
          onChange={(event) => setIncludeArchived(event.target.checked)}
        />
        <label htmlFor={archivedToggleId}>アーカイブ済みも表示する</label>
      </div>

      {tasks.isPending && <p>読み込み中…</p>}

      {tasks.error && (
        <p className={styles.error} role="alert">
          {describeError(tasks.error)}
        </p>
      )}

      {tasks.data?.length === 0 && (
        <p className={styles.empty}>まだタスクがありません。</p>
      )}

      {tasks.data && tasks.data.length > 0 && (
        <ul className={styles.list}>
          {tasks.data.map((task) => (
            <li
              key={task.id}
              className={task.isArchived ? `${styles.item} ${styles.archived}` : styles.item}
            >
              <div>
                <span className={styles.itemTitle}>{task.title}</span>
                <span className={styles.itemMeta}>
                  {task.defaultWorkTypeName}
                  {task.lastUsedAt === null
                    ? '・未実施'
                    : `・最終 ${formatJstDate(task.lastUsedAt)}（${task.sessionCount}回）`}
                  {task.isArchived && '・アーカイブ済み'}
                </span>
                {task.note && <p className={styles.itemNote}>{task.note}</p>}
              </div>

              <div className={styles.itemActions}>
                <button
                  type="button"
                  className={styles.secondary}
                  onClick={() => startEditing(task)}
                >
                  編集
                </button>
                <button
                  type="button"
                  className={styles.secondary}
                  disabled={archiveTask.isPending}
                  onClick={() =>
                    archiveTask.mutate({ id: task.id, archived: !task.isArchived })
                  }
                >
                  {task.isArchived ? 'アーカイブ解除' : 'アーカイブ'}
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
