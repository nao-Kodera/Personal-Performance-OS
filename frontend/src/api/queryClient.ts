import { QueryClient } from '@tanstack/react-query';

import { ApiError } from './client';

/**
 * サーバー状態の設定。
 *
 * アプリの状態のほぼ全てがサーバー由来のため、状態管理ライブラリは入れない。
 * クライアント固有の状態は作業中画面の中断カウンタ程度しか残らない
 * （docs/08-technical-design.md §1.2）。
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // 単一ユーザーのローカル運用であり、他所からの更新は起きない。
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        retry: (failureCount, error) => {
          // 入力の誤り・状態の矛盾・不在は、再試行しても結果が変わらない。
          if (error instanceof ApiError && error.status < 500) {
            return false;
          }

          return failureCount < 2;
        },
      },
      mutations: {
        // 記録操作は再送すると二重登録になりうる。自動再試行しない。
        retry: false,
      },
    },
  });
}

/**
 * クエリキーの定義。
 *
 * 文字列を各所で組み立てると、無効化の対象漏れが起きる。ここに集約する。
 */
export const queryKeys = {
  workTypes: (includeInactive: boolean) => ['work-types', { includeInactive }] as const,
  tasks: (params: { includeArchived: boolean; keyword?: string; sort?: string }) =>
    ['tasks', params] as const,
  activeSession: () => ['work-sessions', 'active'] as const,
  sessionHistory: (params: Record<string, unknown>) =>
    ['work-sessions', 'history', params] as const,
};
