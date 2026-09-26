using HtmlPageAnalyzer.Models;

namespace HtmlPageAnalyzer.Services;

public interface IElementProcessingService
{
    Task<ProcessResponse> ProcessAsync(ProcessRequest request, CancellationToken cancellationToken);
}
