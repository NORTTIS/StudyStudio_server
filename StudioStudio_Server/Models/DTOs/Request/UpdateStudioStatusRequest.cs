namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UpdateStudioStatusRequest
    {
        /// <summary>
        /// New status: true (Active) or false (Inactive)
        /// </summary>
        public bool IsActive { get; set; }
    }
}
