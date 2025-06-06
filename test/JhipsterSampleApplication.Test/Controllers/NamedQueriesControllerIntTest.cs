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

namespace JhipsterSampleApplication.Test.Controllers
{
    public class NamedQueriesControllerIntTest : IAsyncLifetime
    {
        private const string DefaultName = "TestQuery";
        private const string DefaultText = "SELECT * FROM Test";
        private const string DefaultOwner = "testuser";
        private const string UpdatedName = "UpdatedQuery";
        private const string UpdatedText = "SELECT * FROM Updated";

        private readonly AppWebApplicationFactory<TestStartup> _factory;
        private readonly HttpClient _client;
        private readonly INamedQueryRepository _namedQueryRepository;
        private readonly IMapper _mapper;
        private readonly ApplicationDatabaseContext _context;

        private NamedQuery _namedQuery;
        private NamedQuery _globalQuery;

        public NamedQueriesControllerIntTest()
        {
            _factory = new AppWebApplicationFactory<TestStartup>().WithMockUser("admin", new[] { RolesConstants.ADMIN });
            _client = _factory.CreateClient();
            _namedQueryRepository = _factory.GetRequiredService<INamedQueryRepository>();
            _context = _factory.GetRequiredService<ApplicationDatabaseContext>();

            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new AutoMapperProfile());
            });
            _mapper = config.CreateMapper();
        }

        public async Task InitializeAsync()
        {
            // Initialize test data
            _namedQuery = new NamedQuery
            {
                Name = DefaultName,
                Text = DefaultText,
                Owner = DefaultOwner
            };

            _globalQuery = new NamedQuery
            {
                Name = "GlobalQuery",
                Text = "SELECT * FROM Global",
                Owner = "GLOBAL"
            };

            await _namedQueryRepository.CreateOrUpdateAsync(_namedQuery);
            await _namedQueryRepository.CreateOrUpdateAsync(_globalQuery);
            await _namedQueryRepository.SaveChangesAsync();
        }

        public async Task DisposeAsync()
        {
            // Clean up test data
            await _context.Database.EnsureDeletedAsync();
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
                Owner = DefaultOwner
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
        }

        [Fact]
        public async Task GetAllNamedQueries()
        {
            // Get all the namedQueryList
            var response = await _client.GetAsync("/api/NamedQueries?owner=ALL");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = JToken.Parse(await response.Content.ReadAsStringAsync());
            json.SelectTokens("$.[*].id").Should().Contain(_namedQuery.Id);
            json.SelectTokens("$.[*].name").Should().Contain(DefaultName);
        }

        [Fact]
        public async Task GetNamedQueriesByOwner()
        {
            // Get queries by owner
            var response = await _client.GetAsync($"/api/NamedQueries?owner={DefaultOwner}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = JToken.Parse(await response.Content.ReadAsStringAsync());
            json.SelectTokens("$.[*].id").Should().Contain(_namedQuery.Id);
            json.SelectTokens("$.[*].owner").Should().Contain(DefaultOwner);
        }

        [Fact]
        public async Task GetNamedQueriesByName()
        {
            // Get queries by name
            var response = await _client.GetAsync($"/api/NamedQueries?name={DefaultName}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = JToken.Parse(await response.Content.ReadAsStringAsync());
            json.SelectTokens("$.[*].id").Should().Contain(_namedQuery.Id);
            json.SelectTokens("$.[*].name").Should().Contain(DefaultName);
        }

        [Fact]
        public async Task GetNamedQueryByNameAndOwner()
        {
            // Get query by name and owner
            var response = await _client.GetAsync($"/api/NamedQueries?name={DefaultName}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = JToken.Parse(await response.Content.ReadAsStringAsync());
            json.SelectTokens("$.[*].id").Should().Contain(_namedQuery.Id);
            json.SelectTokens("$.[*].name").Should().Contain(DefaultName);
            json.SelectTokens("$.[*].owner").Should().Contain(DefaultOwner);
        }

        [Fact]
        public async Task GetNamedQuery()
        {
            // Get the namedQuery
            var response = await _client.GetAsync($"/api/NamedQueries/{_namedQuery.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = JToken.Parse(await response.Content.ReadAsStringAsync());
            json.SelectTokens("$.id").Should().Contain(_namedQuery.Id);
            json.SelectTokens("$.name").Should().Contain(DefaultName);
        }

        [Fact]
        public async Task UpdateNamedQuery()
        {
            var databaseSizeBeforeUpdate = await _namedQueryRepository.CountAsync();

            // Update the namedQuery
            var updatedNamedQuery = await _namedQueryRepository.QueryHelper().GetOneAsync(it => it.Id == _namedQuery.Id);      
            updatedNamedQuery.Name = UpdatedName;
            updatedNamedQuery.Text = UpdatedText;
            NamedQueryDto updatedNamedQueryDto = _mapper.Map<NamedQueryDto>(updatedNamedQuery);
            var response = await _client.PutAsync($"/api/NamedQueries/{_namedQuery.Id}", TestUtil.ToJsonContent(updatedNamedQueryDto));
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
            var databaseSizeBeforeDelete = await _namedQueryRepository.CountAsync();

            var response = await _client.DeleteAsync($"/api/NamedQueries/{_namedQuery.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Validate the database is empty
            var namedQueryList = await _namedQueryRepository.GetAllAsync();
            namedQueryList.Count().Should().Be(databaseSizeBeforeDelete - 1);
        }

        [Fact]
        public async Task CannotDeleteGlobalNamedQuery()
        {
            // Create a new factory instance with a non-admin user
            var nonAdminFactory = new AppWebApplicationFactory<TestStartup>().WithMockUser("user", new[] { RolesConstants.USER });
            var nonAdminClient = nonAdminFactory.CreateClient();

            var databaseSizeBeforeDelete = await _namedQueryRepository.CountAsync();

            // Try to delete the GLOBAL query as non-admin
            var response = await nonAdminClient.DeleteAsync($"/api/NamedQueries/{_globalQuery.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // Validate the query still exists in the database
            var namedQueryList = await _namedQueryRepository.GetAllAsync();
            namedQueryList.Count().Should().Be(databaseSizeBeforeDelete);
        }

        [Fact]
        public async Task AdminCanDeleteGlobalNamedQuery()
        {
            var databaseSizeBeforeDelete = await _namedQueryRepository.CountAsync();

            // Delete the GLOBAL query as admin
            var response = await _client.DeleteAsync($"/api/NamedQueries/{_globalQuery.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Validate the query is deleted
            var namedQueryList = await _namedQueryRepository.GetAllAsync();
            namedQueryList.Count().Should().Be(databaseSizeBeforeDelete - 1);
        }
    }
} 