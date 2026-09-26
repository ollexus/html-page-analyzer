using FluentValidation;
using HtmlPageAnalyzer.Models;

namespace HtmlPageAnalyzer.Validation;

public sealed class ProcessRequestValidator : AbstractValidator<ProcessRequest>
{
    public ProcessRequestValidator()
    {
        RuleFor(request => request.Selector)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("'selector' must be specified and not empty.");

        RuleFor(request => request.Attribute)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("'attribute' must be specified and not empty.");

        RuleFor(request => request.UrlB64)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("'url_b64' must be specified and not empty.");

        RuleFor(request => request.EncryptedTextBytesB64)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("'encrypted_text_bytes_b64' must be specified and not empty.");

        RuleFor(request => request.KeyBytesB64)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("'key_bytes_b64' must be specified and not empty.");

        RuleFor(request => request.PageB64)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("'page_b64' must be specified and not empty.");
    }
}
