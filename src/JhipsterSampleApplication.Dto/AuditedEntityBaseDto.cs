using System;

namespace JhipsterSampleApplication.Dto;

public class AuditedEntityBaseDto
{
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string LastModifiedBy { get; set; } = string.Empty;
    public DateTime LastModifiedDate { get; set; }
}
