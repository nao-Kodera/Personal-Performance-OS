namespace PerformanceOs.Application.Common;

/// <summary>
/// アプリケーション層が投げる例外の基底。
/// </summary>
/// <remarks>
/// <para>
/// 派生型ごとに HTTP ステータスへ変換される。変換は
/// <c>ExceptionHandlingMiddleware</c> が一元的に行い、コントローラで個別に
/// try-catch しない（docs/08-technical-design.md §3.8）。
/// </para>
/// <para>
/// <b>この階層を増やさないこと。</b>3 種で表現できない状況が出た場合は、
/// 新しい例外型を作る前に、それが本当にアプリケーション層の関心かを確認する。
/// </para>
/// <para>
/// <c>System.ApplicationException</c> と同名だが、直接インスタンス化できないよう
/// abstract にしてあるため取り違えは起きない。
/// </para>
/// </remarks>
public abstract class ApplicationException : Exception
{
    protected ApplicationException(string message) : base(message)
    {
    }
}
