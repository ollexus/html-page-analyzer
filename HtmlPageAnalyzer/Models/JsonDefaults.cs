using System.Text.Json;

namespace HtmlPageAnalyzer.Models;

public static class JsonDefaults
{
    public static JsonSerializerOptions CreateSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        };
    }
}
