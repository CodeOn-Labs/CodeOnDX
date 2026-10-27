using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class TenantIntegration
{
    public Guid TenantId { get; set; }

    public string IntegrationKey { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public bool UseMock { get; set; }

    public string Settings { get; set; } = null!;

    public string? SecretsEncrypted { get; set; }

    public DateTime? LastSyncAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public DateTime? LastTestAt { get; set; }

    public bool? LastTestOk { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
