using StudioStudio_Server.Services.AI;
using StudioStudio_Server.Services.AI.Tools;
using StudioStudio_Server.Services.AI.Tools.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods để đăng ký AI services
/// </summary>
public static class AIServiceExtensions
{
    /// <summary>
    /// Đăng ký AI Tool Calling services
    /// </summary>
    public static IServiceCollection AddAIToolCalling(this IServiceCollection services)
    {
        // Register Tool Registry (Singleton) - lưu tool TYPE để resolve fresh instance
        services.AddSingleton<IAIToolRegistry, AIToolRegistry>();

        // Register AI Agent (Scoped) - đã inject IServiceProvider để resolve tools
        services.AddScoped<AIAgent>();

        // Register all tools (Scoped) - chỉ đăng ký concrete type
        // IAITool resolve sẽ tự động tìm các tool đã đăng ký theo Type
        services.AddScoped<GetTasksTool>();
        services.AddScoped<GetGroupStatsTool>();
        services.AddScoped<GetMembersTool>();
        services.AddScoped<GetDeadlinesTool>();
        services.AddScoped<SearchDocumentsTool>();
        services.AddScoped<SearchStudioDocumentsTool>();
        services.AddScoped<GetStudioGroupsTool>();
        services.AddScoped<GetStudioAnalyticsTool>();
        services.AddScoped<GetGroupComparisonTool>();
        services.AddScoped<GetStorageUsageTool>();
        services.AddScoped<GetMemberPermissionsTool>();
        services.AddScoped<GetGroupDocumentsTool>();
        services.AddScoped<GetGroupPerformanceTool>();
        services.AddScoped<CompareGroupsTool>();
        services.AddScoped<GetStudioHealthTool>();
        services.AddScoped<GetRiskGroupsTool>();
        services.AddScoped<GetGroupRiskTool>();
        services.AddScoped<GetPersonalTasksTool>();
        services.AddScoped<GetPersonalDeadlinesTool>();
        services.AddScoped<GetPersonalStatsTool>();

        return services;
    }

    /// <summary>
    /// Configure AI Tool Registry với tools (chạy sau khi app start)
    /// RegisterTool lưu TYPE (không resolve instance) → tránh DbContext disposed
    /// </summary>
    public static void ConfigureAITools(this IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<AIToolRegistry>>();
        var toolRegistry = services.GetRequiredService<IAIToolRegistry>();

        // Tool types để register
        var toolTypes = new[]
        {
            typeof(GetTasksTool),
            typeof(GetGroupStatsTool),
            typeof(GetMembersTool),
            typeof(GetDeadlinesTool),
            typeof(SearchDocumentsTool),
            typeof(SearchStudioDocumentsTool),
            typeof(GetStudioGroupsTool),
            typeof(GetStudioAnalyticsTool),
            typeof(GetGroupComparisonTool),
            typeof(GetStorageUsageTool),
            typeof(GetMemberPermissionsTool),
            typeof(GetGroupDocumentsTool),
            typeof(GetGroupPerformanceTool),
            typeof(CompareGroupsTool),
            typeof(GetStudioHealthTool),
            typeof(GetRiskGroupsTool),
            typeof(GetGroupRiskTool),
            typeof(GetPersonalTasksTool),
            typeof(GetPersonalDeadlinesTool),
            typeof(GetPersonalStatsTool)
        };

        foreach (var toolType in toolTypes)
        {
            // Resolve instance một lần để lấy metadata (Name, Description)
            // Instance này chỉ dùng cho manifest, không dùng để ExecuteAsync
            using var scope = services.CreateScope();
            var tool = scope.ServiceProvider.GetRequiredService(toolType) as IAITool;
            if (tool != null)
            {
                toolRegistry.RegisterTool(tool);
            }
        }

        logger.LogInformation("AI Tools configured: {Count} tools registered", toolRegistry.GetAllTools().Count);
    }
}
