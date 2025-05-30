using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using Microsoft.Extensions.Logging;

namespace JhipsterSampleApplication.Domain.Services
{
    public class NamedQueryInitializationService
    {
        private readonly INamedQueryService _namedQueryService;
        private readonly ILogger<NamedQueryInitializationService> _logger;

        public NamedQueryInitializationService(INamedQueryService namedQueryService, ILogger<NamedQueryInitializationService> logger)
        {
            _namedQueryService = namedQueryService;
            _logger = logger;
        }

        public async Task InitializeNamedQueriesAsync()
        {
            try
            {
                var resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Data");
                var jsonFilePath = Path.Combine(resourcesPath, "NamedQueries.json");
                
                if (!File.Exists(jsonFilePath))
                {
                    _logger.LogWarning("NamedQueries initialization file not found: {Path}", jsonFilePath);
                    return;
                }

                _logger.LogInformation("Initializing named queries from file: {File}", jsonFilePath);
                var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
                
                try
                {
                    var namedQueries = JsonSerializer.Deserialize<NamedQueryDto[]>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (namedQueries == null)
                    {
                        _logger.LogWarning("No named queries found in file: {File}", jsonFilePath);
                        return;
                    }

                    foreach (var namedQuery in namedQueries)
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(namedQuery.Name))
                            {
                                _logger.LogError("Named query in file {File} has null or empty name", jsonFilePath);
                                continue;
                            }

                            if (string.IsNullOrEmpty(namedQuery.Text))
                            {
                                _logger.LogError("Named query {QueryName} in file {File} has null or empty text", namedQuery.Name, jsonFilePath);
                                continue;
                            }

                            if (string.IsNullOrEmpty(namedQuery.Owner))
                            {
                                _logger.LogError("Named query {QueryName} in file {File} has null or empty owner", namedQuery.Name, jsonFilePath);
                                continue;
                            }

                            var existingQuery = await _namedQueryService.FindByNameAndOwner(namedQuery.Name, namedQuery.Owner);
                            if (existingQuery == null)
                            {
                                var entity = new JhipsterSampleApplication.Domain.Entities.NamedQuery
                                {
                                    Name = namedQuery.Name,
                                    Text = namedQuery.Text,
                                    Owner = namedQuery.Owner
                                };
                                await _namedQueryService.Save(entity);
                                _logger.LogInformation("Created named query: {QueryName}", namedQuery.Name);
                            }
                            else
                            {
                                existingQuery.Text = namedQuery.Text;
                                await _namedQueryService.Save(existingQuery);
                                _logger.LogInformation("Updated named query: {QueryName}", namedQuery.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing named query {QueryName} in file {File}: {Message}", namedQuery.Name, jsonFilePath, ex.Message);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing JSON from file {File}: {Message}", jsonFilePath, ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing named queries: {Message}", ex.Message);
            }
        }
    }
} 