using FluentValidation;
using TestTask.Models;

namespace TestTask.Validators;

public class ProcessRequestValidator : AbstractValidator<ProcessRequestDto>
{
    public ProcessRequestValidator()
    {
        // Проверка на пустые значения
        RuleFor(x => x.Selector)
            .NotEmpty().WithErrorCode("EMPTY_SELECTOR").WithMessage("Selector cannot be empty");

        RuleFor(x => x.Attribute)
            .NotEmpty().WithErrorCode("EMPTY_ATTRIBUTE").WithMessage("Attribute cannot be empty");

        RuleFor(x => x.UrlB64)
            .NotEmpty().WithErrorCode("MISSING_PARAMETER").WithMessage("UrlB64 is required");

        RuleFor(x => x.PageB64)
            .NotEmpty().WithErrorCode("MISSING_PARAMETER").WithMessage("PageB64 is required");

        RuleFor(x => x.KeyBytesB64)
            .NotEmpty().WithErrorCode("MISSING_PARAMETER").WithMessage("KeyBytesB64 is required");

        RuleFor(x => x.EncryptedTextBytesB64)
            .NotEmpty().WithErrorCode("MISSING_PARAMETER").WithMessage("EncryptedTextBytesB64 is required");
    }
}