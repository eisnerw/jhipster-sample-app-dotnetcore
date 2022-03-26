
using AutoMapper;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Jhipster.Infrastructure.Data;
using Jhipster.Domain;
using Jhipster.Domain.Repositories.Interfaces;
using Jhipster.Dto;
using Jhipster.Configuration.AutoMapper;
using Jhipster.Test.Setup;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Jhipster.Test.Controllers
{
    public class SelectorResourceIntTest
    {
        public SelectorResourceIntTest()
        {
            _factory = new AppWebApplicationFactory<TestStartup>().WithMockUser();
            _client = _factory.CreateClient();

            _selectorRepository = _factory.GetRequiredService<ISelectorRepository>();

            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new AutoMapperProfile());
            });

            _mapper = config.CreateMapper();

            InitTest();
        }

        private const string DefaultName = "AAAAAAAAAA";
        private const string UpdatedName = "BBBBBBBBBB";

        private const string DefaultRulesetName = "AAAAAAAAAA";
        private const string UpdatedRulesetName = "BBBBBBBBBB";

        private const string DefaultAction = "AAAAAAAAAA";
        private const string UpdatedAction = "BBBBBBBBBB";

        private const string DefaultActionParameter = "AAAAAAAAAA";
        private const string UpdatedActionParameter = "BBBBBBBBBB";

        private readonly AppWebApplicationFactory<TestStartup> _factory;
        private readonly HttpClient _client;
        private readonly ISelectorRepository _selectorRepository;

        private Selector _selector;

        private readonly IMapper _mapper;

        private Selector CreateEntity()
        {
            return new Selector
            {
                Name = DefaultName,
                RulesetName = DefaultRulesetName,
                Action = DefaultAction,
                ActionParameter = DefaultActionParameter
            };
        }

        private void InitTest()
        {
            _selector = CreateEntity();
        }

        [Fact]
        public async Task CreateSelector()
        {
            var databaseSizeBeforeCreate = await _selectorRepository.CountAsync();

            // Create the Selector
            SelectorDto _selectorDto = _mapper.Map<SelectorDto>(_selector);
            var response = await _client.PostAsync("/api/selectors", TestUtil.ToJsonContent(_selectorDto));
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            // Validate the Selector in the database
            var selectorList = await _selectorRepository.GetAllAsync();
            selectorList.Count().Should().Be(databaseSizeBeforeCreate + 1);
            var testSelector = selectorList.Last();
            testSelector.Name.Should().Be(DefaultName);
            testSelector.RulesetName.Should().Be(DefaultRulesetName);
            testSelector.Action.Should().Be(DefaultAction);
            testSelector.ActionParameter.Should().Be(DefaultActionParameter);
        }

        [Fact]
        public async Task CreateSelectorWithExistingId()
        {
            var databaseSizeBeforeCreate = await _selectorRepository.CountAsync();
            databaseSizeBeforeCreate.Should().Be(0);
            // Create the Selector with an existing ID
            _selector.Id = 1L;

            // An entity with an existing ID cannot be created, so this API call must fail
            SelectorDto _selectorDto = _mapper.Map<SelectorDto>(_selector);
            var response = await _client.PostAsync("/api/selectors", TestUtil.ToJsonContent(_selectorDto));

            // Validate the Selector in the database
            var selectorList = await _selectorRepository.GetAllAsync();
            selectorList.Count().Should().Be(databaseSizeBeforeCreate);
        }

        [Fact]
        public async Task GetAllSelectors()
        {
            // Initialize the database
            await _selectorRepository.CreateOrUpdateAsync(_selector);
            await _selectorRepository.SaveChangesAsync();

            // Get all the selectorList
            var response = await _client.GetAsync("/api/selectors?sort=id,desc");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = JToken.Parse(await response.Content.ReadAsStringAsync());
            json.SelectTokens("$.[*].id").Should().Contain(_selector.Id);
            json.SelectTokens("$.[*].name").Should().Contain(DefaultName);
            json.SelectTokens("$.[*].rulesetName").Should().Contain(DefaultRulesetName);
            json.SelectTokens("$.[*].action").Should().Contain(DefaultAction);
            json.SelectTokens("$.[*].actionParameter").Should().Contain(DefaultActionParameter);
        }

        [Fact]
        public async Task GetSelector()
        {
            // Initialize the database
            await _selectorRepository.CreateOrUpdateAsync(_selector);
            await _selectorRepository.SaveChangesAsync();

            // Get the selector
            var response = await _client.GetAsync($"/api/selectors/{_selector.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = JToken.Parse(await response.Content.ReadAsStringAsync());
            json.SelectTokens("$.id").Should().Contain(_selector.Id);
            json.SelectTokens("$.name").Should().Contain(DefaultName);
            json.SelectTokens("$.rulesetName").Should().Contain(DefaultRulesetName);
            json.SelectTokens("$.action").Should().Contain(DefaultAction);
            json.SelectTokens("$.actionParameter").Should().Contain(DefaultActionParameter);
        }

        [Fact]
        public async Task GetNonExistingSelector()
        {
            var response = await _client.GetAsync("/api/selectors/" + long.MaxValue);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task UpdateSelector()
        {
            // Initialize the database
            await _selectorRepository.CreateOrUpdateAsync(_selector);
            await _selectorRepository.SaveChangesAsync();
            var databaseSizeBeforeUpdate = await _selectorRepository.CountAsync();

            // Update the selector
            var updatedSelector = await _selectorRepository.QueryHelper().GetOneAsync(it => it.Id == _selector.Id);
            // Disconnect from session so that the updates on updatedSelector are not directly saved in db
            //TODO detach
            updatedSelector.Name = UpdatedName;
            updatedSelector.RulesetName = UpdatedRulesetName;
            updatedSelector.Action = UpdatedAction;
            updatedSelector.ActionParameter = UpdatedActionParameter;

            SelectorDto updatedSelectorDto = _mapper.Map<SelectorDto>(_selector);
            var response = await _client.PutAsync("/api/selectors", TestUtil.ToJsonContent(updatedSelectorDto));
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Validate the Selector in the database
            var selectorList = await _selectorRepository.GetAllAsync();
            selectorList.Count().Should().Be(databaseSizeBeforeUpdate);
            var testSelector = selectorList.Last();
            testSelector.Name.Should().Be(UpdatedName);
            testSelector.RulesetName.Should().Be(UpdatedRulesetName);
            testSelector.Action.Should().Be(UpdatedAction);
            testSelector.ActionParameter.Should().Be(UpdatedActionParameter);
        }

        [Fact]
        public async Task UpdateNonExistingSelector()
        {
            var databaseSizeBeforeUpdate = await _selectorRepository.CountAsync();

            // If the entity doesn't have an ID, it will throw BadRequestAlertException
            SelectorDto _selectorDto = _mapper.Map<SelectorDto>(_selector);
            var response = await _client.PutAsync("/api/selectors", TestUtil.ToJsonContent(_selectorDto));
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // Validate the Selector in the database
            var selectorList = await _selectorRepository.GetAllAsync();
            selectorList.Count().Should().Be(databaseSizeBeforeUpdate);
        }

        [Fact]
        public async Task DeleteSelector()
        {
            // Initialize the database
            await _selectorRepository.CreateOrUpdateAsync(_selector);
            await _selectorRepository.SaveChangesAsync();
            var databaseSizeBeforeDelete = await _selectorRepository.CountAsync();

            var response = await _client.DeleteAsync($"/api/selectors/{_selector.Id}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Validate the database is empty
            var selectorList = await _selectorRepository.GetAllAsync();
            selectorList.Count().Should().Be(databaseSizeBeforeDelete - 1);
        }

        [Fact]
        public void EqualsVerifier()
        {
            TestUtil.EqualsVerifier(typeof(Selector));
            var selector1 = new Selector
            {
                Id = 1L
            };
            var selector2 = new Selector
            {
                Id = selector1.Id
            };
            selector1.Should().Be(selector2);
            selector2.Id = 2L;
            selector1.Should().NotBe(selector2);
            selector1.Id = 0;
            selector1.Should().NotBe(selector2);
        }
    }
}
