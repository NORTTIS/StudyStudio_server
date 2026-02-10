namespace StudioStudio_Server.Models.DTOs.Response
{
    public class ApiResponse<T>
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public T? Data { get; set; }

        public ApiResponse(string status, string message, T? data = default)
        {
            Status = status;
            Message = message;
            Data = data;
        }

        public static ApiResponse<T> Success(string message, T? data = default)
        {
            return new ApiResponse<T>("success", message, data);
        }

        public static ApiResponse<T> Error(string message)
        {
            return new ApiResponse<T>("error", message);
        }
    }
}
