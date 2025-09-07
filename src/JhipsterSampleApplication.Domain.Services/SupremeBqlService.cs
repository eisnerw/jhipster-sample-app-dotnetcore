using System;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Dto;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.IO;

namespace JhipsterSampleApplication.Domain.Services
{
	public class SupremeBqlService : GenericBqlService<Supreme>, ISupremeBqlService
	{
		private static JObject LoadSpec()
		{
			var baseDir = AppContext.BaseDirectory;
			var specPath = Path.Combine(baseDir, "Resources", "query-builder", "supreme-qb-spec.json");
			if (!File.Exists(specPath))
			{
				throw new FileNotFoundException($"BQL spec file not found at {specPath}");
			}
			var json = File.ReadAllText(specPath);
			return JObject.Parse(json);
		}

		public SupremeBqlService(ILogger<SupremeBqlService> logger, INamedQueryService namedQueryService) : base(logger, namedQueryService, LoadSpec(), "supreme")
		{
		}
	}
}
