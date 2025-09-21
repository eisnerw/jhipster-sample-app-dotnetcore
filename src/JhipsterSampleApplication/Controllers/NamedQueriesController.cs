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
using System;
using JhipsterSampleApplication.Infrastructure.Data.Repositories;

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
        private readonly IUserService _userService;
        
        

        public NamedQueriesController(INamedQueryService namedQueryService, ILogger<NamedQueriesController> log, IMapper mapper, IUserService userService)
        {
            _namedQueryService = namedQueryService;
            _log = log;
            _mapper = mapper;
            _userService = userService;
        }

        /// <summary>
        /// Get named queries with optional filtering by name and owner
        /// </summary>
        /// <param name="name">Optional name to filter queries</param>
        /// <param name="owner">Optional owner to filter queries</param>
        /// <returns>List of named queries</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<NamedQueryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<NamedQueryDto>>> GetAll([FromQuery] string? name = null, [FromQuery] string? owner = null, [FromQuery] string? entity = null)
        {
            _log.LogDebug("REST request to get NamedQueries with filters: name={Name}, owner={Owner}, entity={Entity}", name, owner, entity);
            var currentUser = await _userService.GetUserWithUserRoles();
            if (currentUser == null)
                return Unauthorized();
            var isAdmin = currentUser.UserRoles?.Any(ur => ur.Role != null && ur.Role.Name == "ROLE_ADMIN") == true; 
            IEnumerable<NamedQuery>? found = null;
            if ((string.IsNullOrEmpty(name) && string.IsNullOrEmpty(owner)) || (isAdmin && !string.IsNullOrEmpty(owner) && owner.StartsWith("*") && string.IsNullOrEmpty(name)))
            {
                found = (await _namedQueryService.FindByOwner(string.IsNullOrEmpty(owner) ? currentUser.Login! : owner.Substring(1), entity)).ToList().OrderBy(nq=>nq.Name).ThenBy(nq=> nq.Owner == "GLOBAL" || nq.Owner == "SYSTEM" ? 1 : 0);
                return Ok(found!.Select(_mapper.Map<NamedQueryDto>));
            }
            if (!isAdmin)
            {
                throw new UnauthorizedAccessException("Only administrators can requet Named Queries by name or userid");
            }
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(owner))
            {
                NamedQuery? namedQuery = await _namedQueryService.FindByNameAndOwner(name!.ToUpper(), owner, entity);
                if (namedQuery == null)
                {
                    return NotFound();
                }
                return Ok(new List<NamedQuery?>{ namedQuery }.Where(n => n != null).Select(_mapper.Map<NamedQueryDto>).ToList()[0]);
            }
            if (!string.IsNullOrEmpty(owner))
            {
                found = (await _namedQueryService.FindBySelectedOwner(owner, entity)).OrderBy(nq=>nq.Name).ThenBy(nq=> nq.Owner == "GLOBAL" || nq.Owner == "SYSTEM" ? 1 : 0);
            }
            else // by name
            {
                found = (await _namedQueryService.FindByName(name!.ToUpper(), entity)).OrderBy(nq=>nq.Name).ThenBy(nq=> nq.Owner == "GLOBAL" || nq.Owner == "SYSTEM" ? 1 : 0);
            }
            return Ok(found!.Select(_mapper.Map<NamedQueryDto>));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<NamedQueryDto>> Get(long id)
        {
            _log.LogDebug("REST request to get NamedQuery : {Id}", id);
            var namedquery = await _namedQueryService.FindOne(id);
            if (namedquery == null)
            {
                return NotFound();
            }
            var dto = new NamedQueryDto {
                Id = namedquery.Id,
                Name = namedquery.Name,
                Text = namedquery.Text,
                Owner = namedquery.Owner,
                Entity = namedquery.Entity
            };
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<NamedQueryDto>> Create([FromBody] NamedQueryDto namedQueryDto)
        {
            _log.LogDebug("REST request to save NamedQuery : {NamedQuery}", namedQueryDto);
            var namedquery = new NamedQuery {
                Name = namedQueryDto.Name,
                Text = namedQueryDto.Text,
                Owner = namedQueryDto.Owner,
                Entity = namedQueryDto.Entity
            };
            var saved = await _namedQueryService.Save(namedquery);
            var dto = new NamedQueryDto {
                Id = saved.Id,
                Name = saved.Name,
                Text = saved.Text,
                Owner = saved.Owner,
                Entity = saved.Entity
            };
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] NamedQueryDto namedQueryDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _log.LogDebug("REST request to update NamedQuery : {NamedQuery}", namedQueryDto);
            var existingNamedQuery = await _namedQueryService.FindOne(id);
            if (existingNamedQuery == null)
            {
                return NotFound();
            }

            string? ownerCandidate = namedQueryDto.Owner ?? existingNamedQuery.Owner;
            if (ownerCandidate == null)
            {
                return BadRequest("Owner cannot be null.");
            }
            string owner = ownerCandidate;
            var namedquery = new NamedQuery {
                Id = id,
                Name = namedQueryDto.Name!,
                Text = namedQueryDto.Text!,
                Owner = owner,
                Entity = namedQueryDto.Entity ?? existingNamedQuery.Entity
            };
            await _namedQueryService.Save(namedquery);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            _log.LogDebug("REST request to delete NamedQuery : {Id}", id);
            try
            {
                await _namedQueryService.Delete(id);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            return NoContent();
        }

        [HttpGet("by-owner/{owner}")]
        public async Task<ActionResult<IEnumerable<NamedQuery>>> GetNamedQueriesByOwner(string owner, [FromQuery] string? entity = null)
        {
            if (string.IsNullOrEmpty(owner))
            {
                return BadRequest("Owner cannot be null or empty.");
            }
            var namedQueries = await _namedQueryService.FindByOwner(owner, entity);
            return Ok(namedQueries);
        }
    }
}