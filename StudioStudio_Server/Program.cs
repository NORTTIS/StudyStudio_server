using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PayOS;
using Prometheus;
using StackExchange.Redis;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Data;
using StudioStudio_Server.Filters;
using StudioStudio_Server.HealthChecks;
using StudioStudio_Server.Hubs;
using StudioStudio_Server.Middlewares;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection("Email"));
builder.Services.Configure<BackblazeConfig>(
    builder.Configuration.GetSection("Backblaze"));
builder.Services.Configure<QdrantConfig>(
    builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<GeminiConfig>(
    builder.Configuration.GetSection("Gemini"));

//redis connection
builder.Services.AddSingleton<IConnectionMultiplexer>(r =>
{
    var configuration = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");
    configuration.AllowAdmin = true; // Enable admin commands for health checks
    return ConnectionMultiplexer.Connect(configuration);
});

//payos registration
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new PayOSClient(
       config["PayOS:ClientId"],
       config["PayOS:ApiKey"],
       config["PayOS:ChecksumKey"]
   );
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// ========== HEALTH CHECKS ==========
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<RedisHealthCheck>("redis")
    .AddCheck<ExternalServicesHealthCheck>("external_services");

// ========== CACHE CONFIGURATION ==========
// Choose cache provider: "Memory" (development) or "Redis" (production)
var cacheProvider = builder.Configuration.GetValue<string>("Cache:Provider") ?? "Memory";

if (cacheProvider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
{
    // Redis Cache for Production (using existing IConnectionMultiplexer)
    builder.Services.AddScoped<ICacheService, RedisCacheService>();
    Console.WriteLine("? Using Redis Distributed Cache (StackExchange.Redis)");
}
else
{
    // Memory Cache for Development
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<ICacheService, CacheService>();
    Console.WriteLine("? Using In-Memory Cache");
}

builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IEmailService, SMTPEmailService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IPasswordResetCacheService, PasswordResetCacheService>();
builder.Services.AddScoped<IEmailVerificationCacheService, EmailVerificationCacheService>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<IUserSubscriptionRepository, UserSubscriptionRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IFavouriteRepository, FavouriteRepository>();
builder.Services.AddScoped<IStudioRepository, StudioRepository>();
builder.Services.AddScoped<IStudioService, StudioService>();
builder.Services.AddScoped<IStudioParticipantRepository, StudioParticipantRepository>();
builder.Services.AddScoped<IStudioInviteService, StudioInviteService>();
builder.Services.AddScoped<IGroupParticipantRepository, GroupParticipantRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<ITemplateRepository, TemplateRepository>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IGroupTaskStatusRepository, GroupTaskStatusRepository>();
builder.Services.AddScoped<IPersonalTaskStatusRepository, PersonalTaskStatusRepository>();
builder.Services.AddScoped<ITaskHistoryRepository, TaskHistoryRepository>();
builder.Services.AddScoped<ITaskAssignmentRepository, TaskAssignmentRepository>();
builder.Services.AddScoped<ISeederService, SeederService>();
builder.Services.AddScoped<IGroupInviteService, GroupInviteService>();
builder.Services.AddScoped<IGroupMessageRepository, GroupMessageRepository>();
builder.Services.AddScoped<ITaskCommentRepository, TaskCommentRepository>();
builder.Services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
builder.Services.AddScoped<IUserAnnouccementRepository, UserAnnouncementRepository>();
builder.Services.AddScoped<IUserAnnouncementService, UserAnnouncementService>();
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();
builder.Services.AddScoped<IAdminAnnouncementService, AdminAnnouncementService>();
builder.Services.AddScoped<IGroupMemberService, GroupMemberService>();
builder.Services.AddScoped<IFavouriteService, FavouriteService>();
builder.Services.AddScoped<IGroupMessageService, GroupMessageService>();
builder.Services.AddScoped<ITaskCommentService, TaskCommentService>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IGroupTaskStatusService, GroupTaskStatusService>();
builder.Services.AddScoped<IHomeService, HomeService>();
builder.Services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
builder.Services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
builder.Services.AddScoped<IRevenueService, RevenueService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IAdminGroupService, AdminGroupService>();
builder.Services.AddScoped<IAdminStatisticsRepository, AdminStatisticsRepository>();
builder.Services.AddScoped<IAdminStatisticsService, AdminStatisticsService>();

// AI & Document Services
builder.Services.AddScoped<IFileStorageService, BackblazeStorageService>();
builder.Services.AddScoped<IVectorDatabaseService, QdrantService>();
builder.Services.AddScoped<IEmbeddingService, GeminiEmbeddingService>();
builder.Services.AddScoped<ILLMService, GeminiLLMService>();
builder.Services.AddScoped<IGroupAttachmentRepository, GroupAttachmentRepository>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IAIService, AIService>();
builder.Services.AddScoped<IAIRequestLogRepository, AIRequestLogRepository>();

// Embedding Queue & Background Service
builder.Services.AddSingleton<StudioStudio_Server.Services.EmbeddingQueue.IEmbeddingQueue, StudioStudio_Server.Services.EmbeddingQueue.EmbeddingQueue>();
builder.Services.AddHostedService<StudioStudio_Server.Services.EmbeddingQueue.EmbeddingBackgroundService>();

// Delete Queue & Background Service
builder.Services.AddSingleton<StudioStudio_Server.Services.DeleteQueue.IDeleteQueue, StudioStudio_Server.Services.DeleteQueue.DeleteQueue>();
builder.Services.AddHostedService<StudioStudio_Server.Services.DeleteQueue.DeleteBackgroundService>();

// Refresh Token Cleanup Background Service (runs every 24 hours)
builder.Services.AddHostedService<StudioStudio_Server.Services.BackgroundServices.RefreshTokenCleanupService>();

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidationFilter>();
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

// Configure file upload
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = int.MaxValue; // 5GB
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Services.AddDbContext<StudioDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add SignalR
builder.Services.AddSignalR();

//JWT config
builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["JWT:Issuer"],
        ValidAudience = builder.Configuration["JWT:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["JWT:Key"]))
    };

    // SignalR authentication configuration
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];

            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/hubs/group-discuss") ||
                 path.StartsWithSegments("/hubs/task-comment")))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebAppPolicy", policy =>
    {
        policy.WithOrigins(
            "http://localhost:3000",
            "http://localhost:5006",
            "https://localhost:7070",
            "https://studystudio.asia",
            "https://www.studystudio.asia"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

builder.Services.AddAuthorization();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(s =>
{
    s.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        Description = "Enter bearer: {access token}"
    });

    s.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[]{}
        }
    });
});
var app = builder.Build();

