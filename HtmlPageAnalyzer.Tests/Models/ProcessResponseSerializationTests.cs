using System.Text.Json;
using HtmlPageAnalyzer.Models;

namespace HtmlPageAnalyzer.Tests.Models;

public sealed class ProcessResponseSerializationTests
{
    private static readonly string[] ExpectedFieldOrder =
    [
        "is_error",
        "error_code",
        "error_message",
        "elements_count",
        "emails_count",
        "url",
        "decrypted_plain_text",
        "elements_attr_list",
        "emails_list"
    ];

    private static string Serialize(ProcessResponse response) =>
        JsonSerializer.Serialize(response, JsonDefaults.CreateSerializerOptions());

    [Fact]
    public void SuccessResponse_UsesExpectedFieldNamesInDeclaredOrder()
    {
        var json = Serialize(new ProcessResponse
        {
            IsError = 0,
            ErrorCode = "OK",
            Url = "https://test.com/page1",
            DecryptedPlainText = "plain",
            ElementsAttrList = ["https://adv.rbc.ru/"],
            EmailsList = ["webmaster@rbc.ru"]
        });

        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

        Assert.Equal(ExpectedFieldOrder, names);
    }

    [Fact]
    public void Serialization_IsIndentedWithTwoSpaces()
    {
        var json = Serialize(new ProcessResponse());

        Assert.Contains("\n  \"is_error\": 0", json, StringComparison.Ordinal);
        Assert.Contains("\n  \"error_code\": \"OK\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void FreshResponse_ExposesEmptyCollectionsRatherThanNull()
    {
        var json = Serialize(new ProcessResponse());

        using var document = JsonDocument.Parse(json);
        Assert.Equal(0, document.RootElement.GetProperty("is_error").GetInt32());
        Assert.Equal("OK", document.RootElement.GetProperty("error_code").GetString());
        Assert.Equal(string.Empty, document.RootElement.GetProperty("error_message").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("elements_count").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("emails_count").GetInt32());
        Assert.Empty(document.RootElement.GetProperty("elements_attr_list").EnumerateArray());
        Assert.Empty(document.RootElement.GetProperty("emails_list").EnumerateArray());
    }

    [Fact]
    public void ErrorResponse_CarriesCodeAndMessage()
    {
        var json = Serialize(new ProcessResponse
        {
            IsError = 1,
            ErrorCode = "INVALID_URL_BASE64",
            ErrorMessage = "'url_b64' is not valid Base64."
        });

        using var document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("is_error").GetInt32());
        Assert.Equal("INVALID_URL_BASE64", document.RootElement.GetProperty("error_code").GetString());
        Assert.Equal(
            "'url_b64' is not valid Base64.",
            document.RootElement.GetProperty("error_message").GetString());
    }

    [Fact]
    public void NonAsciiText_SurvivesTheJsonRoundTrip()
    {
        const string text = "Телеканал — «Привет»";

        var json = Serialize(new ProcessResponse { DecryptedPlainText = text });

        using var document = JsonDocument.Parse(json);
        Assert.Equal(text, document.RootElement.GetProperty("decrypted_plain_text").GetString());
    }

    [Fact]
    public void NonAsciiText_IsWrittenAsStandardJsonEscapes()
    {
        // System.Text.Json escapes non-ASCII by default. That is valid JSON and decodes
        // transparently, which is the safer choice for an API response.
        var json = Serialize(new ProcessResponse { DecryptedPlainText = "Привет" });

        Assert.DoesNotContain("Привет", json, StringComparison.Ordinal);
        Assert.Contains("\\u", json, StringComparison.Ordinal);
    }
}
