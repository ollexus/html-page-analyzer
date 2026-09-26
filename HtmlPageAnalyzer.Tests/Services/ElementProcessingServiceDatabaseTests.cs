using System.Reflection;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using HtmlPageAnalyzer.Models;
using HtmlPageAnalyzer.Services;

namespace HtmlPageAnalyzer.Tests.Services;

/// <summary>
///     End-to-end tests over the real PostgreSQL instance, because the service persists
///     through Dapper and the point of these tests is exactly that persisted result.
///     The connection string comes from the HTMLPAGEANALYZER_TEST_POSTGRES environment variable;
///     without it the tests report as skipped rather than failed.
/// </summary>
public sealed class ElementProcessingServiceDatabaseTests
{
    private const string ConnectionStringVariable = "HTMLPAGEANALYZER_TEST_POSTGRES";

    private static string RequireConnectionString()
    {
        var value = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            Assert.Skip(
                $"Set {ConnectionStringVariable} to a PostgreSQL connection string to run database tests.");
        }

        return value;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "json_payload_1.txt")))
            {
                return directory.FullName;
            }
        }

        Assert.Skip("Could not locate the repository root containing json_payload_1.txt.");
        return null!;
    }

    private static IConfiguration BuildConfiguration(string connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = connectionString
            })
            .Build();

    private static ElementProcessingService CreateService()
    {
        var connectionString = RequireConnectionString();
        return new ElementProcessingService(BuildConfiguration(connectionString));
    }

    private static async Task<NpgsqlConnection> OpenAsync()
    {
        var connectionString = RequireConnectionString();
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<ProcessRequest> LoadRequestAsync(int index)
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, $"json_payload_{index}.txt");
        return JsonSerializer.Deserialize<ProcessRequest>(
            await File.ReadAllTextAsync(path, Encoding.UTF8))!;
    }

    private static async Task<JsonElement> LoadExpectedAsync(int index)
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, $"json_result_{index}.txt");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, Encoding.UTF8));
        return document.RootElement.Clone();
    }

    private static string[] StringArray(JsonElement element, string property) =>
        element.GetProperty(property).EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static async Task<long> ScalarAsync(string sql)
    {
        await using var connection = await OpenAsync();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql));
    }

    [Fact]
    public async Task Payload1_ReproducesCommittedResultAndPersistsElements()
    {
        var service = CreateService();
        var request = await LoadRequestAsync(1);
        var expected = await LoadExpectedAsync(1);
        var before = await ScalarAsync("SELECT count(*) FROM elements;");

        var response = await service.ProcessAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(0, response.IsError);
        Assert.Equal("OK", response.ErrorCode);
        Assert.Equal(string.Empty, response.ErrorMessage);
        Assert.Equal(expected.GetProperty("elements_count").GetInt32(), response.ElementsCount);
        Assert.Equal(expected.GetProperty("emails_count").GetInt32(), response.EmailsCount);
        Assert.Equal(expected.GetProperty("url").GetString(), response.Url);
        Assert.Equal(expected.GetProperty("decrypted_plain_text").GetString(), response.DecryptedPlainText);
        Assert.Equal(StringArray(expected, "elements_attr_list"), response.ElementsAttrList);
        Assert.Equal(StringArray(expected, "emails_list"), response.EmailsList);

        Assert.Equal(before + response.ElementsCount, await ScalarAsync("SELECT count(*) FROM elements;"));
    }

    [Fact]
    public async Task Payload2_ReproducesCommittedResult()
    {
        var service = CreateService();
        var request = await LoadRequestAsync(2);
        var expected = await LoadExpectedAsync(2);

        var response = await service.ProcessAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(0, response.IsError);
        Assert.Equal(expected.GetProperty("elements_count").GetInt32(), response.ElementsCount);
        Assert.Equal(expected.GetProperty("emails_count").GetInt32(), response.EmailsCount);
        Assert.Equal(expected.GetProperty("url").GetString(), response.Url);
        Assert.Equal(StringArray(expected, "elements_attr_list"), response.ElementsAttrList);
        Assert.Equal(StringArray(expected, "emails_list"), response.EmailsList);
    }

    [Fact]
    public async Task StoredHtml_KeepsNonAsciiCharactersIntact()
    {
        var service = CreateService();
        var request = await LoadRequestAsync(1);

        await service.ProcessAsync(request, TestContext.Current.CancellationToken);

        var matches = await ScalarAsync(
            "SELECT count(*) FROM elements WHERE html LIKE '%Телеканал%' OR html LIKE '%Фотобанк%';");

        Assert.True(matches > 0, "non-ASCII HTML was not stored as readable UTF-8 text");
    }

    [Fact]
    public async Task SelectorWithoutMatches_ReturnsZeroCountsAndPersistsNothing()
    {
        var service = CreateService();
        var request = await LoadRequestAsync(2);
        var before = await ScalarAsync("SELECT count(*) FROM elements;");

        var response = await service.ProcessAsync(
            request.With(selector: "table.no.such.element"),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, response.IsError);
        Assert.Equal(0, response.ElementsCount);
        Assert.Empty(response.ElementsAttrList);
        Assert.Equal(before, await ScalarAsync("SELECT count(*) FROM elements;"));
    }

    [Fact]
    public async Task IdentifiersAreUniqueAndAutoGenerated()
    {
        var service = CreateService();
        var request = await LoadRequestAsync(2);

        await service.ProcessAsync(request, TestContext.Current.CancellationToken);

        var total = await ScalarAsync("SELECT count(*) FROM elements;");
        var distinct = await ScalarAsync("SELECT count(DISTINCT id) FROM elements;");

        Assert.Equal(total, distinct);
    }

    [Fact]
    public async Task PersistedRows_KeepAttributeValueAndFullHtml()
    {
        var service = CreateService();
        var request = await LoadRequestAsync(2);
        var before = await ScalarAsync("SELECT coalesce(max(id), 0) FROM elements;");

        var response = await service.ProcessAsync(request, TestContext.Current.CancellationToken);

        await using var connection = await OpenAsync();
        var rows = (await connection.QueryAsync<(long Id, string AttributeValue, string Html)>(new CommandDefinition(
            "SELECT id, attribute_value, html FROM elements WHERE id > @before ORDER BY id;",
            new { before }))).ToArray();

        Assert.Equal(response.ElementsCount, rows.Length);
        Assert.Equal(response.ElementsAttrList, rows.Select(row => row.AttributeValue).ToArray());
        Assert.All(rows, row => Assert.NotEmpty(row.Html));
        Assert.All(rows, row => Assert.Contains(row.AttributeValue, row.Html, StringComparison.Ordinal));
    }
}
