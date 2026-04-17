using StudioStudio_Server.Services.AI.Models;
using System.Text.Json.Nodes;

namespace StudioStudio_Server.Services.AI.Interfaces
{
    /// <summary>
    /// Interface cho tất cả AI Tools
    /// Mỗi tool đại diện cho một chức năng truy cập database
    /// </summary>
    public interface IAITool
    {
        /// <summary>
        /// Tên tool (dùng trong function calling)
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Mô tả tool cho AI
        /// </summary>
        string Description { get; }

        /// <summary>
        /// JSON Schema cho parameters
        /// </summary>
        JsonObject ParametersSchema { get; }

        /// <summary>
        /// Thực thi tool với parameters
        /// </summary>
        Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate parameters trước khi execute
        /// </summary>
        /// <param name="parameters">Parameters cần validate</param>
        /// <returns>True nếu hợp lệ, false nếu không</returns>
        bool ValidateParameters(JsonObject parameters);
    }

    /// <summary>
    /// Interface cho Tool Registry - quản lý tất cả tools
    /// </summary>
    public interface IAIToolRegistry
    {
        /// <summary>
        /// Lấy tool theo tên (tool instance - dùng cho manifest)
        /// </summary>
        IAITool? GetTool(string name);

        /// <summary>
        /// Lấy Type của tool (để resolve fresh instance trong request scope)
        /// </summary>
        Type? GetToolType(string name);

        /// <summary>
        /// Lấy tất cả tools
        /// </summary>
        IReadOnlyList<IAITool> GetAllTools();

        /// <summary>
        /// Lấy tools được phép sử dụng theo context (role-based filtering)
        /// </summary>
        IReadOnlyList<IAITool> GetAllowedTools(AIQueryContext context);

        /// <summary>
        /// Lấy tools manifest cho một context cụ thể
        /// </summary>
        JsonObject GetToolsManifestForContext(AIQueryContext context);

        /// <summary>
        /// Đăng ký tool mới
        /// </summary>
        void RegisterTool(IAITool tool);
    }
}
