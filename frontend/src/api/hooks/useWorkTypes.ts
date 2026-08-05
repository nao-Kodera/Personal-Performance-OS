import { useQuery } from '@tanstack/react-query';

import { apiClient } from '../client';
import { queryKeys } from '../queryClient';
import type { WorkTypeResponse } from '../types';

/**
 * 作業タイプの一覧。表示順の昇順で返る。
 *
 * 既定では無効なものを含めない。過去のセッションが参照する無効な
 * 作業タイプの名称が必要な場合のみ includeInactive を使う。
 */
export function useWorkTypes(includeInactive = false) {
  return useQuery({
    queryKey: queryKeys.workTypes(includeInactive),
    queryFn: ({ signal }) =>
      apiClient.get<WorkTypeResponse[]>(
        '/api/work-types',
        { includeInactive },
        signal,
      ),
  });
}
