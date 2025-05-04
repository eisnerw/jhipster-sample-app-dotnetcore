using System;
using System.Collections.Generic;
using System.Linq;
using Nest;
using JHipsterNet.Core.Pagination;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Domain.Entities;
using Newtonsoft.Json.Linq;

namespace JhipsterSampleApplication.Infrastructure.Data
{
    public class BirthdayQueryBuilder : IQueryBuilder
    {
        private RulesetOrRule? _ruleset;
        private int _page;
        private int _size;
        private string _sortField = string.Empty;
        private bool _ascending;
        private string _filterField = string.Empty;
        private object? _filterValue;

        public IQueryBuilder WithRuleset(RulesetOrRule ruleset)
        {
            _ruleset = ruleset;
            return this;
        }

        public IQueryBuilder WithPagination(int page, int size)
        {
            _page = page;
            _size = size;
            return this;
        }

        public IQueryBuilder WithSort(string field, bool ascending)
        {
            _sortField = field ?? string.Empty;
            _ascending = ascending;
            return this;
        }

        public IQueryBuilder WithFilter(string field, object? value)
        {
            _filterField = field ?? string.Empty;
            _filterValue = value;
            return this;
        }

        public SearchDescriptor<Birthday> Build()
        {
            var searchDescriptor = new SearchDescriptor<Birthday>()
                .From(_page * _size)
                .Size(_size);

            if (!string.IsNullOrEmpty(_sortField))
            {
                searchDescriptor = searchDescriptor.Sort(s => s
                    .Field(f => f
                        .Field(_sortField)
                        .Order(_ascending ? SortOrder.Ascending : SortOrder.Descending)
                    )
                );
            }

            if (_ruleset != null)
            {
                var query = _ruleset.ToElasticSearch();
                if (query != null)
                {
                    searchDescriptor = searchDescriptor.Query(q => q.Raw(query.ToString()));
                }
            }

            if (!string.IsNullOrEmpty(_filterField) && _filterValue != null)
            {
                searchDescriptor = searchDescriptor.Query(q => q
                    .Term(_filterField, GetFilterValue(_filterValue)));
            }

            return searchDescriptor;
        }

        private static object GetFilterValue(object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return value switch
            {
                string s => s,
                int i => i,
                bool b => b,
                DateTime dt => dt,
                _ => value.ToString() ?? string.Empty
            };
        }

        public SearchDescriptor<T> BuildSearchDescriptor<T>(string searchTerm, string[] fields, int from, int size) where T : class
        {
            return BuildSearchDescriptor<T>(searchTerm, fields, from, size, sortField: null!, true);
        }

        public SearchDescriptor<T> BuildSearchDescriptor<T>(string searchTerm, string[] fields, int from, int size, string? sortField, bool ascending) where T : class
        {
            var searchDescriptor = new SearchDescriptor<T>();

            // Apply pagination
            searchDescriptor = searchDescriptor.From(from).Size(size);

            // Apply sorting
            if (!string.IsNullOrEmpty(sortField))
            {
                searchDescriptor = searchDescriptor.Sort(s => s
                    .Field(f => f
                        .Field(sortField)
                        .Order(ascending ? SortOrder.Ascending : SortOrder.Descending)
                    )
                );
            }

            // Apply search term if specified
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchDescriptor = searchDescriptor.Query(q => q
                    .MultiMatch(m => m
                        .Fields(fields)
                        .Query(searchTerm)
                        .Type(TextQueryType.PhrasePrefix)
                    )
                );
            }

            return searchDescriptor;
        }
    }
} 