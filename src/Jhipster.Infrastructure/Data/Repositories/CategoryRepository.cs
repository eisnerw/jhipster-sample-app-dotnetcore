using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JHipsterNet.Core.Pagination;
using JHipsterNet.Core.Pagination.Extensions;
using Jhipster.Domain;
using Jhipster.Domain.Repositories.Interfaces;
using Jhipster.Infrastructure.Data.Extensions;
using System;
using Nest;
using Jhipster.Infrastructure.Data;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Jhipster.Domain.Services.Interfaces;

namespace Jhipster.Infrastructure.Data.Repositories
{
    public class CategoryRepository : GenericRepository<Category>, ICategoryRepository
    {
        private static Uri node = new Uri("https://texttemplate-testing-7087740692.us-east-1.bonsaisearch.net/");
        private static Nest.ConnectionSettings setting = new Nest.ConnectionSettings(node).BasicAuthentication("7303xa0iq9","4cdkz0o14").DefaultIndex("birthdays");
        private static ElasticClient elastic = new ElasticClient(setting);        
        protected readonly IBirthdayService _birthdayService;   
        public CategoryRepository(IUnitOfWork context, IBirthdayService birthdayService) : base(context)
        {
            _birthdayService = birthdayService;
        }
     

        public override async Task<Category> CreateOrUpdateAsync(Category category)
        {
            bool exists = await Exists(x => x.Id == category.Id);

            if (category.Id != 0 && exists)
            {
                Update(category);
            }
            else
            {
                _context.AddOrUpdateGraph(category);
            }
            return category;
        }
        public override async Task<IPage<Category>> GetPageFilteredAsync(IPageable pageable, string queryJson){
            long id = 0;
            List<Category> content = new List<Category>();
            if (!queryJson.StartsWith("{")){
                var result = await elastic.SearchAsync<Aggregation>(q => q
                    .Size(0).Index("birthdays").Aggregations(agg => agg.Terms(
                        "distinct", e =>
                            e.Field("categories.keyword").Size(10000)
                        )
                    )
                );
                Dictionary<string, Category> allCategories = new Dictionary<string, Category>();
                ((BucketAggregate)result.Aggregations.ToList()[0].Value).Items.ToList().ForEach(it => {
                    KeyedBucket<Object> kb = (KeyedBucket<Object>)it;
                    string categoryName = kb.KeyAsString != null ? kb.KeyAsString : (string)kb.Key;
                    allCategories.Add(categoryName, new Category
                    {
                        CategoryName = categoryName,
                        Id = ++id
                    });
                });
                Birthday birthday =  await _birthdayService.FindOne(queryJson);
                if (birthday.Categories != null)
                {
                    birthday.Categories.ForEach(c => {
                        allCategories[c.CategoryName].selected = true;
                    });
                }
                allCategories.Keys.ToList().OrderBy(k => k).ToList().ForEach(k=>{
                    content.Add(allCategories[k]);
                });
                return new Page<Category>(content, pageable, content.Count);
            }
            var categoryRequest = JsonConvert.DeserializeObject<Dictionary<string,object>>(queryJson);
            Dictionary<string, string> view = new Dictionary<string, string>();
            string aggregationKey = "";
            string query = "";
            if (categoryRequest["view"] == null){
                // default
                content.Add(new Category{
                    CategoryName = "(Uncategorized)",
                    selected = false,
                    notCategorized = true
                });
            } else {
                view = JsonConvert.DeserializeObject<Dictionary<string,string>>(categoryRequest["view"].ToString());
                aggregationKey = view["aggregation" ];
                query = view["query"] + ((string)categoryRequest["query"] != "" ? " AND " + categoryRequest["query"] : "");
                var result = await elastic.SearchAsync<Aggregation>(q => q
                    .Size(0)
                    .Index("birthdays")
                    .Aggregations(agg => agg.Terms(
                        "distinct", e =>
                            (view != null && view.Keys.Contains("script") 
                                ? e.Script(view["script"]) 
                                : e.Field(aggregationKey)
                            )                   
                            .Size(10000)
                        )
                    )
                    .QueryOnQueryString(query)
                );
                ((BucketAggregate)result.Aggregations.ToList()[0].Value).Items.ToList().ForEach(it=>{
                    KeyedBucket<Object> kb = (KeyedBucket<Object>)it;
                    string categoryName = kb.KeyAsString != null ? kb.KeyAsString : (string)kb.Key;
                    if (Regex.IsMatch(categoryName, @"\d{4,4}-\d{2,2}-\d{2,2}T\d{2,2}:\d{2,2}:\d{2,2}.\d{3,3}Z")){
                        categoryName = Regex.Replace(categoryName, @"(\d{4,4})-(\d{2,2})-(\d{2,2})T\d{2,2}:\d{2,2}:\d{2,2}.\d{3,3}Z","$1-$2-$3");
                    }
                    content.Add(new Category{
                        CategoryName = categoryName,
                        Id = ++id
                    });

                    var x = kb.Key;
                });
                content = content.OrderBy(cat => cat.CategoryName).ToList();
                result = await elastic.SearchAsync<Aggregation>(q => q
                    .Size(0)
                    .Index("birthdays")
                    .QueryOnQueryString("-" + view["query"])
                );
                if (result.Total > 0){
                    content.Add(new Category{
                        CategoryName = "(Uncategorized)",
                        selected = false,
                        notCategorized = true
                    });
                }
            }
            return new Page<Category>(content, pageable, content.Count);
        }

        class Aggregation{
            string key {get; set;}
            int doc_count {get; set;}
            object[] distinct {get; set;}
        }
    }
}
