using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class Role
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? NameEn { get; set; }

    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
