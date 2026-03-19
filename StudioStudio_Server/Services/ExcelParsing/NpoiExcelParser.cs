using System.Globalization;
using System.Text;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace StudioStudio_Server.Services.ExcelParsing
{
    /// <summary>
    /// NPOI-based parser for CSV and XLSX batch assignment files
    /// Supports UTF-8 BOM for Vietnamese character encoding
    /// </summary>
    public class NpoiExcelParser : IExcelParser
    {
        private const int MaxRows = 1000;
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

        // Expected CSV/Excel headers (case-insensitive)
        private static readonly HashSet<string> ExpectedHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Email", "GroupName", "Role"
        };

        /// <summary>
        /// Parse CSV or Excel file from stream
        /// </summary>
        public async Task<ParseResult> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();

            return extension switch
            {
                ".csv" => await ParseCsvAsync(stream, cancellationToken),
                ".xlsx" or ".xls" => await ParseExcelAsync(stream, cancellationToken),
                _ => new ParseResult
                {
                    ErrorCode = "VALIDATION009",
                    ErrorMessage = "Invalid file format. Only CSV and XLSX are supported."
                }
            };
        }

        /// <summary>
        /// Parse CSV file with UTF-8 BOM support
        /// </summary>
        private async Task<ParseResult> ParseCsvAsync(Stream stream, CancellationToken cancellationToken)
        {
            var result = new ParseResult();

            try
            {
                // Read all content with UTF-8 BOM support
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var content = await reader.ReadToEndAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(content))
                {
                    return new ParseResult
                    {
                        ErrorCode = "VALIDATION018",
                        ErrorMessage = "File contains no data"
                    };
                }

                var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

                if (lines.Length < 2)
                {
                    return new ParseResult
                    {
                        ErrorCode = "VALIDATION018",
                        ErrorMessage = "File contains no data rows"
                    };
                }

                // Validate headers
                var headerLine = lines[0];
                var headers = ParseCsvLine(headerLine);

                if (!ValidateHeaders(headers, out var headerError))
                {
                    return new ParseResult
                    {
                        ErrorCode = "VALIDATION020",
                        ErrorMessage = headerError
                    };
                }

                // Parse data rows
                for (int i = 1; i < lines.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var values = ParseCsvLine(line);
                    var rowNumber = i; // 1-based (after header)

                    if (result.Rows.Count >= MaxRows)
                    {
                        break; // Stop at max rows
                    }

                    var row = new ParsedBatchRow
                    {
                        RowNumber = rowNumber,
                        Email = values.Count > 0 ? values[0].Trim() : string.Empty,
                        GroupName = values.Count > 1 ? values[1].Trim() : string.Empty,
                        Role = values.Count > 2 ? values[2].Trim() : string.Empty
                    };

                    result.Rows.Add(row);
                }

                result.TotalRows = result.Rows.Count;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return new ParseResult
                {
                    ErrorCode = "BATCH003",
                    ErrorMessage = "Failed to parse CSV file"
                };
            }

            return result;
        }

        /// <summary>
        /// Parse CSV line handling quoted values with commas
        /// </summary>
        private List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            values.Add(current.ToString());
            return values;
        }

        /// <summary>
        /// Parse Excel (XLSX) file using NPOI
        /// </summary>
        private async Task<ParseResult> ParseExcelAsync(Stream stream, CancellationToken cancellationToken)
        {
            var result = new ParseResult();

            try
            {
                // NPOI requires the stream to be seekable for XSSFWorkbook
                // If not seekable, copy to MemoryStream
                Stream safeStream;
                if (!stream.CanSeek)
                {
                    var memStream = new MemoryStream();
                    await stream.CopyToAsync(memStream, cancellationToken);
                    memStream.Position = 0;
                    safeStream = memStream;
                }
                else
                {
                    safeStream = stream;
                }

                using (safeStream)
                {
                    var workbook = new XSSFWorkbook(safeStream);
                    var sheet = workbook.GetSheetAt(0);

                    if (sheet == null)
                    {
                        return new ParseResult
                        {
                            ErrorCode = "VALIDATION018",
                            ErrorMessage = "File contains no sheets"
                        };
                    }

                    var rowIterator = sheet.GetRowEnumerator();

                    if (!rowIterator.MoveNext())
                    {
                        return new ParseResult
                        {
                            ErrorCode = "VALIDATION018",
                            ErrorMessage = "File contains no data rows"
                        };
                    }

                    // Validate header row
                    var headerRow = (IRow)rowIterator.Current;
                    var headers = new List<string>();

                    for (int i = 0; i < headerRow.LastCellNum; i++)
                    {
                        var cell = headerRow.GetCell(i);
                        headers.Add(cell?.ToString()?.Trim() ?? string.Empty);
                    }

                    if (!ValidateHeaders(headers, out var headerError))
                    {
                        return new ParseResult
                        {
                            ErrorCode = "VALIDATION020",
                            ErrorMessage = headerError
                        };
                    }

                    // Parse data rows
                    int rowNumber = 1; // 1-based after header
                    while (rowIterator.MoveNext())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (result.Rows.Count >= MaxRows) break;

                        var dataRow = (IRow)rowIterator.Current;

                        // Skip empty rows
                        if (dataRow == null || IsEmptyRow(dataRow)) continue;

                        rowNumber++;

                        var row = new ParsedBatchRow
                        {
                            RowNumber = rowNumber,
                            Email = GetCellValue(dataRow.GetCell(0)),
                            GroupName = GetCellValue(dataRow.GetCell(1)),
                            Role = GetCellValue(dataRow.GetCell(2))
                        };

                        result.Rows.Add(row);
                    }

                    result.TotalRows = result.Rows.Count;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return new ParseResult
                {
                    ErrorCode = "BATCH003",
                    ErrorMessage = "Failed to parse Excel file"
                };
            }

            return result;
        }

        /// <summary>
        /// Get cell value as string, handling different cell types
        /// </summary>
        private string GetCellValue(ICell? cell)
        {
            if (cell == null) return string.Empty;

            return cell.CellType switch
            {
                CellType.String => cell.StringCellValue?.Trim() ?? string.Empty,
                CellType.Numeric => cell.NumericCellValue.ToString(CultureInfo.InvariantCulture),
                CellType.Boolean => cell.BooleanCellValue.ToString(),
                CellType.Formula => cell.CachedFormulaResultType switch
                {
                    CellType.String => cell.StringCellValue?.Trim() ?? string.Empty,
                    CellType.Numeric => cell.NumericCellValue.ToString(CultureInfo.InvariantCulture),
                    _ => cell.ToString()?.Trim() ?? string.Empty
                },
                _ => cell.ToString()?.Trim() ?? string.Empty
            };
        }

        /// <summary>
        /// Check if a row is empty (all cells blank)
        /// </summary>
        private bool IsEmptyRow(IRow row)
        {
            for (int i = 0; i < row.LastCellNum; i++)
            {
                var cell = row.GetCell(i);
                if (cell != null && cell.CellType != CellType.Blank &&
                    !string.IsNullOrWhiteSpace(GetCellValue(cell)))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Validate that required headers are present
        /// </summary>
        private bool ValidateHeaders(List<string> headers, out string error)
        {
            var headerSet = new HashSet<string>(headers.Select(h => h.Trim()), StringComparer.OrdinalIgnoreCase);

            var missingHeaders = ExpectedHeaders
                .Where(h => !headerSet.Contains(h))
                .ToList();

            if (missingHeaders.Count > 0)
            {
                error = $"Missing required headers: {string.Join(", ", missingHeaders)}";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
