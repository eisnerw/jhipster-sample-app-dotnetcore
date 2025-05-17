using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Repositories;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using AutoMapper;

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
            var view = await _viewRepository.GetByIdAsync(id);
            return _mapper.Map<ViewDto>(view);
        }

        public async Task<IEnumerable<ViewDto>> GetAllAsync()
        {
            var views = await _viewRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<ViewDto>>(views);
        }

        public async Task<ViewDto> CreateAsync(ViewDto viewDto)
        {
            var view = _mapper.Map<View>(viewDto);
            var createdView = await _viewRepository.AddAsync(view);
            return _mapper.Map<ViewDto>(createdView);
        }

        public async Task<ViewDto> UpdateAsync(ViewDto viewDto)
        {
            var view = _mapper.Map<View>(viewDto);
            var updatedView = await _viewRepository.UpdateAsync(view);
            return _mapper.Map<ViewDto>(updatedView);
        }

        public async Task DeleteAsync(string id)
        {
            await _viewRepository.DeleteAsync(id);
        }
    }
} 