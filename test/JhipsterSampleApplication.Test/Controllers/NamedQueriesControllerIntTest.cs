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
    public class NamedQueriesControllerIntTest
    {
        public NamedQueriesControllerIntTest()
        {
            if (false && !Debugger.IsAttached)
            {
                Console.WriteLine($"PID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(100);  // Wait for debugger to attach
                }
            }            
            _factory = new AppWebApplicationFactory<TestStartup>().WithMockUser("admin", new[] { RolesConstants.ADMIN });
            _client = _factory.CreateClient();
            _namedQueryRepository = _factory.GetRequiredService<INamedQueryRepository>();

            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new AutoMapperProfile());
            });
            _mapper = config.CreateMapper();

            InitTest();
        }

        private const string DefaultName = "TestQuery";
        private const string DefaultText = "SELECT * FROM Test";
        private const string DefaultOwner = "testuser";
        private const string UpdatedName = "UpdatedQuery";
        private const string UpdatedText = "SELECT * FROM Updated";

        private readonly AppWebApplicationFactory<TestStartup> _factory;
        private readonly HttpClient _client;
        private readonly INamedQueryRepository _namedQueryRepository;
        private readonly IMapper _mapper;

        private NamedQuery _namedQuery;

        private NamedQuery CreateEntity()
        {
            return new NamedQuery
            {
                Name = DefaultName,
                Text = DefaultText,
                Owner = DefaultOwner
            };
        }

        private void InitTest()
        {
            _namedQuery = CreateEntity();
        }

        [Fact]
        public async Task CreateNamedQuery()
        {
            {
                Console.WriteLine($"PID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
                while (false && !Debugger.IsAttached)
                {
                    Thread.Sleep(100);  // Wait for debugger to attach
                }
            }

            var databaseSizeBeforeCreate = await _namedQueryRepository.CountAsync();

            // Create the NamedQuery
            NamedQueryDto namedQueryDto = _mapper.Map<NamedQueryDto>(_namedQuery);
            var response = await _client.PostAsync("/api/NamedQueries", TestUtil.ToJsonContent(namedQueryDto));
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            // Validate the NamedQuery in the database
            var namedQueryList = await _namedQueryRepository.GetAllAsync();
            namedQueryList.Count().Should().Be(databaseSizeBeforeCreate + 1);
            var testNamedQuery = namedQueryList.Last();
            testNamedQuery.Name.Should().Be(DefaultName.ToUpper());
            testNamedQuery.Text.Should().Be(DefaultText);
            testNamedQuery.Owner.Should().Be(DefaultOwner);
        }

        [Fact]
        public async Task GetAllNamedQueries()
        {
            // Initialize the database
            await _namedQueryRepository.CreateOrUpdateAsync(_namedQuery);
            await _namedQueryRepository.SaveChangesAsync();

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
            // Initialize the database
            await _namedQueryRepository.CreateOrUpdateAsync(_namedQuery);
            await _namedQueryRepository.SaveChangesAsync();

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
            // Initialize the database
            await _namedQueryRepository.CreateOrUpdateAsync(_namedQuery);
            await _namedQueryRepository.SaveChangesAsync();

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
            // Initialize the database
            await _namedQueryRepository.CreateOrUpdateAsync(_namedQuery);
            await _namedQueryRepository.SaveChangesAsync();

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
            // Initialize the database
            await _namedQueryRepository.CreateOrUpdateAsync(_namedQuery);
            await _namedQueryRepository.SaveChangesAsync();

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
            // Initialize the database
            await _namedQueryRepository.CreateOrUpdateAsync(_namedQuery);
            await _namedQueryRepository.SaveChangesAsync();
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
            // Initialize the database
            await _namedQueryRepository.CreateOrUpdateAsync(_namedQuery);
            await _namedQueryRepository.SaveChangesAsync();
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
            // Create a client with a non-admin user
            var nonAdminClient = _factory.WithMockUser("user", new[] { RolesConstants.USER }).CreateClient();

            // Initialize the database with a GLOBAL query
            var globalQuery = new NamedQuery
            {
                Name = "GlobalQuery",
                Text = "SELECT * FROM Global",
                Owner = "GLOBAL"
            };
            await _namedQueryRepository.CreateOrUpdateAsync(globalQuery);
            await _namedQueryRepository.SaveChangesAsync();
            var databaseSizeBeforeDelete = await _namedQueryRepository.CountAsync();

            // Try to delete the GLOBAL query as non-admin
            var response = await nonAdminClient.DeleteAsync($"/api/NamedQueries/{globalQuery.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // Validate the query still exists in the database
            var namedQueryList = await _namedQueryRepository.GetAllAsync();
            namedQueryList.Count().Should().Be(databaseSizeBeforeDelete);
        }

        [Fact]
        public async Task CannotDeleteSystemNamedQuery()
        {
            // Create a client with a non-admin user
            var nonAdminClient = _factory.WithMockUser("user", new[] { RolesConstants.USER }).CreateClient();

            // Initialize the database with a SYSTEM query
            var systemQuery = new NamedQuery
            {
                Name = "SystemQuery",
                Text = "SELECT * FROM System",
                Owner = "GLOBAL",
                IsSystem = true
            };
            await _namedQueryRepository.CreateOrUpdateAsync(systemQuery);
            await _namedQueryRepository.SaveChangesAsync();
            var databaseSizeBeforeDelete = await _namedQueryRepository.CountAsync();

            // Try to delete the SYSTEM query as non-admin
            var response = await nonAdminClient.DeleteAsync($"/api/NamedQueries/{systemQuery.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // Validate the query still exists in the database
            var namedQueryList = await _namedQueryRepository.GetAllAsync();
            namedQueryList.Count().Should().Be(databaseSizeBeforeDelete);
        }

        [Fact]
        public async Task AdminCanDeleteGlobalNamedQuery()
        {
            // Initialize the database with a GLOBAL query
            var globalQuery = new NamedQuery
            {
                Name = "GlobalQuery",
                Text = "SELECT * FROM Global",
                Owner = "GLOBAL"
            };
            await _namedQueryRepository.CreateOrUpdateAsync(globalQuery);
            await _namedQueryRepository.SaveChangesAsync();
            var databaseSizeBeforeDelete = await _namedQueryRepository.CountAsync();

            // Delete the GLOBAL query as admin
            var response = await _client.DeleteAsync($"/api/NamedQueries/{globalQuery.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Validate the query was deleted from the database
            var namedQueryList = await _namedQueryRepository.GetAllAsync();
            namedQueryList.Count().Should().Be(databaseSizeBeforeDelete - 1);
        }

        [Fact]
        public async Task AdminCanDeleteSystemNamedQuery()
        {
            // Initialize the database with a SYSTEM query
            var systemQuery = new NamedQuery
            {
                Name = "SystemQuery",
                Text = "SELECT * FROM System",
                Owner = "GLOBAL",
                IsSystem = true
            };
            await _namedQueryRepository.CreateOrUpdateAsync(systemQuery);
            await _namedQueryRepository.SaveChangesAsync();
            var databaseSizeBeforeDelete = await _namedQueryRepository.CountAsync();

            // Delete the SYSTEM query as admin
            var response = await _client.DeleteAsync($"/api/NamedQueries/{systemQuery.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Validate the query was deleted from the database
            var namedQueryList = await _namedQueryRepository.GetAllAsync();
            namedQueryList.Count().Should().Be(databaseSizeBeforeDelete - 1);
        }

        [Fact]
        public void EqualsVerifier()
        {
            TestUtil.EqualsVerifier(typeof(NamedQuery));
            var namedQuery1 = new NamedQuery
            {
                Id = 1L,
                Name = DefaultName,
                Text = DefaultText,
                Owner = DefaultOwner
            };
            var namedQuery2 = new NamedQuery
            {
                Id = namedQuery1.Id,
                Name = DefaultName,
                Text = DefaultText,
                Owner = DefaultOwner
            };
            namedQuery1.Should().Be(namedQuery2);
            namedQuery2.Id = 2L;
            namedQuery1.Should().NotBe(namedQuery2);
            namedQuery1.Id = 0L;
            namedQuery1.Should().NotBe(namedQuery2);
        }
    }
} 