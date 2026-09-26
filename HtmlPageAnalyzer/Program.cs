using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using HtmlPageAnalyzer.Models;
using HtmlPageAnalyzer.Services;
using HtmlPageAnalyzer.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidatorsFromAssemblyContaining<ProcessRequestValidator>();
builder.Services.AddScoped<IElementProcessingService, ElementProcessingService>();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    var defaults = JsonDefaults.CreateSerializerOptions();
    options.JsonSerializerOptions.PropertyNamingPolicy = defaults.PropertyNamingPolicy;
    options.JsonSerializerOptions.WriteIndented = defaults.WriteIndented;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured."),
        name: "postgres");
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = string.Join(
            " ",
            context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .SelectMany(entry => entry.Value!.Errors)
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "Request body is invalid."
                    : error.ErrorMessage));

        return new BadRequestObjectResult(new ProcessResponse
        {
            IsError = 1,
            ErrorCode = "VALIDATION_ERROR",
            ErrorMessage = message
        });
    };
});

var app = builder.Build();

app.UseSwagger(options =>
    options.RouteTemplate = "/api/swagger/{documentName}/swagger.json");
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/api/swagger/v1/swagger.json", "HtmlPageAnalyzer API v1");
    options.RoutePrefix = "api/swagger";
});
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
