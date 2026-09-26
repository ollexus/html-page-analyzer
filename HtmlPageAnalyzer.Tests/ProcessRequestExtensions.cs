using HtmlPageAnalyzer.Models;

namespace HtmlPageAnalyzer.Tests;

internal static class ProcessRequestExtensions
{
    /// <summary>
    ///     Copies a request, overriding only the named fields. A null argument keeps the
    ///     original value, so tests can state just the one field they are changing.
    /// </summary>
    public static ProcessRequest With(
        this ProcessRequest source,
        string? selector = null,
        string? attribute = null,
        string? urlB64 = null,
        string? encryptedTextBytesB64 = null,
        string? keyBytesB64 = null,
        string? pageB64 = null) => new()
        {
            Selector = selector ?? source.Selector,
            Attribute = attribute ?? source.Attribute,
            UrlB64 = urlB64 ?? source.UrlB64,
            EncryptedTextBytesB64 = encryptedTextBytesB64 ?? source.EncryptedTextBytesB64,
            KeyBytesB64 = keyBytesB64 ?? source.KeyBytesB64,
            PageB64 = pageB64 ?? source.PageB64
        };
}
