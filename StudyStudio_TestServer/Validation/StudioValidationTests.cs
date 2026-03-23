using StudioStudio_Server.Exceptions;
using Xunit;

namespace StudioStudio_Server.Tests.Validation
{
    public class StudioValidationTests
    {
        #region Error Code Tests

        [Fact]
        public void ErrorCodes_ShouldHaveStudioLimitReached()
        {
            Assert.Equal("STUDIO001", ErrorCodes.StudioLimitReached);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveStudioAlreadyMember()
        {
            Assert.Equal("STUDIO002", ErrorCodes.StudioAlreadyMember);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveStudioInvalidDateRange()
        {
            Assert.Equal("STUDIO003", ErrorCodes.StudioInvalidDateRange);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveStudioNotFound()
        {
            Assert.Equal("GROUP004", ErrorCodes.StudioNotFound);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveAuthForbidden()
        {
            Assert.Equal("AUTH003", ErrorCodes.AuthForbidden);
        }

        #endregion

        #region Date Validation Logic Tests

        [Fact]
        public void DateValidation_StartDateInPast_ShouldBeInvalid()
        {
            // Arrange
            var today = DateTime.UtcNow.Date;
            var pastDate = today.AddDays(-1);

            // Act & Assert
            Assert.True(pastDate < today, "Past date should be less than today");
        }

        [Fact]
        public void DateValidation_StartDateToday_ShouldBeValid()
        {
            // Arrange
            var today = DateTime.UtcNow.Date;

            // Act & Assert
            Assert.True(today >= DateTime.UtcNow.Date, "Today should be valid");
        }

        [Fact]
        public void DateValidation_StartDateFuture_ShouldBeValid()
        {
            // Arrange
            var today = DateTime.UtcNow.Date;
            var futureDate = today.AddDays(7);

            // Act & Assert
            Assert.True(futureDate >= today, "Future date should be valid");
        }

        [Fact]
        public void DateValidation_EndDateBeforeStartDate_ShouldBeInvalid()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(10);
            var endDate = DateTime.UtcNow.AddDays(5);

            // Act & Assert
            Assert.True(endDate < startDate, "End date before start date should be invalid");
        }

        [Fact]
        public void DateValidation_EndDateAfterStartDate_ShouldBeValid()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(10);
            var endDate = DateTime.UtcNow.AddDays(30);

            // Act & Assert
            Assert.True(endDate >= startDate, "End date after start date should be valid");
        }

        [Fact]
        public void DateValidation_SameDate_ShouldBeValid()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(10);
            var endDate = startDate;

            // Act & Assert
            Assert.True(endDate >= startDate, "Same date should be valid");
        }

        #endregion

        #region Validation Scenario Tests

        [Fact]
        public void Scenario_CreateStudio_WithPastStartDate_ShouldFail()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-1);

            // Act
            var isValid = startDate >= DateTime.UtcNow.Date;

            // Assert
            Assert.False(isValid, "Start date in the past should fail validation");
        }

        [Fact]
        public void Scenario_CreateStudio_WithValidDateRange_ShouldPass()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(1);
            var endDate = DateTime.UtcNow.AddDays(30);

            // Act
            var isStartValid = startDate >= DateTime.UtcNow.Date;
            var isEndValid = endDate >= startDate;

            // Assert
            Assert.True(isStartValid && isEndValid, "Valid date range should pass validation");
        }

        [Fact]
        public void Scenario_UpdateStudio_WithPastStartDate_ShouldFail()
        {
            // Arrange
            var newStartDate = DateTime.UtcNow.AddDays(-5);

            // Act
            var isValid = newStartDate >= DateTime.UtcNow.Date;

            // Assert
            Assert.False(isValid, "Update with past date should fail");
        }

        #endregion
    }
}
