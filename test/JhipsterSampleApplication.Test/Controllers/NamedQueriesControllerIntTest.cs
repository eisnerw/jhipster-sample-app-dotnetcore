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

namespace JhipsterSampleApplication.Test.Controllers
{
    public class NamedQueriesControllerIntTest
    {
        public NamedQueriesControllerIntTest()
        {
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
            var databaseSizeBeforeCreate = await _namedQueryRepository.CountAsync();

            // Create the NamedQuery
            NamedQueryDto namedQueryDto = _mapper.Map<NamedQueryDto>(_namedQuery);
            var response = await _client.PostAsync("/api/NamedQueries", TestUtil.ToJsonContent(namedQueryDto));
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            // Validate the NamedQuery in the database
            var namedQueryList = await _namedQueryRepository.GetAllAsync();
            namedQueryList.Count().Should().Be(databaseSizeBeforeCreate + 1);
            var testNamedQuery = namedQueryList.Last();
            testNamedQuery.Name.Should().Be(DefaultName);
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
            var response = await _client.GetAsync("/api/NamedQueries?sort=id,desc");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = JToken.Parse(await response.Content.ReadAsStringAsync());
            json.SelectTokens("$.[*].id").Should().Contain(_namedQuery.Id);
            json.SelectTokens("$.[*].name").Should().Contain(DefaultName);
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