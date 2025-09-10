using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Repositories;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using AutoMapper;
using System;
using System.Linq;

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
                // As a last resort, load from resource JSON under Resources/Views
                var loaded = LoadFromResources(id);
                if (loaded != null)
                {
                    // Persist for future lookups
                    try
                    {
                        var created = await _viewRepository.AddAsync(loaded);
                        return _mapper.Map<ViewDto>(created);
                    }
                    catch
                    {
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
                var viewsDir = System.IO.Path.Combine(baseDir, "Resources", "Views");
                if (!System.IO.Directory.Exists(viewsDir)) return null;
                foreach (var file in System.IO.Directory.EnumerateFiles(viewsDir, "*.json"))
                {
                    var json = System.IO.File.ReadAllText(file);
                    var dtos = System.Text.Json.JsonSerializer.Deserialize<List<JhipsterSampleApplication.Dto.ViewDto>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (dtos == null) continue;
                    var match = dtos.FirstOrDefault(v =>
                        string.Equals(v.Id, idOrName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(v.Name, idOrName, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        var entity = _mapper.Map<View>(match);
                        entity.EnsureId();
                        return entity;
                    }
                }
            }
            catch
            {
                // ignore resource load errors
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
