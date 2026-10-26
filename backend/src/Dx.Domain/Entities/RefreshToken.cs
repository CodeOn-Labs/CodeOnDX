using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid TenantId { get; set; }

    public Guid FamilyId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedByIp { get; set; }

    public string? UserAgent { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    public Guid? ReplacedById { get; set; }

    public virtual ICollection<RefreshToken> InverseReplacedBy { get; set; } = new List<RefreshToken>();

    public virtual RefreshToken? ReplacedBy { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
