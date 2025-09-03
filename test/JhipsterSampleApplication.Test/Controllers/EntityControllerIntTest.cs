using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using JhipsterSampleApplication.Dto;
using JhipsterSampleApplication.Test.Setup;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using Xunit;

namespace JhipsterSampleApplication.Test.Controllers
{
    public class EntityControllerIntTest : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly HttpClient _client;

        public EntityControllerIntTest(WebApplicationFactory<Startup> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetEntities_ReturnsList()
        {
            var response = await _client.GetAsync("/api/entity");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            var entities = JsonConvert.DeserializeObject<List<EntityDto>>(content);
            entities.Should().NotBeNull();
            entities.Should().NotBeEmpty();
            entities.Should().Contain(e => e.Name == "birthday");
        }

        [Fact]
        public async Task Health_ReturnsOk()
        {
            var response = await _client.GetAsync("/api/entity/birthday/health");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var dto = await response.Content.ReadFromJsonAsync<ClusterHealthDto>();
            dto.Should().NotBeNull();
            dto!.Status.Should().NotBeNullOrEmpty();
        }
    }
}
