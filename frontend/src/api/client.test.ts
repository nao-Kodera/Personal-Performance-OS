import { afterEach, describe, expect, it, vi } from 'vitest';

import { ApiError, NetworkError, apiClient } from './client';

function mockFetch(response: Response | Error) {
  const fetchMock = vi.fn((_input: RequestInfo | URL, _init?: RequestInit) =>
    response instanceof Error ? Promise.reject(response) : Promise.resolve(response),
  );

  vi.stubGlobal('fetch', fetchMock);

  return fetchMock;
}

function problemResponse(status: number, problem: unknown): Response {
  return new Response(JSON.stringify(problem), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('正常系', () => {
  it('JSON を返す', async () => {
    mockFetch(new Response(JSON.stringify([{ id: 1, name: '実装' }]), { status: 200 }));

    const result = await apiClient.get<{ id: number }[]>('/api/work-types');

    expect(result).toEqual([{ id: 1, name: '実装' }]);
  });

  /** 進行中セッションが無い場合など、正常な空応答がある。 */
  it('204 は undefined を返しエラーにしない', async () => {
    mockFetch(new Response(null, { status: 204 }));

    await expect(apiClient.get('/api/work-sessions/active')).resolves.toBeUndefined();
  });

  it('クエリパラメータを組み立てる', async () => {
    const fetchMock = mockFetch(new Response('[]', { status: 200 }));

    await apiClient.get('/api/tasks', { includeArchived: true, keyword: '認証' });

    const url = String(fetchMock.mock.calls[0]?.[0]);
    expect(url).toContain('includeArchived=true');
    expect(url).toContain('keyword=');
  });

  it('未指定のクエリパラメータを送らない', async () => {
    const fetchMock = mockFetch(new Response('[]', { status: 200 }));

    await apiClient.get('/api/tasks', { keyword: undefined, taskItemId: null });

    expect(String(fetchMock.mock.calls[0]?.[0])).not.toContain('?');
  });
});

describe('ProblemDetails の解釈', () => {
  it('404 を not found として扱う', async () => {
    mockFetch(
      problemResponse(404, {
        type: 'https://performance-os.local/errors/not-found',
        title: 'リソースが見つかりません',
        status: 404,
        detail: 'タスクが見つかりません: id=99',
      }),
    );

    const error = await apiClient.get('/api/tasks/99').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).isNotFound).toBe(true);
    expect((error as ApiError).message).toBe('タスクが見つかりません: id=99');
  });

  /**
   * 409 は異常ではない。複数タブや戻る操作で起きうる正常な競合であり、
   * 画面はエラー表示ではなく進行中セッションへ誘導する
   * （docs/07-api-design.md §2.14）。
   */
  it('409 を競合として扱う', async () => {
    mockFetch(
      problemResponse(409, {
        status: 409,
        detail: '進行中の作業セッションが既に存在します。',
      }),
    );

    const error = (await apiClient
      .post('/api/work-sessions/start', {})
      .catch((e: unknown) => e)) as ApiError;

    expect(error.isConflict).toBe(true);
    expect(error.isInvalidInput).toBe(false);
  });

  it('400 の項目別エラーを取り出せる', async () => {
    mockFetch(
      problemResponse(400, {
        status: 400,
        title: '入力値が不正です',
        errors: { 'Result.FocusLevel': ['集中度は 1〜5 で指定してください。'] },
      }),
    );

    const error = (await apiClient
      .post('/api/work-sessions/1/finish', {})
      .catch((e: unknown) => e)) as ApiError;

    expect(error.isInvalidInput).toBe(true);
    expect(error.fieldErrors['Result.FocusLevel']).toEqual([
      '集中度は 1〜5 で指定してください。',
    ]);
  });

  it('422 も入力の問題として扱う', async () => {
    mockFetch(problemResponse(422, { status: 422, detail: 'アーカイブ済みです' }));

    const error = (await apiClient
      .post('/api/work-sessions/start', {})
      .catch((e: unknown) => e)) as ApiError;

    expect(error.isInvalidInput).toBe(true);
  });

  it('本文が JSON でなくても status を保持する', async () => {
    mockFetch(new Response('<html>500</html>', { status: 500 }));

    const error = (await apiClient.get('/api/tasks').catch((e: unknown) => e)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(500);
  });

  it('項目別エラーが無ければ空オブジェクトを返す', async () => {
    mockFetch(problemResponse(409, { status: 409, detail: '競合' }));

    const error = (await apiClient.get('/api/tasks').catch((e: unknown) => e)) as ApiError;

    expect(error.fieldErrors).toEqual({});
  });
});

describe('通信失敗', () => {
  it('到達不能を NetworkError にする', async () => {
    mockFetch(new TypeError('Failed to fetch'));

    const error = await apiClient.get('/api/tasks').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(NetworkError);
  });
});
