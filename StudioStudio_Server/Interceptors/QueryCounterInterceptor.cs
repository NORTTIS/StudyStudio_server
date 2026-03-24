using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace StudioStudio_Server.Interceptors;

/// <summary>
/// EF Core interceptor that counts the number of SQL queries executed per request.
/// Thread-safe, per-request isolation via AsyncLocal.
/// </summary>
public class QueryCounterInterceptor : DbCommandInterceptor
{
    private static readonly AsyncLocal<RequestQueryInfo> _currentRequest = new();

    /// <summary>
    /// Call at the start of a request scope to initialize counting.
    /// </summary>
    public static void BeginRequest()
    {
        _currentRequest.Value = new RequestQueryInfo();
    }

    /// <summary>
    /// Call at the end of a request scope to retrieve results.
    /// Returns null if BeginRequest was never called for this async flow.
    /// </summary>
    public static RequestQueryInfo? EndRequest()
    {
        var info = _currentRequest.Value;
        _currentRequest.Value = null;
        return info;
    }

    /// <summary>
    /// Current request info if inside a tracked request scope.
    /// </summary>
    public static RequestQueryInfo? Current => _currentRequest.Value;

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Increment(eventData);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Increment(eventData);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Increment(eventData);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Increment(eventData);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Increment(eventData);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Increment(eventData);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void Increment(CommandEventData eventData)
    {
        var info = _currentRequest.Value;
        info?.Increment(eventData.Command.CommandText);
    }
}

/// <summary>
/// Aggregated query statistics for a single request scope.
/// </summary>
public class RequestQueryInfo
{
    private int _count = 0;
    private readonly ConcurrentDictionary<string, int> _byCommand = new();
    private readonly ConcurrentQueue<string> _recentCommands = new();

    public int Count => _count;
    public IReadOnlyDictionary<string, int> ByCommand => _byCommand;
    public IReadOnlyList<string> RecentCommands => _recentCommands.ToList();

    public void Increment(string commandText)
    {
        Interlocked.Increment(ref _count);

        // Store a truncated version of the command for analysis
        var key = TruncateCommand(commandText);
        _byCommand.AddOrUpdate(key, 1, (_, c) => c + 1);

        // Keep last 50 commands for debugging
        if (_recentCommands.Count >= 50)
        {
            // Drop oldest
            _recentCommands.TryDequeue(out _);
        }
        _recentCommands.Enqueue(commandText);
    }

    /// <summary>
    /// Get a summary for logging.
    /// </summary>
    public string GetSummary()
    {
        return $"Total queries: {Count}, Unique patterns: {ByCommand.Count}";
    }

    private static string TruncateCommand(string command)
    {
        // Normalize: collapse whitespace, trim to first 100 chars
        var normalized = string.Join(" ", command.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length > 120 ? normalized[..120] + "..." : normalized;
    }
}
