namespace JhipsterSampleApplication.Dto
{
    public class ClusterHealthDto
    {
        public string Status { get; set; } = string.Empty;
        public int NumberOfNodes { get; set; }
        public int NumberOfDataNodes { get; set; }
        public int ActiveShards { get; set; }
        public int ActivePrimaryShards { get; set; }
    }
}
