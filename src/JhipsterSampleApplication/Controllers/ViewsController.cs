using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using JhipsterSampleApplication.Domain.Services;
using JhipsterSampleApplication.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JhipsterSampleApplication.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ViewsController : ControllerBase
    {
        private readonly IEntitySpecRegistry _specRegistry;

        public ViewsController(IEntitySpecRegistry specRegistry)
        {
            _specRegistry = specRegistry;
        }

        // Minimal shim for legacy front-end: GET /api/views?entity={entity}
        // Returns views defined in Resources/Entities/{entity}.json (queryBuilder/views section)
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ViewDto>), 200)]
        public ActionResult<IEnumerable<ViewDto>> GetAll([FromQuery] string entity)
        {
            if (string.IsNullOrWhiteSpace(entity))
            {
                return BadRequest("Query parameter 'entity' is required.");
            }

            if (!_specRegistry.TryGetArray(entity, "views", out var arr))
            {
                return Ok(Enumerable.Empty<ViewDto>());
            }

            var list = arr
                .OfType<JsonObject>()
                .Select(Map)
                .ToList();
            return Ok(list);
        }

        private static ViewDto Map(JsonObject o)
        {
            return new ViewDto
            {
                Id = o["id"]?.GetValue<string>(),
                Name = o["name"]?.GetValue<string>(),
                Field = o["field"]?.GetValue<string>(),
                Aggregation = o["aggregation"]?.GetValue<string>(),
                Query = o["query"]?.GetValue<string>(),
                CategoryQuery = o["categoryQuery"]?.GetValue<string>(),
                Script = o["script"]?.GetValue<string>(),
                parentViewId = o["parentViewId"]?.GetValue<string>(),
                Entity = o["entity"]?.GetValue<string>()
            };
        }
    }
}

