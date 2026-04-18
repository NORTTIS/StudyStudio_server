using Microsoft.EntityFrameworkCore;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Data;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using PaymentStatusEnum = StudioStudio_Server.Models.Enums.PaymentStatus;

namespace StudioStudio_Server.Services
{
    public class PaymentService(
        PayOSClient payOSClient,
        IPaymentRepository paymentRepository,
        IUserSubscriptionRepository subscriptionRepository,
        IUserRepository userRepository,
        IEmailService emailService,
        StudioDbContext db,
        IConfiguration configuration,
        ILogger<PaymentService> logger,
        ICacheService cacheService) : IPaymentService
    {
        private const int PAYOS_CANCEL_TIME = 15;

        public async Task<CreatePaymentResponse> CreatePaymentLinkAsync(Guid userId, CreatePaymentRequest request)
        {
            var plan = await db.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.PlanId == request.PlanId && p.IsActive);

            if (plan == null)
                throw new AppException(ErrorCodes.PaymentPlanNotFound, StatusCodes.Status404NotFound);

            if (plan.BillingCycle == BillingCycle.Free)
                throw new AppException(ErrorCodes.PaymentCannotPayForFreePlan);

            if (plan.Price <= 0)
            {
                throw new AppException(ErrorCodes.PaymentPriceInvalid);
            }

            var currentPlan = await subscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId);
            if (currentPlan != null && currentPlan.BillingCycle == BillingCycle.Monthly)
            {
                throw new AppException(ErrorCodes.PaymentCantProceed);
            }

            await CancelAllPendingPaymentAsync(userId);

