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
            var luceneResult = JsonConvert.DeserializeObject<SearchResultDto<BirthdayDto>>(luceneContent);
            luceneResult.Hits.Should().NotBeEmpty("Lucene search should find the test birthday");
            luceneResult.Hits.First().Lname.Should().Be(_birthdayDto.Lname);
            Console.WriteLine($"<><><><><>Lucene response: {luceneContent}");

            // 4. Search with raw query (POST /api/Birthdays/search/elasticsearch)
            Console.WriteLine($"<><><><><>Starting RAW test");
            var rawQuery = new
            {

                term = new Dictionary<string, object>
                {
                    { "lname.keyword",  _birthdayDto.Lname }
                }

            };
            var rawResponse = await _client.PostAsync(
                "/api/Birthdays/search/elasticsearch?pageSize=20&from=0&includeWikipedia=false",
                TestUtil.ToJsonContent(rawQuery));
            rawResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var rawContent = await rawResponse.Content.ReadAsStringAsync();
            var rawResult = JsonConvert.DeserializeObject<SearchResultDto<BirthdayDto>>(rawContent);
            rawResult.Hits.Should().NotBeEmpty("Raw search should find the test birthday");
            rawResult.Hits.First().Lname.Should().Be(_birthdayDto.Lname);
            Console.WriteLine($"<><><><><>Raw response: {rawContent}");

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
            var rulesetResult = JsonConvert.DeserializeObject<SearchResultDto<BirthdayDto>>(rulesetContent);
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

        [Fact]
        public async Task TestBqlOperations()
        {
            // Create test data
            var testBirthday = new BirthdayDto
            {
                Id = Guid.NewGuid().ToString(),
                Lname = "TestBqlLastName",
                Fname = "TestBqlFirstName",
                Sign = "aries",
                Dob = new DateTime(1990, 1, 1),
                IsAlive = true,
                Text = "Test text for BQL search",
                Wikipedia = "<p>test person</p>"
            };

            // Clean up any existing records
            var deleteResponse = _elasticClient.DeleteByQuery<Birthday>(d => d
                .Index("birthdays")
                .Query(q => q.Term(t => t.Field("lname.keyword").Value(testBirthday.Lname))));
            
            _elasticClient.Indices.Refresh("birthdays");

            // Create test record
            var createResponse = await _client.PostAsync(
                "/api/Birthdays",
                TestUtil.ToJsonContent(testBirthday));
            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            _elasticClient.Indices.Refresh("birthdays");

            // 1. Test BQL to Ruleset conversion
            var bqlQuery = $"lname = {testBirthday.Lname.ToLower()}";
            var bql2RulesetResponse = await _client.PostAsync(
                "/api/Birthdays/bql-to-ruleset",
                TestUtil.ToTextContent(bqlQuery));

            string responseBody = await bql2RulesetResponse.Content.ReadAsStringAsync();

            bql2RulesetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var rulesetDto = await bql2RulesetResponse.Content.ReadFromJsonAsync<RulesetDto>();
            rulesetDto.Should().NotBeNull();
            rulesetDto.field.Should().Be("lname");
            rulesetDto.@operator.Should().Be("=");
            rulesetDto.value.Should().Be(testBirthday.Lname.ToLower());

            // 2. Test Ruleset to BQL conversion
            var ruleset2BqlResponse = await _client.PostAsync(
                "/api/Birthdays/ruleset-to-bql",
                TestUtil.ToJsonContent(rulesetDto));
            ruleset2BqlResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var bqlResult = await ruleset2BqlResponse.Content.ReadAsStringAsync();
            bqlResult.Should().NotBeNull();
            bqlResult.Should().Be(bqlQuery);

            // 3. Test Ruleset to Elasticsearch conversion
            var ruleset2EsResponse = await _client.PostAsync(
                "/api/Birthdays/ruleset-to-elasticsearch",
                TestUtil.ToJsonContent(rulesetDto));
            ruleset2EsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var esQuery = await ruleset2EsResponse.Content.ReadFromJsonAsync<object>();
            esQuery.Should().NotBeNull();

            // 4. Test BQL search
            var bqlSearchResponse = await _client.PostAsync(
                "/api/Birthdays/search/bql",
                TestUtil.ToTextContent(bqlQuery));
            bqlSearchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var searchResult = await bqlSearchResponse.Content.ReadFromJsonAsync<SearchResultDto<BirthdayDto>>();
            searchResult.Should().NotBeNull();
            searchResult.Hits.Should().NotBeEmpty();
            searchResult.Hits.First().Lname.Should().Be(testBirthday.Lname);

            // Clean up
            var deleteTestResponse = await _client.DeleteAsync($"/api/Birthdays/{testBirthday.Id}");
            deleteTestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            _elasticClient.Indices.Refresh("birthdays");
        }

        [Fact]
        public async Task TestComplexBqlOperations()
        {
            // Create test data
            var testBirthday1 = new BirthdayDto
            {
                Id = Guid.NewGuid().ToString(),
                Lname = "ComplexTest1",
                Fname = "Test1",
                Sign = "aries",
                Dob = new DateTime(1990, 1, 1),
                IsAlive = true
            };

            var testBirthday2 = new BirthdayDto
            {
                Id = Guid.NewGuid().ToString(),
                Lname = "ComplexTest2",
                Fname = "Test2",
                Sign = "taurus",
                Dob = new DateTime(1991, 2, 2),
                IsAlive = false
            };

            // Clean up any existing records
            var deleteResponse = _elasticClient.DeleteByQuery<Birthday>(d => d
                .Index("birthdays")
                .Query(q => q.Terms(t => t.Field("lname.keyword").Terms(new[] { testBirthday1.Lname, testBirthday2.Lname }))));
            
            _elasticClient.Indices.Refresh("birthdays");

            // Create test records
            await _client.PostAsync("/api/Birthdays", TestUtil.ToJsonContent(testBirthday1));
            await _client.PostAsync("/api/Birthdays", TestUtil.ToJsonContent(testBirthday2));
            _elasticClient.Indices.Refresh("birthdays");

            // Test complex BQL query with AND/OR operators
            var complexBqlQuery = $"(lname = {testBirthday1.Lname.ToLower()} & sign = aries) | (lname = {testBirthday2.Lname.ToLower()} & sign = taurus)";
            
            // Test BQL to Ruleset conversion
            var bql2RulesetResponse = await _client.PostAsync(
                "/api/Birthdays/bql-to-ruleset",
                TestUtil.ToTextContent(complexBqlQuery));
            bql2RulesetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var rulesetDto = await bql2RulesetResponse.Content.ReadFromJsonAsync<RulesetDto>();
            rulesetDto.Should().NotBeNull();
            rulesetDto.condition.Should().Be("or");
            rulesetDto.rules.Should().HaveCount(2);

            // Test Ruleset to BQL conversion
            var ruleset2BqlResponse = await _client.PostAsync(
                "/api/Birthdays/ruleset-to-bql",
                TestUtil.ToJsonContent(rulesetDto));
            ruleset2BqlResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var bqlResult = await ruleset2BqlResponse.Content.ReadAsStringAsync();
            bqlResult.Should().NotBeNull();
            bqlResult.Should().Be(complexBqlQuery);
  
            // Test BQL search with complex query
            var bqlSearchResponse = await _client.PostAsync(
                "/api/Birthdays/search/bql",
                TestUtil.ToTextContent(complexBqlQuery));
            bqlSearchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var searchResult = await bqlSearchResponse.Content.ReadFromJsonAsync<SearchResultDto<BirthdayDto>>();
            searchResult.Should().NotBeNull();
            searchResult.Hits.Should().HaveCount(2);
            searchResult.Hits.Should().Contain(h => h.Lname == testBirthday1.Lname);
            searchResult.Hits.Should().Contain(h => h.Lname == testBirthday2.Lname);

            // Clean up
            await _client.DeleteAsync($"/api/Birthdays/{testBirthday1.Id}");
            await _client.DeleteAsync($"/api/Birthdays/{testBirthday2.Id}");
            _elasticClient.Indices.Refresh("birthdays");
        }

        [Fact]
        public async Task TestViewOperations()
        {
            // Clean up any existing test objects
            Console.WriteLine($"<><><><><>Starting TestViewOperations (cleanup & create new docs)");
            var deleteResponse = _elasticClient.DeleteByQuery<Birthday>(d => d
                .Index("birthdays")
                .Query(q => q.Term(t => t.Field("fname.keyword").Value("viewTestObject"))));
            
            _elasticClient.Indices.Refresh("birthdays");
            Console.WriteLine($"<><><><><>Deleted {deleteResponse.Deleted} documents");

            // Create test objects
            var testObjects = new[]
            {
                new BirthdayDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Fname = "viewTestObject",
                    Lname = "test1",
                    Dob = new DateTime(1900, 1, 1),
                    Sign = "sign1"
                },
                new BirthdayDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Fname = "viewTestObject",
                    Lname = "test2",
                    Dob = new DateTime(1800, 1, 1),
                    Sign = "sign1"
                },
                new BirthdayDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Fname = "viewTestObject",
                    Lname = "test1",
                    Dob = new DateTime(1800, 1, 1),
                    Sign = "sign2"
                }
            };

            // Create all test objects
            foreach (var obj in testObjects)
            {
                var createResponse = await _client.PostAsync(
                    "/api/Birthdays",
                    TestUtil.ToJsonContent(obj));
                createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            }
            _elasticClient.Indices.Refresh("birthdays");
            Console.WriteLine($"<><><><><>Documents created. Starting BQL query just view");

            // Test 1: Search with view "sign/birth year" and query "lname = test1"
            var bqlQuery = "lname = test1";
            var viewResponse = await _client.PostAsync(
                $"/api/Birthdays/search/bql?view=sign/birth%20year",
                TestUtil.ToTextContent(bqlQuery));
            viewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var viewContent = await viewResponse.Content.ReadAsStringAsync();
            var viewResult = JsonConvert.DeserializeObject<SearchResultDto<ViewResultDto>>(viewContent);
            viewResult.Should().NotBeNull();
            viewResult.HitType.Should().Be("view");
            viewResult.ViewName.Should().Be("sign/birth year");
            viewResult.Hits.Should().HaveCount(2);
            viewResult.Hits.Should().Contain(r => r.CategoryName == "sign1" && r.Count == 1);
            viewResult.Hits.Should().Contain(r => r.CategoryName == "sign2" && r.Count == 1);
            Console.WriteLine($"<><><><><>BQL with sign/birth year resulted in {viewContent}.  Starting test with added category parameter.");

            // Test 2: Add category "sign1"
            var categoryResponse = await _client.PostAsync(
                $"/api/Birthdays/search/bql?view=sign/birth%20year&category=sign1",
                TestUtil.ToTextContent(bqlQuery));
            categoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var categoryContent = await categoryResponse.Content.ReadAsStringAsync();
            var categoryResult = JsonConvert.DeserializeObject<SearchResultDto<ViewResultDto>>(categoryContent);
            categoryResult.Should().NotBeNull();
            categoryResult.HitType.Should().Be("view");
            categoryResult.ViewName.Should().Be("sign/birth year");
            categoryResult.viewCategory.Should().Be("sign1");
            categoryResult.Hits.Should().HaveCount(1);
            categoryResult.Hits.Should().Contain(r => r.CategoryName == "1900" && r.Count == 1);
            Console.WriteLine($"<><><><><>Category resulted in {categoryContent}.  Adding secondaryCategory ");
            // Test 3: Add secondary category "1900"
            var secondaryCategoryResponse = await _client.PostAsync(
                $"/api/Birthdays/search/bql?view=sign/birth%20year&category=sign1&secondaryCategory=1900",
                TestUtil.ToTextContent(bqlQuery));
            secondaryCategoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var secondaryCategoryContent = await secondaryCategoryResponse.Content.ReadAsStringAsync();
            var secondaryCategoryResult = JsonConvert.DeserializeObject<SearchResultDto<BirthdayDto>>(secondaryCategoryContent);
            secondaryCategoryResult.HitType.Should().Be("hit");
            secondaryCategoryResult.Hits.Should().NotBeEmpty("Search should find the test1");
            secondaryCategoryResult.Hits.First().Lname.Should().Be("test1");
            secondaryCategoryResult.Hits.First().Sign.Should().Be("sign1");
            secondaryCategoryResult.Hits.First().Dob.Should().Be(new DateTime(1900, 1, 1));
            Console.WriteLine($"<><><><><>secondaryCategory resulted in {secondaryCategoryContent}.  Cleaning up.");
            // Clean up test objects
            deleteResponse = _elasticClient.DeleteByQuery<Birthday>(d => d
                .Index("birthdays")
                .Query(q => q.Term(t => t.Field("fname.keyword").Value("viewTestObject"))));  
            _elasticClient.Indices.Refresh("birthdays");
            Console.WriteLine($"<><><><><>Deleted {deleteResponse.Deleted} documents.  Done with test.");
        }
        [Fact]
        public async Task TestUncategorizedFirstNameView()
        {
            var deleteResponse = _elasticClient.DeleteByQuery<Birthday>(d => d
                .Index("birthdays")
                .Query(q => q.Term(t => t.Field("sign.keyword").Value("uncatsign"))));
            _elasticClient.Indices.Refresh("birthdays");

            var noFname = new BirthdayDto
            {
                Id = Guid.NewGuid().ToString(),
                Lname = "nofname",
                Sign = "uncatsign"
            };
            var withFname = new BirthdayDto
            {
                Id = Guid.NewGuid().ToString(),
                Fname = "alpha",
                Lname = "withfname",
                Sign = "uncatsign"
            };

            await _client.PostAsync("/api/Birthdays", TestUtil.ToJsonContent(noFname));
            await _client.PostAsync("/api/Birthdays", TestUtil.ToJsonContent(withFname));
            _elasticClient.Indices.Refresh("birthdays");

            var bqlQuery = "sign = uncatsign";
            var viewResponse = await _client.PostAsync(
                "/api/Birthdays/search/bql?view=First%20Name",
                TestUtil.ToTextContent(bqlQuery));
            viewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var viewContent = await viewResponse.Content.ReadAsStringAsync();
            var viewResult = JsonConvert.DeserializeObject<SearchResultDto<ViewResultDto>>(viewContent);
            viewResult.Hits.Should().Contain(r => r.CategoryName == "(Uncategorized)" && r.Count == 1);

            var categoryResponse = await _client.PostAsync(
                "/api/Birthdays/search/bql?view=First%20Name&category=(Uncategorized)",
                TestUtil.ToTextContent(bqlQuery));
            categoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var categoryContent = await categoryResponse.Content.ReadAsStringAsync();
            var categoryResult = JsonConvert.DeserializeObject<SearchResultDto<BirthdayDto>>(categoryContent);
            categoryResult.Hits.Should().HaveCount(1);
            categoryResult.Hits.First().Lname.Should().Be("nofname");

            await _client.DeleteAsync($"/api/Birthdays/{noFname.Id}");
            await _client.DeleteAsync($"/api/Birthdays/{withFname.Id}");
            _elasticClient.Indices.Refresh("birthdays");
        }

        [Fact]
        public async Task TestCategorizeMultiple()
        {
            var id1 = Guid.NewGuid().ToString();
            var id2 = Guid.NewGuid().ToString();
            var b1 = new BirthdayDto { Id = id1, Lname = "CatMulti", Fname = "One", Sign = "aries" };
            var b2 = new BirthdayDto { Id = id2, Lname = "CatMulti", Fname = "Two", Sign = "taurus" };

            // Cleanup any pre-existing
            var deleteResponse = _elasticClient.DeleteByQuery<Birthday>(d => d
                .Index("birthdays")
                .Query(q => q.Terms(t => t.Field("_id").Terms(new[] { id1, id2 }))));
            _elasticClient.Indices.Refresh("birthdays");

            // Create
            (await _client.PostAsync("/api/Birthdays", TestUtil.ToJsonContent(b1))).StatusCode.Should().Be(HttpStatusCode.OK);
            (await _client.PostAsync("/api/Birthdays", TestUtil.ToJsonContent(b2))).StatusCode.Should().Be(HttpStatusCode.OK);
            _elasticClient.Indices.Refresh("birthdays");

            // Add categories: add ["A","B"] to both
            var addReq = new {
                rows = new[] { id1, id2 },
                add = new[] { "A", "B" },
                remove = Array.Empty<string>()
            };
            var addResp = await _client.PostAsync("/api/Birthdays/categorize-multiple", TestUtil.ToJsonContent(addReq));
            addResp.StatusCode.Should().Be(HttpStatusCode.OK);
            _elasticClient.Indices.Refresh("birthdays");

            // Verify both have A and B
            var verifyQuery = new { term = new Dictionary<string, object> { { "lname.keyword", "CatMulti" } } };
            var verifyResp = await _client.PostAsync("/api/Birthdays/search/elasticsearch", TestUtil.ToJsonContent(verifyQuery));
            verifyResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var verifyContent = await verifyResp.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<SearchResultDto<JhipsterSampleApplication.Dto.BirthdayDto>>(verifyContent);
            result.Hits.Should().HaveCount(2);
            result.Hits.All(h => h.Categories != null && h.Categories.Contains("A") && h.Categories.Contains("B")).Should().BeTrue();

            // Remove B and add C (case-insensitive remove)
            var updateReq = new {
                rows = new[] { id1, id2 },
                add = new[] { "c" },
                remove = new[] { "b" }
            };
            var updateResp = await _client.PostAsync("/api/Birthdays/categorize-multiple", TestUtil.ToJsonContent(updateReq));
            updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
            _elasticClient.Indices.Refresh("birthdays");

            // Verify: A and C present, B removed
            var verifyResp2 = await _client.PostAsync("/api/Birthdays/search/elasticsearch", TestUtil.ToJsonContent(verifyQuery));
            verifyResp2.StatusCode.Should().Be(HttpStatusCode.OK);
            var verifyContent2 = await verifyResp2.Content.ReadAsStringAsync();
            var result2 = JsonConvert.DeserializeObject<SearchResultDto<JhipsterSampleApplication.Dto.BirthdayDto>>(verifyContent2);
            result2.Hits.Should().HaveCount(2);
            result2.Hits.All(h => h.Categories != null && h.Categories.Contains("A") && h.Categories.Contains("C") && !h.Categories.Contains("B")).Should().BeTrue();

            // Cleanup
            (await _client.DeleteAsync($"/api/Birthdays/{id1}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await _client.DeleteAsync($"/api/Birthdays/{id2}")).StatusCode.Should().Be(HttpStatusCode.OK);
            _elasticClient.Indices.Refresh("birthdays");
        }

        private class BirthdayDto
        {
            public string Id { get; set; }
            public string Lname { get; set; }
            public string? Fname { get; set; }
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

        private class BqlQueryDto
        {
            public string Query { get; set; } = string.Empty;
        }
    }
} 
