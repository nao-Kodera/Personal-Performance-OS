import { QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Route, Routes } from 'react-router';

import { createQueryClient } from './api/queryClient';
import { ActiveSessionPage } from './pages/ActiveSessionPage';
import { FinishSessionPage } from './pages/FinishSessionPage';
import { HistoryPage } from './pages/HistoryPage';
import { HomePage } from './pages/HomePage';
import { StartSessionPage } from './pages/StartSessionPage';
import { TasksPage } from './pages/TasksPage';

const queryClient = createQueryClient();

/**
 * 画面は docs/03-use-cases.md §2 の S-01〜S-09 に対応する。
 * S-03 / S-07 / S-09 は縦切り2 以降で追加する。
 */
export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          {/* S-01 */}
          <Route path="/" element={<HomePage />} />
          {/* S-02 */}
          <Route path="/tasks" element={<TasksPage />} />
          {/* S-04 */}
          <Route path="/sessions/start" element={<StartSessionPage />} />
          {/* S-05 */}
          <Route path="/sessions/active" element={<ActiveSessionPage />} />
          {/* S-06 */}
          <Route path="/sessions/:id/finish" element={<FinishSessionPage />} />
          {/* S-08 */}
          <Route path="/history" element={<HistoryPage />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
