using Nest;

namespace JhipsterSampleApplication.Infrastructure.Services;

public interface IGenericQueryBuilder
{
    SearchDescriptor<T> BuildSearchDescriptor<T>(string searchTerm, string[] fields, int from, int size, string sortField, bool ascending) where T : class;
    SearchDescriptor<T> BuildSearchDescriptor<T>(string searchTerm, string[] fields, int from, int size) where T : class;
} 