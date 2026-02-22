using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class SeederService : ISeederService
    {
        private readonly StudioDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ILogger<SeederService> _logger;
        private readonly IConfiguration _configuration;

        public SeederService(
            StudioDbContext context,
            IPasswordHasher<User> passwordHasher,
            ILogger<SeederService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SeedInitialDataAsync()
        {
            try
            {
                await SeedSubscriptionPlansAsync();
                await SeedAdminUserAsync();
                await _context.SaveChangesAsync();

                _logger.LogInformation("Seed data initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during seed data initialization");
                throw;
            }
        }

        private async Task SeedSubscriptionPlansAsync()
        {
            var existingPlans = await _context.SubscriptionPlans.CountAsync();

            if (existingPlans > 0)
            {
                _logger.LogInformation("Subscription plans already exist. Skipping seed.");
                return;
            }

            var freePlanId = Guid.NewGuid();
            var premiumPlanId = Guid.NewGuid();

            var freePlan = new SubscriptionPlan
            {
                PlanId = freePlanId,
                PlanName = "Gói miễn phí",
                Price = 0m,
                BillingCycle = BillingCycle.Free,
                Description = "Phù hợp cho người dùng cá nhân và các nhóm nhỏ trải nghiệm",
                MaxStudios = 3,
                MaxStorageMb = 500,
                MaxAiRequestsPerDay = 20,
                MaxGroups = 5,
                MaxMembersPerGroup = 10,
                IsActive = true
            };

            var premiumPlan = new SubscriptionPlan
            {
                PlanId = premiumPlanId,
                PlanName = "Gói nâng cấp",
                Price = 299000m,
                BillingCycle = BillingCycle.Monthly,
                Description = "Phù hợp cho các nhóm lớn cần sự linh hoạt hơn",
                MaxStudios = 10,
                MaxStorageMb = 1024,
                MaxAiRequestsPerDay = 100,
                MaxGroups = 10,
                MaxMembersPerGroup = 10,
                IsActive = true
            };

            _context.SubscriptionPlans.AddRange(freePlan, premiumPlan);
            _logger.LogInformation("Subscription plans seeded successfully. FreePlanId: {FreePlanId}, PremiumPlanId: {PremiumPlanId}",
                freePlanId, premiumPlanId);
        }

        private async Task SeedAdminUserAsync()
        {
            var adminEmail = _configuration["Admin:Email"] ?? "admin@studystudio.com";
            var adminPassword = _configuration["Admin:Password"];
            var adminFirstName = _configuration["Admin:FirstName"] ?? "Admin";
            var adminLastName = _configuration["Admin:LastName"] ?? "User";

            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                _logger.LogWarning("Admin password not configured in environment variables. Using default password: Admin@123456");
                adminPassword = "Admin@123456";
            }

            var existingAdmin = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

            if (existingAdmin != null)
            {
                _logger.LogInformation("Admin user already exists. Skipping seed. Email: {Email}", adminEmail);
                return;
            }

            var adminUser = new User
            {
                UserId = Guid.NewGuid(),
                Email = adminEmail,
                FirstName = adminFirstName,
                LastName = adminLastName,
                Status = UserStatus.Active,
                IsAdmin = true,
                Language = "en",
                EmailNotificationEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, adminPassword);

            _context.Users.Add(adminUser);
            _logger.LogInformation("Admin user seeded successfully. Email: {Email}, FirstName: {FirstName}, LastName: {LastName}",
                adminEmail, adminFirstName, adminLastName);
        }
    }
}
