using System.Collections.Generic;
namespace JhipsterSampleApplication.Domain.Entities
{
    public class CategorizedEntity<TKey> : BaseEntity<TKey>
    {
        public List<string> Categories { get; set; } = new List<string>();
    }
}