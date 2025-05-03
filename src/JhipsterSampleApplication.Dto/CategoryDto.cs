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
        public string CategoryName { get; set; }

        public bool? Selected { get; set; }
        public bool? NotCategorized { get; set; }
        
        [StringLength(50)]
        public string FocusType { get; set; }
        
        [StringLength(100)]
        public string FocusId { get; set; }
        
        public string JsonString { get; set; }
        
        [StringLength(500)]
        public string Description { get; set; }

        [JsonIgnore]
        public IList<BirthdayDto> Birthdays { get; set; } = new List<BirthdayDto>();
    }
} 