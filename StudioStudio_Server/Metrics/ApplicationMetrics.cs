using Prometheus;

namespace StudioStudio_Server.Metrics;

/// <summary>
/// Application metrics for business monitoring
/// </summary>
public static class AppMetrics
{
    // ==================== HTTP Metrics ====================
    // These are automatically collected by prometheus-net.AspNetCore

    // ==================== Business Metrics ====================

    /// <summary>Total user registrations</summary>
    public static Counter UserRegistrationsTotal { get; } = Prometheus.Metrics
        .CreateCounter("studystudio_user_registrations_total", "Total number of user registrations");

    /// <summary>Total login attempts</summary>
    public static Counter UserLoginAttemptsTotal { get; } = Prometheus.Metrics
        .CreateCounter("studystudio_user_login_attempts_total", "Total number of login attempts",
            new CounterConfiguration { LabelNames = new[] { "result" } }); // success, failed

    /// <summary>Active JWT tokens (current valid tokens)</summary>
    public static Gauge ActiveJwtTokens { get; } = Prometheus.Metrics
        .CreateGauge("studystudio_active_jwt_tokens", "Number of currently active JWT tokens");

    /// <summary>Studio count</summary>
    public static Gauge StudiosTotal { get; } = Prometheus.Metrics
        .CreateGauge("studystudio_studios_total", "Total number of studios");

    /// <summary>Group count</summary>
    public static Gauge GroupsTotal { get; } = Prometheus.Metrics
        .CreateGauge("studystudio_groups_total", "Total number of groups");

    /// <summary>Active users (users with valid tokens)</summary>
    public static Gauge ActiveUsers { get; } = Prometheus.Metrics
        .CreateGauge("studystudio_active_users", "Number of currently active users");

    // ==================== SignalR Metrics ====================

    /// <summary>SignalR connections</summary>
    public static Gauge SignalRConnections { get; } = Prometheus.Metrics
        .CreateGauge("studystudio_signalr_connections", "Number of active SignalR connections",
            new GaugeConfiguration { LabelNames = new[] { "hub" } }); // group-discuss, task-comment

    /// <summary>SignalR messages sent</summary>
    public static Counter SignalRMessagesTotal { get; } = Prometheus.Metrics
        .CreateCounter("studystudio_signalr_messages_total", "Total SignalR messages sent",
            new CounterConfiguration { LabelNames = new[] { "hub" } });

    // ==================== Payment Metrics ====================

    /// <summary>Payment transactions</summary>
    public static Counter PaymentTransactionsTotal { get; } = Prometheus.Metrics
        .CreateCounter("studystudio_payment_transactions_total", "Total payment transactions",
            new CounterConfiguration { LabelNames = new[] { "status" } }); // success, failed, pending

    /// <summary>Payment amount (in VND)</summary>
    public static Counter PaymentAmountTotal { get; } = Prometheus.Metrics
        .CreateCounter("studystudio_payment_amount_total", "Total payment amount in VND",
            new CounterConfiguration { LabelNames = new[] { "status" } });

    /// <summary>Active subscriptions</summary>
    public static Gauge ActiveSubscriptions { get; } = Prometheus.Metrics
        .CreateGauge("studystudio_active_subscriptions", "Number of active subscriptions",
            new GaugeConfiguration { LabelNames = new[] { "plan_type" } }); // free, premium, enterprise

    // ==================== AI/Document Metrics ====================

    /// <summary>AI embedding requests</summary>
    public static Counter EmbeddingRequestsTotal { get; } = Prometheus.Metrics
        .CreateCounter("studystudio_embedding_requests_total", "Total embedding requests",
            new CounterConfiguration { LabelNames = new[] { "status" } }); // success, failed

    /// <summary>AI chat requests</summary>
    public static Counter AIChatRequestsTotal { get; } = Prometheus.Metrics
        .CreateCounter("studystudio_ai_chat_requests_total", "Total AI chat requests",
            new CounterConfiguration { LabelNames = new[] { "status" } }); // success, failed

    /// <summary>Documents processed</summary>
    public static Counter DocumentsProcessedTotal { get; } = Prometheus.Metrics
        .CreateCounter("studystudio_documents_processed_total", "Total documents processed",
            new CounterConfiguration { LabelNames = new[] { "status" } }); // success, failed

    /// <summary>Document size in bytes</summary>
    public static Counter DocumentBytesTotal { get; } = Prometheus.Metrics
        .CreateCounter("studystudio_document_bytes_total", "Total document size in bytes");

    // ==================== Task Metrics ====================

    /// <summary>Tasks created</summary>
    public static Counter TasksCreatedTotal { get; } = Prometheus.Metrics
        .CreateCounter("studystudio_tasks_created_total", "Total tasks created",
            new CounterConfiguration { LabelNames = new[] { "type" } }); // personal, group

    /// <summary>Tasks completed</summary>
    public static Counter TasksCompletedTotal { get; } = Prometheus.Metrics
        .CreateCounter("studystudio_tasks_completed_total", "Total tasks completed",
            new CounterConfiguration { LabelNames = new[] { "type" } }); // personal, group

    // ==================== Queue Metrics ====================

    /// <summary>Embedding queue size</summary>
    public static Gauge EmbeddingQueueSize { get; } = Prometheus.Metrics
        .CreateGauge("studystudio_embedding_queue_size", "Current embedding queue size");

    /// <summary>Delete queue size</summary>
    public static Gauge DeleteQueueSize { get; } = Prometheus.Metrics
        .CreateGauge("studystudio_delete_queue_size", "Current delete queue size");

    // ==================== API Response Time ====================

    /// <summary>API response time histogram (in seconds)</summary>
    public static Histogram ApiRequestDuration { get; } = Prometheus.Metrics
        .CreateHistogram("studystudio_api_request_duration_seconds", "API request duration in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "method", "endpoint", "status_code" },
                Buckets = new[] { 0.01, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0 }
            });

    // ==================== External Services Metrics ====================

    /// <summary>External service API calls</summary>
    public static Counter ExternalServiceCallsTotal { get; } = Prometheus.Metrics
        .CreateCounter("studystudio_external_service_calls_total", "Total external service API calls",
            new CounterConfiguration { LabelNames = new[] { "service", "status" } }); // success, failed

    /// <summary>External service latency</summary>
    public static Histogram ExternalServiceLatency { get; } = Prometheus.Metrics
        .CreateHistogram("studystudio_external_service_latency_seconds", "External service latency in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "service" },
                Buckets = new[] { 0.01, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0 }
            });
}
