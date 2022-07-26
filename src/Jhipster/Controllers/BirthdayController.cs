
using AutoMapper;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using Jhipster.Domain;
using Jhipster.Crosscutting.Enums;
using Jhipster.Crosscutting.Exceptions;
using Jhipster.Dto;
using Jhipster.Domain.Services.Interfaces;
using Jhipster.Web.Extensions;
using Jhipster.Web.Filters;
using Jhipster.Web.Rest.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Jhipster.Controllers
{
    [Authorize]
    [Route("api")]
    [ApiController]
    public class BirthdayController : ControllerBase
    {
        private const string EntityName = "birthday";
        private readonly IMapper _mapper;
        private readonly IBirthdayService _birthdayService;
        private readonly ILogger<BirthdayController> _log;

        public BirthdayController(ILogger<BirthdayController> log,
            IMapper mapper,
            IBirthdayService birthdayService)
        {
            _log = log;
            _mapper = mapper;
            _birthdayService = birthdayService;
        }

        [HttpPost("birthdays")]
        [ValidateModel]
        public async Task<ActionResult<BirthdayDto>> CreateBirthday([FromBody] BirthdayDto birthdayDto)
        {
            _log.LogDebug($"REST request to save Birthday : {birthdayDto}");
            if (birthdayDto.Id != "")
                throw new BadRequestAlertException("A new birthday cannot already have an ID", EntityName, "idexists");

            Birthday birthday = _mapper.Map<Birthday>(birthdayDto);
            await _birthdayService.Save(birthday);
            return CreatedAtAction(nameof(GetBirthday), new { id = birthday.Id }, birthday)
                .WithHeaders(HeaderUtil.CreateEntityCreationAlert(EntityName, birthday.Id.ToString()));
        }

        [HttpPut("birthdays")]
        [ValidateModel]
        public async Task<IActionResult> UpdateBirthday([FromBody] BirthdayDto birthdayDto)
        {
            _log.LogDebug($"REST request to update Birthday : {birthdayDto}");
            if (birthdayDto.Id == "") throw new BadRequestAlertException("Invalid Id", EntityName, "idnull");
            Birthday birthday = _mapper.Map<Birthday>(birthdayDto);
            await _birthdayService.Save(birthday);
            return Ok(birthday)
                .WithHeaders(HeaderUtil.CreateEntityUpdateAlert(EntityName, birthday.Id.ToString()));
        }

        [HttpPost("birthdayQuery")]
        public async Task<ActionResult<IEnumerable<BirthdayDto>>> GetAllBirthdays([FromBody] Dictionary<string, object> queryDictionary)
        {
            _log.LogDebug("REST request to get a page of Birthdays");
            var pageable = Pageable.Of(1, 10);
            String query = "";
            RulesetOrRule regexRulesetOrRule = null;
            if (queryDictionary.Keys.Contains("query")){
                query = (string)queryDictionary["query"];
            }
            if (query.StartsWith("{")){
                var birthdayRequest = JsonConvert.DeserializeObject<Dictionary<string,object>>(query);
                string birthdayQuery = "";
                if (birthdayRequest.ContainsKey("ids")){
                    List<string> ids = JsonConvert.DeserializeObject<List<string>>(birthdayRequest["ids"].ToString());
                    birthdayQuery = "_id:(\"" + (string.Join('`', ids).Replace("`", "\" \"")) + "\")";
                } else {
                    if (birthdayRequest.ContainsKey("query")){
                        birthdayQuery = (string)birthdayRequest["query"];
                    } else {
                        birthdayQuery = query;
                    }
                    if (birthdayQuery != ""){
                        birthdayRequest["queryRuleset"] = birthdayQuery;
                        RulesetOrRule rulesetOrRule = JsonConvert.DeserializeObject<RulesetOrRule>(birthdayQuery);
                        if (false && ContainsRegex(rulesetOrRule)){
                            regexRulesetOrRule = DeMorganRemoveNot(rulesetOrRule, false);
                            RulesetOrRule removed = RemoveRegex(regexRulesetOrRule);
                            if (removed == null){
                                birthdayQuery = "";
                            } else {
                                birthdayQuery = removed.ToString();
                            }
                        }
                        birthdayQuery = TextTemplate.Runner.Interpolate("LuceneQueryBuilder", birthdayQuery);
                    }
                }
                birthdayRequest["query"] = birthdayQuery;
                query = JsonConvert.SerializeObject(birthdayRequest);
            }
            var result = await _birthdayService.FindAll(pageable, query);
            List<BirthdayDto> lstBirthdays = new List<BirthdayDto>();
            foreach(Birthday entity in result.Content){
                if (regexRulesetOrRule == null || EvaluateWithRegex(entity, regexRulesetOrRule)){
                    lstBirthdays.Add(_mapper.Map<BirthdayDto>(entity));
                }
            }
            var page = new Page<BirthdayDto>(lstBirthdays, pageable, result.TotalElements);
            return Ok(((IPage<BirthdayDto>)page).Content).WithHeaders(page.GeneratePaginationHttpHeaders());
        }

        private bool EvaluateWithRegex(Birthday result, RulesetOrRule rulesetOrRule){
            bool evalResult = rulesetOrRule.condition == "and" ? true : false;
            if (rulesetOrRule.rules == null){
                string stringValue = rulesetOrRule.value.ToString();
                object value = "";
                switch (rulesetOrRule.field){
                    case "lname":
                        value = result.Lname.ToLower();
                        break;
                    case "fname":
                        value = result.Fname.ToLower();
                        break;
                    case "sign":
                        value = result.Lname.ToLower();
                        break;
                    case "document":
                        value = result.Text.ToLower();
                        break;
                }
                switch (rulesetOrRule.@operator){
                    case "!contains":
                    case "contains":
                        if (stringValue.StartsWith("/") && stringValue.EndsWith("/")){
                            evalResult = Regex.IsMatch(stringValue, stringValue.Substring(1, stringValue.Length - 2), RegexOptions.IgnoreCase);
                        } else if (((string)value).Contains(stringValue.ToLower())){
                            evalResult = true;
                        }
                        return rulesetOrRule.@operator.StartsWith("!") ? !evalResult : evalResult;

                    case "!=":
                    case "=":
                        evalResult = stringValue == (string)value;
                        return rulesetOrRule.@operator.StartsWith("!") ? !evalResult : evalResult;
                }
                return false;
            } else {
                rulesetOrRule.rules.ForEach(r=>{
                    if (rulesetOrRule.condition != "and" || r.rules != null || (r.@operator.EndsWith("contains") && r.value.ToString().StartsWith("/") && r.value.ToString().EndsWith("/"))){
                        if (rulesetOrRule.condition == "and" && evalResult){
                            evalResult = evalResult && EvaluateWithRegex(result, r);
                        } else if (rulesetOrRule.condition == "or" && !evalResult){
                            evalResult = evalResult || EvaluateWithRegex(result, r);
                        }
                    }
                });
                return evalResult;
            }
        }

        private bool ContainsRegex(RulesetOrRule rulesetOrRule){
            if (rulesetOrRule.rules == null){
                return rulesetOrRule.@operator.EndsWith("contains") && rulesetOrRule.value.ToString().StartsWith("/") && rulesetOrRule.value.ToString().EndsWith("/");
            }
            bool bContainsRegex = false;
            rulesetOrRule.rules.ForEach(r=>{
                if (ContainsRegex(r)){
                    bContainsRegex = true;
                }
            });
            return bContainsRegex;
        }

        private RulesetOrRule RemoveRegex(RulesetOrRule rulesetOrRule){
            RulesetOrRule returned = new RulesetOrRule();
            returned.rules = new List<RulesetOrRule>();
            returned.condition = rulesetOrRule.condition;
            bool bReturnNull = false;
            if (rulesetOrRule.condition == "and"){
                rulesetOrRule.rules.ForEach(r=>{
                    if (!(r.rules == null && r.@operator.EndsWith("contains") && r.value.ToString().StartsWith("/") && r.value.ToString().EndsWith("/"))){
                        if (r.rules == null){
                            returned.rules.Add(r);
                        } else {
                            RulesetOrRule removed = RemoveRegex(r);
                            if (removed != null){
                                returned.rules.Add(removed);
                            }
                        }
                    }
                });
            } else { // OR
                rulesetOrRule.rules.ForEach(r=>{
                    if (r.rules == null && r.@operator.EndsWith("contains") && r.value.ToString().StartsWith("/") && r.value.ToString().EndsWith("/")){
                        bReturnNull = true;
                    } else if (r.rules == null){
                        returned.rules.Add(r);
                    } else {
                        RulesetOrRule removed = RemoveRegex(r);
                        if (removed != null){
                            returned.rules.Add(removed);
                        } else {
                            bReturnNull = true;
                        }
                    }
                });
            }
            if (bReturnNull || returned.rules.Count == 0){
                return null;
            }
            return returned;
        }

        private RulesetOrRule DeMorganRemoveNot(RulesetOrRule rulesetOrRule, bool reverse){
            RulesetOrRule returned = new RulesetOrRule();
            if (rulesetOrRule.rules == null){
                if (!reverse){
                    return rulesetOrRule;
                }
                returned.field = rulesetOrRule.field;
                returned.value = rulesetOrRule.value;
                switch (rulesetOrRule.@operator){
                    case "=":
                        returned.@operator = "!=";
                        break;
                    case "!=":
                        returned.@operator = "=";
                        break;
                    case ">":
                        returned.@operator = "<=";
                        break;
                    case "<":
                        returned.@operator = "<=";
                        break;
                    case "<=":
                        returned.@operator = ">";
                        break;
                    case ">=":
                        returned.@operator = "<";
                        break;
                    case "in":
                        returned.@operator = "!in";
                        break;
                    case "!in":
                        returned.@operator = "in";
                        break;
                    case "contains":
                        returned.@operator = "!contains";
                        break;
                    case "!contains":
                        returned.@operator = "contains";
                        break;
                }
                return returned;
            }
            returned.rules = new List<RulesetOrRule>();
            if ((rulesetOrRule.not && reverse) || (!rulesetOrRule.not && !reverse)){
                returned.condition = rulesetOrRule.condition;
                rulesetOrRule.rules.ForEach(r=>{
                    returned.rules.Add(DeMorganRemoveNot(r, false));
                });
            } else {
                returned.condition = rulesetOrRule.condition == "and" ? "or" : "and";
                rulesetOrRule.rules.ForEach(r=>{
                    returned.rules.Add(DeMorganRemoveNot(r, true));
                });
            }
            return returned;
        }

        [AllowAnonymous]
        [HttpGet("birthdays/text/{id}")]
        public async Task<IActionResult> GetBirthdayText([FromRoute] string id)
        {
            _log.LogDebug($"REST request to get text from Birthday : {id}");
            string ret = await _birthdayService.FindOneText(id);
            return new ContentResult()
            {
                Content = ret,
                ContentType = "text/html",
            };
        }

        [HttpGet("birthdays/{id}")]
        public async Task<IActionResult> GetBirthday([FromRoute] string id)
        {
            _log.LogDebug($"REST request to get Birthday : {id}");
            var result = await _birthdayService.FindOne(id);
            BirthdayDto birthdayDto = _mapper.Map<BirthdayDto>(result);
            return ActionResultUtil.WrapOrNotFound(birthdayDto);
         }

        [HttpDelete("birthdays/{id}")]
        public async Task<IActionResult> DeleteBirthday([FromRoute] string id)
        {
            _log.LogDebug($"REST request to delete Birthday : {id}");
            await _birthdayService.Delete(id);
            return Ok().WithHeaders(HeaderUtil.CreateEntityDeletionAlert(EntityName, id.ToString()));
        }
    }

    public interface IBirthdayPageable : IPageable{
        public String query { get; }
    }
}
