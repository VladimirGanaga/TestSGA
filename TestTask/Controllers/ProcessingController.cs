using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using TestTask.Models;
using TestTask.Services;

namespace TestTask.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcessingController : ControllerBase
{
    private readonly IProcessingService _service;
    private readonly IValidator<ProcessRequestDto> _validator;

    public ProcessingController(
        IProcessingService service,
        IValidator<ProcessRequestDto> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpPost]
    public async Task<ActionResult<ProcessResponseDto>> Process(ProcessRequestDto request)
    {
        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Errors.First();
            return Ok(new ProcessResponseDto
            {
                IsError = 1,
                ErrorCode = firstError.ErrorCode,
                ErrorMessage = firstError.ErrorMessage
            });
        }

        var result = await _service.ProcessAsync(request);
        return Ok(result);
    }
}