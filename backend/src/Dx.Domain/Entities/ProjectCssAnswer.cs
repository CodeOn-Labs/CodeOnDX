using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectCssAnswer
{
    public Guid TenantId { get; set; }

    public Guid SurveyId { get; set; }

    public Guid QuestionId { get; set; }

    public string QuestionText { get; set; } = null!;

    public short? Score { get; set; }

    public virtual ProjectCssQuestion Question { get; set; } = null!;

    public virtual ProjectCssSurvey Survey { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
