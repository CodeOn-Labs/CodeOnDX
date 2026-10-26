using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class AccountToken
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Purpose { get; set; } = null!;

    public string TokenHash { get; set; } = null!;

    public string Email { get; set; } = null!;

    public Guid? UserId { get; set; }

    public Guid? EmployeeId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public virtual Employee? Employee { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual User? User { get; set; }
}
