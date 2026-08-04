import { useQuery } from '@tanstack/react-query';

import { apiClient, ApiError, NetworkError } from '../api/client';
import { queryKeys } from '../api/queryClient';
import type { WorkTypeResponse } from '../api/types';

/**
 * 基盤の疎通確認用の画面。
 *
 * 実際の画面（S-01 ホーム）は T-19 で作る。この画面はそのときに置き換える。
 * ここでは API クライアント・TanStack Query・ルーティングが繋がっていることだけを示す。
 */
export function FoundationPage() {
  const { data, isPending, error } = useQuery({
    queryKey: queryKeys.workTypes(false),
    queryFn: ({ signal }) =>
      apiClient.get<WorkTypeResponse[]>('/api/work-types', undefined, signal),
  });

  return (
    <main>
      <h1>Personal Performance OS</h1>
      <p>
        タスクを管理することが目的ではない。自分の状態・環境・行動・成果を観測し、
        高いパフォーマンスが出る条件を発見して再現することが目的である。
      </p>

      <h2>API 疎通確認</h2>

      {isPending && <p>読み込み中…</p>}

      {error instanceof NetworkError && (
        <p role="alert">
          サーバーに接続できませんでした。バックエンドが起動しているか確認してください。
        </p>
      )}

      {error instanceof ApiError && <p role="alert">{error.message}</p>}

      {data && (
        <ul>
          {data.map((workType) => (
            <li key={workType.id}>{workType.name}</li>
          ))}
        </ul>
      )}
    </main>
  );
}
