namespace StudioStudio_Server.Configurations
{
    public enum Language
    {
        Vietnamese,
        English
    }

    public static class EmailTemplate
    {
        #region Email Verification

        public static string VerifyLinkEmail(string verifyUrl, Language language = Language.Vietnamese)
        {
            return language == Language.Vietnamese
                ? VerifyLinkEmailVietnamese(verifyUrl)
                : VerifyLinkEmailEnglish(verifyUrl);
        }

        private static string VerifyLinkEmailEnglish(string verifyUrl)
        {
            return $@"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
            <meta charset=""UTF-8"" />
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
            <title>Verify Your Account</title>
            </head>

            <body style=""margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;"">

            <table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0""
            style=""max-width:620px;margin:40px auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.08);"">

              <!-- HEADER -->
              <tr>
                <td style=""background:linear-gradient(135deg,#FF7043,#FF5722);padding:45px 30px;text-align:center;color:white;"">
      
                  <h1 style=""margin:0;font-size:24px;font-weight:700;"">
                    Verify Your Account
                  </h1>
                </td>
              </tr>

              <!-- BODY -->
              <tr>
                <td style=""padding:40px 35px;color:#333;font-size:15px;line-height:1.7;"">
      
                  <p>Hello,</p>

                  <p>
                    Thank you for registering with <strong>Study Studio</strong>.
                    Please verify your email address to unlock all learning features and connect with friends.
                  </p>

                  <!-- CTA -->
                  <div style=""text-align:center;margin:35px 0;"">
                    <a href=""{verifyUrl}""
                       style=""background:#FF5722;
                              color:#fff;
                              padding:15px 34px;
                              text-decoration:none;
                              font-weight:600;
                              border-radius:40px;
                              display:inline-block;
                              font-size:16px;
                              box-shadow:0 8px 20px rgba(255,87,34,0.45);"">
                       Verify Now
                    </a>
                  </div>

                  <!-- ALT LINK -->
                  <p style=""font-size:14px;color:#666;margin-top:20px;"">
                    Or copy and paste this link into your browser:
                  </p>

                  <p style=""word-break:break-all;
                            background:#fff3e0;
                            padding:12px 14px;
                            border-radius:10px;
                            font-size:13px;
                            color:#FF5722;"">
                    {verifyUrl}
                  </p>

                  <!-- EXPIRE BOX -->
                  <div style=""margin-top:25px;
                              padding:14px 16px;
                              background:#fff8f6;
                              border:1px solid #ffd5c8;
                              border-radius:12px;
                              font-size:14px;"">
                    This link will expire in <strong>15 minutes</strong>.
                  </div>

                  <hr style=""margin:35px 0;border:none;border-top:1px solid #eee;"">

                  <p style=""font-size:13px;color:#888;"">
                    If you did not create this account, please ignore this email.
                  </p>

                </td>
              </tr>

              <!-- FOOTER -->
              <tr>
                <td style=""background:#fafafa;padding:25px;text-align:center;font-size:12px;color:#999;"">
                  © 2026 Study Studio <br/>
                  Made with care for Students
                </td>
              </tr>

            </table>

            </body>
            </html>";
        }

        private static string VerifyLinkEmailVietnamese(string verifyUrl)
        {
            return $@"
            <!DOCTYPE html>
            <html lang=""vi"">
            <head>
            <meta charset=""UTF-8"" />
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
            <title>Xác thực tài khoản</title>
            </head>

            <body style=""margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;"">

            <table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0""
            style=""max-width:620px;margin:40px auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.08);"">

              <!-- HEADER -->
              <tr>
                <td style=""background:linear-gradient(135deg,#FF7043,#FF5722);padding:45px 30px;text-align:center;color:white;"">
      
                  <h1 style=""margin:0;font-size:24px;font-weight:700;"">
                    Xác thực tài khoản của bạn
                  </h1>
                </td>
              </tr>

              <!-- BODY -->
              <tr>
                <td style=""padding:40px 35px;color:#333;font-size:15px;line-height:1.7;"">
      
                  <p>Xin chào,</p>

                  <p>
                    Cảm ơn bạn đã đăng ký tài khoản tại <strong>Study Studio</strong>.
                    Hãy xác thực email để mở khóa đầy đủ tính năng học tập và kết nối bạn bè.
                  </p>

                  <!-- CTA -->
                  <div style=""text-align:center;margin:35px 0;"">
                    <a href=""{verifyUrl}""
                       style=""background:#FF5722;
                              color:#fff;
                              padding:15px 34px;
                              text-decoration:none;
                              font-weight:600;
                              border-radius:40px;
                              display:inline-block;
                              font-size:16px;
                              box-shadow:0 8px 20px rgba(255,87,34,0.45);"">
                       Xác thực ngay
                    </a>
                  </div>

                  <!-- ALT LINK -->
                  <p style=""font-size:14px;color:#666;margin-top:20px;"">
                    Hoặc sao chép liên kết sau vào trình duyệt:
                  </p>

                  <p style=""word-break:break-all;
                            background:#fff3e0;
                            padding:12px 14px;
                            border-radius:10px;
                            font-size:13px;
                            color:#FF5722;"">
                    {verifyUrl}
                  </p>

                  <!-- EXPIRE BOX -->
                  <div style=""margin-top:25px;
                              padding:14px 16px;
                              background:#fff8f6;
                              border:1px solid #ffd5c8;
                              border-radius:12px;
                              font-size:14px;"">
                    Liên kết sẽ hết hạn sau <strong>15 phút</strong>.
                  </div>

                  <hr style=""margin:35px 0;border:none;border-top:1px solid #eee;"">

                  <p style=""font-size:13px;color:#888;"">
                    Nếu bạn không thực hiện đăng ký này, hãy bỏ qua email.
                  </p>

                </td>
              </tr>

              <!-- FOOTER -->
              <tr>
                <td style=""background:#fafafa;padding:25px;text-align:center;font-size:12px;color:#999;"">
                  © 2026 Study Studio <br/>
                  Made with care for Students
                </td>
              </tr>

            </table>

            </body>
            </html>";
        }

        #endregion

        #region Password Reset

        public static string ResetPasswordEmail(string resetURL, Language language = Language.English)
        {
            return language == Language.Vietnamese
                ? ResetPasswordEmailVietnamese(resetURL)
                : ResetPasswordEmailEnglish(resetURL);
        }

        private static string ResetPasswordEmailEnglish(string resetURL)
        {
            return $@"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
            <meta charset=""UTF-8"" />
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
            <title>Reset Your Password</title>
            </head>

            <body style=""margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;"">

            <table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0""
            style=""max-width:620px;margin:40px auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.08);"">

