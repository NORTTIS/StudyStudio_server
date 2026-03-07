using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Filters;
using StackExchange.Redis;
using StudioStudio_Server.Hubs;
using Microsoft.Extensions.FileProviders;
using StudioStudio_Server.Middlewares;

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
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
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
builder.Services.AddScoped<IFavouriteRepository, FavouriteRepository>();
builder.Services.AddScoped<IStudioRepository, StudioRepository>();
builder.Services.AddScoped<IStudioService, StudioService>();
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

app.MapControllers();

// Map SignalR Hubs
app.MapHub<GroupDiscussHub>("/hubs/group-discuss");
app.MapHub<TaskCommentHub>("/hubs/task-comment");

app.Run();
