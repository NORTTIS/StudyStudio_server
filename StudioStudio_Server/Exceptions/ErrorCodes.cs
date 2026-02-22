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
        public const string AuthIncorrectCurrentPassword = "AUTH006";

        // USER
        public const string UserNotFound = "USER001";
        public const string UserAlreadyExist = "USER002";

        // REPORT
        public const string ReportInvalidRequest = "REPORT001";
        public const string ReportEmailNotConfigured = "REPORT002";

        // TASK
        public const string TaskNotFound = "TASK001";
        public const string TaskPermissionDenied = "TASK002";

        // GROUP
        public const string GroupNotFound = "GROUP001";
        public const string GroupNameAlreadyExists = "GROUP002";
        public const string GroupLimitReached = "GROUP003";
        public const string StudioNotFound = "GROUP004";
        public const string StudioPermissionDenied = "GROUP005";
        public const string GroupPermissionDenied = "GROUP006";

        // TEMPLATE
        public const string TemplateNotFound = "TEMPLATE001";
        public const string TemplatePermissionDenied = "TEMPLATE002";
        public const string TemplateGroupNotFound = "TEMPLATE003";

        // VALIDATION
        public const string ValidationInvalidEmail = "VALIDATION001";
        public const string ValidationInvalidPassword = "VALIDATION002";
        public const string ValidationPasswordMismatch = "VALIDATION003";
        public const string ValidationRequiredField = "VALIDATION004";
        public const string ValidationInvalidToken = "VALIDATION005";
        public const string ValidationTokenExpired = "VALIDATION006";
        public const string ValidationEmailAlreadyVerified = "VALIDATION007";
        public const string ValidationFileSizeExceeded = "VALIDATION008";
        public const string ValidationInvalidFileFormat = "VALIDATION009";
        public const string ValidationNewPasswordSameAsCurrent = "VALIDATION010";

        // SUCCESS
        public const string SuccessRegister = "SUCCESS001";
        public const string SuccessLogin = "SUCCESS002";
        public const string SuccessLogout = "SUCCESS003";
        public const string SuccessRefreshToken = "SUCCESS004";
        public const string SuccessReportSent = "SUCCESS005";
        public const string SuccessVerifyEmail = "SUCCESS006";
        public const string SuccessChangePassword = "SUCCESS007";
        public const string SuccessUpdateProfile = "SUCCESS008";
        public const string SuccessSendForgotLink = "SUCCESS009";
        public const string SuccessGetData = "SUCCESS010";
        public const string SuccessResetPassword = "SUCCESS011";
        public const string SuccessResendEmailVerify = "SUCCESS012";
        public const string SuccessGetGroup = "SUCCESS013";
        public const string SuccessCreateGroup = "SUCCESS014";
        public const string SuccessDeleteGroup = "SUCCESS015";
        public const string SuccessCreateTemplate = "SUCCESS016";
        public const string SuccessUpdateTemplate = "SUCCESS017";
        public const string SuccessDeleteTemplate = "SUCCESS018";

        // ANNOUNCEMENT
        public const string AnnouncementNotFound = "ANNOUNCEMENT001";

        // COMMON
        public const string UnexpectedError = "SYS001";
    }
}
