import { QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Route, Routes } from 'react-router';

import { createQueryClient } from './api/queryClient';
import { FoundationPage } from './pages/FoundationPage';
import { TasksPage } from './pages/TasksPage';

const queryClient = createQueryClient();

/**
 * 画面は docs/03-use-cases.md §2 の S-01〜S-09 に対応する。
 * 未実装の画面は各タスク（T-15 以降）で追加する。
 */
export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          {/* S-01 ホームは T-19 で実装する。それまでは疎通確認用の画面。 */}
          <Route path="/" element={<FoundationPage />} />
          {/* S-02 */}
          <Route path="/tasks" element={<TasksPage />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
