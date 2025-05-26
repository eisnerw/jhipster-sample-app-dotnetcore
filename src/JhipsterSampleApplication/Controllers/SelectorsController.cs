#nullable enable

using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Crosscutting.Exceptions;
using JhipsterSampleApplication.Dto;
using JhipsterSampleApplication.Web.Extensions;
using JhipsterSampleApplication.Web.Rest.Utilities;
using AutoMapper;
using System.Linq;
using JhipsterSampleApplication.Domain.Repositories.Interfaces;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Infrastructure.Web.Rest.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace JhipsterSampleApplication.Controllers
{
    [Authorize]
    [Route("api/selectors")]
    [ApiController]
    public class SelectorsController : ControllerBase
    {
        private const string EntityName = "selector";
        private readonly ILogger<SelectorsController> _log;
        private readonly IMapper _mapper;
        private readonly ISelectorService _selectorService;

        public SelectorsController(ILogger<SelectorsController> log,
        IMapper mapper,
        ISelectorService selectorService)
        {
            _log = log;
            _mapper = mapper;
            _selectorService = selectorService;
        }

        [HttpPost]
        public async Task<ActionResult<SelectorDto>> CreateSelector([FromBody] SelectorDto selectorDto)
        {
            _log.LogDebug($"REST request to save Selector : {selectorDto}");
            if (selectorDto.Id != 0)
                 throw new BadRequestAlertException("A new selector cannot already have an ID", EntityName, "idexists");

            Selector selector = _mapper.Map<Selector>(selectorDto);
            await _selectorService.Save(selector);
            return CreatedAtAction(nameof(GetSelector), new { id = selector.Id }, selector)
                .WithHeaders(HeaderUtil.CreateEntityCreationAlert(EntityName, selector.Id.ToString()));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSelector(long id, [FromBody] SelectorDto selectorDto)
        {
            _log.LogDebug($"REST request to update Selector : {selectorDto}");
            if (selectorDto.Id == 0) throw new BadRequestAlertException("Invalid Id", EntityName, "idnull");
            if (id != selectorDto.Id) throw new BadRequestAlertException("Invalid Id", EntityName, "idinvalid");
            Selector selector = _mapper.Map<Selector>(selectorDto);
            await _selectorService.Save(selector);
            return Ok(selector)
                .WithHeaders(HeaderUtil.CreateEntityUpdateAlert(EntityName, selector.Id.ToString()));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SelectorDto>>> GetAllSelectors(
            [FromQuery] int page = 0,
            [FromQuery] int size = 10000,
            [FromQuery] string sort = "id,asc")
        {
            _log.LogDebug("REST request to get a page of Selectors");
            IPageable pageable = Pageable.Of(page, size);
            var result = await _selectorService.FindAll(pageable);
            var resultPage = new Page<SelectorDto>(result.Content.Select(entity => _mapper.Map<SelectorDto>(entity)).ToList(), pageable, result.TotalElements);
            return Ok(((IPage<SelectorDto>)resultPage).Content).WithHeaders(resultPage.GeneratePaginationHttpHeaders());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSelector([FromRoute] long id)
        {
            _log.LogDebug($"REST request to get Selector : {id}");
            var result = await _selectorService.FindOne(id);
            SelectorDto selectorDto = _mapper.Map<SelectorDto>(result);
            return ActionResultUtil.WrapOrNotFound(selectorDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSelector([FromRoute] long id)
        {
            _log.LogDebug($"REST request to delete Selector : {id}");
            await _selectorService.Delete(id);
            return Ok().WithHeaders(HeaderUtil.CreateEntityDeletionAlert(EntityName, id.ToString()));
        }
    }
}
