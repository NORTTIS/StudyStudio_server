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
        // Register Tool Registry (Singleton)
        services.AddSingleton<IAIToolRegistry, AIToolRegistry>();

        // Register AI Agent (Scoped)
        services.AddScoped<AIAgent>();

        // Register all tools (Scoped) — register both interface and concrete type
        services.AddScoped<GetTasksTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<GetTasksTool>());
        services.AddScoped<GetGroupStatsTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<GetGroupStatsTool>());
        services.AddScoped<GetMembersTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<GetMembersTool>());
        services.AddScoped<GetDeadlinesTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<GetDeadlinesTool>());
        services.AddScoped<SearchDocumentsTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<SearchDocumentsTool>());
        services.AddScoped<SearchStudioDocumentsTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<SearchStudioDocumentsTool>());
        services.AddScoped<GetStudioGroupsTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<GetStudioGroupsTool>());
        services.AddScoped<GetStudioAnalyticsTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<GetStudioAnalyticsTool>());
        services.AddScoped<GetGroupComparisonTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<GetGroupComparisonTool>());
        services.AddScoped<GetStorageUsageTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<GetStorageUsageTool>());
        services.AddScoped<GetMemberPermissionsTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<GetMemberPermissionsTool>());
        services.AddScoped<GetGroupDocumentsTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<GetGroupDocumentsTool>());
        services.AddScoped<GetGroupPerformanceTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<GetGroupPerformanceTool>());
        services.AddScoped<CompareGroupsTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<CompareGroupsTool>());
        services.AddScoped<GetStudioHealthTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<GetStudioHealthTool>());
        services.AddScoped<GetRiskGroupsTool>();
        services.AddScoped<IAITool>(sp => sp.GetRequiredService<GetRiskGroupsTool>());

        return services;
    }

    /// <summary>
    /// Configure AI Tool Registry với tools
    /// </summary>
    public static void ConfigureAITools(this IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<AIToolRegistry>>();
        var toolRegistry = services.GetRequiredService<IAIToolRegistry>();

        // Get all tool types and register them
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
            typeof(GetRiskGroupsTool)
        };

        foreach (var toolType in toolTypes)
        {
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
