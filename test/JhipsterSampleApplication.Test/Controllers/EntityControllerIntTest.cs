using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using JhipsterSampleApplication.Test.Setup;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using JhipsterSampleApplication.Test.Helpers;
using Xunit;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using JhipsterSampleApplication.Dto;
using JhipsterSampleApplication.Controllers;
using JhipsterSampleApplication.Domain.Entities;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace JhipsterSampleApplication.Test.Controllers
{
    public class EntityControllerIntTest : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;
        private readonly HttpClient _client;
        private readonly ElasticsearchClient _elasticClient;
        private readonly JObject _birthday;
        private readonly string _birthdayId;

        public EntityControllerIntTest(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
            _elasticClient = factory.Services.GetRequiredService<ElasticsearchClient>();

            // Create a test birthday entity
            _birthdayId = Guid.NewGuid().ToString();
            _birthday = new JObject
            {
                ["id"] = _birthdayId,
                ["lname"] = "TestLastName",
                ["fname"] = "TestFirstName",
                ["sign"] = "TestSign",
                ["dob"] = DateTime.Now,
                ["isAlive"] = true,
                ["text"] = "Test text for search",
                ["wikipedia"] = "<p>good guy</p>"
            };

            InitializeAsync().GetAwaiter().GetResult();
        }

        private async Task InitializeAsync(){
            // Clean up any existing records with the same last name using Birthdays client
            var deleteResponse = await _elasticClient.DeleteByQueryAsync<object>("birthdays", d => d
                .Query(q => q.Term(t => t.Field("lname.keyword").Value(_birthday["lname"]!.ToString()))));
            Console.WriteLine($"<><><><><>Deleted {deleteResponse.Deleted} documents");

            // Wait for delete operation to complete
            await _elasticClient.Indices.RefreshAsync("birthdays");
        }

        [Fact]
        public async Task TestCreateAndGetBirthday()
        {
            // 1. Test health endpoint
            Console.WriteLine($"<><><><><>Starting HEALTH test");
            var healthResponse = await _client.GetAsync("/api/entity/health");
            healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var healthContent = await healthResponse.Content.ReadAsStringAsync();
            var healthResult = JsonConvert.DeserializeObject<ClusterHealthDto>(healthContent);
            healthResult.Status.Should().NotBeNullOrEmpty();
            healthResult.NumberOfNodes.Should().BeGreaterThan(0);
            Console.WriteLine($"<><><><><>health response: {healthContent}");

            // 2. Create a new record (POST /api/entity/birthday)
            Console.WriteLine($"<><><><><>Starting CREATE test");
            var createResponse = await _client.PostAsync(
                "/api/entity/birthday",
                TestUtil.ToJsonContent(_birthday));
            
            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var createContent = await createResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"<><><><><>Create response: {createContent}");
            
            // Wait for the indexing to finish
            await _elasticClient.Indices.RefreshAsync("birthdays");
            await EsAwait.AwaitApiHitsAsync<SearchResultDto<JObject>>(
                _client,
                $"/api/entity/birthday/search/lucene?query={Uri.EscapeDataString($"lname:\"{_birthday["lname"]}\"")}",
                r => r.Hits?.Count ?? 0
            );

            // 3. Search with Lucene query (GET /api/entity/birthday/search/lucene)
            Console.WriteLine($"<><><><><>Starting LUCENE test");
            var luceneQuery = $"lname:\"{_birthday["lname"]}\"";
            await EsAwait.AwaitApiHitsAsync<SearchResultDto<JObject>>(
                _client,
                $"/api/entity/birthday/search/lucene?query={Uri.EscapeDataString(luceneQuery)}",
                r => r.Hits?.Count ?? 0, expected: 1
            );
            var luceneResponse = await _client.GetAsync($"/api/entity/birthday/search/lucene?query={Uri.EscapeDataString(luceneQuery)}");
            luceneResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var luceneContent = await luceneResponse.Content.ReadAsStringAsync();
            var luceneResult = JsonConvert.DeserializeObject<SearchResultDto<JObject>>(luceneContent);
            luceneResult.Hits.Should().NotBeEmpty("Lucene search should find the test birthday");
            luceneResult.Hits.First()["lname"]!.ToString().Should().Be(_birthday["lname"]!.ToString());
            Console.WriteLine($"<><><><><>Lucene response: {luceneContent}");

            // 4. Search with raw query (POST /api/entity/birthday/search/elasticsearch)
            Console.WriteLine($"<><><><><>Starting RAW test");
            var rawQuery = new
            {

                term = new Dictionary<string, object>
                {
                    { "lname.keyword",  _birthday["lname"] }
                }

            };
            var rawResponse = await _client.PostAsync(
                "/api/entity/birthday/search/elasticsearch?pageSize=20&from=0&includeDetails=false",
                TestUtil.ToJsonContent(rawQuery));
            rawResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var rawContent = await rawResponse.Content.ReadAsStringAsync();
            var rawResult = JsonConvert.DeserializeObject<SearchResultDto<JObject>>(rawContent);
            rawResult.Hits.Should().NotBeEmpty("Raw search should find the test birthday");
            rawResult.Hits.First()["lname"]!.ToString().Should().Be(_birthday["lname"]!.ToString());
            Console.WriteLine($"<><><><><>Raw response: {rawContent}");

            // 5. Search with ruleset (POST /api/entity/birthday/search/ruleset)
            Console.WriteLine($"<><><><><>Starting RULE test");
            var rulesetQuery = new Ruleset()
            {
                field = "lname",
                @operator = "=",
                value = _birthday["lname"]
            };
            var rulesetResponse = await _client.PostAsync(
                "/api/entity/birthday/search/ruleset",
                TestUtil.ToJsonContent(rulesetQuery));
            rulesetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var rulesetContent = await rulesetResponse.Content.ReadAsStringAsync();
            var rulesetResult = JsonConvert.DeserializeObject<SearchResultDto<JObject>>(rulesetContent);
            rulesetResult.Hits.Should().NotBeEmpty("Ruleset search should find the test birthday");
            rulesetResult.Hits.First()["lname"]!.ToString().Should().Be(_birthday["lname"]!.ToString());
            Console.WriteLine($"<><><><><>Rule response: {rulesetContent}");

            // set up for the next test
            var retrievedBirthdayDto = rulesetResult.Hits.First();

            // 6. Get by ID (GET /api/entity/birthday/{id})
            Console.WriteLine($"<><><><><>Starting GET test");
            var getResponse = await _client.GetAsync($"/api/entity/birthday/{retrievedBirthdayDto["id"]!.ToString()}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var getResult = JObject.Parse(getContent);
            getResult["lname"]!.ToString().Should().Be(_birthday["lname"]!.ToString());
            Console.WriteLine($"<><><><><>Get response: {getContent}");

            // 7. Update record (PUT /api/entity/birthday/{id})
            Console.WriteLine($"<><><><><>Starting UPDATE test");
            var updatedBirthday = new JObject
            {
                ["id"] = _birthdayId,
                ["lname"] = _birthday["lname"]!.ToString(),
                ["fname"] = "UpdatedFirstName",
                ["sign"] = _birthday["sign"]!.ToString(),
                ["dob"] = _birthday["dob"],
                ["isAlive"] = _birthday["isAlive"],
                ["text"] = _birthday["text"]!.ToString(),
                ["wikipedia"] = _birthday["wikipedia"]!.ToString()
            };
            var updateResponse = await _client.PutAsync(
                $"/api/entity/birthday/{_birthdayId}",
                TestUtil.ToJsonContent(updatedBirthday));
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            Console.WriteLine($"<><><><><>Update response: {updateResponse.Content.ReadAsStringAsync()}");

            // Wait for the update to finish
            await _elasticClient.Indices.RefreshAsync("birthdays");            

            // 8. Get unique values (GET /api/entity/birthday/unique-values/{field})
            Console.WriteLine($"<><><><><>Starting UNIQUE test");
            await EsAwait.AwaitApiHitsAsync<string[]>(
                _client,
                "/api/entity/birthday/unique-values/fname",
                r => r?.Length ?? 0
            );
            var uniqueValuesResponse = await _client.GetAsync("/api/entity/birthday/unique-values/fname");
            uniqueValuesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var uniqueValuesContent = await uniqueValuesResponse.Content.ReadAsStringAsync();
            var uniqueValues = JsonConvert.DeserializeObject<string[]>(uniqueValuesContent);
            uniqueValues.Should().Contain("UpdatedFirstName");
            Console.WriteLine($"<><><><><>Unique response: {uniqueValuesContent}");

            // 9. Delete record (DELETE /api/entity/birthday/{id})
            Console.WriteLine($"<><><><><>Starting DELETE test");
            var deleteResponse2 = await _client.DeleteAsync($"/api/entity/birthday/{_birthdayId}");
            deleteResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
            Console.WriteLine($"<><><><><>Delete response: {deleteResponse2.Content.ReadAsStringAsync()}");

            // Wait for the deletion to finish
            await _elasticClient.Indices.RefreshAsync("birthdays");
            // This expectation depends on TestViewOperations seeding; remove cross-test dependency
            await EsAwait.AwaitApiHitsAsync<SearchResultDto<JObject>>(
                _client,
                $"/api/entity/birthday/search/lucene?query={Uri.EscapeDataString(luceneQuery)}",
                r => r.Hits?.Count ?? 0, expected: 0
            );

            // Verify deletion
            var verifyDeleteResponse = await _client.GetAsync($"/api/entity/birthday/{_birthdayId}");
            verifyDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task TestBqlOperations()
        {
            // Create test data
            var testBirthdayId = Guid.NewGuid().ToString();
            var testBirthday = new JObject
            {
                ["id"] = testBirthdayId,
                ["lname"] = "TestBqlLastName",
                ["fname"] = "TestBqlFirstName",
                ["sign"] = "aries",
                ["dob"] = new DateTime(1990, 1, 1),
                ["isAlive"] = true,
                ["text"] = "Test text for BQL search",
                ["wikipedia"] = "<p>test person</p>"
            };

            // Clean up any existing records
            var deleteResponse = await _elasticClient.DeleteByQueryAsync<object>("birthdays", d => d
                .Query(q => q.Term(t => t.Field("lname.keyword").Value(testBirthday["lname"]!.ToString()))));
            
            await _elasticClient.Indices.RefreshAsync("birthdays");

            // Create test record
            var createResponse = await _client.PostAsync(
                "/api/entity/birthday",
                TestUtil.ToJsonContent(testBirthday));
            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            await _elasticClient.Indices.RefreshAsync("birthdays");
            await EsAwait.AwaitApiHitsAsync<SearchResultDto<JObject>>(
                _client,
                $"/api/entity/birthday/search/lucene?query={Uri.EscapeDataString($"lname:\"{testBirthday["lname"]}\"")}",
                r => r.Hits?.Count ?? 0,
                expected: 1,
                timeoutMs: 5000
            );
            // Seed two CatMulti docs so this test is self-contained
            var seed1Id = Guid.NewGuid().ToString();
            var seed2Id = Guid.NewGuid().ToString();
            var seed1 = new JObject { ["id"] = seed1Id, ["lname"] = "CatMulti", ["fname"] = "One", ["sign"] = "aries" };
            var seed2 = new JObject { ["id"] = seed2Id, ["lname"] = "CatMulti", ["fname"] = "Two", ["sign"] = "taurus" };
            await _client.PostAsync("/api/entity/birthday", TestUtil.ToJsonContent(seed1));
            await _client.PostAsync("/api/entity/birthday", TestUtil.ToJsonContent(seed2));
            await _elasticClient.Indices.RefreshAsync("birthdays");
            await EsAwait.AwaitApiHitsAsync<SearchResultDto<JObject>>(
                _client,
                $"/api/entity/birthday/search/lucene?query={Uri.EscapeDataString("lname:\"CatMulti\"")}",
                r => r.Hits?.Count ?? 0,
                expected: 2,
                timeoutMs: 5000
            );

            // 1. Test BQL to Ruleset conversion
            var bqlQuery = $"lname = {testBirthday["lname"]!.ToString().ToLower()}";
            var bql2RulesetResponse = await _client.PostAsync(
                "/api/entity/birthday/bql-to-ruleset",
                TestUtil.ToTextContent(bqlQuery));

            string responseBody = await bql2RulesetResponse.Content.ReadAsStringAsync();

            bql2RulesetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var rulesetDto = await bql2RulesetResponse.Content.ReadFromJsonAsync<RulesetDto>();
            rulesetDto.Should().NotBeNull();
            rulesetDto.field.Should().Be("lname");
            rulesetDto.@operator.Should().Be("=");
            rulesetDto.value.Should().Be(testBirthday["lname"]!.ToString().ToLower());

            // 2. Test Ruleset to BQL conversion
            var ruleset2BqlResponse = await _client.PostAsync(
                "/api/entity/birthday/ruleset-to-bql",
                TestUtil.ToJsonContent(rulesetDto));
            ruleset2BqlResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var bqlResult = await ruleset2BqlResponse.Content.ReadAsStringAsync();
            bqlResult.Should().NotBeNull();
            bqlResult.Should().Be(bqlQuery);

            // 3. Test Ruleset to Elasticsearch conversion
            var ruleset2EsResponse = await _client.PostAsync(
                "/api/entity/birthday/ruleset-to-elasticsearch",
                TestUtil.ToJsonContent(rulesetDto));
            ruleset2EsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var esQuery = await ruleset2EsResponse.Content.ReadFromJsonAsync<object>();
            esQuery.Should().NotBeNull();

            // 4. Test BQL search
            var bqlSearchResponse = await _client.PostAsync(
                "/api/entity/birthday/search/bql",
                TestUtil.ToTextContent(bqlQuery));
            bqlSearchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var bqlSearchContent = await bqlSearchResponse.Content.ReadAsStringAsync();
            var searchResult = JsonConvert.DeserializeObject<SearchResultDto<JObject>>(bqlSearchContent);
            searchResult.Should().NotBeNull();
            searchResult.Hits.Should().NotBeEmpty();
            searchResult.Hits.First()["lname"]!.ToString().Should().Be(testBirthday["lname"]!.ToString());

            // Clean up created records
            var deleteTestResponse = await _client.DeleteAsync($"/api/entity/birthday/{testBirthdayId}");
            deleteTestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            await _elasticClient.Indices.RefreshAsync("birthdays");
            await EsAwait.AwaitApiHitsAsync<SearchResultDto<JObject>>(
                _client,
                $"/api/entity/birthday/search/lucene?query={Uri.EscapeDataString("lname:\"CatMulti\"")}",
                r => r.Hits?.Count ?? 0,
                expected: 2,
                timeoutMs: 5000
            );
            (await _client.DeleteAsync($"/api/entity/birthday/{seed1Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await _client.DeleteAsync($"/api/entity/birthday/{seed2Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
            await _elasticClient.Indices.RefreshAsync("birthdays");
        }

        [Fact]
        public async Task TestComplexBqlOperations()
        {
            // Create test data
            var testBirthday1Id = Guid.NewGuid().ToString();
            var testBirthday1 = new JObject
            {
                ["id"] = testBirthday1Id,
                ["lname"] = "ComplexTest1",
                ["fname"] = "Test1",
                ["sign"] = "aries",
                ["dob"] = new DateTime(1990, 1, 1),
                ["isAlive"] = true
            };

            var testBirthday2Id = Guid.NewGuid().ToString();
            var testBirthday2 = new JObject
            {
                ["id"] = testBirthday2Id,
                ["lname"] = "ComplexTest2",
                ["fname"] = "Test2",
                ["sign"] = "taurus",
                ["dob"] = new DateTime(1991, 2, 2),
                ["isAlive"] = false
            };

            // Clean up any existing records
            var deleteResponse = await _elasticClient.DeleteByQueryAsync<object>("birthdays", d => d
                .Query(q => q.Terms(t => t.Field("lname.keyword").Terms(new TermsQueryField(new List<FieldValue> { testBirthday1["lname"]!.ToString(), testBirthday2["lname"]!.ToString() })))));
            
            await _elasticClient.Indices.RefreshAsync("birthdays");

            // Create test records
            await _client.PostAsync("/api/entity/birthday", TestUtil.ToJsonContent(testBirthday1));
            await _client.PostAsync("/api/entity/birthday", TestUtil.ToJsonContent(testBirthday2));
            await _elasticClient.Indices.RefreshAsync("birthdays");
            await EsAwait.AwaitApiHitsAsync<SearchResultDto<JObject>>(
                _client,
                $"/api/entity/birthday/search/lucene?query={Uri.EscapeDataString($"lname:\"{testBirthday1["lname"]}\" OR lname:\"{testBirthday2["lname"]}\"")}",
                r => r.Hits?.Count ?? 0,
                expected: 2,
                timeoutMs: 5000
            );

            // Test complex BQL query with AND/OR operators
            var complexBqlQuery = $"(lname = {testBirthday1["lname"]!.ToString().ToLower()} & fname = {testBirthday1["fname"]!.ToString().ToLower()}) | (lname = {testBirthday2["lname"]!.ToString().ToLower()} & fname = {testBirthday2["fname"]!.ToString().ToLower()})";
            
            // Test BQL to Ruleset conversion
            var bql2RulesetResponse = await _client.PostAsync(
                "/api/entity/birthday/bql-to-ruleset",
                TestUtil.ToTextContent(complexBqlQuery));
            bql2RulesetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var rulesetDto = await bql2RulesetResponse.Content.ReadFromJsonAsync<RulesetDto>();
            rulesetDto.Should().NotBeNull();
            rulesetDto.condition.Should().Be("or");
            rulesetDto.rules.Should().HaveCount(2);

            // Test Ruleset to BQL conversion
            var ruleset2BqlResponse = await _client.PostAsync(
                "/api/entity/birthday/ruleset-to-bql",
                TestUtil.ToJsonContent(rulesetDto));
            ruleset2BqlResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var bqlResult = await ruleset2BqlResponse.Content.ReadAsStringAsync();
            bqlResult.Should().NotBeNull();
            var bqlLower = bqlResult.ToLowerInvariant();
            bqlLower.Should().Contain($"lname = {testBirthday1["lname"]!.ToString().ToLower()}");
            bqlLower.Should().Contain($"fname = {testBirthday1["fname"]!.ToString().ToLower()}");
            bqlLower.Should().Contain($"lname = {testBirthday2["lname"]!.ToString().ToLower()}");
            bqlLower.Should().Contain($"fname = {testBirthday2["fname"]!.ToString().ToLower()}");
            (bqlLower.Contains("|") || bqlLower.Contains(" or ")).Should().BeTrue();
            (bqlLower.Contains("&") || bqlLower.Contains(" and ")).Should().BeTrue();
  
            // Test BQL search with complex query
            var bqlSearchResponse = await _client.PostAsync(
                "/api/entity/birthday/search/bql",
                TestUtil.ToTextContent(complexBqlQuery));
            bqlSearchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var bqlSearchContent2 = await bqlSearchResponse.Content.ReadAsStringAsync();
            var searchResult = JsonConvert.DeserializeObject<SearchResultDto<JObject>>(bqlSearchContent2);
            searchResult.Should().NotBeNull();
            searchResult.Hits.Should().HaveCountGreaterThanOrEqualTo(2);
            searchResult.Hits.Should().Contain(h => h["lname"]!.ToString() == testBirthday1["lname"]!.ToString());
            searchResult.Hits.Should().Contain(h => h["lname"]!.ToString() == testBirthday2["lname"]!.ToString());

            // Clean up
            await _client.DeleteAsync($"/api/entity/birthday/{testBirthday1Id}");
            await _client.DeleteAsync($"/api/entity/birthday/{testBirthday2Id}");
            await _elasticClient.Indices.RefreshAsync("birthdays");
        }

        [Fact]
        public async Task TestViewOperations()
        {
            // Clean up any existing test objects
            Console.WriteLine($"<><><><><>Starting TestViewOperations (cleanup & create new docs)");
            var deleteResponse = await _elasticClient.DeleteByQueryAsync<object>("birthdays", d => d
                .Query(q => q.Term(t => t.Field("fname.keyword").Value("viewTestObject"))));
            
            await _elasticClient.Indices.RefreshAsync("birthdays");
            Console.WriteLine($"<><><><><>Deleted {deleteResponse.Deleted} documents");

            // Create test objects
            var testObjects = new[]
            {
                new JObject { ["id"] = Guid.NewGuid().ToString(), ["fname"] = "viewTestObject", ["lname"] = "test1", ["dob"] = new DateTime(1900, 1, 1), ["sign"] = "sign1" },
                new JObject { ["id"] = Guid.NewGuid().ToString(), ["fname"] = "viewTestObject", ["lname"] = "test2", ["dob"] = new DateTime(1800, 1, 1), ["sign"] = "sign1" },
                new JObject { ["id"] = Guid.NewGuid().ToString(), ["fname"] = "viewTestObject", ["lname"] = "test1", ["dob"] = new DateTime(1800, 1, 1), ["sign"] = "sign2" }
            };

            // Create all test objects
            foreach (var obj in testObjects)
            {
                var createResponse = await _client.PostAsync(
                    "/api/entity/birthday",
                    TestUtil.ToJsonContent(obj));
                createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            }
            await _elasticClient.Indices.RefreshAsync("birthdays");
            Console.WriteLine($"<><><><><>Documents created. Starting BQL query just view");

            // Test 1: Search with view "sign/birth year" and query "lname = test1"
            var bqlQuery = "lname = test1";
            var viewResponse = await _client.PostAsync(
                $"/api/entity/birthday/search/bql?view=sign/birth%20year",
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
                $"/api/entity/birthday/search/bql?view=sign/birth%20year&category=sign1",
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
                $"/api/entity/birthday/search/bql?view=sign/birth%20year&category=sign1&secondaryCategory=1900",
                TestUtil.ToTextContent(bqlQuery));
            secondaryCategoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var secondaryCategoryContent = await secondaryCategoryResponse.Content.ReadAsStringAsync();
            var secondaryCategoryResult = JsonConvert.DeserializeObject<SearchResultDto<JObject>>(secondaryCategoryContent);
            secondaryCategoryResult.HitType.Should().Be("hit");
            secondaryCategoryResult.Hits.Should().NotBeEmpty("Search should find the test1");
            secondaryCategoryResult.Hits.First()["lname"]!.ToString().Should().Be("test1");
            secondaryCategoryResult.Hits.First()["sign"]!.ToString().Should().Be("sign1");
            secondaryCategoryResult.Hits.First()["dob"]!.Value<DateTime?>().Should().Be(new DateTime(1900, 1, 1));
            Console.WriteLine($"<><><><><>secondaryCategory resulted in {secondaryCategoryContent}.  Cleaning up.");
            // Clean up test objects
            deleteResponse = await _elasticClient.DeleteByQueryAsync<object>("birthdays", d => d
                .Query(q => q.Term(t => t.Field("fname.keyword").Value("viewTestObject"))));  
            await _elasticClient.Indices.RefreshAsync("birthdays");
            Console.WriteLine($"<><><><><>Deleted {deleteResponse.Deleted} documents.  Done with test.");
        }
        [Fact]
        public async Task TestUncategorizedFirstNameView()
        {
            var deleteResponse = await _elasticClient.DeleteByQueryAsync<object>("birthdays", d => d
                .Query(q => q.Term(t => t.Field("sign.keyword").Value("uncatsign"))));
            await _elasticClient.Indices.RefreshAsync("birthdays");

            var noFnameId = Guid.NewGuid().ToString();
            var noFname = new JObject { ["id"] = noFnameId, ["lname"] = "nofname", ["sign"] = "uncatsign" };
            var withFnameId = Guid.NewGuid().ToString();
            var withFname = new JObject { ["id"] = withFnameId, ["fname"] = "alpha", ["lname"] = "withfname", ["sign"] = "uncatsign" };

            await _client.PostAsync("/api/entity/birthday", TestUtil.ToJsonContent(noFname));
            await _client.PostAsync("/api/entity/birthday", TestUtil.ToJsonContent(withFname));
            await _elasticClient.Indices.RefreshAsync("birthdays");

            var bqlQuery = "lname = nofname | lname = withfname";
            var viewResponse = await _client.PostAsync(
                "/api/entity/birthday/search/bql?view=First%20Name",
                TestUtil.ToTextContent(bqlQuery));
            viewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var viewContent = await viewResponse.Content.ReadAsStringAsync();
            var viewResult = JsonConvert.DeserializeObject<SearchResultDto<ViewResultDto>>(viewContent);
            viewResult.Hits.Should().Contain(r => r.CategoryName == "(Uncategorized)" && r.Count == 1);

            var categoryResponse = await _client.PostAsync(
                "/api/entity/birthday/search/bql?view=First%20Name&category=(Uncategorized)",
                TestUtil.ToTextContent(bqlQuery));
            categoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var categoryContent = await categoryResponse.Content.ReadAsStringAsync();
            var categoryResult = JsonConvert.DeserializeObject<SearchResultDto<JObject>>(categoryContent);
            categoryResult.Hits.Should().HaveCount(1);
            categoryResult.Hits.First()["lname"]!.ToString().Should().Be("nofname");

            await _client.DeleteAsync($"/api/entity/birthday/{noFnameId}");
            await _client.DeleteAsync($"/api/entity/birthday/{withFnameId}");
            await _elasticClient.Indices.RefreshAsync("birthdays");
        }

        [Fact]
        public async Task TestCategorizeMultiple()
        {
            var id1 = Guid.NewGuid().ToString();
            var id2 = Guid.NewGuid().ToString();
            var b1 = new JObject { ["id"] = id1, ["lname"] = "CatMulti", ["fname"] = "One", ["sign"] = "aries" };
            var b2 = new JObject { ["id"] = id2, ["lname"] = "CatMulti", ["fname"] = "Two", ["sign"] = "taurus" };

            // Cleanup any pre-existing
            var deleteResponse = await _elasticClient.DeleteByQueryAsync<object>("birthdays", d => d
                .Query(q => q.Term(t => t.Field("lname.keyword").Value("CatMulti"))));
            await _elasticClient.Indices.RefreshAsync("birthdays");

            // Create
            (await _client.PostAsync("/api/entity/birthday", TestUtil.ToJsonContent(b1))).StatusCode.Should().Be(HttpStatusCode.OK);
            (await _client.PostAsync("/api/entity/birthday", TestUtil.ToJsonContent(b2))).StatusCode.Should().Be(HttpStatusCode.OK);
            await _elasticClient.Indices.RefreshAsync("birthdays");

            // Add categories: add ["A","B"] to both
            var addReq = new {
                rows = new[] { id1, id2 },
                add = new[] { "A", "B" },
                remove = Array.Empty<string>()
            };
            var addResp = await _client.PostAsync("/api/entity/birthday/categorize-multiple", TestUtil.ToJsonContent(addReq));
            addResp.StatusCode.Should().Be(HttpStatusCode.OK);
            await _elasticClient.Indices.RefreshAsync("birthdays");

            // Verify both have A and B (by ids to avoid stray docs)
            var verifyQuery = new { terms = new Dictionary<string, object> { { "_id", new[] { id1, id2 } } } };
            var verifyResp = await _client.PostAsync("/api/entity/birthday/search/elasticsearch", TestUtil.ToJsonContent(verifyQuery));
            verifyResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var verifyContent = await verifyResp.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<SearchResultDto<JObject>>(verifyContent);
            result.Hits.Should().HaveCount(2);
            result.Hits.All(h => h["categories"] != null && h["categories"]!.ToObject<List<string>>()!.Contains("A") && h["categories"]!.ToObject<List<string>>()!.Contains("B")).Should().BeTrue();

            // Remove B and add C (case-insensitive remove)
            var updateReq = new {
                rows = new[] { id1, id2 },
                add = new[] { "c" },
                remove = new[] { "b" }
            };
            var updateResp = await _client.PostAsync("/api/entity/birthday/categorize-multiple", TestUtil.ToJsonContent(updateReq));
            updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
            await _elasticClient.Indices.RefreshAsync("birthdays");

            // Verify: A and C present, B removed
            var verifyResp2 = await _client.PostAsync("/api/entity/birthday/search/elasticsearch", TestUtil.ToJsonContent(verifyQuery));
            verifyResp2.StatusCode.Should().Be(HttpStatusCode.OK);
            var verifyContent2 = await verifyResp2.Content.ReadAsStringAsync();
            var result2 = JsonConvert.DeserializeObject<SearchResultDto<JObject>>(verifyContent2);
            result2.Hits.Should().HaveCount(2);
            result2.Hits.All(h => h["categories"] != null && h["categories"]!.ToObject<List<string>>()!.Contains("A") && h["categories"]!.ToObject<List<string>>()!.Contains("C") && !h["categories"]!.ToObject<List<string>>()!.Contains("B")).Should().BeTrue();

            // Cleanup
            (await _client.DeleteAsync($"/api/entity/birthday/{id1}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await _client.DeleteAsync($"/api/entity/birthday/{id2}")).StatusCode.Should().Be(HttpStatusCode.OK);
            await _elasticClient.Indices.RefreshAsync("birthdays");
        }

        

        private class ClusterHealthDto
        {
            public string Status { get; set; } = string.Empty;
            public int NumberOfNodes { get; set; }
            public int NumberOfDataNodes { get; set; }
            public int ActiveShards { get; set; }
            public int ActivePrimaryShards { get; set; }
        }

        private class BqlQueryDto
        {
            public string Query { get; set; } = string.Empty;
        }
        #nullable restore
    }
} 
