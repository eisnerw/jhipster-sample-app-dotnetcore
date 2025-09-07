using System;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.IO;

namespace JhipsterSampleApplication.Domain.Services
{
    public class MovieBqlService : GenericBqlService<Movie>, IMovieBqlService
    {
        private static JObject LoadSpec()
        {
            var baseDir = AppContext.BaseDirectory;
            var specPath = Path.Combine(baseDir, "Resources", "query-builder", "movie-qb-spec.json");
            if (!File.Exists(specPath))
            {
                throw new FileNotFoundException($"BQL spec file not found at {specPath}");
            }
            var json = File.ReadAllText(specPath);
            return JObject.Parse(json);
        }

        public MovieBqlService(ILogger<MovieBqlService> logger, INamedQueryService namedQueryService) : base(logger, namedQueryService, LoadSpec(), "movies")
        {
        }
    }
}
