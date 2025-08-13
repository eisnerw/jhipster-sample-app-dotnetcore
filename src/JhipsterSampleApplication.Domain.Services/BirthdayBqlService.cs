using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Dto;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;

namespace JhipsterSampleApplication.Domain.Services
{
    public class BirthdayBqlService : GenericBqlService<Birthday>, IBirthdayBqlService
    {
        private static JObject LoadSpec()
        {
            var baseDir = AppContext.BaseDirectory;
            var specPath = Path.Combine(baseDir, "Resources", "query-builder", "birthday-qb-spec.json");
            if (!File.Exists(specPath))
            {
                throw new FileNotFoundException($"BQL spec file not found at {specPath}");
            }
            var json = File.ReadAllText(specPath);
            return JObject.Parse(json);
        }

        public BirthdayBqlService(ILogger<BirthdayBqlService> logger, INamedQueryService namedQueryService) : base(logger, namedQueryService, LoadSpec())
        {
        }

        protected override bool ValidateRuleset(RulesetDto ruleset)
        {
            if (!base.ValidateRuleset(ruleset))
            {
                return false;
            }
            return true;
        }
    }
} 