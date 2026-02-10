namespace StudioStudio_Server.Models.DTOs.Response
{
    public class ApiResponse<T>
    {
        public string Status { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public T? Data { get; set; }

        public ApiResponse(string status, string code, string message, T? data = default)
        {
            Status = status;
            Code = code;
            Message = message;
            Data = data;
        }

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
