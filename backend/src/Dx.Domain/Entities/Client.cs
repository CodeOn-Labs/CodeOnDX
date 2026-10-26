using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class Client
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? ShortName { get; set; }

    public Guid? CountryId { get; set; }

    public Guid? IndustryId { get; set; }

    public string? ContactName { get; set; }

    public string? ContactEmail { get; set; }

    public string? DefaultCurrencyCode { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }

    public virtual MasterDatum? Country { get; set; }

    public virtual MasterDatum? Industry { get; set; }

    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

    public virtual Tenant Tenant { get; set; } = null!;
}
