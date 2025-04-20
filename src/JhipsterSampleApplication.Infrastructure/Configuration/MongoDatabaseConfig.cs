namespace JhipsterSampleApplication.Infrastructure.Configuration;

public class MongoDatabaseConfig : IMongoDatabaseConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}
