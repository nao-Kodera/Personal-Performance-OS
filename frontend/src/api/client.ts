/**
 * API 呼び出しの唯一の窓口。
 *
 * 画面から直接 fetch しないこと。ProblemDetails の解釈をここに集約する
 * （docs/08-technical-design.md §3.10）。
 */

/** RFC 7807 の ProblemDetails（docs/07-api-design.md §0.2）。 */
export type ProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  /** バリデーションエラーのみ。プロパティ名 → メッセージ配列。 */
  errors?: Record<string, string[]>;
};

/**
 * API がエラーを返したことを表す。
 *
 * 呼び出し側は status で分岐する。特に 409 は異常ではなく、
 * 複数タブや戻る操作で起きうる正常な競合である（docs/07-api-design.md §2.14）。
 */
export class ApiError extends Error {
  // erasableSyntaxOnly が有効なため、コンストラクタのパラメータプロパティは使えない。
  readonly status: number;
  readonly problem: ProblemDetails;

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `HTTP ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  /** 現在の状態と操作が矛盾する。時間をおけば成功しうる。 */
  get isConflict(): boolean {
    return this.status === 409;
  }

  get isNotFound(): boolean {
    return this.status === 404;
  }

  /** 入力値の問題（400 バリデーション / 422 ドメイン規則）。 */
  get isInvalidInput(): boolean {
    return this.status === 400 || this.status === 422;
  }

  /**
   * 項目ごとのエラーメッセージ。入力欄の下に出すために使う。
   * キーは API のプロパティ名（例: `Result.FocusLevel`）。
   */
  get fieldErrors(): Record<string, string[]> {
    return this.problem.errors ?? {};
  }
}

/** ネットワーク到達不能など、応答を得られなかった場合。 */
export class NetworkError extends Error {
  constructor(cause: unknown) {
    super('サーバーに接続できませんでした。');
    this.name = 'NetworkError';
    this.cause = cause;
  }
}

// 既定値は `dotnet run` の http プロファイル（launchSettings.json）。
// docker-compose で起動する場合は VITE_API_BASE_URL で 5080 に上書きされる。
const baseUrl: string = (
  import.meta.env['VITE_API_BASE_URL'] ?? 'http://localhost:5238'
).replace(/\/$/, '');

type QueryValue = string | number | boolean | undefined | null;

function buildUrl(path: string, query?: Record<string, QueryValue>): string {
  const url = `${baseUrl}${path}`;

  if (!query) {
    return url;
  }

  const params = new URLSearchParams();

  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null) {
      params.append(key, String(value));
    }
  }

  const queryString = params.toString();

  return queryString ? `${url}?${queryString}` : url;
}

async function toProblem(response: Response): Promise<ProblemDetails> {
  try {
    const body: unknown = await response.json();

    if (body !== null && typeof body === 'object') {
      return body as ProblemDetails;
    }
  } catch {
    // 本文が JSON でない場合は下の既定値を使う。握り潰しではなく、
    // status だけを持つ ProblemDetails に落とす。
  }

  return { status: response.status, title: `HTTP ${response.status}` };
}

type RequestOptions = {
  method?: string;
  body?: unknown;
  query?: Record<string, QueryValue>;
  signal?: AbortSignal;
};

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, query, signal } = options;

  let response: Response;

  try {
    response = await fetch(buildUrl(path, query), {
      method,
      headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    });
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError') {
      throw cause;
    }

    throw new NetworkError(cause);
  }

  if (!response.ok) {
    throw new ApiError(response.status, await toProblem(response));
  }

  // 204 No Content。進行中セッションが無い場合など、正常な空応答がある。
  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export const apiClient = {
  get: <T>(path: string, query?: Record<string, QueryValue>, signal?: AbortSignal) =>
    request<T>(path, { query, signal }),

  post: <T>(path: string, body?: unknown, signal?: AbortSignal) =>
    request<T>(path, { method: 'POST', body, signal }),

  put: <T>(path: string, body?: unknown, signal?: AbortSignal) =>
    request<T>(path, { method: 'PUT', body, signal }),
};
