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
        public const string AuthAccountInactive = "AUTH006";
        public const string AuthIncorrectCurrentPassword = "AUTH007";

        // USER
        public const string UserNotFound = "USER001";
        public const string UserAlreadyExist = "USER002";
        public const string UserAccountAlreadyDeleted = "USER003";

        // REPORT
        public const string ReportInvalidRequest = "REPORT001";
        public const string ReportEmailNotConfigured = "REPORT002";
        public const string ReportNotFound = "REPORT003";

        // TASK
        public const string TaskNotFound = "TASK001";
        public const string TaskPermissionDenied = "TASK002";
        public const string TaskDateTimeError = "TASK003";
        public const string TaskInvalidPriority = "TASK004";
        public const string TaskInvalidSeverity = "TASK005";
        public const string TaskNotPendingDeleted = "TASK006";

        // GROUP
        public const string GroupNotFound = "GROUP001";
        public const string GroupNameAlreadyExists = "GROUP002";
        public const string GroupLimitReached = "GROUP003";
        public const string StudioNotFound = "GROUP004";
        public const string StudioPermissionDenied = "GROUP005";
        public const string GroupPermissionDenied = "GROUP006";
        public const string GroupAlreadyMember = "GROUP007";
        public const string GroupMemberLimitReached = "GROUP008";
        public const string GroupMemberNotFound = "GROUP009";
        public const string GroupCannotRemoveOwner = "GROUP010";
        public const string GroupCannotRemoveSelf = "GROUP011";
        public const string GroupOnlyOneOwner = "GROUP012";
        public const string GroupOnlyOneModerator = "GROUP013";
        public const string GroupCannotChangeOwnRole = "GROUP014";
        public const string GroupUpdatePermissionDenied = "GROUP015";
        public const string GroupAccessDenied = "GROUP016";
        public const string GroupCreateTaskDenied = "GROUP017";
        public const string GroupCreateTaskStatusDenied = "GROUP018";
        public const string GroupDeleteTaskStatusDenied = "GROUP019";
        public const string GroupCreateTaskDeniedMissingStatus = "GROUP020";
        public const string GroupPersonalAlreadyExists = "GROUP021";
        public const string GroupTaskStatusPositionExist = "GROUP022";
        public const string GroupDeleteTaskDenined = "GROUP023";
        public const string GroupRestoreTaskDenined = "GROUP024";
        public const string GroupRestoreTaskFailed = "GROUP025";
        public const string GroupDeleteTaskStatusFailed = "GROUP026";
        public const string GroupStatusNotFound = "GROUP027";

        // STUDIO
        public const string StudioLimitReached = "STUDIO001";
        public const string StudioAlreadyMember = "STUDIO004";

        // STATUS
        public const string StatusNotFound = "STATUS001";
        public const string StatusNameExist = "STATUS002";

        // PERSONAL
        public const string PersonalCreateTaskDeniedMissingStatus = "PERSONAL001";
        public const string PersonalDeleteTaskDenined = "PERSONAL002";

        // FAVOURITE
        public const string FavouriteAlreadyExists = "FAVOURITE001";
        public const string FavouriteNotFound = "FAVOURITE002";
        public const string FavouriteNotMember = "FAVOURITE003";

        // INVITE
        public const string InviteTokenInvalid = "INVITE001";
        public const string InviteTokenExpired = "INVITE002";
        public const string InviteInvalidRole = "INVITE003";
        public const string InviteRateLimitExceeded = "INVITE004";

        // TEMPLATE
        public const string TemplateNotFound = "TEMPLATE001";
        public const string TemplatePermissionDenied = "TEMPLATE002";
        public const string TemplateGroupNotFound = "TEMPLATE003";

        // MESSAGE
        public const string MessageNotFound = "MESSAGE001";
        public const string MessagePermissionDenied = "MESSAGE002";
        public const string MessageParentNotFound = "MESSAGE003";

        // COMMENT
        public const string CommentNotFound = "COMMENT001";
        public const string CommentPermissionDenied = "COMMENT002";
        public const string CommentParentNotFound = "COMMENT003";

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
        public const string ValidationGroupCreationNumber = "VALIDATION011";
        public const string ValidationRateLimitExceeded = "VALIDATION012";
        public const string ValidationStringLength = "VALIDATION013";
        public const string ValidationInvalidRange = "VALIDATION014";
        public const string ValidationInvalidEmailFormat = "VALIDATION015";
        public const string ValidationUserNotInStudio = "VALIDATION016";
        public const string ValidationInvalidRoleValue = "VALIDATION017";
        public const string ValidationFileNoDataRows = "VALIDATION018";
        public const string ValidationFileTooLarge = "VALIDATION019";
        public const string ValidationInvalidFileHeaders = "VALIDATION020";

        // BATCH
        public const string BatchGroupNameNotFound = "BATCH001";
        public const string BatchCannotAssignOwnerRole = "BATCH002";
        public const string BatchRowParseError = "BATCH003";
        public const string BatchStudioNotFound = "BATCH004";
        public const string BatchNotStudioOwner = "BATCH005";
        public const string BatchNoGroupsInStudio = "BATCH006";
        public const string BatchGroupAlreadyHasModerator = "BATCH007";
        public const string BatchDuplicateEmailGroupInFile = "BATCH008";
        public const string BatchGroupMemberLimitExceeded = "BATCH009";
        public const string BatchMemberAlreadyInAnotherGroup = "BATCH010";

        // STORAGE
        public const string StorageQuotaExceeded = "STORAGE001";

        // AI
        public const string AIRateLimitExceeded = "AI001";

        // CONFIGURATION
        public const string ConfigurationMissing = "CONFIG001";

        // EXTERNAL SERVICES
        public const string ExternalServiceError = "EXTERNAL001";

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
        public const string SuccessCreateInvite = "SUCCESS019";
        public const string SuccessAcceptInvite = "SUCCESS020";
        public const string SuccessSendInviteEmail = "SUCCESS021";
        public const string SuccessRemoveMember = "SUCCESS022";
        public const string SuccessAssignRole = "SUCCESS023";
        public const string SuccessUpdateGroup = "SUCCESS024";
        public const string SuccessAddFavourite = "SUCCESS025";
        public const string SuccessRemoveFavourite = "SUCCESS026";
        public const string SuccessCreateStudio = "SUCCESS027";
        public const string SuccessDeleteAccount = "SUCCESS028";
        public const string SuccessUpdateStudio = "SUCCESS029";
        public const string SuccessDeleteStudio = "SUCCESS030";
        public const string SuccessDeleteMessage = "SUCCESS031";
        public const string SuccessSendReply = "SUCCESS032";
        public const string SuccessDeleteComment = "SUCCESS033";
        public const string SuccessCreateTaskStatus = "SUCCESS034";
        public const string SuccessUpdateTaskStatus = "SUCCESS035";
        public const string SuccessDeleteTaskStatus = "SUCCESS036";
        public const string SuccessCreateTask = "SUCCESS037";
        public const string SuccessDeleteTask = "SUCCESS038";
        public const string SuccessRestoreTask = "SUCCESS039";
        public const string SuccessUpdateTask = "SUCCESS040";
        public const string SuccessPaymentCreated = "SUCCESS041";
        public const string SuccessPaymentCancelled = "SUCCESS042";
        public const string SuccessUpdateReport = "SUCCESS043";
        public const string SuccessUpdateSubscriptionPlan = "SUCCESS044";

        // ANNOUNCEMENT
        public const string AnnouncementTagTitle = "ANNOUNCEMENTTAGTITLE";
        public const string AnnouncementTagContent = "ANNOUNCEMENTTAGCONTENT";
        public const string AnnouncementTagTask = "ANNOUNCEMENTTAGTASK";
        public const string AnnouncementNotFound = "ANNOUNCEMENT001";

        // PAYMENT
        public const string PaymentPlanNotFound = "PAYMENT001";
        public const string PaymentCannotPayForFreePlan = "PAYMENT002";
        public const string PaymentNotFound = "PAYMENT003";
        public const string PaymentCannotCancel = "PAYMENT004";
        public const string PaymentWebhookInvalid = "PAYMENT005";
        public const string PaymentPriceInvalid = "PAYMENT006";
        public const string PaymentCantProceed = "PAYMENT007";

        // SUBSCRIPTION PLAN
        public const string SubscriptionPlanNotFound = "SUBSCRIPTION001";

        // REVENUE
        public const string RevenueInvalidDateRange = "REVENUE001";
        public const string RevenueInvalidPeriod = "REVENUE002";
        public const string RevenueInvalidCustomPeriod = "REVENUE003";
        public const string RevenueInvalidLimit = "REVENUE004";

        // USER
        public const string UserInvalidStatus = "USER004";
        public const string UserCannotModifyAdmin = "USER005";

        // SUCCESS
        public const string SuccessUpdateData = "SUCCESS045";
        public const string SuccessBatchAssign = "SUCCESS046";
        public const string SuccessRandomAssign = "SUCCESS047";

        // COMMON
        public const string UnexpectedError = "SYS001";
    }
}
