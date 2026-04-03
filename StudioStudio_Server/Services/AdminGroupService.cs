using Microsoft.AspNetCore.Http;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service xử lý các thao tác admin với nhóm
    /// </summary>
    public class AdminGroupService : IAdminGroupService
    {
        private readonly IGroupRepository _groupRepository;

        public AdminGroupService(IGroupRepository groupRepository)
        {
            _groupRepository = groupRepository;
        }

        /// <summary>
        /// Get paginated list of groups with filters for admin
        /// </summary>
        public async Task<AdminGroupListResponse> GetGroupsAsync(GetGroupsRequest request)
        {
            // Get groups with pagination
            var (groups, totalCount) = await _groupRepository.GetGroupsAsync(
                request.SearchTerm,
                request.GroupType,
                request.PageNumber,
                request.PageSize);

            // Get summary
            var summary = await _groupRepository.GetGroupSummaryAsync(request.GroupType);

            // Get additional data for mapping
            var groupIds = groups.Select(g => g.GroupId).ToList();
            var memberCounts = await _groupRepository.GetMemberCountsAsync(groupIds);
            var taskCounts = await _groupRepository.GetTaskCountsAsync(groupIds);
            var lastActivities = await _groupRepository.GetLastActivityAsync(groupIds);
            var studioNames = await _groupRepository.GetStudioNamesAsync(groups.Select(g => g.StudioId).ToList());

            // Map to response DTOs
            var groupList = groups.Select(g => new GroupListItem
            {
                GroupId = g.GroupId,
                GroupName = g.GroupName,
                GroupType = g.StudioId == null ? "Độc lập" : "Thuộc studio",
                StudioName = g.StudioId.HasValue && studioNames.ContainsKey(g.StudioId.Value)
                    ? studioNames[g.StudioId.Value]
                    : null,
                MemberCount = memberCounts.GetValueOrDefault(g.GroupId),
                TaskCount = taskCounts.GetValueOrDefault(g.GroupId),
                CreatedAt = g.CreatedAt,
                LastActivityAt = lastActivities.GetValueOrDefault(g.GroupId),
                IsActive = g.IsActive
            }).ToList();

            return new AdminGroupListResponse
            {
                Summary = summary,
                GroupList = groupList,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount
            };
        }

        /// <summary>
        /// Update group status (activate/inactivate)
        /// Khi active lại: kiểm tra trùng tên với group active khác cùng owner/studio
        /// Nếu trùng → thêm "_restored" vào tên (lặp lại nếu vẫn trùng)
        /// </summary>
        public async Task UpdateGroupStatusAsync(Guid groupId, bool isActive)
        {
            var group = await _groupRepository.GetByIdAdminAsync(groupId);

            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Nếu đang active group đã bị inactive trước đó
            if (isActive)
            {
                var currentName = group.GroupName;
                var candidateName = currentName;

                // Lặp kiểm tra trùng tên, thêm "_restored" cho đến khi không trùng
                while (await _groupRepository.HasActiveGroupWithNameAsync(group.CreatedBy, group.StudioId, candidateName, group.GroupId))
                {
                    candidateName += "_restored";
                }

                group.GroupName = candidateName;
            }

            group.IsActive = isActive;
            group.UpdatedAt = DateTime.UtcNow;

            await _groupRepository.UpdateAsync(group);
        }
    }
}
