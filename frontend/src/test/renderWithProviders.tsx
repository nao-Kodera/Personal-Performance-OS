import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter } from 'react-router';

import { LocationProbe } from './LocationProbe';

type Options = {
  /** 描画開始時のパス。 */
  route?: string;
};

/**
 * TanStack Query と Router を伴う画面のレンダリング。
 *
 * テストでは再試行を無効にする。有効なままだと、エラー系のテストが
 * 再試行の完了を待つことになり遅く不安定になる。
 */
export function renderWithProviders(ui: ReactElement, { route = '/' }: Options = {}) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
      mutations: { retry: false },
    },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>
        {ui}
        <LocationProbe />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}
