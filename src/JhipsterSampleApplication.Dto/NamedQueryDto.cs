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

        public string? Owner { get; set; }

        [Required]
        public required string Entity { get; set; }
    }
}