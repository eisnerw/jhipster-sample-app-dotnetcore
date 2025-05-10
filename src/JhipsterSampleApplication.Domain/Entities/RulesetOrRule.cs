using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace JhipsterSampleApplication.Domain.Entities
{

    public class RulesetOrRule
    {
        public string? field { get; set; }
        public string? @operator { get; set; }
        public object? value { get; set; }
        public string? condition { get; set; }
        public bool @not { get; set; }
        public List<RulesetOrRule>? rules { get; set; }
        public override string ToString()
        {
            if (rules == null){
                return "{" + "\"field\":\"" + field + "\", \"operator\":\""  + @operator + "\", \"value\":\"" + ((string)value!).ToString().Replace("\"","\\\"") + "\"}";
            } else {
                string listString = "";
                rules.ForEach(r=>{
                    listString += ((listString.Length > 0 ? ", " : "") + r.ToString());
                });
                return "{" + "\"condition\":\"" + condition + "\", \"not\":"  + (@not ? "true" : "false") + ", \"rules\":[" + listString + "]}";
            }
        }
        public Object ToElasticSearch(){
            if (rules == null){
                if (@operator == "contains"){
                    return new JObject{{
                        "term", new JObject{{
                            field!, (string)value!
                        }}
                    }};
                }
                return new JObject{{
                    "term", new JObject{{
                        "lname","johnson"
                    }}
                }};
            } else {
                List<Object> rls = new List<Object>();
                rules.ForEach(r=>{
                    rls.Add(r.ToElasticSearch());
                });
                if (condition == "and"){
                    return new JObject{{
                        "bool", new JObject{{
                            "must", JArray.FromObject(rls)
                        }}
                    }};
                }
                return new JObject{{
                    "bool", new JObject{{
                        "should", JArray.FromObject(rls)
                    }}
                }};          
            }
        }
        private string ToCaseInsensitiveRegEx(){
            return null!;
        }
    }
}
