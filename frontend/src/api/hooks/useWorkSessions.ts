import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient } from '../client';
import { queryKeys } from '../queryClient';
import type {
  AbandonWorkSessionRequest,
  StartWorkSessionRequest,
  WorkSessionResponse,
} from '../types';

/**
 * 進行中のセッション。存在しなければ null。
 *
 * 進行中は全体で最大 1 件のため単一オブジェクトが返る（WS-9）。
 * サーバーは不在時に 204 を返すが、undefined のままだと TanStack Query が
 * 例外にするため null へ落とす。
 */
export function useActiveSession() {
  return useQuery({
    queryKey: queryKeys.activeSession(),
    queryFn: async ({ signal }) => {
      const session = await apiClient.get<WorkSessionResponse | undefined>(
        '/api/work-sessions/active',
        undefined,
        signal,
      );

      return session ?? null;
    },
  });
}

/**
 * 作業を開始する。
 *
 * <b>startedAt を送らないこと。</b>開始時刻はサーバーが決める（WS-8）。
 * クライアントから受け取ると、記憶に基づく後付けの記録が可能になる。
 *
 * 409（進行中が既に存在）はエラーではなく、複数タブや戻る操作で起きうる
 * 正常な競合である。呼び出し側は S-05 へ誘導する
 * （docs/07-api-design.md §2.14）。
 */
export function useStartWorkSession() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: StartWorkSessionRequest) =>
      apiClient.post<WorkSessionResponse>('/api/work-sessions/start', request),
    onSuccess: (session) => {
      queryClient.setQueryData(queryKeys.activeSession(), session);

      // タスク一覧の最終利用時刻とセッション数が変わる。
      void queryClient.invalidateQueries({ queryKey: ['tasks'] });
    },
  });
}

/**
 * 作業として成立しなかったセッションを終了する（UC-06）。
 *
 * 成果は記録されない（WS-4）。Abandoned は分析の母集団から除外されるが、
 * 履歴には残る。<b>削除ではない。</b>「開始したが作業にならなかった」という
 * 事実自体が情報である（docs/01-product-requirements.md §8 原則2）。
 */
export function useAbandonSession() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, note }: AbandonWorkSessionRequest & { id: number }) =>
      apiClient.post<WorkSessionResponse>(`/api/work-sessions/${id}/abandon`, { note }),
    onSuccess: () => {
      queryClient.setQueryData(queryKeys.activeSession(), null);

      // 履歴に中断終了として現れる。
      void queryClient.invalidateQueries({ queryKey: ['work-sessions'] });
    },
  });
}
