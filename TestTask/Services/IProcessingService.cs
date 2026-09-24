using TestTask.Models;

namespace TestTask.Services;

public interface IProcessingService
{
    Task<ProcessResponseDto> ProcessAsync(ProcessRequestDto request);
}