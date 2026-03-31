namespace StudioStudio_Server.Services;

/// <summary>
/// Centralized helper for all analytics activity scoring logic.
/// Ensures consistent formula across Benchmark, Group Heatmap, Studio Activity, and Member Contribution.
///
/// <para>SCORING FORMULA:</para>
/// <para>  Score = BasePoints × PriorityWeight × SeverityWeight + Modifiers</para>
///
/// <para>Only TASK_COMPLETE multiplies by Priority × Severity weights.</para>
/// <para>All other actions (CREATE, UPDATE, DELETE, COMMENT, MESSAGE) are flat to prevent score inflation via spam.</para>
///
/// <para>PriorityWeight:  [Low=0] → 1.0 | [Medium=1] → 1.5 | [High=2] → 2.0</para>
/// <para>SeverityWeight: [Minor=0] → 1.0 | [Moderate=1] → 1.2 | [Major=2] → 1.5 | [Critical=3] → 2.0</para>
/// </summary>
public static class ActivityScoreHelper
{
    // ─── Base Points ────────────────────────────────────────────────────────────
    // Only TASK_COMPLETE should earn weighted points (× Priority × Severity).
    // CREATE/UPDATE/DELETE are flat to prevent users from inflating their score
    // by creating or updating many high-priority/critical tasks without completing them.

    /// <summary>
    /// Base points awarded when a task is marked COMPLETE.
    /// Multiplied by Priority × Severity weight (max 40 pts for Critical+High).
    /// </summary>
    public const double CompleteBase = 10;

    /// <summary>
    /// Flat points awarded when a task is CREATED. NOT weighted by Priority/Severity.
    /// Reason: prevents creating high-priority tasks to inflate score without doing work.
    /// </summary>
    public const double CreateBase = 3;

    /// <summary>
    /// Flat points awarded when a task is UPDATED. NOT weighted by Priority/Severity.
    /// Reason: prevents mass-updating tasks to inflate score.
    /// </summary>
    public const double UpdateBase = 1;

    /// <summary>
    /// Flat points awarded when a task is DELETED. NOT weighted by Priority/Severity.
    /// Reason: delete spam should not yield high reward.
    /// </summary>
    public const double DeleteBase = 1;

    /// <summary>
    /// Default flat score for any unmatched ActionType (e.g. COMMENT_CREATE).
    /// Comments are worth 1 pt flat — reflecting that commenting is low-effort
    /// and should not be inflated by associating with high-priority tasks.
    /// </summary>
    public const double DefaultBase = 1;

    // ─── Weights ────────────────────────────────────────────────────────────────
    // Index maps to Priority/Severity enum value:
    //   Priority: 0=Low, 1=Medium, 2=High
    //   Severity: 0=Minor, 1=Moderate, 2=Major, 3=Critical

    /// <summary>
    /// Multiplier applied to CompleteBase based on task Priority.
    /// Higher priority tasks reward proportionally more when completed.
    /// </summary>
    public static IReadOnlyList<double> PriorityWeight { get; } = new double[] { 1.0, 1.5, 2.0 };

    /// <summary>
    /// Multiplier applied to CompleteBase based on task Severity.
    /// More severe (impactful) tasks reward proportionally more when completed.
    /// </summary>
    public static IReadOnlyList<double> SeverityWeight { get; } = new double[] { 1.0, 1.2, 1.5, 2.0 };

    // ─── Score Calculation ──────────────────────────────────────────────────────

    /// <summary>
    /// Calculates the activity score for a single ActivityLog entry.
    ///
    /// Only TASK_COMPLETE is weighted by Priority × Severity.
    /// All other actions return their flat base score to prevent spam inflation.
    ///
    /// <para>Examples:</para>
    /// <para>  Complete High(2)+Critical(3) task → 10 × 2.0 × 2.0 = 40 pts</para>
    /// <para>  Complete Low(0)+Minor(0) task    → 10 × 1.0 × 1.0 = 10 pts</para>
    /// <para>  Create any task                  → 3 pts (flat)</para>
    /// <para>  Update any task                  → 1 pt  (flat)</para>
    /// <para>  Comment on any task              → 1 pt  (flat, falls to DefaultBase)</para>
    /// </summary>
    /// <param name="actionType">The ActionType from ActivityLog (e.g. "TASK_COMPLETE").</param>
    /// <param name="priority">Priority enum value: 0=Low, 1=Medium, 2=High.</param>
    /// <param name="severity">Severity enum value: 0=Minor, 1=Moderate, 2=Major, 3=Critical.</param>
    /// <returns>The computed score as a double.</returns>
    public static double GetScore(string actionType, int priority, int severity)
    {
        return actionType switch
        {
            "TASK_COMPLETE" => CompleteBase * GetPriority(priority) * GetSeverity(severity),
            "TASK_CREATE"  => CreateBase,   // Flat — does not scale with priority/severity
            "TASK_UPDATE"  => UpdateBase,   // Flat — does not scale with priority/severity
            "TASK_DELETE"  => DeleteBase,   // Flat — does not scale with priority/severity
            _              => DefaultBase    // COMMENT_CREATE and all other types: 1 pt flat
        };
    }

