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
        public async Task<ActionResult<IEnumerable<NamedQueryDto>>> GetAll([FromQuery] string? name = null, [FromQuery] string? owner = null)
        {
            _log.LogDebug("REST request to get NamedQueries with filters: name={Name}, owner={Owner}", name, owner);
            var currentUser = await _userService.GetUserWithUserRoles();
            if (currentUser == null)
                return Unauthorized();
            var isAdmin = currentUser.UserRoles?.Any(ur => ur.Role != null && ur.Role.Name == "ROLE_ADMIN") == true; 
            IEnumerable<NamedQuery>? found = null;
            if ((string.IsNullOrEmpty(name) && string.IsNullOrEmpty(owner)) || (isAdmin && !string.IsNullOrEmpty(owner) && owner.StartsWith("*") && string.IsNullOrEmpty(name)))
            {
                found = (await _namedQueryService.FindByOwner(string.IsNullOrEmpty(owner) ? currentUser.Login! : owner.Substring(1))).ToList().OrderBy(nq=>nq.Name).ThenBy(nq=> nq.Owner == "GLOBAL" || nq.Owner == "SYSTEM" ? 1 : 0);
                return Ok(found!.Select(_mapper.Map<NamedQueryDto>));
            }
            if (!isAdmin)
            {
                throw new UnauthorizedAccessException("Only administrators can requet Named Queries by name or userid");
            }
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(owner))
            {
                NamedQuery? namedQuery = await _namedQueryService.FindByNameAndOwner(name!.ToUpper(), owner!);
                if (namedQuery == null)
                {
                    return NotFound();
                }
                return Ok(new List<NamedQuery?>{ namedQuery }.Where(n => n != null).Select(_mapper.Map<NamedQueryDto>).ToList()[0]);
            }
            if (!string.IsNullOrEmpty(owner))
            {
                found = (await _namedQueryService.FindBySelectedOwner(owner)).OrderBy(nq=>nq.Name).ThenBy(nq=> nq.Owner == "GLOBAL" || nq.Owner == "SYSTEM" ? 1 : 0);
            }
            else // by name
            {
                found = (await _namedQueryService.FindByName(name!.ToUpper())).OrderBy(nq=>nq.Name).ThenBy(nq=> nq.Owner == "GLOBAL" || nq.Owner == "SYSTEM" ? 1 : 0);
            }
            return Ok(found!.Select(_mapper.Map<NamedQueryDto>));
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _log.LogDebug("REST request to update NamedQuery : {NamedQuery}", namedQueryDto);
            var existingEntity = await _namedQueryService.FindOne(id);
            if (existingEntity == null)
            {
                return NotFound();
            }

            string? ownerCandidate = namedQueryDto.Owner ?? existingEntity.Owner;
            if (ownerCandidate == null)
            {
                return BadRequest("Owner cannot be null.");
            }
            string owner = ownerCandidate;
            var entity = new NamedQuery {
                Id = id,
                Name = namedQueryDto.Name!,
                Text = namedQueryDto.Text!,
                Owner = owner
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

        [HttpGet("by-owner/{owner}")]
        public async Task<ActionResult<IEnumerable<NamedQuery>>> GetNamedQueriesByOwner(string owner)
        {
            if (string.IsNullOrEmpty(owner))
            {
                return BadRequest("Owner cannot be null or empty.");
            }
            var namedQueries = await _namedQueryService.FindByOwner(owner);
            return Ok(namedQueries);
        }
    }
} 