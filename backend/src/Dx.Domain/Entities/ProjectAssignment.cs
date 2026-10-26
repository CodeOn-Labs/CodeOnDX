using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectAssignment
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid? ContractStaffId { get; set; }

    public int SlotIndex { get; set; }

    public Guid PositionId { get; set; }

    public Guid? LevelId { get; set; }

    public decimal FteAllocated { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }

    public virtual ProjectContractStaff? ContractStaff { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual MasterDatum? Level { get; set; }

    public virtual MasterDatum Position { get; set; } = null!;

    public virtual Project Project { get; set; } = null!;

    public virtual ICollection<ProjectAssignmentPeriod> ProjectAssignmentPeriods { get; set; } = new List<ProjectAssignmentPeriod>();

    public virtual Tenant Tenant { get; set; } = null!;
}
