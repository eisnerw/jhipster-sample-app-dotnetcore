    using System.Collections.Generic;
    using JhipsterSampleApplication.Domain;

    namespace JhipsterSampleApplication.Domain.Entities
    {
        public class View<T>
        {
            public long Id { get; set; }
            public string? Name { get; set; }
            public string? Query { get; set; }
            public string? CategoryQuery { get; set; }
            public string? TopLevelCategory { get; set; }
            public string? aggregation { get; set; }
            public string? script { get; set; }
            public string? field { get; set; }
            public List<T>? focus { get; set; }
            public View<T>? topLevelView { get; set; }
        }
    }