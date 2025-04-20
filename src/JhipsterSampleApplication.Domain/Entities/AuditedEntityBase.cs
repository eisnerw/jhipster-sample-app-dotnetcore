using System;
using JhipsterSampleApplication.Domain.Entities.Interfaces;

namespace JhipsterSampleApplication.Domain.Entities;

public abstract class AuditedEntityBase<TKey> : BaseEntity<TKey>, IAuditedEntityBase
{
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string LastModifiedBy { get; set; } = string.Empty; 
    public DateTime LastModifiedDate { get; set; }
}
