namespace JhipsterSampleApplication.Dto
{
    public class HistoryDto
    {
        public long Id { get; set; }
        public string? User { get; set; }
        public string? Entity { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
