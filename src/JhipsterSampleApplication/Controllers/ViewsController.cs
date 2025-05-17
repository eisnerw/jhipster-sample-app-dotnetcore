using System.Collections.Generic;
using System.Threading.Tasks;
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
    public class ViewsController : ControllerBase
    {
        private readonly IViewService _viewService;
        private readonly ILogger<ViewsController> _log;

        public ViewsController(IViewService viewService, ILogger<ViewsController> log)
        {
            _viewService = viewService;
            _log = log;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ViewDto>>> GetAll()
        {
            _log.LogDebug("REST request to get all Views");
            var views = await _viewService.GetAllAsync();
            return Ok(views);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ViewDto>> Get(string id)
        {
            _log.LogDebug($"REST request to get View : {id}");
            var view = await _viewService.GetByIdAsync(id);
            if (view == null)
            {
                return NotFound();
            }
            return Ok(view);
        }

        [HttpPost]
        public async Task<ActionResult<ViewDto>> Create([FromBody] ViewDto viewDto)
        {
            _log.LogDebug($"REST request to save View : {viewDto}");
            if (string.IsNullOrEmpty(viewDto.Id))
            {
                return BadRequest("View ID is required");
            }
            var view = await _viewService.CreateAsync(viewDto);
            return CreatedAtAction(nameof(Get), new { id = view.Id }, view);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ViewDto viewDto)
        {
            _log.LogDebug($"REST request to update View : {viewDto}");
            if (id != viewDto.Id)
            {
                return BadRequest("Invalid ID");
            }
            await _viewService.UpdateAsync(viewDto);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            _log.LogDebug($"REST request to delete View : {id}");
            await _viewService.DeleteAsync(id);
            return NoContent();
        }
    }
} 