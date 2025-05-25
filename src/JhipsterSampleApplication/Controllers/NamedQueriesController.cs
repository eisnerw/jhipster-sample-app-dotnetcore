#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Web;
using Microsoft.AspNetCore.Http;
using JHipsterNet.Core.Pagination;
using JhipsterSampleApplication.Domain.Entities;
using JHipsterNet.Core.Pagination.Extensions;
using AutoMapper;
using System.Linq;

namespace JhipsterSampleApplication.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NamedQueriesController : ControllerBase
    {
        private readonly INamedQueryService _namedQueryService;
        private readonly ILogger<NamedQueriesController> _log;
        private readonly IMapper _mapper;

        public NamedQueriesController(INamedQueryService namedQueryService, ILogger<NamedQueriesController> log, IMapper mapper)
        {
            _namedQueryService = namedQueryService;
            _log = log;
            _mapper = mapper;
        }

        /// <summary>
        /// Get named queries with optional filtering by name and owner
        /// </summary>
        /// <param name="name">Optional name to filter queries</param>
        /// <param name="owner">Optional owner to filter queries</param>
        /// <returns>List of named queries</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<NamedQueryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<NamedQueryDto>>> GetAll([FromQuery] string? name = null, [FromQuery] string? owner = null)
        {
            _log.LogDebug("REST request to get NamedQueries with filters: name={Name}, owner={Owner}", name, owner);

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(owner))
            {
                var queries = await _namedQueryService.FindByNameAndOwner(name, owner);
                return Ok(queries.Select(_mapper.Map<NamedQueryDto>));
            }
            else if (!string.IsNullOrEmpty(owner))
            {
                var queries = await _namedQueryService.FindByOwner(owner);
                return Ok(queries.Select(_mapper.Map<NamedQueryDto>));
            }
            else if (!string.IsNullOrEmpty(name))
            {
                var queries = await _namedQueryService.FindByName(name);
                return Ok(queries.Select(_mapper.Map<NamedQueryDto>));
            }
            else
            {
                var pageable = Pageable.Of(0, int.MaxValue);
                var page = await _namedQueryService.FindAll(pageable);
                return Ok(page.Content.Select(_mapper.Map<NamedQueryDto>));
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<NamedQueryDto>> Get(long id)
        {
            _log.LogDebug("REST request to get NamedQuery : {Id}", id);
            var entity = await _namedQueryService.FindOne(id);
            if (entity == null)
            {
                return NotFound();
            }
            var dto = new NamedQueryDto {
                Id = entity.Id,
                Name = entity.Name,
                Text = entity.Text,
                Owner = entity.Owner
            };
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<NamedQueryDto>> Create([FromBody] NamedQueryDto namedQueryDto)
        {
            _log.LogDebug("REST request to save NamedQuery : {NamedQuery}", namedQueryDto);
            var entity = new NamedQuery {
                Name = namedQueryDto.Name,
                Text = namedQueryDto.Text,
                Owner = namedQueryDto.Owner
            };
            var saved = await _namedQueryService.Save(entity);
            var dto = new NamedQueryDto {
                Id = saved.Id,
                Name = saved.Name,
                Text = saved.Text,
                Owner = saved.Owner
            };
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] NamedQueryDto namedQueryDto)
        {
            _log.LogDebug("REST request to update NamedQuery : {NamedQuery}", namedQueryDto);
            var existingEntity = await _namedQueryService.FindOne(id);
            if (existingEntity == null)
            {
                return NotFound();
            }

            var entity = new NamedQuery {
                Id = id,
                Name = namedQueryDto.Name,
                Text = namedQueryDto.Text,
                Owner = namedQueryDto.Owner
            };
            await _namedQueryService.Save(entity);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            _log.LogDebug("REST request to delete NamedQuery : {Id}", id);
            await _namedQueryService.Delete(id);
            return NoContent();
        }
    }
} 