namespace StudioStudio_Server.Exceptions
{
    public static class ErrorCodes
    {
        // AUTH
        public const string AuthInvalidCredential = "AUTH001";
        public const string AuthTokenExpired = "AUTH002";
        public const string AuthForbidden = "AUTH003";
        public const string AuthPasswordMismatch = "AUTH004";
        public const string AuthAccountNotVerified = "AUTH005";

        // USER
        public const string UserNotFound = "USER001";
        public const string UserAlreadyExist = "USER002";

        // REPORT
        public const string ReportInvalidRequest = "REPORT001";
        public const string ReportEmailNotConfigured = "REPORT002";

        // TASK
        public const string TaskNotFound = "TASK001";
        public const string TaskPermissionDenied = "TASK002";

        // SUCCESS
        public const string SuccessRegister = "SUCCESS001";
        public const string SuccessLogin = "SUCCESS002";
        public const string SuccessLogout = "SUCCESS003";
        public const string SuccessRefreshToken = "SUCCESS004";
        public const string SuccessReportSent = "SUCCESS005";
        public const string SuccessVerifyEmail = "SUCCESS006";

        // COMMON
        public const string UnexpectedError = "SYS001";
        public const string TooManyRequests = "SYS002";


        //Email
        public const string EmailSendFailed = "EMAIL001";

    }
}
