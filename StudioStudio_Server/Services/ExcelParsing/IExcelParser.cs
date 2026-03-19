namespace StudioStudio_Server.Services.ExcelParsing
{
    /// <summary>
    /// Parsed row from CSV/Excel file
    /// </summary>
    public class ParsedBatchRow
    {
        public int RowNumber { get; set; }
        public string Email { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of parsing a batch assign file
    /// </summary>
    public class ParseResult
    {
        public List<ParsedBatchRow> Rows { get; set; } = new();
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public int TotalRows { get; set; }
    }

    /// <summary>
    /// Interface for parsing CSV/Excel files for batch assignment
    /// </summary>
    public interface IExcelParser
    {
        /// <summary>
        /// Parse CSV or Excel file from stream
        /// Validates file headers (Email, GroupName, Role)
        /// Returns rows with 1-based row numbers
        /// </summary>
        /// <param name="stream">File stream (disposed after parsing)</param>
        /// <param name="fileName">Original file name (for extension detection)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<ParseResult> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
    }
}
