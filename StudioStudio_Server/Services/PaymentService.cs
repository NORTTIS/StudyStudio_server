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
    public class PaymentService : IPaymentService
    {
        private readonly PayOSClient _payOSClient;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IUserSubscriptionRepository _subscriptionRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly StudioDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentService> _logger;
        private readonly ICacheService _cacheService;

        public PaymentService(
            PayOSClient payOSClient,
            IPaymentRepository paymentRepository,
            IUserSubscriptionRepository subscriptionRepository,
            IUserRepository userRepository,
            IEmailService emailService,
            StudioDbContext db,
            IConfiguration configuration,
            ILogger<PaymentService> logger,
            ICacheService cacheService)
        {
            _payOSClient = payOSClient;
            _paymentRepository = paymentRepository;
            _subscriptionRepository = subscriptionRepository;
            _userRepository = userRepository;
            _emailService = emailService;
            _db = db;
            _configuration = configuration;
            _logger = logger;
            _cacheService = cacheService;
        }

        public async Task<CreatePaymentResponse> CreatePaymentLinkAsync(Guid userId, CreatePaymentRequest request)
        {
            var plan = await _db.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.PlanId == request.PlanId && p.IsActive);

            if (plan == null)
                throw new AppException(ErrorCodes.PaymentPlanNotFound, StatusCodes.Status404NotFound);

            if (plan.BillingCycle == BillingCycle.Free)
                throw new AppException(ErrorCodes.PaymentCannotPayForFreePlan, StatusCodes.Status400BadRequest);

            if (plan.Price <= 0)
            {
                throw new AppException(ErrorCodes.PaymentPriceInvalid, StatusCodes.Status400BadRequest);
            }
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

            var baseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            var returnUrl = $"{baseUrl}/payment/success?orderCode={orderCode}";
            var cancelUrl = $"{baseUrl}/payment/cancel?orderCode={orderCode}";

            var createRequest = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = (int)plan.Price,
                Description = $"Premium - {plan.PlanName}",
                ReturnUrl = returnUrl,
                CancelUrl = cancelUrl,
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

            var paymentLink = await _payOSClient.PaymentRequests.CreateAsync(createRequest);

            payment.PaymentUrl = paymentLink.CheckoutUrl;
            await _paymentRepository.AddAsync(payment);

            return new CreatePaymentResponse
            {
                PaymentId = payment.PaymentId,
                OrderCode = orderCode,
                PaymentUrl = paymentLink.CheckoutUrl,
                Amount = plan.Price,
                PlanName = plan.PlanName
            };
        }

        public async Task HandleWebhookAsync(Webhook webhook)
        {
            WebhookData webhookData = null;

            try
            {
                if (webhook == null)
                    throw new AppException(ErrorCodes.PaymentWebhookInvalid);

                webhookData = await _payOSClient.Webhooks.VerifyAsync(webhook);
            }
            catch (Exception ex) when (ex is not AppException)
            {
                _logger.LogWarning(ex, "PayOS webhook verification failed");
                throw new AppException(ErrorCodes.PaymentWebhookInvalid);
            }

            var payment = await _paymentRepository.GetByOrderCodeAsync(webhookData.OrderCode);
            if (payment == null)
            {
                _logger.LogWarning("PayOS webhook received for unknown order code: {OrderCode}", webhookData.OrderCode);
                return;
            }

            if (payment.PaymentStatus != PaymentStatusEnum.PENDING)
                return;

            var user = await _userRepository.GetByIdAsync(payment.UserId);
            var plan = await _db.SubscriptionPlans.FindAsync(payment.PlanId);
            var language = user?.Language == "vi" ? Language.Vietnamese : Language.English;

            if (webhookData.Code == "00")
            {
                payment.PaymentStatus = PaymentStatusEnum.SUCCESS;
                payment.TransactionId = webhookData.Reference;
                payment.PaidAt = DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment);

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
                        await _emailService.SendLinkAsync(user.Email, "Payment Successful - Study Studio", emailBody);
                        _logger.LogInformation("Payment success email sent to {Email}", user.Email);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send payment success email to {Email}", user.Email);
                    }
                }
            }
            else
            {
                payment.PaymentStatus = PaymentStatusEnum.FAILED;
                await _paymentRepository.UpdateAsync(payment);

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
                        await _emailService.SendLinkAsync(user.Email, "Payment Failed - Study Studio", emailBody);
                        _logger.LogInformation("Payment failed email sent to {Email}", user.Email);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send payment failed email to {Email}", user.Email);
                    }
                }
            }
        }

        public async Task<PaymentStatusResponse> GetPaymentStatusAsync(Guid userId, Guid paymentId)
        {
            var payment = await _paymentRepository.GetByPaymentIdAsync(paymentId);

            if (payment == null || payment.UserId != userId)
                throw new AppException(ErrorCodes.PaymentNotFound, StatusCodes.Status404NotFound);

            return MapToStatusResponse(payment);
        }

        public async Task<PaymentStatusResponse> CancelPaymentAsync(Guid userId, Guid paymentId)
        {
            var payment = await _paymentRepository.GetByPaymentIdAsync(paymentId);

            if (payment == null || payment.UserId != userId)
                throw new AppException(ErrorCodes.PaymentNotFound, StatusCodes.Status404NotFound);

            if (payment.PaymentStatus != PaymentStatusEnum.PENDING)
                throw new AppException(ErrorCodes.PaymentCannotCancel, StatusCodes.Status400BadRequest);

            await _payOSClient.PaymentRequests.CancelAsync(payment.OrderCode);

            payment.PaymentStatus = PaymentStatusEnum.CANCELLED;
            await _paymentRepository.UpdateAsync(payment);

            return MapToStatusResponse(payment);
        }

        private async Task ActivateSubscriptionAsync(Payment payment)
        {
            await _subscriptionRepository.DeactivateActiveSubscriptionsAsync(payment.UserId);

            var plan = await _db.SubscriptionPlans.FindAsync(payment.PlanId);
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

            await _subscriptionRepository.AddAsync(subscription);
            
            // ? INVALIDATE USER SUBSCRIPTION CACHE - User purchased new subscription
            await _cacheService.InvalidateUserCacheAsync(payment.UserId);
            
            _logger.LogInformation("Subscription activated for user {UserId}, plan {PlanId}. Cache invalidated.", payment.UserId, payment.PlanId);
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
            var paymentList = await _paymentRepository.GetByUserIdAsync(userId);
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

        public async Task<BillingHistoryResponse> GetBillingHistoryAsync(GetBillingHistoryRequest request)
        {
            var (items, totalCount) = await _paymentRepository.GetBillingHistoryAsync(
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
