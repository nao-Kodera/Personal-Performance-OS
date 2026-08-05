import { useLocation } from 'react-router';

/**
 * 現在地の表示。遷移先を検証するためのテスト用であり、画面の一部ではない。
 *
 * renderWithProviders から分けているのは、コンポーネントと関数を同じファイルから
 * 公開しないため（react/only-export-components）。
 */
export function LocationProbe() {
  const location = useLocation();

  return (
    <>
      <span data-testid="location">{location.pathname}</span>
      <span data-testid="location-state">{JSON.stringify(location.state)}</span>
    </>
  );
}
