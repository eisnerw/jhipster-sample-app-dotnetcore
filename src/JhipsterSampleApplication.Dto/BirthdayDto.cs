using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JhipsterSampleApplication.Dto
{
    public class BirthdayDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Lname { get; set; }
        public string? Fname { get; set; }
        public string? Sign { get; set; }
        public DateTime? Dob { get; set; }
        public bool? IsAlive { get; set; }
        public string? Text { get; set; }
        public string? Wikipedia { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
    }
} 