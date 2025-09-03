using Newtonsoft.Json.Linq;

namespace JhipsterSampleApplication.Domain.Entities
{
    /// <summary>
    /// Represents a generic entity specification loaded from the Resources/Entities folder.
    /// </summary>
    public class Entity
    {
        public string Name { get; set; } = string.Empty;
        public JObject Spec { get; set; } = new JObject();
    }
}
