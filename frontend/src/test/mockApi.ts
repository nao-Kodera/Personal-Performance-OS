import { vi } from 'vitest';

type Handler = (request: {
  method: string;
  url: string;
  body: unknown;
}) => { status: number; body?: unknown } | undefined;

/**
 * fetch を URL とメソッドで振り分けるスタブ。
 *
 * モックライブラリを増やさず、fetch を直接差し替える。
 * 一致するハンドラが無ければテストを失敗させる（暗黙に 404 を返さない）。
 */
export function mockApi(handlers: Handler[]) {
  const calls: { method: string; url: string; body: unknown }[] = [];

  vi.stubGlobal(
    'fetch',
    vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';
      const body =
        typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;

      calls.push({ method, url, body });

      for (const handler of handlers) {
        const result = handler({ method, url, body });

        if (result) {
          return Promise.resolve(
            new Response(result.body === undefined ? null : JSON.stringify(result.body), {
              status: result.status,
              headers: { 'Content-Type': 'application/json' },
            }),
          );
        }
      }

      throw new Error(`ハンドラが定義されていないリクエスト: ${method} ${url}`);
    }),
  );

  return { calls };
}

export function get(pathFragment: string, body: unknown): Handler {
  return ({ method, url }) =>
    method === 'GET' && url.includes(pathFragment) ? { status: 200, body } : undefined;
}

/** 本文のない 200 系。進行中セッションが無い場合の 204 など。 */
export function getNoContent(pathFragment: string): Handler {
  return ({ method, url }) =>
    method === 'GET' && url.includes(pathFragment) ? { status: 204 } : undefined;
}

export function post(pathFragment: string, status: number, body?: unknown): Handler {
  return ({ method, url }) =>
    method === 'POST' && url.includes(pathFragment) ? { status, body } : undefined;
}
