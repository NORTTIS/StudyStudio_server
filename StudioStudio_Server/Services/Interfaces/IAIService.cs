using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho AI Question & Answer
    /// Hybrid RAG: Document context + Task statistics
    /// </summary>
    public interface IAIService
    {
        Task<AIAnswerResponse> AskQuestionAsync(
            Guid userId, 
            AIQuestionRequest request, 
            string language = "vi",
            CancellationToken cancellationToken = default);
    }
}
