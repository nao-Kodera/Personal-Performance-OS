namespace PerformanceOs.Application.Common;

/// <summary>
/// 値そのものがドメイン規則に反する。HTTP 422 に変換される。
/// </summary>
/// <remarks>
/// 何度試しても成功しない場合に使う。
/// 例: アーカイブ済みの TaskItem を指定した、過去日の DailyCondition を記録しようとした。
/// </remarks>
public sealed class DomainRuleException : ApplicationException
{
    public DomainRuleException(string message) : base(message)
    {
    }
}
