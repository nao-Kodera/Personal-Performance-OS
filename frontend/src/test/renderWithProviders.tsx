import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router';

import { LocationProbe } from './LocationProbe';

type Options = {
  /** 描画開始時のパス。 */
  route?: string;
  /** URL パラメータを使う画面で指定する。ルート定義に載せて描画する。 */
  path?: string;
  /** navigate の state で運ばれてくる値を再現する。 */
  state?: unknown;
};

/**
 * TanStack Query と Router を伴う画面のレンダリング。
 *
 * テストでは再試行を無効にする。有効なままだと、エラー系のテストが
 * 再試行の完了を待つことになり遅く不安定になる。
 */
export function renderWithProviders(
  ui: ReactElement,
  { route = '/', path, state }: Options = {},
) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
      mutations: { retry: false },
    },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter
        initialEntries={[state === undefined ? route : { pathname: route, state }]}
      >
        {path === undefined ? (
          ui
        ) : (
          <Routes>
            <Route path={path} element={ui} />
          </Routes>
        )}
        <LocationProbe />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}
