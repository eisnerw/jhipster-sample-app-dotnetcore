using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Test.Setup;
using Nest;
using Newtonsoft.Json;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace JhipsterSampleApplication.Test.Controllers
{
    public class ElasticSearchControllerIntTest : IClassFixture<WebApplicationFactoryFixture>
    {
        private readonly WebApplicationFactoryFixture _factory;
        private readonly HttpClient _client;
        private readonly IElasticClient _elasticClient;
        private readonly BirthdayDto _birthdayDto;

        public ElasticSearchControllerIntTest(WebApplicationFactoryFixture factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
            _elasticClient = factory.GetRequiredService<IElasticClient>();

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
                .Query(q => q.Raw($@"
                {{
                ""match"": {{
                    ""lname"": {{
                    ""query"": ""{_birthdayDto.Lname}""
                    }}
                }}
                }}
                "))
            );
            Console.WriteLine($"Delete by query response: {deleteResponse.DebugInformation}");
            Console.WriteLine($"Deleted {deleteResponse.Deleted} documents");

            // Wait for delete operation to complete
            _elasticClient.Indices.Refresh("birthdays");

            // Index the test birthday using the controller's POST endpoint
            var response = _client.PostAsync(
                "/api/elasticsearch",
                TestUtil.ToJsonContent(_birthdayDto)).Result;
            
            var responseContent = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine($"Index response: {responseContent}");
            
            response.StatusCode.Should().Be(HttpStatusCode.OK, "Indexing the test birthday should succeed");

            _elasticClient.Indices.Refresh("birthdays");
        }

        [Fact]
        public async Task SearchWithLuceneQuery()
        {
            // Arrange
            var query = $"lname:{_birthdayDto.Lname}";

            // Act
            var response = await _client.GetAsync($"/api/elasticsearch/search/lucene?query={Uri.EscapeDataString(query)}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Lucene search API response: {content}");
            
            var result = JsonConvert.DeserializeObject<SearchResult<BirthdayDto>>(content);
            result.Hits.Should().NotBeEmpty("Search should find the test birthday");
            result.Hits.First().Lname.Should().Be(_birthdayDto.Lname);
        }

        [Fact]
        public async Task SearchWithRawQuery()
        {
            // Arrange
            var searchQuery = new
            {
                query = new
                {
                    match = new
                    {
                        lname = new
                        {
                            query = _birthdayDto.Lname
                        }
                    }
                }
            };

            // Act
            var response = await _client.PostAsync(
                "/api/elasticsearch/search/raw",
                TestUtil.ToJsonContent(searchQuery));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Raw search API response: {content}");
            
            var result = JsonConvert.DeserializeObject<SearchResult<BirthdayDto>>(content);
            result.Hits.Should().NotBeEmpty("Search should find the test birthday");
            result.Hits.First().Lname.Should().Be(_birthdayDto.Lname);
        }

        [Fact]
        public async Task GetHealth()
        {
            // Act
            var response = await _client.GetAsync("/api/elasticsearch/health");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Health check response: {content}");
            
            var result = JsonConvert.DeserializeObject<ClusterHealthDto>(content);
            result.Status.Should().NotBeNullOrEmpty();
            result.NumberOfNodes.Should().BeGreaterThan(0);
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