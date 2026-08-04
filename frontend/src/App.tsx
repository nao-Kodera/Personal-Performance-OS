import { QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Route, Routes } from 'react-router';

import { createQueryClient } from './api/queryClient';
import { FoundationPage } from './pages/FoundationPage';

const queryClient = createQueryClient();

/**
 * 画面は docs/03-use-cases.md §2 の S-01〜S-09 に対応する。
 * ルートは各画面のタスク（T-14 以降）で追加する。
 */
export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<FoundationPage />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
