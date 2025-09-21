using System.Collections.Generic;
namespace JhipsterSampleApplication.Domain.Entities
{
    public abstract class CategorizedEntity<TKey> : BaseEntity<TKey>
    {
        public List<string> categories { get; set; } = new List<string>();
    }
}