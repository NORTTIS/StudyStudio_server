namespace StudioStudio_Server.Configurations
{
    public static class EmailTemplate
    {
        public static string VerifyLinkEmail(string verifyUrl)
        {
            return $@"
            <html>
            <body style='font-family:Arial'>
                <h2>Verify your account</h2>
                <p>Click the button below to activate your account:</p>
                <a href='{verifyUrl}'
                    style='padding:10px 20px;
                    background:#4CAF50;
                    color:white;
                    text-decoration:none;
                    border-radius:5px'>
                Verify Email
                </a>
                <p>This link will expire in 5 minutes.</p>
            </body>
            </html>";
        }
    }
}