              <!-- HEADER -->
              <tr>
                <td style=""background:linear-gradient(135deg,#FF7043,#FF5722);padding:45px 30px;text-align:center;color:white;"">
                  <h1 style=""margin:0;font-size:24px;font-weight:700;"">
                    Reset Your Password
                  </h1>
                  <p style=""margin-top:10px;font-size:14px;opacity:0.95;"">
                    Secure your Study Studio account
                  </p>
                </td>
              </tr>

              <!-- BODY -->
              <tr>
                <td style=""padding:40px 35px;color:#333;font-size:15px;line-height:1.7;"">
      
                  <p>Hello,</p>

                  <p>
                    We received a request to reset your password for your 
                    <strong>Study Studio</strong> account.
                  </p>

                  <p>
                    Click the button below to create a new password:
                  </p>

                  <!-- CTA -->
                  <div style=""text-align:center;margin:35px 0;"">
                    <a href=""{resetURL}""
                       style=""background:#FF5722;
                              color:#fff;
                              padding:15px 34px;
                              text-decoration:none;
                              font-weight:600;
                              border-radius:40px;
                              display:inline-block;
                              font-size:16px;
                              box-shadow:0 8px 20px rgba(255,87,34,0.45);"">
                       Reset Password
                    </a>
                  </div>

                  <!-- ALT LINK -->
                  <p style=""font-size:14px;color:#666;margin-top:20px;"">
                    Or copy and paste this link into your browser:
                  </p>

                  <p style=""word-break:break-all;
                            background:#fff3e0;
                            padding:12px 14px;
                            border-radius:10px;
                            font-size:13px;
                            color:#FF5722;"">
                    {resetURL}
                  </p>

                  <!-- EXPIRY BOX -->
                  <div style=""margin-top:25px;
                              padding:14px 16px;
                              background:#fff8f6;
                              border:1px solid #ffd5c8;
                              border-radius:12px;
                              font-size:14px;"">
                    This link will expire in <strong>15 minutes</strong>.
                  </div>

                  <!-- SECURITY WARNING -->
                  <div style=""margin-top:20px;
                              padding:14px 16px;
                              background:#fff4e5;
                              border-radius:10px;
                              font-size:14px;"">
                    <strong>Security Notice:</strong><br/>
                    If you did not request a password reset, please ignore this email 
                    or contact our support team immediately.
                  </div>

                  <hr style=""margin:35px 0;border:none;border-top:1px solid #eee;"">

                  <p style=""font-size:12px;color:#999;"">
                    This is an automated message. Please do not reply to this email.
                  </p>

                </td>
              </tr>

              <!-- FOOTER -->
              <tr>
                <td style=""background:#fafafa;padding:25px;text-align:center;font-size:12px;color:#999;"">
                  © 2026 Study Studio <br/>
                  Made with care for Students
                </td>
              </tr>

            </table>

            </body>
            </html>";
        }

        private static string ResetPasswordEmailVietnamese(string resetURL)
        {
            return $@"
            <!DOCTYPE html>
            <html lang=""vi"">
            <head>
            <meta charset=""UTF-8"" />
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
            <title>Đặt lại mật khẩu</title>
            </head>

            <body style=""margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;"">

            <table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0""
            style=""max-width:620px;margin:40px auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.08);"">

              <!-- HEADER -->
              <tr>
                <td style=""background:linear-gradient(135deg,#FF7043,#FF5722);padding:45px 30px;text-align:center;color:white;"">
                  <h1 style=""margin:0;font-size:24px;font-weight:700;"">
                    Đặt lại mật khẩu
                  </h1>
                  <p style=""margin-top:10px;font-size:14px;opacity:0.95;"">
                    Bảo mật tài khoản Study Studio của bạn
                  </p>
                </td>
              </tr>

              <!-- BODY -->
              <tr>
                <td style=""padding:40px 35px;color:#333;font-size:15px;line-height:1.7;"">
      
                  <p>Xin chào,</p>

                  <p>
                    Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản 
                    <strong>Study Studio</strong> của bạn.
                  </p>

                  <p>
                    Nhấn vào nút bên dưới để tạo mật khẩu mới:
                  </p>

                  <!-- CTA -->
                  <div style=""text-align:center;margin:35px 0;"">
                    <a href=""{resetURL}""
                       style=""background:#FF5722;
                              color:#fff;
                              padding:15px 34px;
                              text-decoration:none;
                              font-weight:600;
                              border-radius:40px;
                              display:inline-block;
                              font-size:16px;
                              box-shadow:0 8px 20px rgba(255,87,34,0.45);"">
                       Đặt lại mật khẩu
                    </a>
                  </div>

                  <!-- ALT LINK -->
                  <p style=""font-size:14px;color:#666;margin-top:20px;"">
                    Hoặc sao chép và dán liên kết sau vào trình duyệt:
                  </p>

                  <p style=""word-break:break-all;
                            background:#fff3e0;
                            padding:12px 14px;
                            border-radius:10px;
                            font-size:13px;
                            color:#FF5722;"">
                    {resetURL}
                  </p>

                  <!-- EXPIRY BOX -->
                  <div style=""margin-top:25px;
                              padding:14px 16px;
                              background:#fff8f6;
                              border:1px solid #ffd5c8;
                              border-radius:12px;
                              font-size:14px;"">
                    Liên kết này sẽ hết hạn sau <strong>15 phút</strong>.
                  </div>

                  <!-- SECURITY WARNING -->
                  <div style=""margin-top:20px;
                              padding:14px 16px;
                              background:#fff4e5;
                              border-radius:10px;
                              font-size:14px;"">
                    <strong>Lưu ý bảo mật:</strong><br/>
                    Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này 
                    hoặc liên hệ bộ phận hỗ trợ ngay lập tức.
                  </div>

                  <hr style=""margin:35px 0;border:none;border-top:1px solid #eee;"">

                  <p style=""font-size:12px;color:#999;"">
                    Đây là email tự động. Vui lòng không trả lời email này.
                  </p>

                </td>
              </tr>

              <!-- FOOTER -->
              <tr>
                <td style=""background:#fafafa;padding:25px;text-align:center;font-size:12px;color:#999;"">
                  © 2026 Study Studio <br/>
                  Made with care for Students
                </td>
              </tr>

            </table>

            </body>
            </html>";
        }

        #endregion

        #region Group Invite

        public static string GroupInviteEmail(
            string inviteUrl,
            string inviterName,
            string groupName,
            string role,
            string? groupDescription = null,
            Language language = Language.English)
        {
            return language == Language.Vietnamese
                ? GroupInviteEmailVietnamese(inviteUrl, inviterName, groupName, role, groupDescription)
                : GroupInviteEmailEnglish(inviteUrl, inviterName, groupName, role, groupDescription);
        }

        private static string GroupInviteEmailEnglish(
    string inviteUrl,
    string inviterName,
    string groupName,
    string role,
    string? groupDescription = null)
        {
            return $@"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
            <meta charset=""UTF-8"" />
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
            <title>Group Invitation</title>
            </head>

            <body style=""margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;"">

            <table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0""
            style=""max-width:620px;margin:40px auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.08);"">

