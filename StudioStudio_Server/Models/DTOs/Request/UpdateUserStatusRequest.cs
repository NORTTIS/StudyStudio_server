namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UpdateUserStatusRequest
    {
        /// <summary>
        /// New status: "Active" or "Inactive"
        /// </summary>
        public string Status { get; set; } = string.Empty;
    }
}
