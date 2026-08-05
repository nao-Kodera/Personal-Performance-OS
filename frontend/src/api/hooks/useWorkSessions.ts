import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient } from '../client';
import { queryKeys } from '../queryClient';
import type {
  AbandonWorkSessionRequest,
  SaveResultRequest,
  StartWorkSessionRequest,
  WorkSessionDayResponse,
  WorkSessionHistoryQuery,
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
 * 履歴（UC-08）。JST の日付でグループ化され、日付・セッションとも新しい順で返る。
 *
 * 既定は直近 28 日（当日を含む）。深夜をまたぐセッションは開始日側に入る
 * （docs/02-glossary.md §4）。
 */
export function useSessionHistory(query: WorkSessionHistoryQuery = {}) {
  return useQuery({
    queryKey: queryKeys.sessionHistory(query),
    queryFn: ({ signal }) =>
      apiClient.get<WorkSessionDayResponse[]>('/api/work-sessions', query, signal),
  });
}

/**
 * 成果評価を訂正する（UC-08）。
 *
 * <b>訂正できるのは 5 指標・メモ・中断回数だけである。</b>開始/終了時刻、
 * PreWorkState、WorkContext は変更できない。結果を知った後に作業前の状態を
 * 書き換えられると、説明変数と目的変数の独立性が失われ、分析が意味を持たなく
 * なる（docs/03-use-cases.md UC-08・技術設計 §8 の禁止事項 1）。
 *
 * recordedAt は変更されない。updatedAt との差で事後編集を識別する（PR-2）。
 */
export function useUpdateResult() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...request }: SaveResultRequest & { id: number }) =>
      apiClient.put<WorkSessionResponse>(`/api/work-sessions/${id}/result`, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-sessions'] });
    },
  });
}

/**
 * 作業を終了し、成果を記録する（UC-05）。
 *
 * <b>終了と評価は分離できない。</b>分けると「終了したが評価していない」状態が
 * 生じ、Completed なら PerformanceResult が必ず存在するという不変条件が破れる
 * （WS-3）。<b>スキップ導線を作らないこと</b>（技術設計 §8 の禁止事項 4）。
 *
 * finishedAt はサーバーが決める。リクエストに含めない。
 */
export function useFinishSession() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...request }: SaveResultRequest & { id: number }) =>
      apiClient.post<WorkSessionResponse>(`/api/work-sessions/${id}/finish`, request),
    onSuccess: () => {
      queryClient.setQueryData(queryKeys.activeSession(), null);

      void queryClient.invalidateQueries({ queryKey: ['work-sessions'] });
      // タスク一覧のセッション数が変わる。
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
