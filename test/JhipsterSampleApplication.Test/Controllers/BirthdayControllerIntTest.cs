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
using System.Collections.Generic;

namespace JhipsterSampleApplication.Test.Controllers
{
    public class BirthdaysControllerIntTest : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;
        private readonly HttpClient _client;
        private readonly IElasticClient _elasticClient;
        private readonly BirthdayDto _birthdayDto;

        public BirthdaysControllerIntTest(WebApplicationFactory<Startup> factory)
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

            // Clean up any existing records with the same last name using Birthdays client
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
            // 1. Test health endpoint
            Console.WriteLine($"<><><><><>Starting HEALTH test");
            var healthResponse = await _client.GetAsync("/api/Birthdays/health");
            healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var healthContent = await healthResponse.Content.ReadAsStringAsync();
            var healthResult = JsonConvert.DeserializeObject<ClusterHealthDto>(healthContent);
            healthResult.Status.Should().NotBeNullOrEmpty();
            healthResult.NumberOfNodes.Should().BeGreaterThan(0);
            Console.WriteLine($"<><><><><>health response: {healthContent}");

            // 2. Create a new record (POST /api/Birthdays)
            Console.WriteLine($"<><><><><>Starting CREATE test");
            var createResponse = await _client.PostAsync(
                "/api/Birthdays",
                TestUtil.ToJsonContent(_birthdayDto));
            
            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var createContent = await createResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"<><><><><>Create response: {createContent}");
            
            // Wait for the indexing to finish
            _elasticClient.Indices.Refresh("birthdays");

            // 3. Search with Lucene query (GET /api/Birthdays/search/lucene)
            Console.WriteLine($"<><><><><>Starting LUCENE test");
            var luceneQuery = $"lname:{_birthdayDto.Lname}";
            var luceneResponse = await _client.GetAsync($"/api/Birthdays/search/lucene?query={Uri.EscapeDataString(luceneQuery)}");
            luceneResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var luceneContent = await luceneResponse.Content.ReadAsStringAsync();
            var luceneResult = JsonConvert.DeserializeObject<SearchResult<BirthdayDto>>(luceneContent);
            luceneResult.Hits.Should().NotBeEmpty("Lucene search should find the test birthday");
            luceneResult.Hits.First().Lname.Should().Be(_birthdayDto.Lname);
            Console.WriteLine($"<><><><><>Lucene response: {luceneContent}");

            // 4. Search with raw query (POST /api/Birthdays/search/elasticsearch)
            Console.WriteLine($"<><><><><>Starting RAW test");
            var rawQuery = new
            {
                query = new
                {
                    term = new Dictionary<string, object>
                    {
                        { "lname.keyword",  _birthdayDto.Lname }
                    }
                }
            };
            var rawResponse = await _client.PostAsync(
                "/api/Birthdays/search/elasticsearch",
                TestUtil.ToJsonContent(rawQuery));
            rawResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var rawContent = await rawResponse.Content.ReadAsStringAsync();
            var rawResult = JsonConvert.DeserializeObject<SearchResult<BirthdayDto>>(rawContent);
            rawResult.Hits.Should().NotBeEmpty("Raw search should find the test birthday");
            rawResult.Hits.First().Lname.Should().Be(_birthdayDto.Lname);
            Console.WriteLine($"<><><><><>Lucene response: {rawContent}");

            // 5. Search with ruleset (POST /api/Birthdays/search/ruleset)
            Console.WriteLine($"<><><><><>Starting RULE test");
            var rulesetQuery = new Ruleset()
            {
                field = "lname",
                @operator = "=",
                value = _birthdayDto.Lname
            };
            var rulesetResponse = await _client.PostAsync(
                "/api/Birthdays/search/ruleset",
                TestUtil.ToJsonContent(rulesetQuery));
            rulesetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var rulesetContent = await rulesetResponse.Content.ReadAsStringAsync();
            var rulesetResult = JsonConvert.DeserializeObject<SearchResult<BirthdayDto>>(rulesetContent);
            rulesetResult.Hits.Should().NotBeEmpty("Ruleset search should find the test birthday");
            rulesetResult.Hits.First().Lname.Should().Be(_birthdayDto.Lname);
            Console.WriteLine($"<><><><><>Rule response: {rulesetContent}");

            // set up for the next test
            var retrievedBirthdayDto = rulesetResult.Hits.First();

            // 6. Get by ID (GET /api/Birthdays/{id})
            Console.WriteLine($"<><><><><>Starting GET test");
            var getResponse = await _client.GetAsync($"/api/Birthdays/{retrievedBirthdayDto.Id}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var getResult = JsonConvert.DeserializeObject<BirthdayDto>(getContent);
            getResult.Lname.Should().Be(_birthdayDto.Lname);
            Console.WriteLine($"<><><><><>Get response: {getContent}");

            // 7. Update record (PUT /api/Birthdays/{id})
            Console.WriteLine($"<><><><><>Starting UPDATE test");
            var updatedBirthday = new BirthdayDto
            {
                Id = _birthdayDto.Id,
                Lname = _birthdayDto.Lname,
                Fname = "UpdatedFirstName",
                Sign = _birthdayDto.Sign,
                Dob = _birthdayDto.Dob,
                IsAlive = _birthdayDto.IsAlive,
                Text = _birthdayDto.Text,
                Wikipedia = _birthdayDto.Wikipedia
            };
            var updateResponse = await _client.PutAsync(
                $"/api/Birthdays/{_birthdayDto.Id}",
                TestUtil.ToJsonContent(updatedBirthday));
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            Console.WriteLine($"<><><><><>Update response: {updateResponse.Content.ReadAsStringAsync()}");

            // Wait for the update to finish
            _elasticClient.Indices.Refresh("birthdays");            

            // 8. Get unique values (GET /api/Birthdays/unique-values/{field})
            Console.WriteLine($"<><><><><>Starting UNIQUE test");
            var uniqueValuesResponse = await _client.GetAsync("/api/Birthdays/unique-values/fname");
            uniqueValuesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var uniqueValuesContent = await uniqueValuesResponse.Content.ReadAsStringAsync();
            var uniqueValues = JsonConvert.DeserializeObject<string[]>(uniqueValuesContent);
            uniqueValues.Should().Contain("UpdatedFirstName");
            Console.WriteLine($"<><><><><>Unique response: {uniqueValuesContent}");

            // 9. Delete record (DELETE /api/Birthdays/{id})
            Console.WriteLine($"<><><><><>Starting DELETE test");
            var deleteResponse = await _client.DeleteAsync($"/api/Birthdays/{_birthdayDto.Id}");
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            Console.WriteLine($"<><><><><>Delete response: {deleteResponse.Content.ReadAsStringAsync()}");

            // Wait for the deletion to finish
            _elasticClient.Indices.Refresh("birthdays");

            // Verify deletion
            var verifyDeleteResponse = await _client.GetAsync($"/api/Birthdays/{_birthdayDto.Id}");
            verifyDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
                private class SearchResult<T>
        {
            public System.Collections.Generic.List<T> Hits { get; set; }
        }

        private class BirthdayDto
        {
            public string Id { get; set; }
            public string Lname { get; set; }
            public string Fname { get; set; }
            public string Sign { get; set; }
            public DateTime? Dob { get; set; }
            public bool? IsAlive { get; set; }
            public string Text { get; set; }
            public string Wikipedia { get; set; }
        }

        private class ClusterHealthDto
        {
            public string Status { get; set; }
            public int NumberOfNodes { get; set; }
            public int NumberOfDataNodes { get; set; }
            public int ActiveShards { get; set; }
            public int ActivePrimaryShards { get; set; }
        }
    }
} 