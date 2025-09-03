using Newtonsoft.Json.Linq;

namespace JhipsterSampleApplication.Dto
{
    /// <summary>
    /// Data transfer object representing a generic entity specification.
    /// </summary>
    public class EntityDto
    {
        public string Name { get; set; } = string.Empty;
        public JObject Spec { get; set; } = new JObject();
    }
}
