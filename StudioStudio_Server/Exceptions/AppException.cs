namespace StudioStudio_Server.Exceptions
{
    public class AppException : Exception
    {
        public string Code { get; }
        public int HttpStatus { get; }

        public AppException(string code, int httpStatus = StatusCodes.Status400BadRequest, Exception? inner = null) : base(code, inner)
        {
            Code = code;
            HttpStatus = httpStatus;
        }
    }
}
