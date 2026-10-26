using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class SystemRule
{
    public string Key { get; set; } = null!;

    public string Module { get; set; } = null!;

    public string ValueType { get; set; } = null!;

    public string DefaultValue { get; set; } = null!;

    public decimal? MinValue { get; set; }

    public decimal? MaxValue { get; set; }

    public string Name { get; set; } = null!;

    public string? NameEn { get; set; }

    public string? Description { get; set; }

    public string? DescriptionEn { get; set; }

    public int SortOrder { get; set; }

    public virtual ICollection<TenantRule> TenantRules { get; set; } = new List<TenantRule>();
}
