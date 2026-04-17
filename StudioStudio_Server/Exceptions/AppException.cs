namespace StudioStudio_Server.Exceptions
{
    public class AppException(string code, int httpStatus = StatusCodes.Status400BadRequest, Exception? inner = null) : Exception(code, inner)
    {
        public string Code { get; } = code;
        public int HttpStatus { get; } = httpStatus;
    }
}
