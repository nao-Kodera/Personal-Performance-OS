using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PerformanceOs.Api.Serialization;

/// <summary>
/// <see cref="DateTimeOffset"/> を UTC の ISO 8601（末尾 Z）で書き出す。
/// </summary>
/// <remarks>
/// <para>
/// 既定の書式はオフセット表記（<c>+00:00</c>）になるが、API 設計 §0 は
/// <c>2026-08-04T04:12:33Z</c> の形を定めている。
/// </para>
/// <para>
/// 併せて、値が UTC 以外のオフセットを持っていても UTC に正規化する。
/// レスポンスの日時は常に UTC であり、JST への変換はクライアントで行う
/// （docs/07-api-design.md §0.1）。
/// </para>
/// </remarks>
public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTimeOffset Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTimeOffset();

    public override void Write(
        Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(
            value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture));
}
