using System.Text.Json.Serialization;

namespace HtmlPageAnalyzer.Models;

public sealed class ProcessResponse
{
    [JsonPropertyName("is_error")]
    public int IsError { get; set; }

    [JsonPropertyName("error_code")]
    public string ErrorCode { get; set; } = "OK";

    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; set; } = string.Empty;

    [JsonPropertyName("elements_count")]
    public int ElementsCount { get; set; }

    [JsonPropertyName("emails_count")]
    public int EmailsCount { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("decrypted_plain_text")]
    public string DecryptedPlainText { get; set; } = string.Empty;

    [JsonPropertyName("elements_attr_list")]
    public List<string> ElementsAttrList { get; set; } = [];

    [JsonPropertyName("emails_list")]
    public List<string> EmailsList { get; set; } = [];
}
