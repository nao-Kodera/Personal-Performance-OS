import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
  },
  // API のオリジンは VITE_API_BASE_URL で指定する（docker-compose.yml 参照）。
  // 開発用プロキシは使わない。バックエンドの CORS で明示的に許可する方が、
  // 開発と本番で経路が変わらず、設定の食い違いに気づける。
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    // テストのタイムゾーンを UTC に固定する。日付境界の扱いは本プロダクトの
    // 根幹であり（docs/02-glossary.md §4）、端末のタイムゾーンに依存すると
    // JST 環境でだけ通るテストができてしまう。表示変換は常に Asia/Tokyo を
    // 明示する実装のため、UTC 環境でも同じ結果になるはずである。
    env: { TZ: 'UTC' },
  },
});
