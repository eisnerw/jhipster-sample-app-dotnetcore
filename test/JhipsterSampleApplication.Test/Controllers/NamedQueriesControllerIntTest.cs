using AutoMapper;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JhipsterSampleApplication.Infrastructure.Data;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Repositories.Interfaces;
using JhipsterSampleApplication.Dto;
using JhipsterSampleApplication.Configuration.AutoMapper;
using JhipsterSampleApplication.Test.Setup;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Xunit;
using JhipsterSampleApplication.Crosscutting.Constants;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Threading;
using Microsoft.AspNetCore.Identity;

namespace JhipsterSampleApplication.Test.Controllers
{
    public class NamedQueriesControllerIntTest : IAsyncLifetime
    {
        private const string DefaultName = "TestQuery";
        private const string DefaultText = "SELECT * FROM Test";
        private const string DefaultOwner = "testuser";
        private const string DefaultDomain = "BIRTHDAY";
        private const string UpdatedName = "UpdatedQuery";
        private const string UpdatedText = "SELECT * FROM Updated";

        private readonly AppWebApplicationFactory<TestStartup> _factory;
        private readonly HttpClient _client;
        private readonly INamedQueryRepository _namedQueryRepository;
        private readonly IMapper _mapper;
        private readonly ApplicationDatabaseContext _context;
        private readonly UserManager<User> _userManager;

