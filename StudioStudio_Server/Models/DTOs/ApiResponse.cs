namespace StudioStudio_Server.Models.DTOs
{
    public class ApiResponse
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public object? Data { get; set; }

        public ApiResponse(string status, string message, object? data = null)
        {
            Status = status;
            Message = message;
            Data = data;
        }
    }
}
