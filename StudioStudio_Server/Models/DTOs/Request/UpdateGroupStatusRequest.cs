namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UpdateGroupStatusRequest
    {
        /// <summary>
        /// New status: true (Active) or false (Inactive)
        /// </summary>
        public bool IsActive { get; set; }
    }
}