        public NamedQueriesControllerIntTest()
        {
            _factory = new AppWebApplicationFactory<TestStartup>().WithMockUser("admin", new[] { RolesConstants.ADMIN });
            _client = _factory.CreateClient();
            _namedQueryRepository = _factory.GetRequiredService<INamedQueryRepository>();
            _context = _factory.GetRequiredService<ApplicationDatabaseContext>();
            _userManager = _factory.GetRequiredService<UserManager<User>>();

            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new AutoMapperProfile());
            });
            _mapper = config.CreateMapper();
        }

        public async Task InitializeAsync()
        {
            // Clean up any existing data
            await _context.Database.EnsureDeletedAsync();
            await _context.Database.EnsureCreatedAsync();

            // Ensure the database is properly initialized
            await _context.Database.MigrateAsync();

            // Create the admin role
            var roleManager = _factory.GetRequiredService<RoleManager<Role>>();
            var adminRole = new Role { Id = "role_admin", Name = RolesConstants.ADMIN };
            await roleManager.CreateAsync(adminRole);

            // Create the admin user
            var adminUser = new User
            {
                Login = "admin",
                Email = "admin@localhost",
                PasswordHash = _userManager.PasswordHasher.HashPassword(null, "admin"),
                Activated = true
            };
            await _userManager.CreateAsync(adminUser);
            await _userManager.AddToRolesAsync(adminUser, new[] { RolesConstants.ADMIN });
        }

        public async Task DisposeAsync()
        {
            // Clean up test data
            await _context.Database.EnsureDeletedAsync();
        }

       private async Task<NamedQuery> CreateTestQuery(string name = DefaultName, string text = DefaultText, string owner = DefaultOwner)
        {
            var query = new NamedQuery
            {
                Name = name,
                Text = text,
                Owner = owner,
                Domain = DefaultDomain
            };
            NamedQueryDto namedQueryDto = _mapper.Map<NamedQueryDto>(query);
            var response = await _client.PostAsync("/api/NamedQueries", TestUtil.ToJsonContent(namedQueryDto));
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            
            var json = JToken.Parse(await response.Content.ReadAsStringAsync());
            var createdId = json.SelectToken("$.id").Value<long>();
            return await _namedQueryRepository.QueryHelper().GetOneAsync(q => q.Id == createdId);
        }
        
        [Fact]
        public async Task CreateNamedQuery()
        {
            var databaseSizeBeforeCreate = await _namedQueryRepository.CountAsync();

            // Create the NamedQuery
            var newQuery = new NamedQuery
            {
                Name = "NewQuery",
                Text = "SELECT * FROM New",
                Owner = DefaultOwner,
                Domain = DefaultDomain
            };
            NamedQueryDto namedQueryDto = _mapper.Map<NamedQueryDto>(newQuery);
            var response = await _client.PostAsync("/api/NamedQueries", TestUtil.ToJsonContent(namedQueryDto));
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            // Validate the NamedQuery in the database
            var namedQueryList = await _namedQueryRepository.GetAllAsync();
            namedQueryList.Count().Should().Be(databaseSizeBeforeCreate + 1);
            var testNamedQuery = namedQueryList.Last();
            testNamedQuery.Name.Should().Be("NEWQUERY");
            testNamedQuery.Text.Should().Be("SELECT * FROM New");
            testNamedQuery.Owner.Should().Be(DefaultOwner);
            testNamedQuery.Domain.Should().Be(DefaultDomain);
        }

        [Fact]
        public async Task GetAllNamedQueries()
        {
            // Create test queries
            var query1 = await CreateTestQuery("Query1", "SELECT * FROM Test1");
            var query2 = await CreateTestQuery("Query2", "SELECT * FROM Test2");

            // Get all the namedQueryList
            var response = await _client.GetAsync($"/api/NamedQueries?owner=ALL&domain={DefaultDomain}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = JToken.Parse(await response.Content.ReadAsStringAsync());
            json.SelectTokens("$.[*].id").Should().Contain(query1.Id);
            json.SelectTokens("$.[*].id").Should().Contain(query2.Id);
        }

        [Fact]
        public async Task GetNamedQueriesByOwner()
        {
            // Create test queries
            var query1 = await CreateTestQuery("Query1", "SELECT * FROM Test1", "owner1");
            var query2 = await CreateTestQuery("Query2", "SELECT * FROM Test2", "owner2");

            // Get queries by owner
            var response = await _client.GetAsync($"/api/NamedQueries?owner=owner1&domain={DefaultDomain}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = JToken.Parse(await response.Content.ReadAsStringAsync());
            json.SelectTokens("$.[*].id").Should().Contain(query1.Id);
            json.SelectTokens("$.[*].owner").Should().Contain("owner1");
        }

        [Fact] 
        public async Task GetNamedQueriesByName()
        {
            // Create test queries
            var query1 = await CreateTestQuery("Query1", "SELECT * FROM Test1");
            var query2 = await CreateTestQuery("Query2", "SELECT * FROM Test2");

            // Get queries by name
            var response = await _client.GetAsync($"/api/NamedQueries?name=Query1&domain={DefaultDomain}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = JToken.Parse(await response.Content.ReadAsStringAsync());
            json.SelectTokens("$.[*].id").Should().Contain(query1.Id);
            json.SelectTokens("$.[*].name").Should().Contain("Query1".ToUpperInvariant());
        }

        [Fact]
        public async Task GetNamedQueryByNameAndOwner()
        {
            // Create test queries
            var query1 = await CreateTestQuery("Query1", "SELECT * FROM Test1", "owner1");
            var query2 = await CreateTestQuery("Query2", "SELECT * FROM Test2", "owner2");

            // Get query by name and owner
            var response = await _client.GetAsync($"/api/NamedQueries?name=Query1&owner=owner1&domain={DefaultDomain}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = JToken.Parse(await response.Content.ReadAsStringAsync());
            ((string)json["name"]).Should().Be("Query1".ToUpperInvariant());
            ((string)json["text"]).Should().Be("SELECT * FROM Test1");
            ((string)json["owner"]).Should().Be("owner1");
            ((string)json["domain"]).Should().Be(DefaultDomain);
            ((long)json["id"]).Should().Be(query1.Id);
        }

        [Fact]
        public async Task GetNamedQuery()
        {
            // Create test query
            var query = await CreateTestQuery();

            // Get the namedQuery
            var response = await _client.GetAsync($"/api/NamedQueries/{query.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = JToken.Parse(await response.Content.ReadAsStringAsync());
            ((string)json["name"]).Should().Be(DefaultName.ToUpperInvariant());
            ((long)json["id"]).Should().Be(query.Id);
            ((string)json["domain"]).Should().Be(DefaultDomain);
        }

        [Fact]
        public async Task UpdateNamedQuery()
        {
            // Create test query
            var query = await CreateTestQuery();
            var databaseSizeBeforeUpdate = await _namedQueryRepository.CountAsync();

            // Update the namedQuery
            var updatedNamedQuery = await _namedQueryRepository.QueryHelper().GetOneAsync(it => it.Id == query.Id);      
            updatedNamedQuery.Name = UpdatedName;
            updatedNamedQuery.Text = UpdatedText;
            NamedQueryDto updatedNamedQueryDto = _mapper.Map<NamedQueryDto>(updatedNamedQuery);
            var response = await _client.PutAsync($"/api/NamedQueries/{query.Id}", TestUtil.ToJsonContent(updatedNamedQueryDto));
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Validate the NamedQuery in the database
            var namedQueryList = await _namedQueryRepository.GetAllAsync();
            namedQueryList.Count().Should().Be(databaseSizeBeforeUpdate);
            var testNamedQuery = namedQueryList.Last();
            testNamedQuery.Name.Should().Be(UpdatedName);
            testNamedQuery.Text.Should().Be(UpdatedText);
        }

        [Fact]
        public async Task DeleteNamedQuery()
        {
            // Create test query
            var query = await CreateTestQuery();
            var databaseSizeBeforeDelete = await _namedQueryRepository.CountAsync();

            var response = await _client.DeleteAsync($"/api/NamedQueries/{query.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Validate the database is empty
            var namedQueryList = await _namedQueryRepository.GetAllAsync();
            namedQueryList.Count().Should().Be(databaseSizeBeforeDelete - 1);
        }

        [Fact]
        public async Task CannotDeleteGlobalNamedQuery()
        {
            // Create a global query
            var globalQuery = await CreateTestQuery("GlobalQuery", "SELECT * FROM Global", "GLOBAL");

            // Create a new factory instance with a non-admin user
            var nonAdminFactory = new AppWebApplicationFactory<TestStartup>().WithMockUser("user", new[] { RolesConstants.USER });
            var nonAdminClient = nonAdminFactory.CreateClient();

            var databaseSizeBeforeDelete = await _namedQueryRepository.CountAsync();

            // Try to delete the GLOBAL query as non-admin
            var response = await nonAdminClient.DeleteAsync($"/api/NamedQueries/{globalQuery.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // Validate the query still exists in the database
            var namedQueryList = await _namedQueryRepository.GetAllAsync();
            namedQueryList.Count().Should().Be(databaseSizeBeforeDelete);
        }

        [Fact]
        public async Task AdminCanDeleteGlobalNamedQuery()
        {
            // Create a global query
            var globalQuery = await CreateTestQuery("GlobalQuery", "SELECT * FROM Global", "GLOBAL");
            var databaseSizeBeforeDelete = await _namedQueryRepository.CountAsync();

            // Delete the GLOBAL query as admin
            var response = await _client.DeleteAsync($"/api/NamedQueries/{globalQuery.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Validate the query is deleted
            var namedQueryList = await _namedQueryRepository.GetAllAsync();
            namedQueryList.Count().Should().Be(databaseSizeBeforeDelete - 1);
        }
    }
} 