              <!-- HEADER -->
              <tr>
                <td style=""background:linear-gradient(135deg,#FF7043,#FF5722);padding:45px 30px;text-align:center;color:white;"">
                  <h1 style=""margin:0;font-size:24px;font-weight:700;"">
                    Group Invitation
                  </h1>
                  <p style=""margin-top:10px;font-size:14px;opacity:0.95;"">
                    Join a Study Studio learning group
                  </p>
                </td>
              </tr>

              <!-- BODY -->
              <tr>
                <td style=""padding:40px 35px;color:#333;font-size:15px;line-height:1.7;"">
      
                  <p>Hello,</p>

                  <p>
                    <strong>{inviterName}</strong> has invited you to join 
                    <strong>{groupName}</strong>
                  </p>

                  <!-- ROLE BADGE -->
                  <div style=""margin:15px 0;"">
                    <span style=""display:inline-block;
                                 background:#fff3e0;
                                 color:#FF5722;
                                 padding:6px 16px;
                                 border-radius:30px;
                                 font-weight:600;
                                 font-size:13px;"">
                        Role: {role}
                    </span>
                  </div>

                  {(!string.IsNullOrEmpty(groupDescription) ? $@"
                  <!-- DESCRIPTION -->
                  <div style=""margin-top:20px;
                              padding:16px;
                              background:#fff8f6;
                              border-radius:12px;
                              font-size:14px;
                              color:#555;"">
                      {groupDescription}
                  </div>
                  " : "")}

                  <p style=""margin-top:25px;"">
                    Click the button below to accept the invitation and start collaborating:
                  </p>

                  <!-- CTA -->
                  <div style=""text-align:center;margin:35px 0;"">
                    <a href=""{inviteUrl}""
                       style=""background:#FF5722;
                              color:#fff;
                              padding:15px 34px;
                              text-decoration:none;
                              font-weight:600;
                              border-radius:40px;
                              display:inline-block;
                              font-size:16px;
                              box-shadow:0 8px 20px rgba(255,87,34,0.45);"">
                       Accept Invitation
                    </a>
                  </div>

                  <!-- ALT LINK -->
                  <p style=""font-size:14px;color:#666;margin-top:20px;"">
                    Or copy and paste this link into your browser:
                  </p>

                  <p style=""word-break:break-all;
                            background:#fff3e0;
                            padding:12px 14px;
                            border-radius:10px;
                            font-size:13px;
                            color:#FF5722;"">
                    {inviteUrl}
                  </p>

                  <!-- EXPIRY -->
                  <div style=""margin-top:25px;
                              padding:14px 16px;
                              background:#fff8f6;
                              border:1px solid #ffd5c8;
                              border-radius:12px;
                              font-size:14px;"">
                    This invitation will expire in <strong>15 minutes</strong>.
                  </div>

                  <hr style=""margin:35px 0;border:none;border-top:1px solid #eee;"">

                  <p style=""font-size:12px;color:#999;"">
                    If you were not expecting this invitation, you can safely ignore this email.
                  </p>

                </td>
              </tr>

              <!-- FOOTER -->
              <tr>
                <td style=""background:#fafafa;padding:25px;text-align:center;font-size:12px;color:#999;"">
                  © 2026 Study Studio <br/>
                  Made with care for Students
                </td>
              </tr>

            </table>

            </body>
            </html>";
        }

        private static string GroupInviteEmailVietnamese(
     string inviteUrl,
     string inviterName,
     string groupName,
     string role,
     string? groupDescription = null)
        {
            return $@"
            <!DOCTYPE html>
            <html lang=""vi"">
            <head>
            <meta charset=""UTF-8"" />
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
            <title>Lời mời tham gia nhóm</title>
            </head>

            <body style=""margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;"">

            <table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0""
            style=""max-width:620px;margin:40px auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.08);"">

              <!-- HEADER -->
              <tr>
                <td style=""background:linear-gradient(135deg,#FF7043,#FF5722);padding:45px 30px;text-align:center;color:white;"">
                  <h1 style=""margin:0;font-size:24px;font-weight:700;"">
                    Lời mời tham gia nhóm
                  </h1>
                  <p style=""margin-top:10px;font-size:14px;opacity:0.95;"">
                    Tham gia cộng đồng học tập cùng Study Studio
                  </p>
                </td>
              </tr>

              <!-- BODY -->
              <tr>
                <td style=""padding:40px 35px;color:#333;font-size:15px;line-height:1.7;"">
      
                  <p>Xin chào,</p>

                  <p>
                    <strong>{inviterName}</strong> đã mời bạn tham gia nhóm 
                    <strong>{groupName}</strong>
                  </p>

                  <!-- ROLE BADGE -->
                  <div style=""margin:15px 0;"">
                    <span style=""display:inline-block;
                                 background:#fff3e0;
                                 color:#FF5722;
                                 padding:6px 16px;
                                 border-radius:30px;
                                 font-weight:600;
                                 font-size:13px;"">
                        Vai trò: {role}
                    </span>
                  </div>

                  {(!string.IsNullOrEmpty(groupDescription) ? $@"
                  <!-- DESCRIPTION -->
                  <div style=""margin-top:20px;
                              padding:16px;
                              background:#fff8f6;
                              border-radius:12px;
                              font-size:14px;
                              color:#555;"">
                      {groupDescription}
                  </div>
                  " : "")}

                  <p style=""margin-top:25px;"">
                    Nhấn vào nút bên dưới để chấp nhận lời mời và bắt đầu học tập cùng nhau:
                  </p>

                  <!-- CTA -->
                  <div style=""text-align:center;margin:35px 0;"">
                    <a href=""{inviteUrl}""
                       style=""background:#FF5722;
                              color:#fff;
                              padding:15px 34px;
                              text-decoration:none;
                              font-weight:600;
                              border-radius:40px;
                              display:inline-block;
                              font-size:16px;
                              box-shadow:0 8px 20px rgba(255,87,34,0.45);"">
                       Chấp nhận lời mời
                    </a>
                  </div>

                  <!-- ALT LINK -->
                  <p style=""font-size:14px;color:#666;margin-top:20px;"">
                    Hoặc sao chép và dán liên kết sau vào trình duyệt:
                  </p>

                  <p style=""word-break:break-all;
                            background:#fff3e0;
                            padding:12px 14px;
                            border-radius:10px;
                            font-size:13px;
                            color:#FF5722;"">
                    {inviteUrl}
                  </p>

                  <!-- EXPIRY -->
                  <div style=""margin-top:25px;
                              padding:14px 16px;
                              background:#fff8f6;
                              border:1px solid #ffd5c8;
                              border-radius:12px;
                              font-size:14px;"">
                    Lời mời này sẽ hết hạn sau <strong>15 phút</strong>.
                  </div>

                  <hr style=""margin:35px 0;border:none;border-top:1px solid #eee;"">

                  <p style=""font-size:12px;color:#999;"">
                    Nếu bạn không mong đợi lời mời này, bạn có thể bỏ qua email.
                  </p>

                </td>
              </tr>

              <!-- FOOTER -->
              <tr>
                <td style=""background:#fafafa;padding:25px;text-align:center;font-size:12px;color:#999;"">
                  © 2026 Study Studio <br/>
                  Made with care for Students
                </td>
              </tr>

            </table>

            </body>
            </html>";
        }

        #endregion

        #region Studio Invite

        public static string StudioInviteEmail(
            string inviteUrl,
            string inviterName,
            string studioName,
            string role,
            string? studioDescription = null,
            Language language = Language.Vietnamese)
        {
            return language == Language.Vietnamese
                ? StudioInviteEmailVietnamese(inviteUrl, inviterName, studioName, role, studioDescription)
                : StudioInviteEmailEnglish(inviteUrl, inviterName, studioName, role, studioDescription);
        }

        private static string StudioInviteEmailEnglish(
            string inviteUrl,
            string inviterName,
            string studioName,
            string role,
            string? studioDescription = null)
        {
            return $@"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
            <meta charset=""UTF-8"" />
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
            <title>Invitation to join Studio</title>
            </head>

            <body style=""margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;"">

            <table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0""
            style=""max-width:620px;margin:40px auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.08);"">

              <!-- HEADER -->
              <tr>
                <td style=""background:linear-gradient(135deg,#7E57C2,#5E35B1);padding:45px 30px;text-align:center;color:white;"">
                  <h1 style=""margin:0;font-size:24px;font-weight:700;"">
                    Invitation to join Studio
                  </h1>
                  <p style=""margin-top:10px;font-size:14px;opacity:0.95;"">
                    Join your collaborative study space on Study Studio
                  </p>
                </td>
              </tr>

              <!-- BODY -->
              <tr>
                <td style=""padding:40px 35px;color:#333;font-size:15px;line-height:1.7;"">

                  <p>Hello,</p>

                  <p>
                    <strong>{inviterName}</strong> has invited you to join the studio
                    <strong>{studioName}</strong>
                  </p>

                  <!-- ROLE BADGE -->
                  <div style=""margin:15px 0;"">
                    <span style=""display:inline-block;
                                 background:#ede7f6;
                                 color:#5E35B1;
                                 padding:6px 16px;
                                 border-radius:30px;
                                 font-weight:600;
                                 font-size:13px;"">
                        Role: {role}
                    </span>
                  </div>

                  {(!string.IsNullOrEmpty(studioDescription) ? $@"
                  <!-- DESCRIPTION -->
                  <div style=""margin-top:20px;
                              padding:16px;
                              background:#f5f3fa;
                              border-radius:12px;
                              font-size:14px;
                              color:#555;"">
                      {studioDescription}
                  </div>
                  " : "")}

                  <p style=""margin-top:25px;"">
                    Click the button below to accept the invitation and start collaborating:
                  </p>

                  <!-- CTA -->
                  <div style=""text-align:center;margin:35px 0;"">
                    <a href=""{inviteUrl}""
                       style=""background:#5E35B1;
                              color:#fff;
                              padding:15px 34px;
                              text-decoration:none;
                              font-weight:600;
                              border-radius:40px;
                              display:inline-block;
                              font-size:16px;
                              box-shadow:0 8px 20px rgba(94,53,177,0.45);"">
                       Accept Invitation
                    </a>
                  </div>

                  <!-- ALT LINK -->
                  <p style=""font-size:14px;color:#666;margin-top:20px;"">
                    Or copy and paste the link below into your browser:
                  </p>

                  <p style=""word-break:break-all;
                            background:#ede7f6;
                            padding:12px 14px;
                            border-radius:10px;
                            font-size:13px;
                            color:#5E35B1;"">
                    {inviteUrl}
                  </p>

                  <!-- EXPIRY -->
                  <div style=""margin-top:25px;
                              padding:14px 16px;
                              background:#f5f3fa;
                              border:1px solid #d1c4e9;
                              border-radius:12px;
                              font-size:14px;"">
                    This invitation will expire in <strong>15 minutes</strong>.
                  </div>

                  <hr style=""margin:35px 0;border:none;border-top:1px solid #eee;"">

                  <p style=""font-size:12px;color:#999;"">
                    If you didn't expect this invitation, you can ignore this email.
                  </p>

                </td>
              </tr>

              <!-- FOOTER -->
              <tr>
                <td style=""background:#fafafa;padding:25px;text-align:center;font-size:12px;color:#999;"">
                  © 2026 Study Studio <br/>
                  Made with care for Students
                </td>
              </tr>

            </table>

            </body>
            </html>";
        }

        private static string StudioInviteEmailVietnamese(
            string inviteUrl,
            string inviterName,
            string studioName,
            string role,
            string? studioDescription = null)
        {
            return $@"
            <!DOCTYPE html>
            <html lang=""vi"">
            <head>
            <meta charset=""UTF-8"" />
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
            <title>Lời mời tham gia Studio</title>
            </head>

            <body style=""margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;"">

            <table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0""
            style=""max-width:620px;margin:40px auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.08);"">

              <!-- HEADER -->
              <tr>
                <td style=""background:linear-gradient(135deg,#7E57C2,#5E35B1);padding:45px 30px;text-align:center;color:white;"">
                  <h1 style=""margin:0;font-size:24px;font-weight:700;"">
                    Lời mời tham gia Studio
                  </h1>
                  <p style=""margin-top:10px;font-size:14px;opacity:0.95;"">
                    Tham gia không gian học tập cộng tác trên Study Studio
                  </p>
                </td>
              </tr>

              <!-- BODY -->
              <tr>
                <td style=""padding:40px 35px;color:#333;font-size:15px;line-height:1.7;"">

                  <p>Xin chào,</p>

                  <p>
                    <strong>{inviterName}</strong> đã mời bạn tham gia studio
                    <strong>{studioName}</strong>
                  </p>

                  <!-- ROLE BADGE -->
                  <div style=""margin:15px 0;"">
                    <span style=""display:inline-block;
                                 background:#ede7f6;
                                 color:#5E35B1;
                                 padding:6px 16px;
                                 border-radius:30px;
                                 font-weight:600;
                                 font-size:13px;"">
                        Vai trò: {role}
                    </span>
                  </div>

                  {(!string.IsNullOrEmpty(studioDescription) ? $@"
                  <!-- DESCRIPTION -->
                  <div style=""margin-top:20px;
                              padding:16px;
                              background:#f5f3fa;
                              border-radius:12px;
                              font-size:14px;
                              color:#555;"">
                      {studioDescription}
                  </div>
                  " : "")}

                  <p style=""margin-top:25px;"">
                    Nhấn vào nút bên dưới để chấp nhận lời mời và bắt đầu cộng tác:
                  </p>

                  <!-- CTA -->
                  <div style=""text-align:center;margin:35px 0;"">
                    <a href=""{inviteUrl}""
                       style=""background:#5E35B1;
                              color:#fff;
                              padding:15px 34px;
                              text-decoration:none;
                              font-weight:600;
                              border-radius:40px;
                              display:inline-block;
                              font-size:16px;
                              box-shadow:0 8px 20px rgba(94,53,177,0.45);"">
                       Chấp nhận lời mời
                    </a>
                  </div>

                  <!-- ALT LINK -->
                  <p style=""font-size:14px;color:#666;margin-top:20px;"">
                    Hoặc sao chép và dán liên kết sau vào trình duyệt:
                  </p>

                  <p style=""word-break:break-all;
                            background:#ede7f6;
                            padding:12px 14px;
                            border-radius:10px;
                            font-size:13px;
                            color:#5E35B1;"">
                    {inviteUrl}
                  </p>

                  <!-- EXPIRY -->
                  <div style=""margin-top:25px;
                              padding:14px 16px;
                              background:#f5f3fa;
                              border:1px solid #d1c4e9;
                              border-radius:12px;
                              font-size:14px;"">
                    Lời mời này sẽ hết hạn sau <strong>15 phút</strong>.
                  </div>

                  <hr style=""margin:35px 0;border:none;border-top:1px solid #eee;"">

                  <p style=""font-size:12px;color:#999;"">
                    Nếu bạn không mong đợi lời mời này, bạn có thể bỏ qua email.
                  </p>

                </td>
              </tr>

              <!-- FOOTER -->
              <tr>
                <td style=""background:#fafafa;padding:25px;text-align:center;font-size:12px;color:#999;"">
                  © 2026 Study Studio <br/>
                  Made with care for Students
                </td>
              </tr>

            </table>

            </body>
            </html>";
        }

        #endregion

        #region Report

        public static string ReportEmail(
            string reportType,
            string title,
            string email,
            string content,
            string userId,
            Language language = Language.English)
        {
            return language == Language.Vietnamese
                ? ReportEmailVietnamese(reportType, title, email, content, userId)
                : ReportEmailEnglish(reportType, title, email, content, userId);
        }

        private static string ReportEmailEnglish(
    string reportType,
    string title,
    string email,
    string content,
    string userId)
        {
            return $@"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
            <meta charset=""UTF-8"" />
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
            <title>New Report Notification</title>
            </head>

            <body style=""margin:0;padding:0;background:#f4f6f8;font-family:Arial,Helvetica,sans-serif;"">

            <table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0""
            style=""max-width:650px;margin:40px auto;background:#ffffff;border-radius:18px;overflow:hidden;box-shadow:0 8px 24px rgba(0,0,0,0.08);"">

              <!-- HEADER -->
              <tr>
                <td style=""background:linear-gradient(135deg,#FF7043,#FF5722);
                           padding:35px 30px;
                           text-align:center;
                           color:white;"">
                  <h2 style=""margin:0;font-size:20px;font-weight:700;"">
                    New Report Submitted
                  </h2>
                  <p style=""margin-top:8px;font-size:13px;opacity:0.95;"">
                    Study Studio Report System
                  </p>
                </td>
              </tr>

              <!-- BODY -->
              <tr>
                <td style=""padding:35px 30px;color:#333;font-size:14px;line-height:1.7;"">

                  <p style=""margin-top:0;"">
                    A new report has been submitted with the following details:
                  </p>

                  <!-- REPORT SUMMARY BOX -->
                  <div style=""background:#fff8f6;
                              border:1px solid #ffd5c8;
                              border-radius:12px;
                              padding:18px;
                              margin:20px 0;"">

                    <!-- Report Type -->
                    <div style=""margin-bottom:15px;"">
                      <div style=""font-size:12px;font-weight:700;color:#FF5722;text-transform:uppercase;"">
                        Report Type
                      </div>
                      <div style=""margin-top:6px;font-size:14px;"">
                        {reportType}
                      </div>
                    </div>

                    <!-- Title -->
                    <div style=""margin-bottom:15px;"">
                      <div style=""font-size:12px;font-weight:700;color:#FF5722;text-transform:uppercase;"">
                        Title
                      </div>
                      <div style=""margin-top:6px;font-size:14px;font-weight:600;"">
                        {title}
                      </div>
                    </div>

                    <!-- Reporter Email -->
                    <div style=""margin-bottom:15px;"">
                      <div style=""font-size:12px;font-weight:700;color:#FF5722;text-transform:uppercase;"">
                        Reporter Email
                      </div>
                      <div style=""margin-top:6px;font-size:14px;"">
                        <a href=""mailto:{email}"" style=""color:#FF5722;text-decoration:none;"">{email}</a>
                      </div>
                    </div>

                    <!-- User ID -->
                    <div style=""margin-bottom:15px;"">
                      <div style=""font-size:12px;font-weight:700;color:#FF5722;text-transform:uppercase;"">
                        User ID
                      </div>
                      <div style=""margin-top:6px;font-size:13px;color:#666;"">
                        {userId}
                      </div>
                    </div>

                    <!-- Content -->
                    <div>
                      <div style=""font-size:12px;font-weight:700;color:#FF5722;text-transform:uppercase;"">
                        Report Content
                      </div>
                      <div style=""margin-top:8px;
                                  background:#ffffff;
                                  padding:14px;
                                  border-radius:10px;
                                  border:1px solid #eee;
                                  white-space:pre-wrap;"">
                        {content}
                      </div>
                    </div>

                  </div>

                  <hr style=""margin:30px 0;border:none;border-top:1px solid #eee;""/>

                  <p style=""font-size:12px;color:#999;"">
                    This is an automated notification from Study Studio's reporting system.
                  </p>

                </td>
              </tr>

              <!-- FOOTER -->
              <tr>
                <td style=""background:#fafafa;padding:20px;text-align:center;font-size:12px;color:#999;"">
                  © 2026 Study Studio <br/>
                  Internal System Notification
                </td>
              </tr>

            </table>

            </body>
            </html>";
        }

        private static string ReportEmailVietnamese(
     string reportType,
     string title,
     string email,
     string content,
     string userId)
        {
            return $@"
            <!DOCTYPE html>
            <html lang=""vi"">
            <head>
            <meta charset=""UTF-8"" />
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
            <title>Thông báo báo cáo mới</title>
            </head>

            <body style=""margin:0;padding:0;background:#f4f6f8;font-family:Arial,Helvetica,sans-serif;"">

            <table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0""
            style=""max-width:650px;margin:40px auto;background:#ffffff;border-radius:18px;overflow:hidden;box-shadow:0 8px 24px rgba(0,0,0,0.08);"">

              <!-- HEADER -->
              <tr>
                <td style=""background:linear-gradient(135deg,#FF7043,#FF5722);
                           padding:35px 30px;
                           text-align:center;
                           color:white;"">
                  <h2 style=""margin:0;font-size:20px;font-weight:700;"">
                    Có báo cáo mới được gửi
                  </h2>
                  <p style=""margin-top:8px;font-size:13px;opacity:0.95;"">
                    Hệ thống báo cáo - Study Studio
                  </p>
                </td>
              </tr>

              <!-- BODY -->
              <tr>
                <td style=""padding:35px 30px;color:#333;font-size:14px;line-height:1.7;"">

                  <p style=""margin-top:0;"">
                    Một báo cáo mới đã được gửi với thông tin chi tiết như sau:
                  </p>

                  <!-- REPORT SUMMARY BOX -->
                  <div style=""background:#fff8f6;
                              border:1px solid #ffd5c8;
                              border-radius:12px;
                              padding:18px;
                              margin:20px 0;"">

                    <!-- Loại báo cáo -->
                    <div style=""margin-bottom:15px;"">
                      <div style=""font-size:12px;font-weight:700;color:#FF5722;text-transform:uppercase;"">
                        Loại báo cáo
                      </div>
                      <div style=""margin-top:6px;font-size:14px;"">
                        {reportType}
                      </div>
                    </div>

                    <!-- Tiêu đề -->
                    <div style=""margin-bottom:15px;"">
                      <div style=""font-size:12px;font-weight:700;color:#FF5722;text-transform:uppercase;"">
                        Tiêu đề
                      </div>
                      <div style=""margin-top:6px;font-size:14px;font-weight:600;"">
                        {title}
                      </div>
                    </div>

                    <!-- Email người gửi -->
                    <div style=""margin-bottom:15px;"">
                      <div style=""font-size:12px;font-weight:700;color:#FF5722;text-transform:uppercase;"">
                        Email người gửi
                      </div>
                      <div style=""margin-top:6px;font-size:14px;"">
                        <a href=""mailto:{email}"" style=""color:#FF5722;text-decoration:none;"">{email}</a>
                      </div>
                    </div>

                    <!-- User ID -->
                    <div style=""margin-bottom:15px;"">
                      <div style=""font-size:12px;font-weight:700;color:#FF5722;text-transform:uppercase;"">
                        User ID
                      </div>
                      <div style=""margin-top:6px;font-size:13px;color:#666;"">
                        {userId}
                      </div>
                    </div>

                    <!-- Nội dung báo cáo -->
                    <div>
                      <div style=""font-size:12px;font-weight:700;color:#FF5722;text-transform:uppercase;"">
                        Nội dung báo cáo
                      </div>
                      <div style=""margin-top:8px;
                                  background:#ffffff;
                                  padding:14px;
                                  border-radius:10px;
                                  border:1px solid #eee;
                                  white-space:pre-wrap;"">
                        {content}
                      </div>
                    </div>

                  </div>

                  <hr style=""margin:30px 0;border:none;border-top:1px solid #eee;""/>

                  <p style=""font-size:12px;color:#999;"">
                    Đây là email thông báo tự động từ hệ thống báo cáo của Study Studio.
                  </p>

                </td>
              </tr>

              <!-- FOOTER -->
              <tr>
                <td style=""background:#fafafa;padding:20px;text-align:center;font-size:12px;color:#999;"">
                  © 2026 Study Studio <br/>
                  Thông báo nội bộ hệ thống
                </td>
              </tr>

            </table>

            </body>
            </html>";
        }

        #endregion

        #region Payment Success

        public static string PaymentSuccessEmail(
            string userName,
            string planName,
            decimal amount,
            DateTime paidAt,
            Language language = Language.English)
        {
            return language == Language.Vietnamese
                ? PaymentSuccessEmailVietnamese(userName, planName, amount, paidAt)
                : PaymentSuccessEmailEnglish(userName, planName, amount, paidAt);
        }

        private static string PaymentSuccessEmailEnglish(
            string userName,
            string planName,
            decimal amount,
            DateTime paidAt)
        {
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>Payment Successful</title>
</head>

<body style=""margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;"">

<table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0""
style=""max-width:620px;margin:40px auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.08);"">

  <!-- HEADER -->
  <tr>
    <td style=""background:linear-gradient(135deg,#4CAF50,#2E7D32);padding:45px 30px;text-align:center;color:white;"">
      <h1 style=""margin:0;font-size:24px;font-weight:700;"">
        Payment Successful
      </h1>
      <p style=""margin-top:10px;font-size:14px;opacity:0.95;"">
        Thank you for your subscription
      </p>
    </td>
  </tr>

  <!-- BODY -->
  <tr>
    <td style=""padding:40px 35px;color:#333;font-size:15px;line-height:1.7;"">

      <p>Hello <strong>{userName}</strong>,</p>

      <p>
        Thank you for your payment! Your subscription has been successfully activated.
      </p>

      <!-- PAYMENT DETAILS -->
      <div style=""margin-top:25px;
                  padding:20px;
                  background:#f8fdf4;
                  border:1px solid #c8e6c9;
                  border-radius:12px;"">

        <div style=""margin-bottom:12px;"">
          <div style=""font-size:12px;font-weight:700;color:#2E7D32;text-transform:uppercase;"">
            Plan
          </div>
          <div style=""margin-top:4px;font-size:16px;font-weight:600;"">
            {planName}
          </div>
        </div>

        <div style=""margin-bottom:12px;"">
          <div style=""font-size:12px;font-weight:700;color:#2E7D32;text-transform:uppercase;"">
            Amount Paid
          </div>
          <div style=""margin-top:4px;font-size:16px;font-weight:600;"">
            {amount:N0} VND
          </div>
        </div>

        <div>
          <div style=""font-size:12px;font-weight:700;color:#2E7D32;text-transform:uppercase;"">
            Payment Date
          </div>
          <div style=""margin-top:4px;font-size:14px;color:#555;"">
            {paidAt:MMMM dd, yyyy HH:mm} (UTC)
          </div>
        </div>

      </div>

      <p style=""margin-top:25px;"">
        You can now enjoy all the premium features of Study Studio.
        If you have any questions, please don't hesitate to contact our support team.
      </p>

      <hr style=""margin:35px 0;border:none;border-top:1px solid #eee;"">

      <p style=""font-size:12px;color:#999;"">
        This is an automated message. Please do not reply to this email.
      </p>

    </td>
  </tr>

  <!-- FOOTER -->
  <tr>
    <td style=""background:#fafafa;padding:25px;text-align:center;font-size:12px;color:#999;"">
      © 2026 Study Studio <br/>
      Made with care for Students
    </td>
  </tr>

</table>

</body>
</html>";
        }

        private static string PaymentSuccessEmailVietnamese(
            string userName,
            string planName,
            decimal amount,
            DateTime paidAt)
        {
            return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>Thanh toán thành công</title>
</head>

<body style=""margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;"">

<table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0""
style=""max-width:620px;margin:40px auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.08);"">

  <!-- HEADER -->
  <tr>
    <td style=""background:linear-gradient(135deg,#4CAF50,#2E7D32);padding:45px 30px;text-align:center;color:white;"">
      <h1 style=""margin:0;font-size:24px;font-weight:700;"">
        Thanh toán thành công
      </h1>
      <p style=""margin-top:10px;font-size:14px;opacity:0.95;"">
        Cảm ơn bạn đã đăng ký dịch vụ
      </p>
    </td>
  </tr>

  <!-- BODY -->
  <tr>
    <td style=""padding:40px 35px;color:#333;font-size:15px;line-height:1.7;"">

      <p>Xin chào <strong>{userName}</strong>,</p>

      <p>
        Cảm ơn bạn đã thanh toán! Gói dịch vụ của bạn đã được kích hoạt thành công.
      </p>

      <!-- PAYMENT DETAILS -->
      <div style=""margin-top:25px;
                  padding:20px;
                  background:#f8fdf4;
                  border:1px solid #c8e6c9;
                  border-radius:12px;"">

        <div style=""margin-bottom:12px;"">
          <div style=""font-size:12px;font-weight:700;color:#2E7D32;text-transform:uppercase;"">
            Gói dịch vụ
          </div>
          <div style=""margin-top:4px;font-size:16px;font-weight:600;"">
            {planName}
          </div>
        </div>

        <div style=""margin-bottom:12px;"">
          <div style=""font-size:12px;font-weight:700;color:#2E7D32;text-transform:uppercase;"">
            Số tiền đã thanh toán
          </div>
          <div style=""margin-top:4px;font-size:16px;font-weight:600;"">
            {amount:N0} VND
          </div>
        </div>

        <div>
          <div style=""font-size:12px;font-weight:700;color:#2E7D32;text-transform:uppercase;"">
            Ngày thanh toán
          </div>
          <div style=""margin-top:4px;font-size:14px;color:#555;"">
            {paidAt:dd MMMM yyyy, HH:mm} (UTC)
          </div>
        </div>

      </div>

      <p style=""margin-top:25px;"">
        Bây giờ bạn có thể tận hưởng tất cả các tính năng cao cấp của Study Studio.
        Nếu có bất kỳ câu hỏi nào, vui lòng liên hệ với đội ngũ hỗ trợ của chúng tôi.
      </p>

      <hr style=""margin:35px 0;border:none;border-top:1px solid #eee;"">

      <p style=""font-size:12px;color:#999;"">
        Đây là email tự động. Vui lòng không trả lời email này.
      </p>

    </td>
  </tr>

  <!-- FOOTER -->
  <tr>
    <td style=""background:#fafafa;padding:25px;text-align:center;font-size:12px;color:#999;"">
      © 2026 Study Studio <br/>
      Made with care for Students
    </td>
  </tr>

</table>

</body>
</html>";
        }

        #endregion

        #region Payment Failed

        public static string PaymentFailedEmail(
            string userName,
            string planName,
            decimal amount,
            string reason,
            Language language = Language.English)
        {
            return language == Language.Vietnamese
                ? PaymentFailedEmailVietnamese(userName, planName, amount, reason)
                : PaymentFailedEmailEnglish(userName, planName, amount, reason);
        }

        private static string PaymentFailedEmailEnglish(
            string userName,
            string planName,
            decimal amount,
            string reason)
        {
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>Payment Failed</title>
</head>

<body style=""margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;"">

<table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0""
style=""max-width:620px;margin:40px auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.08);"">

  <!-- HEADER -->
  <tr>
    <td style=""background:linear-gradient(135deg,#f44336,#c62828);padding:45px 30px;text-align:center;color:white;"">
      <h1 style=""margin:0;font-size:24px;font-weight:700;"">
        Payment Failed
      </h1>
      <p style=""margin-top:10px;font-size:14px;opacity:0.95;"">
        Something went wrong with your payment
      </p>
    </td>
  </tr>

  <!-- BODY -->
  <tr>
    <td style=""padding:40px 35px;color:#333;font-size:15px;line-height:1.7;"">

      <p>Hello <strong>{userName}</strong>,</p>

      <p>
        We're sorry, but your payment could not be completed. Please try again or use a different payment method.
      </p>

      <!-- PAYMENT DETAILS -->
      <div style=""margin-top:25px;
                  padding:20px;
                  background:#fff5f5;
                  border:1px solid #ffcdd2;
                  border-radius:12px;"">

        <div style=""margin-bottom:12px;"">
          <div style=""font-size:12px;font-weight:700;color:#c62828;text-transform:uppercase;"">
            Plan
          </div>
          <div style=""margin-top:4px;font-size:16px;font-weight:600;"">
            {planName}
          </div>
        </div>

        <div style=""margin-bottom:12px;"">
          <div style=""font-size:12px;font-weight:700;color:#c62828;text-transform:uppercase;"">
            Amount
          </div>
          <div style=""margin-top:4px;font-size:16px;font-weight:600;"">
            {amount:N0} VND
          </div>
        </div>

        <div>
          <div style=""font-size:12px;font-weight:700;color:#c62828;text-transform:uppercase;"">
            Reason
          </div>
          <div style=""margin-top:4px;font-size:14px;color:#555;"">
            {reason}
          </div>
        </div>

      </div>

      <p style=""margin-top:25px;"">
        Please try again or contact our support team if you need assistance.
      </p>

      <hr style=""margin:35px 0;border:none;border-top:1px solid #eee;"">

      <p style=""font-size:12px;color:#999;"">
        This is an automated message. Please do not reply to this email.
      </p>

    </td>
  </tr>

  <!-- FOOTER -->
  <tr>
    <td style=""background:#fafafa;padding:25px;text-align:center;font-size:12px;color:#999;"">
      © 2026 Study Studio <br/>
      Made with care for Students
    </td>
  </tr>

</table>

</body>
</html>";
        }

        private static string PaymentFailedEmailVietnamese(
            string userName,
            string planName,
            decimal amount,
            string reason)
        {
            return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>Thanh toán thất bại</title>
</head>

<body style=""margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;"">

<table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0""
style=""max-width:620px;margin:40px auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.08);"">

  <!-- HEADER -->
  <tr>
    <td style=""background:linear-gradient(135deg,#f44336,#c62828);padding:45px 30px;text-align:center;color:white;"">
      <h1 style=""margin:0;font-size:24px;font-weight:700;"">
        Thanh toán thất bại
      </h1>
      <p style=""margin-top:10px;font-size:14px;opacity:0.95;"">
        Đã xảy ra lỗi với thanh toán của bạn
      </p>
    </td>
  </tr>

  <!-- BODY -->
  <tr>
    <td style=""padding:40px 35px;color:#333;font-size:15px;line-height:1.7;"">

      <p>Xin chào <strong>{userName}</strong>,</p>

      <p>
        Rất tiếc, thanh toán của bạn không thể hoàn tất. Vui lòng thử lại hoặc sử dụng phương thức thanh toán khác.
      </p>

      <!-- PAYMENT DETAILS -->
      <div style=""margin-top:25px;
                  padding:20px;
                  background:#fff5f5;
                  border:1px solid #ffcdd2;
                  border-radius:12px;"">

        <div style=""margin-bottom:12px;"">
          <div style=""font-size:12px;font-weight:700;color:#c62828;text-transform:uppercase;"">
            Gói dịch vụ
          </div>
          <div style=""margin-top:4px;font-size:16px;font-weight:600;"">
            {planName}
          </div>
        </div>

        <div style=""margin-bottom:12px;"">
          <div style=""font-size:12px;font-weight:700;color:#c62828;text-transform:uppercase;"">
            Số tiền
          </div>
          <div style=""margin-top:4px;font-size:16px;font-weight:600;"">
            {amount:N0} VND
          </div>
        </div>

        <div>
          <div style=""font-size:12px;font-weight:700;color:#c62828;text-transform:uppercase;"">
            Lý do
          </div>
          <div style=""margin-top:4px;font-size:14px;color:#555;"">
            {reason}
          </div>
        </div>

      </div>

      <p style=""margin-top:25px;"">
        Vui lòng thử lại hoặc liên hệ đội ngũ hỗ trợ nếu bạn cần giúp đỡ.
      </p>

      <hr style=""margin:35px 0;border:none;border-top:1px solid #eee;"">

      <p style=""font-size:12px;color:#999;"">
        Đây là email tự động. Vui lòng không trả lời email này.
      </p>

    </td>
  </tr>

  <!-- FOOTER -->
  <tr>
    <td style=""background:#fafafa;padding:25px;text-align:center;font-size:12px;color:#999;"">
      © 2026 Study Studio <br/>
      Made with care for Students
    </td>
  </tr>

</table>

</body>
</html>";
        }

        #endregion

        #region Task Notifications

        public static string TaskAssignedEmail(string taskTitle, string assignerName, DateTime? deadline, string taskUrl, Language language = Language.Vietnamese)
        {
            var title = language == Language.Vietnamese ? "Bạn được giao task mới" : "You have been assigned a new task";
            var detail = language == Language.Vietnamese
                ? $"<p><strong>{assignerName}</strong> đã giao cho bạn task: <strong>{taskTitle}</strong>.</p><p>Deadline: <strong>{(deadline.HasValue ? deadline.Value.ToString("dd/MM/yyyy HH:mm") : "N/A")}</strong></p>"
                : $"<p><strong>{assignerName}</strong> assigned you a task: <strong>{taskTitle}</strong>.</p><p>Deadline: <strong>{(deadline.HasValue ? deadline.Value.ToString("MM/dd/yyyy HH:mm") : "N/A")}</strong></p>";

            return BuildTaskNotificationEmail(title, detail, taskUrl, language);
        }

        public static string TaskReassignedEmail(string taskTitle, string fromName, string toName, string taskUrl, Language language = Language.Vietnamese)
        {
            var title = language == Language.Vietnamese ? "Task đã được chuyển người phụ trách" : "Task has been reassigned";
            var detail = language == Language.Vietnamese
                ? $"<p>Task <strong>{taskTitle}</strong> đã được chuyển từ <strong>{fromName}</strong> sang <strong>{toName}</strong>.</p>"
                : $"<p>Task <strong>{taskTitle}</strong> was reassigned from <strong>{fromName}</strong> to <strong>{toName}</strong>.</p>";

            return BuildTaskNotificationEmail(title, detail, taskUrl, language);
        }

        public static string TaskStatusChangedEmail(string taskTitle, string oldStatus, string newStatus, string changedBy, string taskUrl, Language language = Language.Vietnamese)
        {
            var title = language == Language.Vietnamese ? "Trạng thái task đã thay đổi" : "Task status changed";
            var detail = language == Language.Vietnamese
                ? $"<p><strong>{changedBy}</strong> đã đổi trạng thái task <strong>{taskTitle}</strong>: <strong>{oldStatus}</strong> → <strong>{newStatus}</strong>.</p>"
                : $"<p><strong>{changedBy}</strong> changed task <strong>{taskTitle}</strong> status: <strong>{oldStatus}</strong> → <strong>{newStatus}</strong>.</p>";

            return BuildTaskNotificationEmail(title, detail, taskUrl, language);
        }

        public static string TaskCompletedEmail(string taskTitle, string completedBy, string taskUrl, Language language = Language.Vietnamese)
        {
            var title = language == Language.Vietnamese ? "Task đã hoàn thành" : "Task completed";
            var detail = language == Language.Vietnamese
                ? $"<p><strong>{completedBy}</strong> đã hoàn thành task: <strong>{taskTitle}</strong>.</p>"
                : $"<p><strong>{completedBy}</strong> completed task: <strong>{taskTitle}</strong>.</p>";

            return BuildTaskNotificationEmail(title, detail, taskUrl, language);
        }

        public static string MentionedInCommentEmail(string taskTitle, string mentionerName, string commentPreview, string taskUrl, Language language = Language.Vietnamese)
        {
            var title = language == Language.Vietnamese ? "Bạn được nhắc trong comment" : "You were mentioned in a comment";
            var detail = language == Language.Vietnamese
                ? $"<p><strong>{mentionerName}</strong> đã nhắc bạn trong task <strong>{taskTitle}</strong>.</p><p>Nội dung: " +
                  $"<em>{commentPreview}</em></p>"
                : $"<p><strong>{mentionerName}</strong> mentioned you in task <strong>{taskTitle}</strong>.</p><p>Comment: <em>{commentPreview}</em></p>";

            return BuildTaskNotificationEmail(title, detail, taskUrl, language);
        }

        public static string MentionedInGroupDiscussEmail(string groupName, string mentionerName, string messagePreview, string discussUrl, Language language = Language.Vietnamese)
        {
            var title = language == Language.Vietnamese ? "Bạn được nhắc trong thảo luận nhóm" : "You were mentioned in group discussion";
            var detail = language == Language.Vietnamese
                ? $"<p><strong>{mentionerName}</strong> đã nhắc bạn trong nhóm <strong>{groupName}</strong>.</p><p>Nội dung: <em>{messagePreview}</em></p>"
                : $"<p><strong>{mentionerName}</strong> mentioned you in group <strong>{groupName}</strong>.</p><p>Message: <em>{messagePreview}</em></p>";

            return BuildTaskNotificationEmail(title, detail, discussUrl, language);
        }

        public static string TaskDeletedEmail(string taskTitle, string deletedBy, Language language = Language.Vietnamese)
        {
            var title = language == Language.Vietnamese ? "Task đã bị xóa" : "Task deleted";
            var detail = language == Language.Vietnamese
                ? $"<p><strong>{deletedBy}</strong> đã xóa task: <strong>{taskTitle}</strong>.</p>"
                : $"<p><strong>{deletedBy}</strong> deleted task: <strong>{taskTitle}</strong>.</p>";

            return BuildTaskNotificationEmail(title, detail, null, language);
        }

        public static string TaskUnassignedEmail(string taskTitle, string unassignedBy, Language language = Language.Vietnamese)
        {
            var title = language == Language.Vietnamese ? "Bạn không còn được giao task" : "You were unassigned from task";
            var detail = language == Language.Vietnamese
                ? $"<p><strong>{unassignedBy}</strong> đã gỡ bạn khỏi task: <strong>{taskTitle}</strong>.</p>"
                : $"<p><strong>{unassignedBy}</strong> removed you from task: <strong>{taskTitle}</strong>.</p>";

            return BuildTaskNotificationEmail(title, detail, null, language);
        }

        public static string TaskOverdueEmail(string taskTitle, DateTime deadline, int overdueDays, string taskUrl, Language language = Language.Vietnamese)
        {
            var title = language == Language.Vietnamese ? "Task đã quá hạn" : "Task is overdue";
            var detail = language == Language.Vietnamese
                ? $"<p>Task <strong>{taskTitle}</strong> đã quá hạn từ <strong>{overdueDays}</strong> ngày.</p><p>Deadline: <strong>{deadline:dd/MM/yyyy HH:mm}</strong></p>"
                : $"<p>Task <strong>{taskTitle}</strong> is overdue by <strong>{overdueDays}</strong> day(s).</p><p>Deadline: <strong>{deadline:MM/dd/yyyy HH:mm}</strong></p>";

            return BuildTaskNotificationEmail(title, detail, taskUrl, language);
        }

        public static string TaskReminderEmail(string taskTitle, DateTime deadline, int hoursUntilDeadline, string taskUrl, Language language = Language.Vietnamese)
        {
            var title = language == Language.Vietnamese ? "Nhắc deadline task" : "Task deadline reminder";
            var detail = language == Language.Vietnamese
                ? $"<p>Task <strong>{taskTitle}</strong> sẽ đến hạn sau khoảng <strong>{hoursUntilDeadline}</strong> giờ.</p><p>Deadline: <strong>{deadline:dd/MM/yyyy HH:mm}</strong></p>"
                : $"<p>Task <strong>{taskTitle}</strong> will be due in about <strong>{hoursUntilDeadline}</strong> hour(s).</p><p>Deadline: <strong>{deadline:MM/dd/yyyy HH:mm}</strong></p>";

            return BuildTaskNotificationEmail(title, detail, taskUrl, language);
        }

        private static string BuildTaskNotificationEmail(string title, string detailHtml, string? actionUrl, Language language)
        {
            var actionText = language == Language.Vietnamese ? "Xem chi tiết" : "View details";
            var fallback = language == Language.Vietnamese ? "Đây là email tự động từ Study Studio." : "This is an automated email from Study Studio.";
            var buttonHtml = string.IsNullOrWhiteSpace(actionUrl)
                ? string.Empty
                : $@"<div style=""text-align:center;margin:28px 0;""><a href=""{actionUrl}"" style=""background:#FF5722;color:#fff;padding:12px 28px;text-decoration:none;border-radius:24px;display:inline-block;font-weight:600;"">{actionText}</a></div>";

            return $@"
<!DOCTYPE html>
<html lang=""{(language == Language.Vietnamese ? "vi" : "en")}"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>{title}</title>
</head>
<body style=""margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;"">
  <table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width:620px;margin:40px auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.08);"">
    <tr>
      <td style=""background:linear-gradient(135deg,#FF7043,#FF5722);padding:38px 30px;text-align:center;color:white;"">
        <h1 style=""margin:0;font-size:24px;font-weight:700;"">{title}</h1>
      </td>
    </tr>
    <tr>
      <td style=""padding:34px 30px;color:#333;font-size:15px;line-height:1.7;"">
        {detailHtml}
        {buttonHtml}
        <hr style=""margin:30px 0;border:none;border-top:1px solid #eee;"" />
        <p style=""font-size:12px;color:#999;"">{fallback}</p>
      </td>
    </tr>
    <tr>
      <td style=""background:#fafafa;padding:22px;text-align:center;font-size:12px;color:#999;"">
        © 2026 Study Studio <br/>
        Made with care for Students
      </td>
    </tr>
  </table>
</body>
</html>";
        }

        #endregion

        #region Member Approved

        public static string MemberApprovedNotification(
          string name, 
          string url, 
          DateTime approvedAt,
          Language language = Language.Vietnamese)
        {
            var title = language == Language.Vietnamese ? "Bạn đã được duyệt" : "Member approved";
            var detail = language == Language.Vietnamese
                ? $"<p>Xin chúc mừng! Bạn đã được là thành viên của <strong>{name}</strong> vào lúc {approvedAt:dd/MM/yyyy HH:mm}.</p>"
                : $"<p>Congratulations! You have been approved as a member of <strong>{name}</strong> at {approvedAt:MM/dd/yyyy HH:mm}.</p>";

            return MemberApprovedNotificationEmail(title, detail, url, language);
        }

        private static string MemberApprovedNotificationEmail(string title, string detailHtml, string? actionUrl, Language language)
        {
          var actionText = language == Language.Vietnamese ? "Xem chi tiết" : "View details";
            var fallback = language == Language.Vietnamese ? "Đây là email tự động từ Study Studio." : "This is an automated email from Study Studio.";
            var buttonHtml = string.IsNullOrWhiteSpace(actionUrl)
                ? string.Empty
                : $@"<div style=""text-align:center;margin:28px 0;""><a href=""{actionUrl}"" style=""background:#FF5722;color:#fff;padding:12px 28px;text-decoration:none;border-radius:24px;display:inline-block;font-weight:600;"">{actionText}</a></div>";

            return $@"
<!DOCTYPE html>
<html lang=""{(language == Language.Vietnamese ? "vi" : "en")}"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>{title}</title>
</head>
<body style=""margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;"">
  <table align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width:620px;margin:40px auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.08);"">
    <tr>
      <td style=""background:linear-gradient(135deg,#FF7043,#FF5722);padding:38px 30px;text-align:center;color:white;"">
        <h1 style=""margin:0;font-size:24px;font-weight:700;"">{title}</h1>
      </td>
    </tr>
    <tr>
      <td style=""padding:34px 30px;color:#333;font-size:15px;line-height:1.7;"">
        {detailHtml}
        {buttonHtml}
        <hr style=""margin:30px 0;border:none;border-top:1px solid #eee;"" />
        <p style=""font-size:12px;color:#999;"">{fallback}</p>
      </td>
    </tr>
    <tr>
      <td style=""background:#fafafa;padding:22px;text-align:center;font-size:12px;color:#999;"">
        © 2026 Study Studio <br/>
        Made with care for Students
      </td>
    </tr>
  </table>
</body>
</html>";
        }
        #endregion
    }
}
