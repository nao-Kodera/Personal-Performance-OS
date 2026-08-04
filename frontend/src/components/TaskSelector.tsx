import { useId, useState } from 'react';

import type { TaskItemResponse } from '../api/types';
import { formatJstDate } from '../lib/datetime';
import styles from './TaskSelector.module.css';

type Props = {
  /**
   * 選択肢。直近使用順で渡すこと（API の sort=recent）。
   * アーカイブ済みは含めない。
   */
  tasks: readonly TaskItemResponse[];
  value: number | null;
  onChange: (taskItemId: number) => void;
};

/**
 * タスクの選択。
 *
 * 件数が増えるため検索できるようにする。並び順は呼び出し側が決める。
 * 作業開始時の入力は 30 秒以内に収める必要があり、タスク選択が速いことが
 * 前提になる（docs/03-use-cases.md §6）。
 */
export function TaskSelector({ tasks, value, onChange }: Props) {
  const [keyword, setKeyword] = useState('');
  const searchId = useId();
  const groupName = useId();

  const normalized = keyword.trim().toLowerCase();
  const visible =
    normalized === ''
      ? tasks
      : tasks.filter((task) => task.title.toLowerCase().includes(normalized));

  return (
    <div className={styles.field}>
      <label className={styles.label} htmlFor={searchId}>
        タスク
      </label>

      <input
        id={searchId}
        type="search"
        className={styles.search}
        placeholder="タスク名で絞り込む"
        value={keyword}
        onChange={(event) => setKeyword(event.target.value)}
      />

      {visible.length === 0 ? (
        <p className={styles.empty}>
          {tasks.length === 0
            ? 'タスクが登録されていません。'
            : '該当するタスクがありません。'}
        </p>
      ) : (
        <ul className={styles.list}>
          {visible.map((task) => (
            <li key={task.id} className={styles.item}>
              <label className={styles.option}>
                <input
                  type="radio"
                  className="visually-hidden"
                  name={groupName}
                  value={task.id}
                  checked={value === task.id}
                  onChange={() => onChange(task.id)}
                />
                <span className={styles.body}>
                  {task.title}
                  <span className={styles.meta}>
                    {task.defaultWorkTypeName}
                    {task.lastUsedAt === null
                      ? '・未実施'
                      : `・最終 ${formatJstDate(task.lastUsedAt)}（${task.sessionCount}回）`}
                  </span>
                </span>
              </label>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
