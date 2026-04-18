using Microsoft.Extensions.Options;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Text;
using System.Text.Json;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service for vector database operations using Qdrant Cloud
    /// Pattern: REST API client for semantic search and vector storage
    /// </summary>
    public class QdrantService : IVectorDatabaseService
    {
        private readonly QdrantConfig _config;
        private readonly ILogger<QdrantService> _logger;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initialize Qdrant Service
        /// Note: If config is incomplete, service operates in degraded mode
        /// </summary>
        public QdrantService(
            IOptions<QdrantConfig> config,
            ILogger<QdrantService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _config = config.Value;
            _logger = logger;

            if (string.IsNullOrEmpty(_config.Endpoint) || string.IsNullOrEmpty(_config.ApiKey))
            {
                _logger.LogWarning("Qdrant Cloud not configured. Vector operations will be skipped.");
                _httpClient = null!;
                return;
            }

            _httpClient = httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri(_config.Endpoint);
            _httpClient.DefaultRequestHeaders.Add("api-key", _config.ApiKey);
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        }

        /// <summary>
        /// Add/update vector to collection
        /// Flow: Convert data ? Generate embedding ? Store in Qdrant
        /// </summary>
        public async Task<bool> UpsertVectorAsync(string id, float[] vector, Dictionary<string, object> payload)
        {
            if (vector.Length != _config.VectorSize)
            {
                _logger.LogError("Vector size mismatch. Expected: {Expected}, Got: {Actual}", _config.VectorSize, vector.Length);
                return false;
            }

            string url = $"/collections/{_config.CollectionName}/points";

            object requestBody = new
            {
                points = new[]
                {
                    new
                    {
                        id,
                        vector,
                        payload
                    }
                }
            };

            StringContent content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            HttpResponseMessage response = await _httpClient.PutAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Vector upserted successfully. ID: {Id}", id);
                return true;
            }

            string errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Qdrant upsert failed. Status: {Status}, Error: {Error}", response.StatusCode, errorContent);
            return false;
        }

        /// <summary>
        /// Search vectors with groupId filter (for AI Q&amp;A)
        /// Only returns documents belonging to specific group
        /// </summary>
        public async Task<List<VectorSearchResponse.SearchResult>> SearchVectorsAsync(
            float[] queryVector,
            int topK,
            Guid groupId,
            Guid? documentId = null,
            CancellationToken cancellationToken = default)
        {
            if (queryVector.Length != _config.VectorSize)
            {
                _logger.LogError("Query vector size mismatch. Expected: {Expected}, Got: {Actual}",
                    _config.VectorSize, queryVector.Length);
                return new List<VectorSearchResponse.SearchResult>();
            }

            string url = $"/collections/{_config.CollectionName}/points/search";

            // Build filter: must=groupId [+documentId], must_not=deleted:true
            var mustClauses = new List<object>
            {
                new { key = "groupId", match = new { value = groupId.ToString() } }
            };
            if (documentId.HasValue)
            {
                mustClauses.Add(new { key = "documentId", match = new { value = documentId.Value.ToString() } });
            }

            object requestBody = new
            {
                vector = queryVector,
                limit = topK,
                with_payload = true,
                with_vector = false,
                filter = new
                {
                    must = mustClauses.ToArray(),
                    must_not = new[]
                    {
                        new { key = "deleted", match = new { value = true } }
                    }
                }
            };

            StringContent content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            _logger.LogInformation("Searching Qdrant with groupId filter: {GroupId}, topK: {TopK}", 
                groupId, topK);

            HttpResponseMessage response = await _httpClient.PostAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("[QDRANT-FAIL] HTTP={Status} groupId={GroupId} | Qdrant returned non-200 — returning empty list",
                    (int)response.StatusCode, groupId);
                return new List<VectorSearchResponse.SearchResult>();
            }

            string jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonDocument doc = JsonDocument.Parse(jsonResponse);

            List<VectorSearchResponse.SearchResult> results = new List<VectorSearchResponse.SearchResult>();

            if (doc.RootElement.TryGetProperty("result", out JsonElement resultArray))
            {
                foreach (JsonElement item in resultArray.EnumerateArray())
                {
                    string itemId = item.GetProperty("id").GetString() ?? string.Empty;
                    float score = item.GetProperty("score").GetSingle();
                    JsonElement payloadElement = item.GetProperty("payload");

                    // Parse payload to Dictionary
                    Dictionary<string, object> payload = new Dictionary<string, object>();
                    foreach (JsonProperty prop in payloadElement.EnumerateObject())
                    {
                        // Store value as string or parse by type
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            payload[prop.Name] = prop.Value.GetString() ?? string.Empty;
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.Number)
                        {
                            if (prop.Value.TryGetInt32(out int intValue))
                            {
                                payload[prop.Name] = intValue;
                            }
                            else
                            {
                                payload[prop.Name] = prop.Value.GetDouble();
                            }
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.True ||
                                 prop.Value.ValueKind == JsonValueKind.False)
                        {
                            payload[prop.Name] = prop.Value.GetBoolean();
                        }
                        else
                        {
                            payload[prop.Name] = prop.Value.ToString();
                        }
                    }

                    results.Add(new VectorSearchResponse.SearchResult
                    {
                        Id = itemId,
                        Score = score,
                        Payload = payload
                    });
                }
            }

            if (results.Count == 0)
            {
                _logger.LogInformation("[QDRANT-OK] HTTP=200 groupId={GroupId} topK={TopK} | 0 results (collection may be empty)",
                    groupId, topK);
            }
            else
            {
                _logger.LogInformation("[QDRANT-OK] HTTP=200 groupId={GroupId} | Found {Count} results",
                    groupId, results.Count);
            }

            return results;
        }

        /// <summary>
        /// Search vectors across multiple groups (for studio-level AI)
        /// Uses Qdrant MatchAny filter on groupId
        /// </summary>
        public async Task<List<VectorSearchResponse.SearchResult>> SearchVectorsMultiGroupAsync(
            float[] queryVector,
            int topK,
            List<Guid> groupIds,
            Guid? documentId = null,
            CancellationToken cancellationToken = default)
        {
            if (queryVector.Length != _config.VectorSize)
            {
                _logger.LogError("Query vector size mismatch. Expected: {Expected}, Got: {Actual}",
                    _config.VectorSize, queryVector.Length);
                return new List<VectorSearchResponse.SearchResult>();
            }

            string url = $"/collections/{_config.CollectionName}/points/search";

            // Build filter: must=groupId(MatchAny) [+documentId], must_not=deleted:true
            var mustClauses = new List<object>
            {
                new
                {
                    key = "groupId",
                    match = new { any = groupIds.Select(g => g.ToString()).ToArray() }
                }
            };
            if (documentId.HasValue)
            {
                mustClauses.Add(new { key = "documentId", match = new { value = documentId.Value.ToString() } });
            }

            object requestBody = new
            {
                vector = queryVector,
                limit = topK,
                with_payload = true,
                with_vector = false,
                filter = new
                {
                    must = mustClauses.ToArray(),
                    must_not = new[]
                    {
                        new { key = "deleted", match = new { value = true } }
                    }
                }
            };

            StringContent content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            _logger.LogInformation("Searching Qdrant across {Count} groups, topK: {TopK}, documentId: {DocumentId}",
                groupIds.Count, topK, documentId);

            HttpResponseMessage response = await _httpClient.PostAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("[QDRANT-FAIL] HTTP={Status} multi-group | Qdrant returned non-200 — returning empty list. Error: {Error}",
                    (int)response.StatusCode, errorContent);
                return new List<VectorSearchResponse.SearchResult>();
            }

            string jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonDocument doc = JsonDocument.Parse(jsonResponse);

            List<VectorSearchResponse.SearchResult> results = new List<VectorSearchResponse.SearchResult>();

            if (doc.RootElement.TryGetProperty("result", out JsonElement resultArray))
            {
                foreach (JsonElement item in resultArray.EnumerateArray())
                {
                    string itemId = item.GetProperty("id").GetString() ?? string.Empty;
                    float score = item.GetProperty("score").GetSingle();
                    JsonElement payloadElement = item.GetProperty("payload");

                    Dictionary<string, object> payload = new Dictionary<string, object>();
                    foreach (JsonProperty prop in payloadElement.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            payload[prop.Name] = prop.Value.GetString() ?? string.Empty;
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.Number)
                        {
                            if (prop.Value.TryGetInt32(out int intValue))
                                payload[prop.Name] = intValue;
                            else
                                payload[prop.Name] = prop.Value.GetDouble();
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.True ||
                                 prop.Value.ValueKind == JsonValueKind.False)
                        {
                            payload[prop.Name] = prop.Value.GetBoolean();
                        }
                        else
                        {
                            payload[prop.Name] = prop.Value.ToString();
                        }
                    }

                    results.Add(new VectorSearchResponse.SearchResult
                    {
                        Id = itemId,
                        Score = score,
                        Payload = payload
                    });
                }
            }

            _logger.LogInformation("Qdrant multi-group search completed. Found {Count} results across {GroupCount} groups",
                results.Count, groupIds.Count);

            return results;
        }

        /// <summary>
        /// Delete vector by ID
        /// </summary>
        public async Task<bool> DeleteVectorAsync(string id)
        {
            string url = $"/collections/{_config.CollectionName}/points/delete";

            object requestBody = new
            {
                points = new[] { id }
            };

            StringContent content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Vector deleted successfully. ID: {Id}", id);
                return true;
            }

            string errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Qdrant delete failed. Status: {Status}, Error: {Error}", response.StatusCode, errorContent);
            return false;
        }

        /// <summary>
        /// Delete multiple vectors by filter
        /// Qdrant Filter DSL format:
        /// {
        ///   "filter": {
        ///     "must": [{ "key": "field", "match": { "value": "value" }}],
        ///     "must_not": [{ "key": "field", "match": { "value": "value" }}]
        ///   }
        /// }
        /// </summary>
        public async Task<bool> DeleteVectorsByFilterAsync(Dictionary<string, object> filters)
        {
            string url = $"/collections/{_config.CollectionName}/points/delete";

            object requestBody = new
            {
                filter = filters
            };

            StringContent content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Vectors deleted successfully by filter");
                return true;
            }

            string errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Qdrant batch delete failed. Status: {Status}, Error: {Error}", response.StatusCode, errorContent);
            return false;
        }

        /// <summary>
        /// Test connection to Qdrant
        /// </summary>
        public async Task TestConnectionAsync()
        {
            if (_httpClient == null)
            {
                throw new Exception("Qdrant Cloud not configured");
            }

            // Test by getting cluster info
            string url = "/cluster";
            HttpResponseMessage response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Qdrant connection test failed: {response.StatusCode}");
            }
        }
    }
}