// Ensure upload directories exist
var webRoot = app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var uploadsPath = Path.Combine(webRoot, "uploads", "avatars");
Directory.CreateDirectory(uploadsPath);

// Log the path for debugging
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation($"WebRoot: {webRoot}");
logger.LogInformation($"Uploads Path: {uploadsPath}");
logger.LogInformation($"Directory exists: {Directory.Exists(uploadsPath)}");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
    db.Database.Migrate();

    var seeder = scope.ServiceProvider.GetRequiredService<ISeederService>();
    await seeder.SeedInitialDataAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(app.Environment.WebRootPath, "uploads")),
    RequestPath = "/uploads"
});

app.UseCors("WebAppPolicy");

app.UseAuthentication();

// Add custom middlewares after authentication
app.UseMiddleware<TokenValidationMiddleware>();
app.UseMiddleware<RateLimitMiddleware>();

app.UseAuthorization();

// Health check endpoints - return JSON response
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            results = report.Entries.Select(e => new
            {
                key = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                data = e.Value.Data
            })
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
});

app.MapControllers();

// Map SignalR Hubs
app.MapHub<GroupDiscussHub>("/hubs/group-discuss");
app.MapHub<TaskCommentHub>("/hubs/task-comment");

// Prometheus metrics endpoint
app.MapMetrics();

app.Run();
