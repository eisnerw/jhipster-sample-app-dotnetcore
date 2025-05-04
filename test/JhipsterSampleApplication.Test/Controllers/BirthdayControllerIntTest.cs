using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using JhipsterSampleApplication.Test.Setup;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using Xunit;
using Nest;
using JhipsterSampleApplication.Dto;
using JhipsterSampleApplication.Controllers;
using JhipsterSampleApplication.Domain.Entities;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace JhipsterSampleApplication.Test.Controllers
{
    public class BirthdayControllerIntTest : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;
        private readonly HttpClient _client;
        private readonly IElasticClient _elasticClient;
        private readonly BirthdayDto _birthdayDto;

        public BirthdayControllerIntTest(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
            _elasticClient = factory.Services.GetRequiredService<IElasticClient>();

            // Create a test birthday entity
            _birthdayDto = new BirthdayDto
            {
                Id = Guid.NewGuid().ToString(),
                Lname = "TestLastName",
                Fname = "TestFirstName",
                Sign = "TestSign",
                Dob = DateTime.Now,
                IsAlive = true,
                Text = "Test text for search",
                Wikipedia = "<p>good guy</p>"
            };

            // Clean up any existing records with the same last name using Elasticsearch client
            var deleteResponse = _elasticClient.DeleteByQuery<Birthday>(d => d
                .Index("birthdays")
                .Query(q => q.Term(t => t.Field("lname.keyword").Value(_birthdayDto.Lname))));
            
            Console.WriteLine($"<><><><><>Delete by query response: {deleteResponse.DebugInformation}");
            Console.WriteLine($"<><><><><>Deleted {deleteResponse.Deleted} documents");

            // Wait for delete operation to complete
            _elasticClient.Indices.Refresh("birthdays");
        }

        [Fact]
        public async Task TestCreateAndGetBirthday()
        {
            // Create a new birthday
            var createResponse = await _client.PostAsJsonAsync("/api/birthdays", _birthdayDto);
            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Get the created birthday
            var getResponse = await _client.GetAsync($"/api/birthdays/{_birthdayDto.Id}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var responseContent = await getResponse.Content.ReadAsStringAsync();
            var retrievedBirthday = JsonConvert.DeserializeObject<BirthdayDto>(responseContent);
            retrievedBirthday.Should().NotBeNull();
            retrievedBirthday.Lname.Should().Be(_birthdayDto.Lname);
            retrievedBirthday.Fname.Should().Be(_birthdayDto.Fname);
            retrievedBirthday.Sign.Should().Be(_birthdayDto.Sign);
        }

        [Fact]
        public async Task TestUpdateBirthday()
        {
            // Create a new birthday
            var createResponse = await _client.PostAsJsonAsync("/api/birthdays", _birthdayDto);
            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Update the birthday
            _birthdayDto.Fname = "UpdatedFirstName";
            var updateResponse = await _client.PutAsJsonAsync($"/api/birthdays/{_birthdayDto.Id}", _birthdayDto);
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Get the updated birthday
            var getResponse = await _client.GetAsync($"/api/birthdays/{_birthdayDto.Id}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var responseContent = await getResponse.Content.ReadAsStringAsync();
            var retrievedBirthday = JsonConvert.DeserializeObject<BirthdayDto>(responseContent);
            retrievedBirthday.Should().NotBeNull();
            retrievedBirthday.Fname.Should().Be("UpdatedFirstName");
        }

        [Fact]
        public async Task TestDeleteBirthday()
        {
            // Create a new birthday
            var createResponse = await _client.PostAsJsonAsync("/api/birthdays", _birthdayDto);
            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Delete the birthday
            var deleteResponse = await _client.DeleteAsync($"/api/birthdays/{_birthdayDto.Id}");
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Try to get the deleted birthday
            var getResponse = await _client.GetAsync($"/api/birthdays/{_birthdayDto.Id}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
} 