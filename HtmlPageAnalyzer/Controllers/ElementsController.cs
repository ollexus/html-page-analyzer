using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using HtmlPageAnalyzer.Models;
using HtmlPageAnalyzer.Services;

namespace HtmlPageAnalyzer.Controllers;

[ApiController]
[Route("api/elements")]
[Produces("application/json")]
public sealed class ElementsController(
    IValidator<ProcessRequest> requestValidator,
    IElementProcessingService processingService) : ControllerBase
{
    [HttpPost("process")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ProcessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProcessResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProcessResponse>> ProcessAsync(
        [FromBody] ProcessRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await requestValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var message = string.Join(
                " ",
                validationResult.Errors.Select(error => error.ErrorMessage));
            return BadRequest(new ProcessResponse
            {
                IsError = 1,
                ErrorCode = "VALIDATION_ERROR",
                ErrorMessage = message
            });
        }

        return Ok(await processingService.ProcessAsync(request, cancellationToken));
    }
}
