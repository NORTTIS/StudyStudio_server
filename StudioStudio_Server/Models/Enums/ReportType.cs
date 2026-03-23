namespace StudioStudio_Server.Models.Enums
{
    public enum ReportType
    {
        Bug = 0,
        Feedback = 1,
        Support = 2,
        [System.Obsolete("Deprecated: no UI/endpoint assigns this value; retained for existing DB rows.")]
        Other = 3
    }
}
