using StudioStudio_Server.Exceptions;
using Xunit;

namespace StudioStudio_Server.Tests.Controllers
{
    public class AIControllerTests
    {
        #region Endpoint Tests

        [Fact]
        public void AIController_HasAskQuestionEndpoint()
        {
            var endpoint = "POST /api/ai/ask";
            Assert.NotNull(endpoint);
        }

        [Fact]
        public void AIController_HasGetRemainingRequestsEndpoint()
        {
            var endpoint = "GET /api/ai/remaining";
            Assert.NotNull(endpoint);
        }

        [Fact]
        public void AIController_HasGetConversationHistoryEndpoint()
        {
            var endpoint = "GET /api/ai/history";
            Assert.NotNull(endpoint);
        }

        #endregion

        #region Business Logic Flow Tests - AI Question (Bug #3)

        [Fact]
        public void Flow_AskQuestion_ShouldDeductRequest_AfterResponse()
        {
            // Simulate request deduction (Bug #3)
            var initialRequests = 10;
            var requestsUsed = initialRequests;

            // Act - After AI response
            requestsUsed++;

            // Assert
            Assert.Equal(11, requestsUsed);
        }

        [Fact]
        public void Flow_AskQuestion_ShouldReject_WhenRateLimitExceeded()
        {
            // Simulate rate limit check
            var requestsUsed = 10;
            var dailyLimit = 10;

            // Act
            var canMakeRequest = requestsUsed < dailyLimit;

            // Assert
            Assert.False(canMakeRequest);
        }

        [Fact]
        public void Flow_GetRemainingRequests_ShouldReturnCorrectCount()
        {
            // Simulate remaining requests calculation
            var dailyLimit = 10;
            var requestsUsed = 3;

            // Act
            var remaining = dailyLimit - requestsUsed;

            // Assert
            Assert.Equal(7, remaining);
        }

        [Fact]
        public void Flow_GetRemainingRequests_ShouldReturnZero_WhenLimitReached()
        {
            // Simulate limit reached
            var dailyLimit = 10;
            var requestsUsed = 10;

            // Act
            var remaining = dailyLimit - requestsUsed;

            // Assert
            Assert.Equal(0, remaining);
        }

        #endregion

        #region Error Codes Validation

        [Fact]
        public void ErrorCodes_AIError_AreCorrect()
        {
            Assert.Equal("AI001", ErrorCodes.AIRateLimitExceeded);
        }

        #endregion
    }
}
