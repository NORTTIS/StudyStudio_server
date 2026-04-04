namespace StudioStudio_Server.Models.DTOs.Response
{
    public class LoginResponse
    {
        //id of user
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public long AccessExpireIn { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public long RefreshExpireIn { get; set; }
        public bool IsAdmin { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
