using FluentValidation.TestHelper;
using HtmlPageAnalyzer.Models;
using HtmlPageAnalyzer.Validation;

namespace HtmlPageAnalyzer.Tests.Validation;

public sealed class ProcessRequestValidatorTests
{
    private const string ValidBase64 = "aHR0cHM6Ly90ZXN0LmNvbS9wYWdlMQ==";

    private readonly ProcessRequestValidator _validator = new();

    private static ProcessRequest CreateRequest(
        string selector = "a[href]",
        string attribute = "href",
        string urlB64 = ValidBase64,
        string encryptedTextBytesB64 = "hXeVCcIEyC/5ovf4eyJCozhRbTUV5jjBzOUPBM6dgZnoGyY8CNFBYxffu9fHJp5bSPKzdsFbMZ9gNZfhCG17Sg==",
        string keyBytesB64 = "SGVsbG8gVGVzdEpvYiAyNTYgYml0IHNlY3JldCBrZXk=",
        string pageB64 = "PHA+SGVsbG88L3A+")
    {
        return new ProcessRequest
        {
            Selector = selector,
            Attribute = attribute,
            UrlB64 = urlB64,
            EncryptedTextBytesB64 = encryptedTextBytesB64,
            KeyBytesB64 = keyBytesB64,
            PageB64 = pageB64
        };
    }

    private static void AssertRejected(
        FluentValidation.Results.ValidationResult result,
        string propertyName,
        string wireFieldName)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == propertyName);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains($"'{wireFieldName}'"));
    }

    [Fact]
    public async Task FullyPopulatedRequest_PassesValidation()
    {
        var result = await _validator.ValidateAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankSelector_IsRejected(string selector)
    {
        var result = await _validator.ValidateAsync(CreateRequest(selector: selector), TestContext.Current.CancellationToken);

        AssertRejected(result, nameof(ProcessRequest.Selector), "selector");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankAttribute_IsRejected(string attribute)
    {
        var result = await _validator.ValidateAsync(CreateRequest(attribute: attribute), TestContext.Current.CancellationToken);

        AssertRejected(result, nameof(ProcessRequest.Attribute), "attribute");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankUrlB64_IsRejected(string urlB64)
    {
        var result = await _validator.ValidateAsync(CreateRequest(urlB64: urlB64), TestContext.Current.CancellationToken);

        AssertRejected(result, nameof(ProcessRequest.UrlB64), "url_b64");
    }

    [Fact]
    public async Task BlankEncryptedText_IsRejected()
    {
        var result = await _validator.ValidateAsync(
            CreateRequest(encryptedTextBytesB64: " "),
            TestContext.Current.CancellationToken);

        AssertRejected(result, nameof(ProcessRequest.EncryptedTextBytesB64), "encrypted_text_bytes_b64");
    }

    [Fact]
    public async Task BlankKey_IsRejected()
    {
        var result = await _validator.ValidateAsync(
            CreateRequest(keyBytesB64: string.Empty),
            TestContext.Current.CancellationToken);

        AssertRejected(result, nameof(ProcessRequest.KeyBytesB64), "key_bytes_b64");
    }

    [Fact]
    public async Task BlankPage_IsRejected()
    {
        var result = await _validator.ValidateAsync(
            CreateRequest(pageB64: "  "),
            TestContext.Current.CancellationToken);

        AssertRejected(result, nameof(ProcessRequest.PageB64), "page_b64");
    }

    [Fact]
    public async Task MissingEveryField_ReportsAllSixErrors()
    {
        var result = await _validator.ValidateAsync(new ProcessRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal(6, result.Errors.Count);
    }

    [Fact]
    public void TestHelper_ReportsFailuresForBlankSelector()
    {
        var shouldHaveFailures = CreateRequest(selector: string.Empty);

        _validator.TestValidate(shouldHaveFailures)
            .ShouldHaveValidationErrorFor(request => request.Selector)
            .WithErrorMessage("'selector' must be specified and not empty.");
    }

    [Fact]
    public void TestHelper_PassesForFullyPopulatedRequest()
    {
        _validator.TestValidate(CreateRequest()).ShouldNotHaveAnyValidationErrors();
    }
}
