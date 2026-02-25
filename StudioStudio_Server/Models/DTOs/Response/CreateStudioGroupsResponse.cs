namespace StudioStudio_Server.Models.DTOs.Response
{
    public class CreateStudioGroupsResponse
    {
        public List<CreateGroupResponse> CreateGroups { get; set; } = new List<CreateGroupResponse>();
    }
}
