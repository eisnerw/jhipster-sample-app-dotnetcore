using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Web;
using Microsoft.AspNetCore.Http;

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

        /// <summary>
        /// Get all views, optionally filtered by domain
        /// </summary>
        /// <param name="domain">Optional domain name to filter views</param>
        /// <returns>List of views</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ViewDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ViewDto>>> GetAll([FromQuery] string domain = null)
        {
            _log.LogDebug("REST request to get Views" + (domain != null ? $" for domain: {domain}" : ""));
            
            if (!string.IsNullOrEmpty(domain))
            {
                var views = await _viewService.GetByDomainAsync(domain);
                return Ok(views);
            }
            
            var allViews = await _viewService.GetAllAsync();
            return Ok(allViews);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ViewDto>> Get(string id)
        {
            _log.LogDebug($"REST request to get View : {id}");
            var decodedId = HttpUtility.UrlDecode(id);
            var view = await _viewService.GetByIdAsync(decodedId);
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
            var view = await _viewService.CreateAsync(viewDto);
            return CreatedAtAction(nameof(Get), new { id = HttpUtility.UrlEncode(view.Id) }, view);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ViewDto viewDto)
        {
            _log.LogDebug($"REST request to update View : {viewDto}");
            var decodedId = HttpUtility.UrlDecode(id);
            if (decodedId != viewDto.Id)
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
            var decodedId = HttpUtility.UrlDecode(id);
            await _viewService.DeleteAsync(decodedId);
            return NoContent();
        }
    }
} 