using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using Jhipster.Domain.Services.Interfaces;
using Jhipster.Domain.Repositories.Interfaces;
using System.Collections.Generic;
using Jhipster.Dto;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Linq;
using AutoMapper;
using System;
using Microsoft.EntityFrameworkCore;

namespace Jhipster.Domain.Services
{
    public class CategoryService : ICategoryService
    {
        protected readonly ICategoryRepository _categoryRepository;
        private readonly IRulesetService _rulesetService;
        private readonly ISelectorService _selectorService;
        private readonly IMapper _mapper;
        protected readonly IBirthdayService _birthdayService;

        public CategoryService(ICategoryRepository categoryRepository, IRulesetService rulesetService, ISelectorService selectorService, IMapper mapper, IBirthdayService birthdayService)
        {
            _categoryRepository = categoryRepository;
            _rulesetService = rulesetService;
            _selectorService = selectorService;
            _mapper = mapper;
            _birthdayService = birthdayService;  
        }

        public virtual async Task<Category> Save(Category category)
        {
            await _categoryRepository.CreateOrUpdateAsync(category);
            await _categoryRepository.SaveChangesAsync();
            return category;
        }

        public virtual async Task<IPage<Category>> FindAll(IPageable pageable, string query)
        {
            var page = await _categoryRepository.GetPageFilteredAsync(pageable, query);
            return page;
        }

        public virtual async Task<Category> FindOne(long id)
        {
            var result = await _categoryRepository.QueryHelper()
                .GetOneAsync(category => category.Id == id);
            return result;
        }

        public virtual async Task Delete(long id)
        {
            await _categoryRepository.DeleteByIdAsync(id);
            await _categoryRepository.SaveChangesAsync();
        }

        public virtual async Task<AnalysisResultDto> Analyze(IList<string> ids)
        {
            var pageable = JHipsterNet.Core.Pagination.PageableConstants.UnPaged;
            var result = await _selectorService.FindAll(pageable);{}
            List<SelectorDto> lstSelector = result.Content.Select(entity => _mapper.Map<SelectorDto>(entity)).ToList();
            List<SelectorForMatch> lstSelectorForMatch = new List<SelectorForMatch>();
            lstSelector.ForEach(async s=>{
                Ruleset ruleset = await _rulesetService.FindOneByName(s.Name);
                RulesetOrRule rulesetOrRule = JsonConvert.DeserializeObject<RulesetOrRule>(ruleset.JsonString);
                lstSelectorForMatch.Add(new SelectorForMatch{
                    selectorDto = s,
                    ruleset = rulesetOrRule
                });
            });
            string error = null;
            int countTries = 0;
            int countMatches = 0;
            for (int i = 0; i < ids.Count; i++){
                Birthday birthday = null;
                try {
                    birthday = await _birthdayService.FindOneWithText(ids[i]);
                } catch (Exception e){
                    error = $"Error doing anaysis {e.Message}";
                }
                if (error == null){
                    lstSelectorForMatch.ForEach(s=>{
                        countTries += 1;
                        if (Evaluate(birthday, s.ruleset)){
                            countMatches += 1;
                        }
                    });
                }
            }
            return new AnalysisResultDto{
                result = error == null ? $"Looked at {ids.Count} documents and got {countMatches} matches out of {countTries} comparisons." : error
            };
        }

        private bool Evaluate(Birthday birthday, RulesetOrRule set){
            if (set.rules == null){
                string fieldValue = "";
                switch (set.field){
                    case "document":
                        fieldValue = " " + Regex.Replace(birthday.Text, @"<[^>]*>", " ") + " " + birthday.Fname + " " + birthday.Lname + " " + birthday.Sign + " ";
                        break;
                    case "lname":
                        fieldValue = birthday.Lname;
                        break;
                    case "fname":
                        fieldValue = birthday.Fname;
                        break;
                    case "sign":
                        fieldValue = birthday.Sign;
                        break;
                }
                switch (set.@operator){
                    case "=":
                        return fieldValue == set.value;
                    case "contains":
                        string reString = "";
                        if (set.value.StartsWith("\"") && set.value.EndsWith("\"")){
                            string unquoted = set.value.Substring(1, set.value.Length -2);
                            reString = Regex.Replace(unquoted, @"[^A-Z\d]+", @"[^A-Z\d]+",RegexOptions.IgnoreCase);
                        } else {
                            reString =  @"[^A-Z\d]+" + set.value +  @"[^A-Z\d]+";
                        }
                        if (Regex.IsMatch(fieldValue, reString,RegexOptions.IgnoreCase)){
                            return true;
                        }
                        break;
                }
                return false;
            } else {
                bool evaluation = set.condition == "and" ? true : false;
                for (int i = 0; i < set.rules.Count; i++){
                    if (evaluation && set.condition == "and" & !Evaluate(birthday, set.rules[i])){
                        evaluation = false;
                        break;
                    } else {
                        // or
                        if (Evaluate(birthday, set.rules[i])){
                            evaluation = true;
                            break;
                        }
                    }
                }
                return set.not ? !evaluation : evaluation;
            }
        }
    }


    public class SelectorForMatch{
        public SelectorDto selectorDto { get; set; }
        public RulesetOrRule ruleset { get; set; }
    }

    public class RulesetOrRule{
        public string condition { get; set; }
        public List<RulesetOrRule> rules { get; set; }
        public bool not { get; set; }
        public string name { get; set; }
        public string field { get; set; }
        public string value { get; set; }
        public string @operator { get; set; }
    }    
}
