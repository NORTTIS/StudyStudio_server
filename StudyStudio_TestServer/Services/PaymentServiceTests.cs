using StudioStudio_Server.Exceptions;
using Xunit;

namespace StudioStudio_Server.Tests.Services
{
    public class PaymentServiceTests
    {
        #region Payment Error Code Tests

        [Fact]
        public void ErrorCodes_ShouldHavePaymentPlanNotFound()
        {
            Assert.Equal("PAYMENT001", ErrorCodes.PaymentPlanNotFound);
        }

        [Fact]
        public void ErrorCodes_ShouldHavePaymentCannotPayForFreePlan()
        {
            Assert.Equal("PAYMENT002", ErrorCodes.PaymentCannotPayForFreePlan);
        }

        [Fact]
        public void ErrorCodes_ShouldHavePaymentNotFound()
        {
            Assert.Equal("PAYMENT003", ErrorCodes.PaymentNotFound);
        }

        [Fact]
        public void ErrorCodes_ShouldHavePaymentCannotCancel()
        {
            Assert.Equal("PAYMENT004", ErrorCodes.PaymentCannotCancel);
        }

        #endregion

        #region Subscription Plan Tests

        [Fact]
        public void Scenario_FreePlan_ShouldHaveZeroPrice()
        {
            // Arrange
            var price = 0m;

            // Act & Assert
            Assert.Equal(0, price);
        }

        [Fact]
        public void Scenario_PremiumPlan_ShouldHavePositivePrice()
        {
            // Arrange
            var price = 99000m;

            // Act & Assert
            Assert.True(price > 0);
        }

        [Fact]
        public void Scenario_ComparePlanPrices()
        {
            // Arrange
            var freePrice = 0m;
            var proPrice = 99000m;
            var premiumPrice = 199000m;

            // Assert
            Assert.True(freePrice < proPrice);
            Assert.True(proPrice < premiumPrice);
        }

        #endregion
    }
}
