using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Dapper;
using Npgsql;
using HtmlPageAnalyzer.Models;

namespace HtmlPageAnalyzer.Services;

public partial class ElementProcessingService(IConfiguration configuration) : IElementProcessingService
{
    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS elements (
            id BIGSERIAL PRIMARY KEY,
            attribute_value TEXT NOT NULL,
            html TEXT NOT NULL
        );
        """;

    private const string InsertElementsSql = """
        INSERT INTO elements (attribute_value, html)
        SELECT data.attribute_value, data.html
        FROM UNNEST(@AttributeValues, @HtmlValues) AS data(attribute_value, html);
        """;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly string _connectionString = configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

    public async Task<ProcessResponse> ProcessAsync(
        ProcessRequest request,
        CancellationToken cancellationToken)
    {
        var response = new ProcessResponse();

        try
        {
            response.Url = DecodeText(request.UrlB64, "INVALID_URL_BASE64", "url_b64");
            var page = DecodeText(request.PageB64, "INVALID_PAGE_BASE64", "page_b64");
            var parser = new HtmlParser();
            var document = parser.ParseDocument(page);
            var selectedElements = document.QuerySelectorAll(request.Selector).ToArray();
            var elements = selectedElements
                .Select(element => new ElementData(
                    element.GetAttribute(request.Attribute) ?? string.Empty,
                    element.OuterHtml))
                .ToArray();

            response.ElementsCount = elements.Length;
            response.ElementsAttrList = elements.Select(element => element.AttributeValue).ToList();
            response.EmailsList = EmailRegex()
                .Matches(page)
                .Select(match => match.Value)
                .ToList();
            response.EmailsCount = response.EmailsList.Count;
            response.DecryptedPlainText = DecryptText(
                request.EncryptedTextBytesB64,
                request.KeyBytesB64);

            await SaveElementsAsync(elements, cancellationToken);
            return response;
        }
        catch (ProcessingException exception)
        {
            SetError(response, exception.ErrorCode, exception.Message);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            SetError(response, "INTERNAL_ERROR", exception.Message);
            return response;
        }
    }

    private static string DecodeText(string value, string errorCode, string fieldName)
    {
        try
        {
            return StrictUtf8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException)
        {
            throw new ProcessingException(errorCode, $"'{fieldName}' is not valid Base64.");
        }
        catch (DecoderFallbackException)
        {
            throw new ProcessingException(errorCode, $"'{fieldName}' must contain UTF-8 text.");
        }
    }

    private static string DecryptText(string encryptedTextB64, string keyB64)
    {
        byte[] encryptedText;
        byte[] key;

        try
        {
            encryptedText = Convert.FromBase64String(encryptedTextB64);
        }
        catch (FormatException)
        {
            throw new ProcessingException(
                "INVALID_ENCRYPTED_TEXT_BASE64",
                "'encrypted_text_bytes_b64' is not valid Base64.");
        }

        try
        {
            key = Convert.FromBase64String(keyB64);
        }
        catch (FormatException)
        {
            throw new ProcessingException(
                "INVALID_KEY_BASE64",
                "'key_bytes_b64' is not valid Base64.");
        }

        if (key.Length != 32)
        {
            throw new ProcessingException(
                "INVALID_AES_KEY_LENGTH",
                "AES-256 requires a 32-byte key.");
        }

        if (encryptedText.Length == 0 || encryptedText.Length % 16 != 0)
        {
            throw new ProcessingException(
                "INVALID_CIPHERTEXT_LENGTH",
                "AES ciphertext length must be a non-zero multiple of 16 bytes.");
        }

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key;

        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(encryptedText, 0, encryptedText.Length);

        try
        {
            return StrictUtf8.GetString(decryptedBytes);
        }
        catch (DecoderFallbackException)
        {
            throw new ProcessingException(
                "INVALID_DECRYPTED_TEXT",
                "Decrypted bytes do not contain valid UTF-8 text.");
        }
    }

    private async Task SaveElementsAsync(
        IReadOnlyCollection<ElementData> elements,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            CreateTableSql,
            cancellationToken: cancellationToken));

        if (elements.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            InsertElementsSql,
            new
            {
                AttributeValues = elements.Select(element => element.AttributeValue).ToArray(),
                HtmlValues = elements.Select(element => element.Html).ToArray()
            },
            cancellationToken: cancellationToken));
    }

    private static void SetError(ProcessResponse response, string errorCode, string errorMessage)
    {
        response.IsError = 1;
        response.ErrorCode = errorCode;
        response.ErrorMessage = errorMessage;
    }

    [GeneratedRegex(
        @"(?<![A-Z0-9._%+-])[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}(?![A-Z0-9.-])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    private sealed record ElementData(string AttributeValue, string Html);

    private sealed class ProcessingException(string errorCode, string message) : Exception(message)
    {
        public string ErrorCode { get; } = errorCode;
    }
}
