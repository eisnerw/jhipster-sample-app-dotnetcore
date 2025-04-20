namespace JhipsterSampleApplication.Infrastructure.Configuration;

public class SecuritySettings
{
    public Authentication? Authentication { get; set; }
    public Cors? Cors { get; set; }
    public bool EnforceHttps { get; set; }
}

public class Authentication
{
    public Jwt? Jwt { get; set; }
}

public class Jwt
{
    public string Secret { get; set; } = string.Empty;
    public string Base64Secret { get; set; } = string.Empty;
    public int TokenValidityInSeconds { get; set; }
    public int TokenValidityInSecondsForRememberMe { get; set; }
}


public class Cors
{
    public string AllowedOrigins { get; set; } = string.Empty;
    public string AllowedMethods { get; set; } = string.Empty;
    public string AllowedHeaders { get; set; } = string.Empty;
    public string ExposedHeaders { get; set; } = string.Empty;
    public bool AllowCredentials { get; set; }
    public int MaxAge { get; set; }
}
