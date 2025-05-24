using System.ComponentModel.DataAnnotations;

namespace JhipsterSampleApplication.Dto
{
    public class NamedQueryDto
    {
        public long Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public string Text { get; set; }

        [Required]
        [StringLength(50)]
        public string Owner { get; set; }
    }
} 