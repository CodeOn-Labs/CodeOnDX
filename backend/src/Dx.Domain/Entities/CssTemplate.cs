using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class CssTemplate
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Title { get; set; } = null!;

    public string Intro { get; set; } = null!;

    public string ScaleNote { get; set; } = null!;

    public string CommentLabel { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<CssTemplateQuestion> CssTemplateQuestions { get; set; } = new List<CssTemplateQuestion>();

    public virtual Tenant Tenant { get; set; } = null!;
}
