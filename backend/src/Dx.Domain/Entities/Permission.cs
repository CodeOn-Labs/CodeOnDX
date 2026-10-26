using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class Permission
{
    public Guid Id { get; set; }

    public string Key { get; set; } = null!;

    public string GroupKey { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? NameEn { get; set; }

    public int SortOrder { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
