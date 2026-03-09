using Microsoft.EntityFrameworkCore;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using StudioStudio_Server.Data;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly PayOSClient _payOSClient;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IUserSubscriptionRepository _subscriptionRepository;
        private readonly StudioDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            PayOSClient payOSClient,
            IPaymentRepository paymentRepository,
            IUserSubscriptionRepository subscriptionRepository,
            StudioDbContext db,
            IConfiguration configuration,
            ILogger<PaymentService> logger)
        {
            _payOSClient = payOSClient;
            _paymentRepository = paymentRepository;
            _subscriptionRepository = subscriptionRepository;
            _db = db;
            _configuration = configuration;
            _logger = logger;
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
                PaymentStatus = "PENDING",
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

        public async Task HandleWebhookAsync(object webhookBody)
        {
            WebhookData webhookData;

            try
            {
                var webhook = System.Text.Json.JsonSerializer.Deserialize<Webhook>(
                    webhookBody.ToString()!,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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

            if (payment.PaymentStatus != "PENDING")
                return;

            if (webhookData.Code == "00")
            {
                payment.PaymentStatus = "SUCCESS";
                payment.TransactionId = webhookData.Reference;
                payment.PaidAt = DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment);

                await ActivateSubscriptionAsync(payment);
            }
            else
            {
                payment.PaymentStatus = "FAILED";
                await _paymentRepository.UpdateAsync(payment);
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

            if (payment.PaymentStatus != "PENDING")
                throw new AppException(ErrorCodes.PaymentCannotCancel, StatusCodes.Status400BadRequest);

            await _payOSClient.PaymentRequests.CancelAsync(payment.OrderCode);

            payment.PaymentStatus = "CANCELLED";
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
            _logger.LogInformation("Subscription activated for user {UserId}, plan {PlanId}", payment.UserId, payment.PlanId);
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
    }
}
