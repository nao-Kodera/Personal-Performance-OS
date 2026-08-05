import { ApiError, NetworkError } from '../api/client';

/**
 * 例外を利用者向けの一文にする。
 *
 * 画面ごとにエラー文言を組み立てないこと。API が返す detail をそのまま
 * 使うのが基本であり、ここでは到達不能と想定外だけを補う。
 */
export function describeError(error: unknown): string {
  if (error instanceof NetworkError) {
    return 'サーバーに接続できませんでした。バックエンドが起動しているか確認してください。';
  }

  if (error instanceof ApiError) {
    // 項目別のエラーがある場合はそれも並べる。どの入力が問題かを示すため。
    const fieldMessages = Object.values(error.fieldErrors).flat();

    if (fieldMessages.length > 0) {
      return fieldMessages.join(' ');
    }

    return error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return '予期しないエラーが発生しました。';
}
