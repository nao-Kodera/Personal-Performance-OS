namespace PerformanceOs.Application.Common;

/// <summary>
/// 指定された ID のリソースが存在しない。HTTP 404 に変換される。
/// </summary>
public sealed class NotFoundException : ApplicationException
{
    public NotFoundException(string message) : base(message)
    {
    }

    public static NotFoundException For(string resourceName, long id)
        => new($"{resourceName}が見つかりません: id={id}");
}
