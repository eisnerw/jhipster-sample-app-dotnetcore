using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using Microsoft.Extensions.Logging;

namespace JhipsterSampleApplication.Domain.Services
{
    public class ViewInitializationService
    {
        private readonly IViewService _viewService;
        private readonly ILogger<ViewInitializationService> _logger;

        public ViewInitializationService(IViewService viewService, ILogger<ViewInitializationService> logger)
        {
            _viewService = viewService;
            _logger = logger;
        }

        public async Task InitializeViewsAsync()
        {
            try
            {
                var resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Views");
                if (!Directory.Exists(resourcesPath))
                {
                    _logger.LogWarning("Views initialization directory not found: {Path}", resourcesPath);
                    return;
                }

                var jsonFiles = Directory.GetFiles(resourcesPath, "*.json");
                foreach (var jsonFile in jsonFiles)
                {
                    _logger.LogInformation("Initializing views from file: {File}", jsonFile);
                    var jsonContent = await File.ReadAllTextAsync(jsonFile);
                    
                    try
                    {
                        var views = JsonSerializer.Deserialize<ViewDto[]>(jsonContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (views == null)
                        {
                            _logger.LogWarning("No views found in file: {File}", jsonFile);
                            continue;
                        }

                        foreach (var view in views)
                        {
                            try
                            {
                                if (string.IsNullOrEmpty(view.Name))
                                {
                                    _logger.LogError("View in file {File} has null or empty name", jsonFile);
                                    continue;
                                }

                                if (string.IsNullOrEmpty(view.Domain))
                                {
                                    _logger.LogError("View {ViewName} in file {File} has null or empty domain", view.Name, jsonFile);
                                    continue;
                                }

                                var existingView = await _viewService.GetByIdAsync(view.Id);
                                if (existingView == null)
                                {
                                    await _viewService.CreateAsync(view);
                                    _logger.LogInformation("Created view: {ViewId}", view.Id);
                                }
                                else
                                {
                                    await _viewService.UpdateAsync(view);
                                    _logger.LogInformation("Updated view: {ViewId}", view.Id);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing view {ViewId} in file {File}: {Message}", view.Id, jsonFile, ex.Message);
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Error deserializing JSON from file {File}: {Message}", jsonFile, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing views: {Message}", ex.Message);
                throw;
            }
        }
    }
} 