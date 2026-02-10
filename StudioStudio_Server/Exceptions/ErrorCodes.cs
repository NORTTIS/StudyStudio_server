namespace StudioStudio_Server.Exceptions
{
    public static class ErrorCodes
    {
        // AUTH
        public const string AuthInvalidCredential = "AUTH001";
        public const string AuthTokenExpired = "AUTH002";
        public const string AuthForbidden = "AUTH003";
        public const string AccountAlreadyVerified = "AUTH004";

        // USER
        public const string UserNotFound = "USER001";
        public const string UserAlreadyExist = "USER002";

        // TASK
        public const string TaskNotFound = "TASK001";
        public const string TaskPermissionDenied = "TASK002";

        // COMMON
        public const string UnexpectedError = "SYS001";
        public const string TooManyRequests = "SYS002";


        //Email
        public const string EmailSendFailed = "EMAIL001";

    }
}
