using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class SeederService(
        StudioDbContext context,
        IPasswordHasher<User> passwordHasher,
        ILogger<SeederService> logger,
        IConfiguration configuration) : ISeederService
    {
        private readonly StudioDbContext _context = context;
        private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
        private readonly ILogger<SeederService> _logger = logger;
        private readonly IConfiguration _configuration = configuration;

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
                PlanName = "Free Plan",
                Price = 0m,
                BillingCycle = BillingCycle.Free,
                Description = "Suitable for individual users and small groups to try out",
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
                PlanName = "Premium Plan",
                Price = 299000m,
                BillingCycle = BillingCycle.Monthly,
                Description = "Suitable for large groups that need more flexibility",
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
                IsVerify = true,
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
