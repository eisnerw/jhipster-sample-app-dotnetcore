namespace JhipsterSampleApplication.Dto
{
    public class ViewResultDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public bool? Selected { get; set; }
        public bool? NotCategorized { get; set; }
        public string? FocusType { get; set; }
        public string? FocusId { get; set; }
        public long? Count { get; set; }
    }
} 