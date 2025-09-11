using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Repositories;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using AutoMapper;
using System;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace JhipsterSampleApplication.Domain.Services
{
    public class ViewService : IViewService
    {
        private readonly IViewRepository _viewRepository;
        private readonly IMapper _mapper;

        public ViewService(IViewRepository viewRepository, IMapper mapper)
        {
            _viewRepository = viewRepository;
            _mapper = mapper;
        }

        public async Task<ViewDto> GetByIdAsync(string id)
        {
            // Try exact match first
            var view = await _viewRepository.GetByIdAsync(id);
            if (view == null)
            {
                // Try common normalizations used by initialization (lower-cased ids)
                var altId = id.ToLowerInvariant();
                view = await _viewRepository.GetByIdAsync(altId);
            }
            if (view == null)
            {
                // Fallback: scan all and match either Id or Name case-insensitively
                var all = await _viewRepository.GetAllAsync();
                view = all.FirstOrDefault(v =>
                    string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(v.Name, id, StringComparison.OrdinalIgnoreCase));
            }
            if (view == null)
            {
                // Last-resort: load from Resources/Views/*.json
                var loaded = LoadFromResources(id);
                if (loaded != null)
                {
                    try
                    {
                        // Persist for next time
                        var created = await _viewRepository.AddAsync(loaded);
                        return _mapper.Map<ViewDto>(created);
                    }
                    catch
                    {
                        // If persistence fails, still return the loaded definition
                        return _mapper.Map<ViewDto>(loaded);
                    }
                }
            }
            return _mapper.Map<ViewDto>(view);
        }

        private View? LoadFromResources(string idOrName)
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var viewsDir = Path.Combine(baseDir, "Resources", "Views");
                if (!Directory.Exists(viewsDir)) return null;
                foreach (var file in Directory.EnumerateFiles(viewsDir, "*.json"))
                {
                    List<JhipsterSampleApplication.Dto.ViewDto>? dtos = null;
                    try
                    {
                        dtos = JsonSerializer.Deserialize<List<JhipsterSampleApplication.Dto.ViewDto>>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    catch { continue; }
                    if (dtos == null) continue;

                    var match = dtos.FirstOrDefault(d =>
                        string.Equals(d.Id, idOrName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(d.Name, idOrName, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        var entity = new View
                        {
                            Id = match.Id ?? string.Empty,
                            Name = match.Name ?? string.Empty,
                            Field = match.Field,
                            Aggregation = match.Aggregation,
                            Query = match.Query,
                            CategoryQuery = match.CategoryQuery,
                            Script = match.Script,
                            parentViewId = match.parentViewId,
                            Domain = match.Domain ?? string.Empty
                        };
                        entity.EnsureId();
                        return entity;
                    }
                }
            }
            catch
            {
                // ignore resource failures
            }
            return null;
        }

        public async Task<ViewDto?> GetChildByParentIdAsync(string id)
        {
            var views = await _viewRepository.GetAllAsync();            
            var viewList = views.Where(v=>v.parentViewId == id).ToList();
            if (viewList.Count != 1){
                return null;
            }
            return _mapper.Map<ViewDto>(viewList[0]);
        }

        public async Task<IEnumerable<ViewDto>> GetAllAsync()
        {
            var views = await _viewRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<ViewDto>>(views);
        }

        public async Task<IEnumerable<ViewDto>> GetByDomainAsync(string domain)
        {
            if (string.IsNullOrEmpty(domain))
            {
                throw new ArgumentException("Domain cannot be null or empty", nameof(domain));
            }

            var views = await _viewRepository.GetAllAsync();
            var domainViews = views.Where(v => v.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));
            return _mapper.Map<IEnumerable<ViewDto>>(domainViews);
        }

        public async Task<ViewDto> CreateAsync(ViewDto viewDto)
        {
            if (viewDto == null)
            {
                throw new ArgumentNullException(nameof(viewDto));
            }

            if (string.IsNullOrEmpty(viewDto.Name))
            {
                throw new InvalidOperationException("View name cannot be null or empty");
            }

            if (string.IsNullOrEmpty(viewDto.Domain))
            {
                throw new InvalidOperationException("View domain cannot be null or empty");
            }

            var view = _mapper.Map<View>(viewDto);
            view.EnsureId();
            var createdView = await _viewRepository.AddAsync(view);
            return _mapper.Map<ViewDto>(createdView);
        }

        public async Task<ViewDto> UpdateAsync(ViewDto viewDto)
        {
            if (viewDto == null)
            {
                throw new ArgumentNullException(nameof(viewDto));
            }

            if (string.IsNullOrEmpty(viewDto.Name))
            {
                throw new InvalidOperationException("View name cannot be null or empty");
            }

            if (string.IsNullOrEmpty(viewDto.Domain))
            {
                throw new InvalidOperationException("View domain cannot be null or empty");
            }

            var view = _mapper.Map<View>(viewDto);
            view.EnsureId();
            var updatedView = await _viewRepository.UpdateAsync(view);
            return _mapper.Map<ViewDto>(updatedView);
        }

        public async Task DeleteAsync(string id)
        {
            await _viewRepository.DeleteAsync(id);
        }
    }
} 
