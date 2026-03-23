namespace StudioStudio_Server.Models.Enums
{
    public enum ReportPriority
    {
        Low = 0,
        [System.Obsolete("Deprecated: no UI/endpoint assigns this value; retained for existing DB rows.")]
        Medium = 1,
        [System.Obsolete("Deprecated: no UI/endpoint assigns this value; retained for existing DB rows.")]
        High = 2,
        [System.Obsolete("Deprecated: no UI/endpoint assigns this value; retained for existing DB rows.")]
        Critical = 3
    }
}
