using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Jhipster.Domain
{

    public class RulesetOrRule
    {
        public string condition { get; set; }
        public object value { get; set; }
        public string @operator { get; set; }
        public Boolean @not { get; set; }
        public string field { get; set; }
        public List<RulesetOrRule> rules { get; set; }
        public override string ToString()
        {
            if (rules == null){
                return "{" + "\"field\":\"" + field + "\", \"operator\":\""  + @operator + "\", \"value\":\"" + value.ToString().Replace("\"","\\\"") + "\"}";
            } else {
                string listString = "";
                rules.ForEach(r=>{
                    listString += ((listString.Length > 0 ? ", " : "") + r.ToString());
                });
                return "{" + "\"condition\":\"" + condition + "\", \"not\":"  + (@not ? "true" : "false") + ", \"rules\":[" + listString + "]}";
            }
        }
    }
}
