using Nest;

namespace JhipsterSampleApplication.Domain.Services.Interfaces;

public interface IQueryBuilder
{
    SearchDescriptor<T> BuildSearchDescriptor<T>(string searchTerm, string[] fields, int from, int size, string sortField, bool ascending) where T : class;
    SearchDescriptor<T> BuildSearchDescriptor<T>(string searchTerm, string[] fields, int from, int size) where T : class;
} 