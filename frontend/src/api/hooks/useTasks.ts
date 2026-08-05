import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient } from '../client';
import { queryKeys } from '../queryClient';
import type {
  SaveTaskItemRequest,
  TaskItemDetailResponse,
  TaskItemResponse,
  TaskItemSort,
} from '../types';

type TaskListParams = {
  includeArchived?: boolean;
  keyword?: string;
  sort?: TaskItemSort;
};

export function useTasks({
  includeArchived = false,
  keyword,
  sort = 'recent',
}: TaskListParams = {}) {
  return useQuery({
    queryKey: queryKeys.tasks({ includeArchived, keyword, sort }),
    queryFn: ({ signal }) =>
      apiClient.get<TaskItemResponse[]>(
        '/api/tasks',
        { includeArchived, keyword, sort },
        signal,
      ),
  });
}

/** 一覧の集計値（セッション数・最終利用時刻）は更新系の応答に含まれないため、再取得する。 */
function useInvalidateTasks() {
  const queryClient = useQueryClient();

  return () => queryClient.invalidateQueries({ queryKey: ['tasks'] });
}

export function useCreateTask() {
  const invalidate = useInvalidateTasks();

  return useMutation({
    mutationFn: (request: SaveTaskItemRequest) =>
      apiClient.post<TaskItemDetailResponse>('/api/tasks', request),
    onSuccess: invalidate,
  });
}

export function useUpdateTask() {
  const invalidate = useInvalidateTasks();

  return useMutation({
    mutationFn: ({ id, ...request }: SaveTaskItemRequest & { id: number }) =>
      apiClient.put<TaskItemDetailResponse>(`/api/tasks/${id}`, request),
    onSuccess: invalidate,
  });
}

/**
 * アーカイブ・アーカイブ解除。
 *
 * <b>これは完了操作ではない。</b>TaskItem に完了の概念はなく、アーカイブは
 * 選択肢からの除外である（docs/08-technical-design.md §8 の禁止事項 2）。
 * 進行中セッションを持つタスクは 409 になる。
 */
export function useArchiveTask() {
  const invalidate = useInvalidateTasks();

  return useMutation({
    mutationFn: ({ id, archived }: { id: number; archived: boolean }) =>
      apiClient.post<TaskItemDetailResponse>(
        `/api/tasks/${id}/${archived ? 'archive' : 'unarchive'}`,
      ),
    onSuccess: invalidate,
  });
}
