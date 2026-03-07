using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IHomeService
    {
        /// <summary>
        /// Get personal task board with status columns and tasks.
        /// </summary>
        Task<PersonalTaskBoardResponse> GetPersonalTaskBoardAsync(Guid userId);

        /// <summary>
        /// Get summary metrics for Home screen.
        /// </summary>
        Task<HomeSummaryResponse> GetHomeSummaryAsync(Guid userId);

        /// <summary>
        /// Get merged Home task list with pagination, search, filter, and sort.
        /// </summary>
        Task<HomeTaskListResponse> GetHomeTaskListAsync(
            Guid userId, 
            int page, 
            int pageSize, 
            string? search = null, 
            Guid? groupId = null, 
            string? sortBy = "duedate_asc");

        /// <summary>
        /// Create a new personal task status column.
        /// </summary>
        Task<PersonalTaskStatusResponse> CreateNewPersonalTaskStatus(Guid userId, PersonalTaskStatusRequest request);

        /// <summary>
        /// Delete a personal task status column.
        /// </summary>
        Task DeletePersonalTaskStatus(Guid userId, Guid taskStatusId);

        /// <summary>
        /// Update details of a personal task status column.
        /// </summary>
        Task UpdatePersonalTaskStatus(Guid userId, Guid taskStatusId, PersonalTaskStatusRequest request);

        /// <summary>
        /// Reorder personal task status columns.
        /// </summary>
        Task ReorderPersonalTaskStatus(Guid userId, ReorderPersonalTaskStatusRequest request);
    }
}
