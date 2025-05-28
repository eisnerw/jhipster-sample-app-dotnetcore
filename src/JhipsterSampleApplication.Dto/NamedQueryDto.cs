using System.ComponentModel.DataAnnotations;

namespace JhipsterSampleApplication.Dto
{
    public class NamedQueryDto
    {
        public long Id { get; set; }

        [Required]
        public required string Name { get; set; }

        [Required]
        public required string Text { get; set; }

        public required string Owner { get; set; }
    }
} 