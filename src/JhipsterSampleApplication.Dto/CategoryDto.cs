using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace JhipsterSampleApplication.Dto
{
    public class CategoryDto
    {
        public long Id { get; set; }

        [Required]
        [StringLength(100)]
        public required string CategoryName { get; set; }

        public string? ParentCategoryName { get; set; }
        public long? ParentCategoryId { get; set; }
        
        [Required]
        [StringLength(50)]
        public required string FocusType { get; set; }
        
        public string? FocusName { get; set; }
        
        [Required]
        [StringLength(100)]
        public required string FocusId { get; set; }
        
        [Required]
        [StringLength(100)]
        public required string JsonString { get; set; }
        
        public string? JsonString2 { get; set; }
        
        [Required]
        [StringLength(500)]
        public required string Description { get; set; }

        [JsonIgnore]
        public IList<BirthdayDto> Birthdays { get; set; } = new List<BirthdayDto>();
    }
} 