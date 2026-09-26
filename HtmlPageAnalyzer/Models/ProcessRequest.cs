using System.Text.Json.Serialization;

namespace HtmlPageAnalyzer.Models;

public sealed class ProcessRequest
{
    [JsonPropertyName("selector")]
    public string Selector { get; init; } = string.Empty;

    [JsonPropertyName("attribute")]
    public string Attribute { get; init; } = string.Empty;

    [JsonPropertyName("url_b64")]
    public string UrlB64 { get; init; } = string.Empty;

    [JsonPropertyName("encrypted_text_bytes_b64")]
    public string EncryptedTextBytesB64 { get; init; } = string.Empty;

    [JsonPropertyName("key_bytes_b64")]
    public string KeyBytesB64 { get; init; } = string.Empty;

    [JsonPropertyName("page_b64")]
    public string PageB64 { get; init; } = string.Empty;
}
