using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class RolePermission
{
    public Guid TenantId { get; set; }

    public Guid RoleId { get; set; }

    public Guid PermissionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public virtual Permission Permission { get; set; } = null!;

    public virtual Role Role { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
