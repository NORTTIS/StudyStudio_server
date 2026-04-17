namespace StudioStudio_Server.Models.DTOs.Response
{
    public class ApiResponse<T>(string status, string code, string message, T? data = default)
    {
        public string Status { get; set; } = status;
        public string Code { get; set; } = code;
        public string Message { get; set; } = message;
        public T? Data { get; set; } = data;

        public static ApiResponse<T> Success(string code, string message, T? data = default)
        {
            return new ApiResponse<T>("success", code, message, data);
        }

        public static ApiResponse<T> Error(string code, string message, T? data = default)
        {
            return new ApiResponse<T>("error", code, message, data);
        }
    }
}