            // Generate unique order code using timestamp (max 9999999999999)
            long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 9999999999999;

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                UserId = userId,
                PlanId = plan.PlanId,
                OrderCode = orderCode,
                Amount = plan.Price,
                PaymentStatus = PaymentStatusEnum.PENDING,
                CreatedAt = DateTime.UtcNow
            };

            var baseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            var returnUrl = $"{baseUrl}/payment/success?orderCode={orderCode}";
            var cancelUrl = $"{baseUrl}/payment/cancel?orderCode={orderCode}";

            var createRequest = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = (int)plan.Price,
                Description = $"Premium - {plan.PlanName}",
                ReturnUrl = returnUrl,
                CancelUrl = cancelUrl,
                ExpiredAt = (int)DateTimeOffset.UtcNow.AddMinutes(PAYOS_CANCEL_TIME).ToUnixTimeSeconds(),
                Items =
                [
                    new PaymentLinkItem
                    {
                        Name = plan.PlanName,
                        Quantity = 1,
                        Price = (int)plan.Price
                    }
                ]
            };

            var paymentLink = await payOSClient.PaymentRequests.CreateAsync(createRequest);

            payment.PaymentUrl = paymentLink.CheckoutUrl;
            await paymentRepository.AddAsync(payment);

            return new CreatePaymentResponse
            {
                PaymentId = payment.PaymentId,
                OrderCode = orderCode,
                PaymentUrl = paymentLink.CheckoutUrl,
                Amount = plan.Price,
                PlanName = plan.PlanName,
                ExpiredAt = paymentLink.ExpiredAt.HasValue
                    ? DateTimeOffset.FromUnixTimeSeconds(paymentLink.ExpiredAt.Value)
                    : DateTimeOffset.MinValue
            };
        }

        public async Task HandleWebhookAsync(Webhook webhook)
        {
            WebhookData webhookData = null;

            try
            {
                if (webhook == null)
                    throw new AppException(ErrorCodes.PaymentWebhookInvalid);

                webhookData = await payOSClient.Webhooks.VerifyAsync(webhook);
            }
            catch (Exception ex) when (ex is not AppException)
            {
                logger.LogWarning(ex, "PayOS webhook verification failed");
                throw new AppException(ErrorCodes.PaymentWebhookInvalid);
            }

            var payment = await paymentRepository.GetByOrderCodeAsync(webhookData.OrderCode);
            if (payment == null)
            {
                logger.LogWarning("PayOS webhook received for unknown order code: {OrderCode}", webhookData.OrderCode);
                return;
            }

            if (payment.PaymentStatus != PaymentStatusEnum.PENDING)
                return;

            var user = await userRepository.GetByIdAsync(payment.UserId);
            var plan = await db.SubscriptionPlans.FindAsync(payment.PlanId);
            var language = user?.Language == "vi" ? Language.Vietnamese : Language.English;

            if (webhookData.Code == "00")
            {
                payment.PaymentStatus = PaymentStatusEnum.SUCCESS;
                payment.TransactionId = webhookData.Reference;
                payment.PaidAt = DateTime.UtcNow;
                await paymentRepository.UpdateAsync(payment);

                await ActivateSubscriptionAsync(payment);

                // Send success email
                if (user != null && plan != null)
                {
                    var userDisplayName = !string.IsNullOrEmpty(user.FirstName) || !string.IsNullOrEmpty(user.LastName)
                        ? $"{user.FirstName} {user.LastName}".Trim()
                        : user.Email;

                    var emailBody = EmailTemplate.PaymentSuccessEmail(
                        userDisplayName,
                        plan.PlanName,
                        payment.Amount,
                        payment.PaidAt ?? DateTime.UtcNow,
                        language);

                    try
                    {
                        await emailService.SendLinkAsync(user.Email, "Payment Successful - Study Studio", emailBody);
                        logger.LogInformation("Payment success email sent to {Email}", user.Email);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to send payment success email to {Email}", user.Email);
                    }
                }
            }
            else
            {
                payment.PaymentStatus = PaymentStatusEnum.FAILED;
                await paymentRepository.UpdateAsync(payment);

                // Send failed email
                if (user != null && plan != null)
                {
                    var userDisplayName = !string.IsNullOrEmpty(user.FirstName) || !string.IsNullOrEmpty(user.LastName)
                        ? $"{user.FirstName} {user.LastName}".Trim()
                        : user.Email;

                    var reason = "Payment was not completed. Please try again or use a different payment method.";
                    var emailBody = EmailTemplate.PaymentFailedEmail(
                        userDisplayName,
                        plan.PlanName,
                        payment.Amount,
                        reason,
                        language);

                    try
                    {
                        await emailService.SendLinkAsync(user.Email, "Payment Failed - Study Studio", emailBody);
                        logger.LogInformation("Payment failed email sent to {Email}", user.Email);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to send payment failed email to {Email}", user.Email);
                    }
                }
            }
        }

        public async Task<PaymentStatusResponse> GetPaymentStatusAsync(Guid userId, Guid paymentId)
        {
            var payment = await paymentRepository.GetByPaymentIdAsync(paymentId);

            if (payment == null || payment.UserId != userId)
                throw new AppException(ErrorCodes.PaymentNotFound, StatusCodes.Status404NotFound);

            return MapToStatusResponse(payment);
        }

        public async Task<PaymentStatusResponse> CancelPaymentAsync(Guid userId, long orderCode)
        {
            var payment = await paymentRepository.GetByOrderCodeAsync(orderCode);

            if (payment == null || payment.UserId != userId)
                throw new AppException(ErrorCodes.PaymentNotFound, StatusCodes.Status404NotFound);

            if (payment.PaymentStatus != PaymentStatusEnum.PENDING)
                throw new AppException(ErrorCodes.PaymentCannotCancel);

            await payOSClient.PaymentRequests.CancelAsync(orderCode);

            payment.PaymentStatus = PaymentStatusEnum.CANCELLED;
            await paymentRepository.UpdateAsync(payment);

            return MapToStatusResponse(payment);
        }

        private async Task ActivateSubscriptionAsync(Payment payment)
        {
            await subscriptionRepository.DeactivateActiveSubscriptionsAsync(payment.UserId);

            var plan = await db.SubscriptionPlans.FindAsync(payment.PlanId);
            if (plan == null) return;

            var startDate = DateTime.UtcNow;
            var endDate = plan.BillingCycle == BillingCycle.Monthly
                ? startDate.AddMonths(1)
                : startDate.AddYears(1);

            var subscription = new UserSubscription
            {
                SubscriptionId = Guid.NewGuid(),
                UserId = payment.UserId,
                PlanId = payment.PlanId,
                StartDate = startDate,
                EndDate = endDate,
                IsActive = true
            };

            await subscriptionRepository.AddAsync(subscription);
            
            // ? INVALIDATE USER SUBSCRIPTION CACHE - User purchased new subscription
            await cacheService.InvalidateUserCacheAsync(payment.UserId);
            
            logger.LogInformation("Subscription activated for user {UserId}, plan {PlanId}. Cache invalidated.", payment.UserId, payment.PlanId);
        }

        private static PaymentStatusResponse MapToStatusResponse(Payment payment) => new()
        {
            PaymentId = payment.PaymentId,
            OrderCode = payment.OrderCode,
            PaymentStatus = payment.PaymentStatus,
            Amount = payment.Amount,
            PlanName = payment.Plan?.PlanName ?? string.Empty,
            CreatedAt = payment.CreatedAt,
            PaidAt = payment.PaidAt
        };

        public async Task<PaymentHistoryResponse> GetPaymentHistoryAsync(Guid userId)
        {
            var paymentList = await paymentRepository.GetByUserIdAsync(userId);
            var histories = paymentList.Select(p => new PaymentHistory
            {
                PaymentId = p.PaymentId,
                PlanId = p.PlanId,
                Status = p.PaymentStatus,
                PaidAt = p.PaidAt
            }).ToList();

            return new PaymentHistoryResponse
            {
                PaymentHistories = histories
            };
        }

        private async Task CancelAllPendingPaymentAsync(Guid userId)
        {
            var pendingPayments = await paymentRepository.GetAllPendingByUserIdAsync(userId);

            if (!pendingPayments.Any()) return;

            foreach (var payment in pendingPayments)
            {
                try
                {
                    await payOSClient.PaymentRequests.CancelAsync(
                        payment.OrderCode,
                        "Người dùng tạo đơn thanh toán mới"
                    );
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Could not cancel PayOS payment link for order {OrderCode}",
                        payment.OrderCode);
                }
                payment.PaymentStatus = PaymentStatusEnum.CANCELLED;
                await paymentRepository.UpdateAsync(payment);
            }
        }

        public async Task<BillingHistoryResponse> GetBillingHistoryAsync(GetBillingHistoryRequest request)
        {
            var (items, totalCount) = await paymentRepository.GetBillingHistoryAsync(
                request.SearchTerm,
                request.PaymentStatus,
                request.PageNumber,
                request.PageSize);

            var billingItems = items.Select(p => new BillingHistoryItem
            {
                PaymentId = p.PaymentId,
                OrderCode = p.OrderCode,
                PaymentStatus = p.PaymentStatus,
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod,
                CreatedAt = p.CreatedAt,
                PaidAt = p.PaidAt,
                UserId = p.UserId,
                UserEmail = p.User?.Email ?? string.Empty,
                UserName = !string.IsNullOrEmpty(p.User?.FirstName) || !string.IsNullOrEmpty(p.User?.LastName)
                    ? $"{p.User.FirstName} {p.User.LastName}".Trim()
                    : p.User?.Email ?? string.Empty,
                PlanId = p.PlanId,
                PlanName = p.Plan?.PlanName ?? string.Empty
            }).ToList();

            return new BillingHistoryResponse
            {
                Items = billingItems,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount
            };
        }
    }
}
