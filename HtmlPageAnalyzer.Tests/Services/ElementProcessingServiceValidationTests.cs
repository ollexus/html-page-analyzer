using System.Text;
using Microsoft.Extensions.Configuration;
using HtmlPageAnalyzer.Models;
using HtmlPageAnalyzer.Services;

namespace HtmlPageAnalyzer.Tests.Services;

/// <summary>
///     Failure handling that happens before the service ever touches the database, so these
///     tests deliberately run against an unreachable connection string.
/// </summary>
public sealed class ElementProcessingServiceValidationTests
{
    private const string UnreachableConnectionString =
        "Host=127.0.0.1;Port=1;Database=missing;Username=none;Password=none;Timeout=1;Command Timeout=1";

    private static readonly byte[] Key32 = "HtmlPageAnalyzer 256-bit AES key"u8.ToArray();

    private static byte[] Encrypt(byte[] plainText)
    {
        // AES-256 needs exactly 32 key bytes; fail loudly rather than deep inside the crypto stack.
        Assert.Equal(32, Key32.Length);

        // The service decrypts with PaddingMode.None, which cannot process a partial block,
        // so the fixtures must hand us block-aligned plaintext.
        var aligned = new byte[(plainText.Length + 15) / 16 * 16];
        plainText.CopyTo(aligned, 0);

        using var aes = System.Security.Cryptography.Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.Mode = System.Security.Cryptography.CipherMode.ECB;
        aes.Padding = System.Security.Cryptography.PaddingMode.None;
        aes.Key = Key32;

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(aligned, 0, aligned.Length);
    }

    private static ProcessRequest ValidRequest() => new()
    {
        Selector = "a[href]",
        Attribute = "href",
        UrlB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("https://test.com/page1")),
        EncryptedTextBytesB64 = Convert.ToBase64String(Encrypt("hello"u8.ToArray())),
        KeyBytesB64 = Convert.ToBase64String(Key32),
        PageB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            "<html><body><a href=\"https://example.com/a\">a</a><a href=\"https://example.com/b\">b</a>" +
            "<p>mail: user.name+tag@example.co.uk</p></body></html>"))
    };

    private static ElementProcessingService CreateService() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = UnreachableConnectionString
            })
            .Build());

    [Fact]
    public async Task MalformedUrlBase64_ReportsDedicatedErrorCode()
    {
        var request = ValidRequest().With(urlB64: "!!! not base64 !!!");

        var response = await CreateService().ProcessAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(1, response.IsError);
        Assert.Equal("INVALID_URL_BASE64", response.ErrorCode);
        Assert.Equal("'url_b64' is not valid Base64.", response.ErrorMessage);
    }

    [Fact]
    public async Task MalformedPageBase64_ReportsDedicatedErrorCode()
    {
        var request = ValidRequest().With(pageB64: "@@@ not base64 @@@");

        var response = await CreateService().ProcessAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(1, response.IsError);
        Assert.Equal("INVALID_PAGE_BASE64", response.ErrorCode);
        Assert.Equal("'page_b64' is not valid Base64.", response.ErrorMessage);
    }

    [Fact]
    public async Task MalformedKeyBase64_ReportsDedicatedErrorCode()
    {
        var request = ValidRequest().With(keyBytesB64: "###");

        var response = await CreateService().ProcessAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(1, response.IsError);
        Assert.Equal("INVALID_KEY_BASE64", response.ErrorCode);
        Assert.Equal("'key_bytes_b64' is not valid Base64.", response.ErrorMessage);
    }

    [Fact]
    public async Task MalformedCiphertextBase64_ReportsDedicatedErrorCode()
    {
        var request = ValidRequest().With(encryptedTextBytesB64: "###");

        var response = await CreateService().ProcessAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(1, response.IsError);
        Assert.Equal("INVALID_ENCRYPTED_TEXT_BASE64", response.ErrorCode);
    }

    [Fact]
    public async Task KeyThatIsNot256Bits_ReportsDedicatedErrorCode()
    {
        var request = ValidRequest().With(keyBytesB64: Convert.ToBase64String(new byte[16]));

        var response = await CreateService().ProcessAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(1, response.IsError);
        Assert.Equal("INVALID_AES_KEY_LENGTH", response.ErrorCode);
        Assert.Equal("AES-256 requires a 32-byte key.", response.ErrorMessage);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(20)]
    public async Task CiphertextNotAMultipleOfTheBlockSize_ReportsDedicatedErrorCode(int length)
    {
        var request = ValidRequest().With(encryptedTextBytesB64: Convert.ToBase64String(new byte[length]));

        var response = await CreateService().ProcessAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(1, response.IsError);
        Assert.Equal("INVALID_CIPHERTEXT_LENGTH", response.ErrorCode);
    }

    [Fact]
    public async Task Base64UrlThatDecodesToNonUtf8Bytes_ReportsDedicatedErrorCode()
    {
        var request = ValidRequest().With(urlB64: Convert.ToBase64String(new byte[] { 0xC3, 0x28 }));

        var response = await CreateService().ProcessAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(1, response.IsError);
        Assert.Equal("INVALID_URL_BASE64", response.ErrorCode);
        Assert.Equal("'url_b64' must contain UTF-8 text.", response.ErrorMessage);
    }

    [Fact]
    public async Task DecryptedBytesThatAreNotUtf8Text_ReportDedicatedErrorCode()
    {
        var notUtf8 = Enumerable.Repeat(new byte[] { 0xC3, 0x28 }, 8).SelectMany(pair => pair).ToArray();
        var request = ValidRequest().With(encryptedTextBytesB64: Convert.ToBase64String(Encrypt(notUtf8)));

        var response = await CreateService().ProcessAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(1, response.IsError);
        Assert.Equal("INVALID_DECRYPTED_TEXT", response.ErrorCode);
    }

    [Fact]
    public async Task DatabaseOutage_IsReportedAsInternalErrorCarryingExceptionMessage()
    {
        var response = await CreateService().ProcessAsync(ValidRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(1, response.IsError);
        Assert.Equal("INTERNAL_ERROR", response.ErrorCode);
        Assert.NotEmpty(response.ErrorMessage);
    }

    [Fact]
    public async Task CancelledToken_PropagatesInsteadOfBeingSwallowed()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService().ProcessAsync(ValidRequest(), cancellation.Token));
    }
}