    /// <summary>
    /// Returns the Priority weight multiplier for a given priority index.
    /// Out-of-range values safely return 1.0.
    /// </summary>
    /// <param name="priority">Priority enum value: 0=Low, 1=Medium, 2=High.</param>
    public static double GetPriority(int priority) =>
        priority >= 0 && priority < PriorityWeight.Count ? PriorityWeight[priority] : 1.0;

    /// <summary>
    /// Returns the Severity weight multiplier for a given severity index.
    /// Out-of-range values safely return 1.0.
    /// </summary>
    /// <param name="severity">Severity enum value: 0=Minor, 1=Moderate, 2=Major, 3=Critical.</param>
    public static double GetSeverity(int severity) =>
        severity >= 0 && severity < SeverityWeight.Count ? SeverityWeight[severity] : 1.0;

    // ─── Activity Level Thresholds ─────────────────────────────────────────────

    // FIXED thresholds — used by all heatmap/chart visualizations.
    // These thresholds are absolute and NOT relative to the group's maximum,
    // ensuring consistent comparison across groups and time periods.
    //
    //   Level 0 → No activity (score = 0)
    //   Level 1 → Low activity   (0 < score ≤ 5)
    //   Level 2 → Medium activity (5 < score ≤ 15)
    //   Level 3 → High activity   (15 < score ≤ 30)
    //   Level 4 → Very high activity (score > 30)

    /// <summary>
    /// Maps a raw activity score to a fixed activity level (0–4).
    /// Thresholds are ABSOLUTE, not relative to any max value.
    ///
    /// <para>Fixed thresholds ensure that:</para>
    /// <para>  - Activity levels are comparable across different groups (no group normalisation)</para>
    /// <para>  - A user's "Level 3" today means the same as a "Level 3" last month</para>
    /// <para>  - No user is penalised simply because a teammate had a very active day</para>
    ///
    /// <para>Use <see cref="GetActivityLevelDynamic"/> if group-relative levels are needed instead.</para>
    /// </summary>
    /// <param name="score">The raw weighted activity score for one member on one day.</param>
    /// <returns>Integer activity level: 0 (none), 1 (low), 2 (medium), 3 (high), 4 (very high).</returns>
    public static int GetActivityLevel(double score) =>
        score == 0 ? 0
        : score <= 5  ? 1
        : score <= 15 ? 2
        : score <= 30 ? 3
        : 4;

    /// <summary>
    /// Maps a raw activity score to a DYNAMIC activity level (0–4) relative to a group maximum.
    /// A user with the highest score in the group always gets Level 4; others are proportioned.
    ///
    /// <para>DEPRECATED: Use <see cref="GetActivityLevel"/> with fixed thresholds for all</para>
    /// <para>new analytics components. Dynamic levels cause inconsistent comparisons across groups.</para>
    ///
    /// <para>Thresholds:</para>
    /// <para>  Level 0 → score = 0 (no activity that day)</para>
    /// <para>  Level 1 → 0%   &lt; score ≤ 25%   of groupMax</para>
    /// <para>  Level 2 → 25%  &lt; score ≤ 50%   of groupMax</para>
    /// <para>  Level 3 → 50%  &lt; score ≤ 75%   of groupMax</para>
    /// <para>  Level 4 → 75%  &lt; score ≤ 100%  of groupMax</para>
    /// </summary>
    /// <param name="score">The raw activity score for one member on one day.</param>
    /// <param name="groupMax">The highest raw score any member achieved in the group for that period.</param>
    /// <returns>Integer activity level 0–4.</returns>
    public static int GetActivityLevelDynamic(double score, double groupMax) =>
        groupMax <= 0 ? 0
        : score == 0 ? 0
        : score <= groupMax * 0.25 ? 1
        : score <= groupMax * 0.50 ? 2
        : score <= groupMax * 0.75 ? 3
        : 4;
}
