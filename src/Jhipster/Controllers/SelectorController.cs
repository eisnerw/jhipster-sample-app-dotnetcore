
using AutoMapper;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using Jhipster.Domain;
using Jhipster.Crosscutting.Exceptions;
using Jhipster.Dto;
using Jhipster.Domain.Services.Interfaces;
using Jhipster.Web.Extensions;
using Jhipster.Web.Filters;
using Jhipster.Web.Rest.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Jhipster.Controllers
{
    [Authorize]
    [Route("api")]
    [ApiController]
    public class SelectorController : ControllerBase
    {
        private const string EntityName = "selector";
        private readonly IMapper _mapper;
        private readonly ISelectorService _selectorService;
        private readonly ILogger<SelectorController> _log;

        public SelectorController(ILogger<SelectorController> log,
            IMapper mapper,
            ISelectorService selectorService)
        {
            _log = log;
            _mapper = mapper;
            _selectorService = selectorService;
        }

        [HttpPost("selectors")]
        [ValidateModel]
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

        [HttpPut("selectors")]
        [ValidateModel]
        public async Task<IActionResult> UpdateSelector([FromBody] SelectorDto selectorDto)
        {
            _log.LogDebug($"REST request to update Selector : {selectorDto}");
            if (selectorDto.Id == 0) throw new BadRequestAlertException("Invalid Id", EntityName, "idnull");
            Selector selector = _mapper.Map<Selector>(selectorDto);
            await _selectorService.Save(selector);
            return Ok(selector)
                .WithHeaders(HeaderUtil.CreateEntityUpdateAlert(EntityName, selector.Id.ToString()));
        }

        [HttpGet("selectors")]
        public async Task<ActionResult<IEnumerable<SelectorDto>>> GetAllSelectors(IPageable pageable)
        {
            _log.LogDebug("REST request to get a page of Selectors");
            var result = await _selectorService.FindAll(pageable);
            var page = new Page<SelectorDto>(result.Content.Select(entity => _mapper.Map<SelectorDto>(entity)).ToList(), pageable, result.TotalElements);
            return Ok(((IPage<SelectorDto>)page).Content).WithHeaders(page.GeneratePaginationHttpHeaders());
        }

        [HttpGet("selectors/{id}")]
        public async Task<IActionResult> GetSelector([FromRoute] long id)
        {
            _log.LogDebug($"REST request to get Selector : {id}");
            var result = await _selectorService.FindOne(id);
            SelectorDto selectorDto = _mapper.Map<SelectorDto>(result);
            return ActionResultUtil.WrapOrNotFound(selectorDto);
        }

        [HttpDelete("selectors/{id}")]
        public async Task<IActionResult> DeleteSelector([FromRoute] long id)
        {
            _log.LogDebug($"REST request to delete Selector : {id}");
            await _selectorService.Delete(id);
            return Ok().WithHeaders(HeaderUtil.CreateEntityDeletionAlert(EntityName, id.ToString()));
        }
    }
}
