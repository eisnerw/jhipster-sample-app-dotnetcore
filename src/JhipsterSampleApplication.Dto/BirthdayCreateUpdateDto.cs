using System;
using System.Collections.Generic;

namespace JhipsterSampleApplication.Dto
{
    public class BirthdayCreateUpdateDto
    {
        public string? Id { get; set; }
        public string? Lname { get; set; }
        public string? Fname { get; set; }
        public string? Sign { get; set; }
        public DateTime? Dob { get; set; }
        public bool? IsAlive { get; set; }
        public string? Text { get; set; }
        public string? Wikipedia { get; set; }
        public List<long>? CategoryIds { get; set; }  // Flattened relationship
    }
} 