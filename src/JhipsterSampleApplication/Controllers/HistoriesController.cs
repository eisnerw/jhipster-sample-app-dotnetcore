#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JhipsterSampleApplication.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class HistoriesController : ControllerBase
    {
        private readonly IHistoryService _historyService;
        private readonly ILogger<HistoriesController> _log;

        public HistoriesController(IHistoryService historyService, ILogger<HistoriesController> log)
        {
            _historyService = historyService;
            _log = log;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<HistoryDto>>> GetAll([FromQuery] string? entity = null)
        {
            // Admins: when no entity filter is provided, return ALL histories
            if (User.IsInRole("ROLE_ADMIN") && string.IsNullOrEmpty(entity))
            {
                var all = await _historyService.FindAll();
                return Ok(all.Select(h => new HistoryDto { Id = h.Id, User = h.User, Entity = h.Entity, Text = h.Text }));
            }

            // Non-admins or entity-scoped request: return only current user's history for the entity
            var user = User?.Identity?.Name;
            if (string.IsNullOrEmpty(user))
            {
                return Unauthorized();
            }
            if (string.IsNullOrEmpty(entity))
            {
                // Require entity for non-admin requests
                return Ok(Enumerable.Empty<HistoryDto>());
            }
            var histories = await _historyService.FindByUserAndEntity(user, entity);
            return Ok(histories.Select(h => new HistoryDto { Id = h.Id, User = h.User, Entity = h.Entity, Text = h.Text }));
        }

        [HttpPost]
        public async Task<ActionResult<HistoryDto>> Create([FromBody] HistoryDto historyDto)
        {
            var user = User?.Identity?.Name;
            if (string.IsNullOrEmpty(user))
            {
                return Unauthorized();
            }
            var history = new History { User = user, Entity = historyDto.Entity ?? string.Empty, Text = historyDto.Text };
            var saved = await _historyService.Save(history);
            var result = new HistoryDto { Id = saved.Id, User = saved.User, Entity = saved.Entity, Text = saved.Text };
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<HistoryDto>> Get(long id)
        {
            var user = User?.Identity?.Name;
            if (string.IsNullOrEmpty(user))
            {
                return Unauthorized();
            }
            var histories = await _historyService.FindByUserAndEntity(user, null);
            var history = histories.FirstOrDefault(h => h.Id == id);
            if (history == null)
            {
                return NotFound();
            }
            return Ok(new HistoryDto { Id = history.Id, User = history.User, Entity = history.Entity, Text = history.Text });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            // Basic delete via repository
            var user = User?.Identity?.Name;
            if (string.IsNullOrEmpty(user))
            {
                return Unauthorized();
            }
            var histories = await _historyService.FindByUserAndEntity(user, null);
            var history = histories.FirstOrDefault(h => h.Id == id);
            if (history == null)
            {
                return NotFound();
            }
            await _historyService.Save(new History { Id = id, User = history.User, Entity = history.Entity, Text = history.Text });
            return NoContent();
        }
    }
}
