using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectContractStaff
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid PositionId { get; set; }

    public Guid? LevelId { get; set; }

    public short Quantity { get; set; }

    public decimal FtePerPerson { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal? MonthlyRate { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }

    public virtual MasterDatum? Level { get; set; }

    public virtual MasterDatum Position { get; set; } = null!;

    public virtual Project Project { get; set; } = null!;

    public virtual ICollection<ProjectAssignment> ProjectAssignments { get; set; } = new List<ProjectAssignment>();

    public virtual Tenant Tenant { get; set; } = null!;
}
