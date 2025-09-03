using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Dto;

namespace JhipsterSampleApplication.Controllers
{
    /// <summary>
    /// Generic controller for serving entity specifications and basic operations.
    /// </summary>
    [ApiController]
    [Route("api/entity")]
    public class EntityController : ControllerBase
    {
        /// <summary>
        /// Returns all entity specifications from the Resources/Entities folder.
        /// </summary>
        [HttpGet]
        [Produces("application/json")]
        public ActionResult<IEnumerable<EntityDto>> GetEntities()
        {
            var entities = new List<EntityDto>();
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Entities");
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path, "*.json"))
                {
                    var spec = JObject.Parse(System.IO.File.ReadAllText(file));
                    entities.Add(new EntityDto
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Spec = spec
                    });
                }
            }
            return Ok(entities);
        }

        /// <summary>
        /// Simple health endpoint for a given entity. Currently returns a static healthy response.
        /// </summary>
        [HttpGet("{entity}/health")]
        public ActionResult<ClusterHealthDto> GetHealth(string entity)
        {
            var dto = new ClusterHealthDto
            {
                Status = "green",
                NumberOfNodes = 1,
                NumberOfDataNodes = 1,
                ActiveShards = 1,
                ActivePrimaryShards = 1
            };
            return Ok(dto);
        }
    }
}
