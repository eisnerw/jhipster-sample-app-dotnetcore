using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JHipsterNet.Core.Pagination;
using JHipsterNet.Core.Pagination.Extensions;
using JhipsterSampleApplication.Domain;
using JhipsterSampleApplication.Domain.Repositories.Interfaces;
using JhipsterSampleApplication.Infrastructure.Data.Extensions;
using System;
using Nest;
using JhipsterSampleApplication.Infrastructure.Data;
using System.Linq.Expressions;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Query;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Infrastructure.Data.Repositories
{
    public class BirthdayRepository : GenericRepository<Birthday, long>, IBirthdayRepository
    {
        private static Uri node = new Uri("https://texttemplate-testing-7087740692.us-east-1.bonsaisearch.net/");
        private static Nest.ConnectionSettings setting = new Nest.ConnectionSettings(node).BasicAuthentication("7303xa0iq9","4cdkz0o14").DefaultIndex("birthdays");
        private static ElasticClient elastic = new ElasticClient(setting);
        public BirthdayRepository(IUnitOfWork context) : base(context)
        {
        }

        static Dictionary<string, List<string>> refKeys = new Dictionary<string, List<string>>{
            {"Hank Aaron",  new List<string>{"Hank Aaron"}},
             {"Amy Winehouse",  new List<string>{"Amy Winehouse"}},
            {"Oprah Winfrey",  new List<string>{"Oprah Winfrey"}},
            {"Kate Winslet",  new List<string>{"Kate Winslet"}},
            {"Anna Wintour",  new List<string>{"Anna Wintour"}},
            {"Tom Wolfe",  new List<string>{"Tom Wolfe"}},
            {"Paul D Wolfowitz",  new List<string>{"Paul Wolfowitz"}},
            {"Stevie Wonder",  new List<string>{"Stevie Wonder"}},
            {"Tiger Woods",  new List<string>{"Tiger Woods"}},
            {"Bob Woodward",  new List<string>{"Bob Woodward"}},
            {"Joanne Woodward",  new List<string>{"Joanne Woodward"}},
            {"Virginia Woolf",  new List<string>{"Virginia Woolf"}},
            {"Frank Lloyd Wright",  new List<string>{"Frank Lloyd Wright"}},
            {"Andrew Wyeth",  new List<string>{"Andrew Wyeth"}},
            {"William Butler Yeats",  new List<string>{"William Butler Yeats"}},
            {"Francesca Zambello",  new List<string>{"Francesca Zambello"}},
            {"Frank Zappa",  new List<string>{"Frank Zappa"}},
            {"Renee Zellweger",  new List<string>{"Renee Zellweger"}},
            {"Catherine Zeta-Jones",  new List<string>{"Catherine Zeta-Jones"}},
            {" Zhao Ziyang",  new List<string>{"Zhao Ziyang"}},
            {"Pinchas Zukerman",  new List<string>{"Pinchas Zukerman"}}
        };

        public override async Task<Birthday> CreateOrUpdateAsync(Birthday birthday)
        {
            List<string> categoryStrings = new List<string>();
            birthday.Categories.ForEach(c => {
                categoryStrings.Add(c.CategoryName!);
            });
            ElasticBirthday elasticBirthday = new ElasticBirthday
            {
                dob = (System.DateTime)birthday.Dob!,
                fname = birthday.Fname!,
                Id = birthday.ElasticId!,
                lname = birthday.Lname!,
                sign = birthday.Sign!,
                isAlive = (bool)birthday.IsAlive!,
                categories = categoryStrings.ToArray<string>()
            };
            await elastic.UpdateAsync<ElasticBirthday>(new DocumentPath<ElasticBirthday>(elasticBirthday.Id), u =>
                u.Index("birthdays").Doc(elasticBirthday)
            );
            if (categoryStrings.Count == 0)
            {
                await elastic.UpdateAsync<ElasticBirthday>(new DocumentPath<ElasticBirthday>(elasticBirthday.Id), u =>
                    u.Script(s => s.Source("ctx._source.remove('categories')"))
                );
            }
            return birthday;
        }
        public override async Task<IPage<Birthday>> GetPageAsync(IPageable pageable)
        {
            return await GetPageFilteredAsync(pageable, "");
        }

        public async Task<IPage<Birthday>> GetPageFilteredAsync(IPageable pageable, string queryJson)
        {
            if (!queryJson.StartsWith("{"))
            {
                // backwards compatibility
                Dictionary<string, object> queryObject = new Dictionary<string, object>();
                queryObject["query"] = queryJson;
                queryObject["view"] = null!;
                queryJson = JsonConvert.SerializeObject(queryObject);
            }
            var birthdayRequest = JsonConvert.DeserializeObject<Dictionary<string, object>>(queryJson)!;
            View<Birthday> view = new View<Birthday>();
            string query = birthdayRequest.ContainsKey("query") ? (string)birthdayRequest["query"] : "";
            if (birthdayRequest.ContainsKey("view") && birthdayRequest["view"] != null)
            {
                view = JsonConvert.DeserializeObject<View<Birthday>>(((string)birthdayRequest["view"]).ToString())!;
            }
            string categoryClause = "";
            if (birthdayRequest.ContainsKey("category") && view!.field != null)
            {
                if (view.categoryQuery != null)
                {
                    categoryClause = view.categoryQuery.Replace("{}", (string)birthdayRequest["category"]);
                }
                else if (!birthdayRequest.ContainsKey("focusType"))
                {
                    categoryClause = (string)birthdayRequest["category"] == "-" ? "-" + view.field + ":*" : view.field + ":\"" + birthdayRequest["category"] + "\"";
                }
                query = categoryClause + (query != "" ? " AND (" + query + ")" : "");
                if (view.topLevelView != null)
                {
                    View<Birthday> topLevelView = view.topLevelView;
                    categoryClause = "";
                    if (view.topLevelCategory != null && topLevelView.field != null)
                    {
                        if (birthdayRequest.ContainsKey("focusType"))
                        {
                            string focusType = (string)birthdayRequest["focusType"];
                            if (focusType == "FOCUS")
                            {
                                categoryClause = "_id:" + birthdayRequest["focusId"];
                            }
                            else
                            {
                                categoryClause = "*:columbus";
                            }
                        }
                        else if (topLevelView.categoryQuery != null)
                        {
                            categoryClause = (view?.categoryQuery ?? string.Empty).Replace("{}", view?.topLevelCategory ?? string.Empty);;
                        }
                        else
                        {
                            categoryClause = view.topLevelCategory == "-" ? "-" + topLevelView.field + ":*" : topLevelView.field + ":\"" + view.topLevelCategory + "\"";
                        }
                        query = categoryClause + (query.Length > 1 ? " AND (" + query + ")" : "");
                    }
                }
            }
            ISearchResponse<ElasticBirthday> searchResponse = null!;
            if (query == "" || query == "()")
            {
                searchResponse = await elastic.SearchAsync<ElasticBirthday>(s => s
                    .Size(10000)
                    .Query(q => q
                        .MatchAll()
                    )
                );
            }
            else if (birthdayRequest.ContainsKey("queryRuleset"))
            {
                RulesetOrRule queryRuleset = JsonConvert.DeserializeObject<RulesetOrRule>((string)birthdayRequest["queryRuleset"])!;
                JObject obj = (JObject) await RulesetToElasticSearch(queryRuleset);
                searchResponse = await elastic.SearchAsync<ElasticBirthday>(s => s
                    .Index("birthdays")
                    .Size(10000)
                    .Query(q => q
                        .Raw(obj.ToString())
                    )
                );
            } else
            {
                searchResponse = await elastic.SearchAsync<ElasticBirthday>(x => x
                    .Index("birthdays")
                    .QueryOnQueryString(query)
                    .Size(10000)
                );
            }
            List<Birthday> content = new List<Birthday>();
            Console.WriteLine(searchResponse.Hits.Count + " hits");
            int Id = 0;            
            foreach (var hit in searchResponse.Hits)
            {
                List<Category> listCategory = new List<Category>();
                if (hit.Source.categories != null)
                {
                    hit.Source.categories.ToList().ForEach(c => {
                        listCategory.Add(new Category
                        {
                            CategoryName = c
                        });
                    });
                }
                content.Add(new Birthday
                {
                    Id = Id++,
                    ElasticId = hit.Id,
                    Lname = hit.Source.lname,
                    Fname = hit.Source.fname,
                    Dob = hit.Source.dob,
                    Sign = hit.Source.sign,
                    IsAlive = hit.Source.isAlive,
                    Categories = listCategory,
                    Text = Regex.Replace(hit.Source.wikipedia!, "<.*?>|&.*?;", string.Empty)
                });
            }
            content = content.OrderBy(b => b.Dob).ToList();
            return new Page<Birthday>(content, pageable, content.Count);
        }

        public async Task<List<string>> GetUniqueFieldValuesAsync(string field)
        {
            var result = await elastic.SearchAsync<Aggregation>(q => q
                .Size(0).Index("birthdays").Aggregations(agg => agg.Terms(
                    "distinct", e =>
                        e.Field(field).Size(10000)
                    )
                )
            );
            List<string> ret = new List<string>();
            ((BucketAggregate)result.Aggregations.ToList()[0].Value).Items.ToList().ForEach(it =>
            {
                KeyedBucket<Object> kb = (KeyedBucket<Object>)it;
                ret.Add(kb.KeyAsString != null ? kb.KeyAsString : (string)kb.Key);
            });
            return ret;
        }

        private async Task<Object> RulesetToElasticSearch(RulesetOrRule rr)
        {
            // this routine converts rulesets into elasticsearch DSL as json.  For inexact matching (contains), it uses the field.  For exact matching (=),
            // it uses the keyworkd fields.  Since those are case sensitive, it forces a search for all cased values that would match insenitively
            if (rr.rules == null)
            {
                JObject ret = new JObject{{
                    "term", new JObject{{
                        "BOGUSFIELD", "CANTMATCH"
                    }}
                }};
                if (rr!.@operator!.Contains("contains"))
                {
                    string stringValue = (string)rr!.value!;
                    if (stringValue.StartsWith("/") && (stringValue.EndsWith("/") || stringValue.EndsWith("/i")))
                    {
                        Boolean bCaseInsensitive = stringValue.EndsWith("/i");
                        string re = (rr?.value?.ToString() ?? string.Empty).Substring(1, (rr?.value?.ToString()?.Length ?? 0) - (bCaseInsensitive ? 3 : 2));
                        string regex = ToElasticRegEx(re.Replace(@"\\",@"\"), bCaseInsensitive);
                        if (regex.StartsWith("^"))
                        {
                            regex = regex.Substring(1, regex.Length - 1);
                        }
                        else
                        {
                            regex = ".*" + regex;
                        }
                        if (regex.EndsWith("$"))
                        {
                            regex = regex.Substring(0, regex.Length - 1);
                        }
                        else
                        {
                            regex += ".*";
                        }
                        if (rr!.field == "document")
                        {
                            List<JObject> lstRegexes = "wikipedia,fname,lname,categories,sign,id".Split(',').ToList().Select(s =>
                            {
                                return new JObject{{
                                    "regexp", new JObject{{
                                        s + ".keyword", new JObject{
                                            { "value", regex}
                                            ,{ "flags", "ALL" }
                                            ,{ "rewrite", "constant_score" }
                                        }
                                    }}
                                }};
                            }).ToList();
                            return new JObject{{
                                "bool", new JObject{{
                                    "should", JArray.FromObject(lstRegexes)
                                }}
                            }};
                        }
                        return new JObject{{
                            "regexp", new JObject{{
                                rr.field + ".keyword", new JObject{
                                    { "value", regex}
                                    ,{ "flags", "ALL" }
                                    ,{ "rewrite", "constant_score" }
                                }
                            }}
                        }};
                    }
                    string quote = Regex.IsMatch(rr?.value?.ToString() ?? string.Empty, @"\W") ? "\"" : string.Empty;
                    ret = new JObject{{
                        "query_string", new JObject{{
                            "query", (rr!.field != "document" ? (rr!.field + ":") : "") + quote + ((string)rr!.value!).ToLower().Replace(@"""", @"\""") + quote
                        }}
                    }};
                }
                else if (rr.@operator.Contains("="))
                {
                    List<string> uniqueValues = await GetUniqueFieldValuesAsync(rr!.field! + ".keyword");
                    List<JObject> oredTerms = uniqueValues.Where(v => string.Equals(v, rr?.value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase)).Select(s =>
                    {
                        return new JObject{{
                            "term", new JObject{{
                                rr.field + ".keyword", s
                            }}
                        }};
                    }).ToList();
                    if (oredTerms.Count > 1)
                    {
                        ret = new JObject{{
                            "bool", new JObject{{
                                "should", JArray.FromObject(oredTerms)
                            }}
                        }};
                    }
                    else if (oredTerms.Count == 1)
                    {
                        ret = oredTerms[0];
                    }
                } else if (rr.@operator.Contains("in")) {
                    List<string> uniqueValues = await GetUniqueFieldValuesAsync(rr.field + ".keyword");
                    // The following creates a list of case sensitive possibilities for the case sensitive 'term' query from case insensitive terms
                    List<string> caseSensitiveMatches = ((JArray)rr!.value!).Select(v =>
                    {
                        return uniqueValues.Where(s => s.ToLower() == v.ToString().ToLower());
                    }).Aggregate((agg,list) => {
                        return agg.Concat(list).ToList();
                    }).ToList();
                    return new JObject{{
                        "terms", new JObject{{
                            rr.field + ".keyword", JArray.FromObject(caseSensitiveMatches)
                        }}
                    }};
                } else if (rr.@operator.Contains("exists")) {
                    List<JObject> lstExists = new List<JObject>();
                    List<JObject> lstEmptyString = new List<JObject>();
                    lstEmptyString.Add(new JObject{{
                        "term", new JObject{{
                            rr.field + ".keyword",""
                        }}
                    }});
                    lstExists.Add(new JObject{{
                        "exists", new JObject{{
                            "field", rr.field
                        }}
                    }});
                    lstExists.Add(new JObject{{
                        "bool", new JObject{{
                            "must_not", JArray.FromObject(lstEmptyString)
                        }}
                    }});
                    ret = new JObject{{
                        "bool", new JObject{{
                            "must", JArray.FromObject(lstExists)
                        }}
                    }};
                }
                if (rr.@operator.Contains("!") || (rr.@operator == "exists" && !(rr.value != null && (Boolean)rr.value))){
                    ret = new JObject {{
                        "bool", new JObject{{
                            "must_not", JObject.FromObject(ret)
                        }}
                    }};
                }
                return ret;
            }
            else
            {
                List<Object> rls = new List<Object>();
                for (int i = 0; i < rr.rules.Count; i++)
                {
                    rls.Add(await RulesetToElasticSearch(rr.rules[i]));
                }
                if (rr.condition == "and")
                {
                    return new JObject{{
                        "bool", new JObject{{
                            rr.not == true ? "must_not" : "must", JArray.FromObject(rls)
                        }}
                    }};
                }
                Object ret = new JObject{{
                    "bool", new JObject{{
                        "should", JArray.FromObject(rls)
                    }}
                }};
                if (rr.not == true)
                {
                    ret = new JObject{{
                        "bool", new JObject{{
                            "must_not", JObject.FromObject(ret)
                        }}
                    }};
                }
                return ret;
            }
        }
        private string ToElasticRegEx(string pattern, Boolean bCaseInsensitive)
        {
            string ret = "";
            string[] regexTokens = Regex.Replace(pattern, @"([\[\]]|\\\\|\\\[|\\\]|\\s|\\S|\\w|\\W|\\d|\\D|.)", "`$1").Split('`');
            Boolean bBracketed = false;
            for (int i = 1; i < regexTokens.Length; i++){
                if (bBracketed){
                    switch (regexTokens[i]){
                        case "]":
                            bBracketed = false;
                            ret += regexTokens[i];
                            break;
                        case @"\s":
                            ret += " \n\t\r";
                            break;
                        case @"\d":
                            ret += "0-9";
                            break;
                        case @"\w":
                            ret += "A-Za-z_";
                            break;
                        default:
                            if (bCaseInsensitive && Regex.IsMatch(regexTokens[i], @"^[A-Za-z]+$")){
                                if ((i + 2) < regexTokens.Length && regexTokens[i + 1] == "-" && Regex.IsMatch(regexTokens[i + 2], @"^[A-Za-z]+$")){
                                    // alpha rannge
                                    ret += (regexTokens[i].ToLower() + "-" + regexTokens[i + 2].ToLower() + regexTokens[i].ToUpper() + "-" + regexTokens[i + 2].ToUpper());
                                    i += 2;
                                } else {
                                    ret += (regexTokens[i].ToLower() + regexTokens[i].ToUpper());
                                }
                            } else {
                                ret += regexTokens[i];
                            }
                            break;
                    }
                } else if (regexTokens[i] == "["){
                    bBracketed = true;
                    ret += regexTokens[i];
                } else if (regexTokens[i] == @"\s"){
                    ret += (@"[ \n\t\r]");
                } else if (regexTokens[i] == @"\d"){
                    ret += (@"[0-9]");
                } else if (regexTokens[i] == @"\w"){
                    ret += (@"[A-Za-z_]");
                } else if (bCaseInsensitive && Regex.IsMatch(regexTokens[i], @"[A-Za-z]")){
                    ret += ("[" + regexTokens[i].ToLower() + regexTokens[i].ToUpper() + "]");
                } else {
                    ret += regexTokens[i];
                }
            }
            return ret;
        }

        private class ElasticBirthday
        {
            public string? Id { get; set; }
            public string? lname { get; set; }
            public string? fname { get; set; }
            public DateTime dob { get; set; }
            public string? sign { get; set; }
            public bool isAlive { get; set; }
            public string[]? categories {get; set; }
            public string? wikipedia {get; set; }
        }
        public async Task<Birthday> GetOneAsync(object id)
        {
            return await GetOneAsync(id, false);
        }
        public async Task<Birthday> GetOneAsync(object id, bool bText)
        {
            var hit = await elastic.GetAsync<ElasticBirthday>((string)id);
            Birthday birthday = new Birthday
            {
                ElasticId = hit.Id,
                Lname = hit.Source.lname,
                Fname = hit.Source.fname,
                Dob = hit.Source.dob,
                Sign = hit.Source.sign,
                IsAlive = hit.Source.isAlive,
                Categories = new List<Category>()
            };
            if (bText)
            {
                birthday.Text = hit.Source.wikipedia;
            }
            if (hit.Source.categories != null)
            {
                hit.Source.categories.ToList().ForEach(c => {
                    Category category = new Category
                    {
                        CategoryName = c
                    };
                    birthday.Categories.Add(category);
                });
            }
            return birthday;
        }
        public async Task<string> GetOneTextAsync(object id)
        {
            var hit = await elastic.GetAsync<ElasticBirthday>((string)id);
            return hit.Source.wikipedia!;
        }

        public async Task<List<Birthday>?> GetReferencesFromAsync(string id)
        {
            await Task.Yield();    // satisfy the compiler’s “must await” rule
            return null;
        }

        public async Task<List<Birthday>> GetReferencesToAsync(string id)
        {
            List<Birthday> birthdays = new List<Birthday>();
            Birthday bday = await GetOneAsync(id);
            string key = bday.Fname + " " + bday.Lname;
            string query = "\"" + bday.Fname + " " + bday.Lname + "\"~4";
            if (refKeys.ContainsKey(key))
            {
                query = "\"" + String.Join("\"~4 OR \"", refKeys[key]) + "\"~4";
            }
            var searchResponse = await elastic.SearchAsync<ElasticBirthday>(x => x
                .Index("birthdays")
                .QueryOnQueryString(query)
                .Size(10000)
            );
            foreach (var hit in searchResponse.Hits)
            {
                if (hit.Id != id)
                {
                    List<Category> listCategory = new List<Category>();
                    if (hit.Source.categories != null)
                    {
                        hit.Source.categories.ToList().ForEach(c => {
                            listCategory.Add(new Category
                            {
                                CategoryName = c
                            });
                        });
                    }
                    birthdays.Add(new Birthday
                    {
                        ElasticId = hit.Id,
                        Lname = hit.Source.lname,
                        Fname = hit.Source.fname,
                        Dob = hit.Source.dob,
                        Sign = hit.Source.sign,
                        IsAlive = hit.Source.isAlive,
                        Categories = listCategory
                    });
                }
            }
            return birthdays;
        }
    }
}
