using StudioStudio_Server.Exceptions;
using Xunit;

namespace StudioStudio_Server.Tests.Services
{
    public class AuthServiceTests
    {
        #region Error Code Tests

        [Fact]
        public void ErrorCodes_ShouldHaveAuthInvalidCredential()
        {
            Assert.Equal("AUTH001", ErrorCodes.AuthInvalidCredential);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveAuthTokenExpired()
        {
            Assert.Equal("AUTH002", ErrorCodes.AuthTokenExpired);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveAuthForbidden()
        {
            Assert.Equal("AUTH003", ErrorCodes.AuthForbidden);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveUserNotFound()
        {
            Assert.Equal("USER001", ErrorCodes.UserNotFound);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveUserAlreadyExist()
        {
            Assert.Equal("USER002", ErrorCodes.UserAlreadyExist);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveValidationInvalidPassword()
        {
            Assert.Equal("VALIDATION002", ErrorCodes.ValidationInvalidPassword);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveValidationInvalidEmail()
        {
            Assert.Equal("VALIDATION001", ErrorCodes.ValidationInvalidEmail);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void Scenario_ValidEmail_ShouldPass()
        {
            // Arrange
            var email = "test@example.com";

            // Act
            var isValid = email.Contains("@") && email.Contains(".");

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void Scenario_InvalidEmail_ShouldFail()
        {
            // Arrange
            var email = "invalid-email";

            // Act
            var isValid = email.Contains("@") && email.Contains(".");

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void Scenario_PasswordStrength_Valid()
        {
            // Arrange
            var password = "Password123!";

            // Act
            var hasUppercase = password.Any(char.IsUpper);
            var hasLowercase = password.Any(char.IsLower);
            var hasDigit = password.Any(char.IsDigit);
            var isValidLength = password.Length >= 8;

            // Assert
            Assert.True(hasUppercase && hasLowercase && hasDigit && isValidLength);
        }

        #endregion
    }
}
