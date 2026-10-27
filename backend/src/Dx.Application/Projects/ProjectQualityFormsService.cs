using System.Security.Cryptography;
using Dx.Application.Common;
using Dx.Application.Settings;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Projects;

public sealed record PcvItemDto(Guid Id, string Category, string Label, bool IsDeleted, int SortSeq);

public sealed record PcvResultDto(Guid ItemId, bool Passed, string? Note);

public sealed record PcvAssessmentDto(DateTime AssessedAt, string? AssessorName, IReadOnlyList<PcvResultDto> Results);

public sealed record PcvSummaryDto(int Checked, int Passed, int Failed, decimal Score, int Total, string Tone);

public sealed record PcvHistoryDto(string Month, int Checked, int Passed, int Failed, decimal Score, string Tone, string? AssessorName, DateTime AssessedAt);

public sealed record ScoreThresholdsDto(decimal GreenMin, decimal OrangeMin);

public sealed record PcvDto(
    string Month, bool IsFuture, bool CanEdit, string? ProjectGrade, IReadOnlyList<PcvItemDto> Items, PcvAssessmentDto? Assessment,
    PcvSummaryDto Summary, IReadOnlyList<PcvHistoryDto> History, ScoreThresholdsDto Thresholds);

public sealed record PcvResultRequest(string? ItemId, bool? Passed, string? Note);

public sealed record SavePcvRequest(string? Month, IReadOnlyList<PcvResultRequest>? Results);

public sealed record SavePcvItemRequest(string? Category, string? Label);

public sealed record CssFormDto(string Title, string Intro, string ScaleNote, string CommentLabel);

public sealed record CssQuestionDto(Guid Id, string Label, bool Required, int SortSeq);

public sealed record CssAnswerDto(string Question, bool Required, int? Score);

public sealed record CssSurveyDto(
    string Status, string Token, DateTime? SentAt, string? SenderName, DateTime? SubmittedAt, string? Respondent, string? Comment,
    IReadOnlyList<CssAnswerDto> Answers);

public sealed record CssSummaryDto(int Answered, decimal Score, string Tone);

public sealed record CssHistoryDto(string Month, string Status, decimal? Score, int Answered, string Tone, DateTime? SubmittedAt, string? Respondent);

public sealed record ProjectCssDto(
    string Month, bool IsFuture, bool CanEdit, CssFormDto Form, IReadOnlyList<CssQuestionDto> Questions, CssSurveyDto? Survey,
    CssSummaryDto? Summary, IReadOnlyList<CssHistoryDto> History, ScoreThresholdsDto Thresholds);

public sealed record SaveCssFormRequest(string? Title, string? Intro, string? ScaleNote, string? CommentLabel);

public sealed record SaveCssQuestionRequest(string? Label, bool? Required);

public sealed record CssLinkRequest(string? Month);

public sealed record CssLinkDto(string Token);

public sealed record PublicCssQuestionDto(Guid Id, string Label, bool Required);

public sealed record PublicCssSurveyDto(string? ProjectName, string PeriodMonth, bool Submitted, CssFormDto Form, IReadOnlyList<PublicCssQuestionDto> Questions);

public sealed record CssAnswerRequest(string? QuestionId, int? Score);

public sealed record SubmitCssSurveyRequest(string? Respondent, string? Comment, IReadOnlyList<CssAnswerRequest>? Answers);

/// <summary>Finds the tenant of a live CSS survey link before any tenant is known (SECURITY DEFINER function, RLS).</summary>
public interface ICssSurveyTenantResolver
{
    Task<Guid?> FindTenantAsync(string token, CancellationToken ct);
}

/// <summary>
/// Metrics tab › PCV (process compliance, monthly checklist) and CSS (customer satisfaction
/// survey sent as a public link). A project gets its own copy of the tenant templates
/// (Admin › PCV criteria / CSS form) the first time it opens these tabs, so later template
/// edits never change projects already started. Colour thresholds come from Admin › Rules.
/// </summary>
public sealed class ProjectQualityFormsService(
    IAppDbContext db, ICurrentUser user, ITenantContext tenant, ProjectAccess access, RuleService rules, ITenantClock clock,
    ICssSurveyTenantResolver surveyTenants)
{
    // ─── PCV ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /projects/{id}/pcv?month=yyyy-MM. score = passed ÷ (passed + failed) × 100 (criteria left
    /// empty count in neither), e.g. 18 passed, 2 failed → 90 %. Tone: ≥ QUALITY_PCV_GREEN_MIN (90)
    /// green, ≥ QUALITY_PCV_ORANGE_MIN (70) orange, else red; nothing checked → none. A deleted
    /// criterion still shows (read-only) in a month that saved a result for it.
    /// </summary>
    public async Task<PcvDto> PcvAsync(Guid projectId, string? monthParam, CancellationToken ct)
    {
        var scope = await access.OpenAsync(projectId, ct);
        var p = scope.Project;
        var month = await MonthAsync(monthParam, ct);
        var thisMonth = await ThisMonthAsync(ct);
        await EnsurePcvItemsAsync(p.Id, ct);
        var (green, orange) = (await rules.GetNumberAsync("QUALITY_PCV_GREEN_MIN", 90, ct), await rules.GetNumberAsync("QUALITY_PCV_ORANGE_MIN", 70, ct));
        string Tone(decimal score, int checkedCount) => checkedCount == 0 ? "none" : score >= green ? "green" : score >= orange ? "orange" : "red";

        var items = await db.Set<ProjectPcvItem>().IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking()
            .Where(i => i.ProjectId == p.Id).OrderBy(i => i.SortOrder).ToListAsync(ct);
        var assessments = await db.Set<ProjectPcvAssessment>().AsNoTracking().Include(a => a.AssessedByNavigation)
            .Include(a => a.ProjectPcvResults).Where(a => a.ProjectId == p.Id).OrderByDescending(a => a.PeriodMonth).ToListAsync(ct);
        static List<PcvResultDto> ResultsOf(ProjectPcvAssessment a)
            => a.ProjectPcvResults.Where(r => r.Result != null).Select(r => new PcvResultDto(r.ItemId, r.Result == "pass", r.Note)).ToList();

        var current = assessments.FirstOrDefault(a => a.PeriodMonth == MonthStart(month));
        var currentResults = current is null ? [] : ResultsOf(current);
        var saved = currentResults.Select(r => r.ItemId).ToHashSet();
        var visible = items.Where(i => !i.IsDeleted || saved.Contains(i.Id)).ToList();
        var s = Summarize(currentResults);
        return new PcvDto(
            month, string.CompareOrdinal(month, thisMonth) > 0,
            user.Has(Permissions.PcvManage) && !p.IsDeleted && string.CompareOrdinal(month, thisMonth) <= 0,
            await db.Set<MasterDatum>().AsNoTracking().Where(m => m.Id == p.GradeId).Select(m => m.Name).FirstOrDefaultAsync(ct),
            visible.Select(i => new PcvItemDto(i.Id, i.Category, i.Content, i.IsDeleted, i.SortOrder)).ToList(),
            current is null ? null : new PcvAssessmentDto(current.AssessedAt, current.AssessedByNavigation?.FullName, currentResults),
            new PcvSummaryDto(s.Checked, s.Passed, s.Failed, s.Score, visible.Count, Tone(s.Score, s.Checked)),
            assessments.Select(a =>
            {
                var h = Summarize(ResultsOf(a));
                return new PcvHistoryDto(Key(a.PeriodMonth), h.Checked, h.Passed, h.Failed, h.Score, Tone(h.Score, h.Checked), a.AssessedByNavigation?.FullName, a.AssessedAt);
            }).ToList(),
            new ScoreThresholdsDto(green, orange));
    }

    /// <summary>PUT /projects/{id}/pcv — one assessment per month; saving again replaces it. A failed criterion needs a note.</summary>
    public async Task<(string Month, decimal Score)> SavePcvAsync(Guid projectId, SavePcvRequest r, CancellationToken ct)
    {
        var p = (await WriteAsync(projectId, Permissions.PcvManage, ct)).Project;
        var errors = new ValidationErrors();
        var month = Text.Clean(r.Month);
        if (!ProjectTimelineService.IsMonth(month)) errors.Add("month", "validation.invalid_value");
        else if (string.CompareOrdinal(month, await ThisMonthAsync(ct)) > 0) errors.Add("month", "project_pcv.err_future_month");
        var results = r.Results ?? [];
        if (results.Count == 0) errors.Add("results", "project_pcv.err_empty");
        var itemIds = (await db.Set<ProjectPcvItem>().IgnoreQueryFilters([QueryFilters.SoftDelete]).Where(i => i.ProjectId == p.Id).Select(i => i.Id).ToListAsync(ct)).ToHashSet();
        var parsed = new List<(Guid ItemId, bool Passed, string? Note)>();
        for (var i = 0; i < results.Count; i++)
        {
            var x = results[i];
            var note = Text.Clean(x.Note);
            if (!Guid.TryParse(x.ItemId, out var itemId) || !itemIds.Contains(itemId)) errors.Add($"results.{i}.itemId", "validation.invalid_value");
            if (x.Passed == false && note.Length == 0) errors.Add($"results.{i}.note", "project_pcv.err_note_required");
            errors.MaxLength($"results.{i}.note", note, 1000);
            parsed.Add((itemId, x.Passed == true, x.Passed == true ? null : note));
        }
        errors.ThrowIfAny();

        var monthStart = MonthStart(month);
        var a = await db.Set<ProjectPcvAssessment>().Include(x => x.ProjectPcvResults).FirstOrDefaultAsync(x => x.ProjectId == p.Id && x.PeriodMonth == monthStart, ct);
        if (a is null)
        {
            a = new ProjectPcvAssessment { Id = Guid.CreateVersion7(), ProjectId = p.Id, PeriodMonth = monthStart };
            db.Set<ProjectPcvAssessment>().Add(a);
        }
        a.AssessedBy = user.UserId;
        a.AssessedAt = DateTime.UtcNow;
        foreach (var old in a.ProjectPcvResults.ToList()) db.Set<ProjectPcvResult>().Remove(old);
        foreach (var x in parsed.DistinctBy(x => x.ItemId))
            db.Set<ProjectPcvResult>().Add(new ProjectPcvResult { AssessmentId = a.Id, ItemId = x.ItemId, Result = x.Passed ? "pass" : "fail", Note = x.Note });
        await db.SaveChangesAsync(ct);
        return (month, Summarize(parsed.Select(x => new PcvResultDto(x.ItemId, x.Passed, x.Note)).ToList()).Score);
    }

    public async Task CreatePcvItemAsync(Guid projectId, SavePcvItemRequest r, CancellationToken ct)
    {
        var p = (await WriteAsync(projectId, Permissions.PcvManage, ct)).Project;
        var (category, label) = ParsePcvItem(r);
        await EnsurePcvItemsAsync(p.Id, ct);
        var next = (await db.Set<ProjectPcvItem>().IgnoreQueryFilters([QueryFilters.SoftDelete]).Where(i => i.ProjectId == p.Id).MaxAsync(i => (int?)i.SortOrder, ct) ?? 0) + 1;
        db.Set<ProjectPcvItem>().Add(new ProjectPcvItem { ProjectId = p.Id, Category = category, Content = label, SortOrder = next });
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdatePcvItemAsync(Guid projectId, Guid itemId, SavePcvItemRequest r, CancellationToken ct)
    {
        var p = (await WriteAsync(projectId, Permissions.PcvManage, ct)).Project;
        var item = await db.Set<ProjectPcvItem>().FirstOrDefaultAsync(i => i.Id == itemId && i.ProjectId == p.Id, ct) ?? throw new NotFoundException();
        (item.Category, item.Content) = ParsePcvItem(r);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Soft delete: assessments already saved keep their results.</summary>
    public async Task DeletePcvItemAsync(Guid projectId, Guid itemId, CancellationToken ct)
    {
        var p = (await WriteAsync(projectId, Permissions.PcvManage, ct)).Project;
        var item = await db.Set<ProjectPcvItem>().FirstOrDefaultAsync(i => i.Id == itemId && i.ProjectId == p.Id, ct) ?? throw new NotFoundException();
        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static (string Category, string Label) ParsePcvItem(SavePcvItemRequest r)
    {
        var errors = new ValidationErrors();
        var category = Text.Clean(r.Category);
        var label = Text.Clean(r.Label);
        errors.Required("category", category);
        errors.MaxLength("category", category, 100);
        errors.Required("label", label);
        errors.MaxLength("label", label, 500);
        errors.ThrowIfAny();
        return (category, label);
    }

    /// <summary>A project without any criterion (not even deleted) copies the tenant's PCV template.</summary>
    private async Task EnsurePcvItemsAsync(Guid projectId, CancellationToken ct)
    {
        if (await db.Set<ProjectPcvItem>().IgnoreQueryFilters([QueryFilters.SoftDelete]).AnyAsync(i => i.ProjectId == projectId, ct)) return;
        var template = await db.Set<PcvTemplateItem>().AsNoTracking().OrderBy(t => t.SortOrder).ToListAsync(ct);
        if (template.Count == 0) return;
        foreach (var t in template)
            db.Set<ProjectPcvItem>().Add(new ProjectPcvItem { ProjectId = projectId, TemplateItemId = t.Id, Category = t.Category, Content = t.Content, SortOrder = t.SortOrder });
        await db.SaveChangesAsync(ct);
    }

    private static (int Checked, int Passed, int Failed, decimal Score) Summarize(IReadOnlyCollection<PcvResultDto> results)
    {
        var passed = results.Count(r => r.Passed);
        return (results.Count, passed, results.Count - passed, results.Count > 0 ? Math.Round((decimal)passed / results.Count * 100, 2, MidpointRounding.AwayFromZero) : 0);
    }

    // ─── CSS ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /projects/{id}/css?month=yyyy-MM. score = Σ scores of the answered questions ÷ number
    /// answered (scale 1–5; an optional question left empty is not counted as 0), e.g. 5, 4, 5, –, 5 →
    /// 19 ÷ 4 = 4.75. Tone: ≥ QUALITY_CSS_GREEN_MIN (4.5) green, ≥ QUALITY_CSS_ORANGE_MIN (3.5) orange, else red.
    /// </summary>
    public async Task<ProjectCssDto> CssAsync(Guid projectId, string? monthParam, CancellationToken ct)
    {
        var scope = await access.OpenAsync(projectId, ct);
        var p = scope.Project;
        var month = await MonthAsync(monthParam, ct);
        var form = await EnsureCssFormAsync(p.Id, ct);
        var questions = await QuestionsAsync(p.Id, ct);
        var (green, orange) = (await rules.GetNumberAsync("QUALITY_CSS_GREEN_MIN", 4.5m, ct), await rules.GetNumberAsync("QUALITY_CSS_ORANGE_MIN", 3.5m, ct));
        string Tone(decimal score) => score >= green ? "green" : score >= orange ? "orange" : "red";

        var surveys = await db.Set<ProjectCssSurvey>().AsNoTracking().Include(s => s.SentByNavigation).Include(s => s.ProjectCssAnswers).ThenInclude(a => a.Question)
            .Where(s => s.ProjectId == p.Id && !s.IsRevoked).OrderByDescending(s => s.PeriodMonth).ToListAsync(ct);
        var current = surveys.FirstOrDefault(s => s.PeriodMonth == MonthStart(month));
        var summary = current?.Status == "submitted" ? SummarizeCss(current) : ((int, decimal)?)null;
        return new ProjectCssDto(
            month, string.CompareOrdinal(month, await ThisMonthAsync(ct)) > 0, user.Has(Permissions.CssManage) && !p.IsDeleted,
            ToDto(form), questions.Select(q => new CssQuestionDto(q.Id, q.Content, q.IsRequired, q.SortOrder)).ToList(),
            current is null ? null : new CssSurveyDto(
                current.Status, current.Token, current.SentAt, current.SentByNavigation?.FullName, current.SubmittedAt, current.Respondent, current.Comment,
                current.ProjectCssAnswers.OrderBy(a => a.Question.SortOrder).Select(a => new CssAnswerDto(a.QuestionText, a.Question.IsRequired, a.Score)).ToList()),
            summary is { } su ? new CssSummaryDto(su.Item1, su.Item2, Tone(su.Item2)) : null,
            surveys.Select(s =>
            {
                var sum = s.Status == "submitted" ? SummarizeCss(s) : ((int, decimal)?)null;
                return new CssHistoryDto(Key(s.PeriodMonth), s.Status, sum?.Item2, sum?.Item1 ?? 0, sum is { } x ? Tone(x.Item2) : "none", s.SubmittedAt, s.Respondent);
            }).ToList(),
            new ScoreThresholdsDto(green, orange));
    }

    public async Task UpdateCssFormAsync(Guid projectId, SaveCssFormRequest r, CancellationToken ct)
    {
        var p = (await WriteAsync(projectId, Permissions.CssManage, ct)).Project;
        var errors = new ValidationErrors();
        var title = Text.Clean(r.Title);
        errors.Required("title", title);
        errors.MaxLength("title", title, 200);
        errors.MaxLength("intro", Text.Clean(r.Intro), 2000);
        errors.MaxLength("scaleNote", Text.Clean(r.ScaleNote), 2000);
        errors.MaxLength("commentLabel", Text.Clean(r.CommentLabel), 2000);
        errors.ThrowIfAny();
        await EnsureCssFormAsync(p.Id, ct);
        var form = await db.Set<ProjectCssForm>().FirstAsync(f => f.ProjectId == p.Id, ct);
        (form.Title, form.Intro, form.ScaleNote, form.CommentLabel) = (title, Text.Clean(r.Intro), Text.Clean(r.ScaleNote), Text.Clean(r.CommentLabel));
        await db.SaveChangesAsync(ct);
    }

    public async Task CreateCssQuestionAsync(Guid projectId, SaveCssQuestionRequest r, CancellationToken ct)
    {
        var p = (await WriteAsync(projectId, Permissions.CssManage, ct)).Project;
        var label = ParseQuestion(r);
        await EnsureCssFormAsync(p.Id, ct);
        var next = (await db.Set<ProjectCssQuestion>().Where(q => q.ProjectId == p.Id).MaxAsync(q => (int?)q.SortOrder, ct) ?? 0) + 1;
        db.Set<ProjectCssQuestion>().Add(new ProjectCssQuestion { ProjectId = p.Id, Content = label, IsRequired = r.Required != false, SortOrder = next });
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateCssQuestionAsync(Guid projectId, Guid questionId, SaveCssQuestionRequest r, CancellationToken ct)
    {
        var p = (await WriteAsync(projectId, Permissions.CssManage, ct)).Project;
        var q = await db.Set<ProjectCssQuestion>().FirstOrDefaultAsync(x => x.Id == questionId && x.ProjectId == p.Id, ct) ?? throw new NotFoundException();
        q.Content = ParseQuestion(r);
        q.IsRequired = r.Required != false;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteCssQuestionAsync(Guid projectId, Guid questionId, CancellationToken ct)
    {
        var p = (await WriteAsync(projectId, Permissions.CssManage, ct)).Project;
        var q = await db.Set<ProjectCssQuestion>().FirstOrDefaultAsync(x => x.Id == questionId && x.ProjectId == p.Id, ct) ?? throw new NotFoundException();
        q.IsDeleted = true;
        q.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// POST /projects/{id}/css/link — (re)creates the survey link of a month: the previous link of
    /// that month stops working and its answers are dropped from the screen (revoked).
    /// </summary>
    public async Task<(CssLinkDto Link, string Month)> CreateLinkAsync(Guid projectId, CssLinkRequest r, CancellationToken ct)
    {
        var p = (await WriteAsync(projectId, Permissions.CssManage, ct)).Project;
        var month = Text.Clean(r.Month);
        if (!ProjectTimelineService.IsMonth(month)) throw AppValidationException.Single("month", "validation.invalid_value");
        if (string.CompareOrdinal(month, await ThisMonthAsync(ct)) > 0) throw AppValidationException.Single("month", "project_css.err_future_month");
        await EnsureCssFormAsync(p.Id, ct);
        if ((await QuestionsAsync(p.Id, ct)).Count == 0) throw new ConflictException("project_css.err_no_questions");
        var monthStart = MonthStart(month);
        foreach (var old in await db.Set<ProjectCssSurvey>().Where(s => s.ProjectId == p.Id && s.PeriodMonth == monthStart && !s.IsRevoked).ToListAsync(ct))
            old.IsRevoked = true;
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        db.Set<ProjectCssSurvey>().Add(new ProjectCssSurvey
        {
            ProjectId = p.Id, PeriodMonth = monthStart, Token = token, Status = "sent", SentBy = user.UserId, SentAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return (new CssLinkDto(token), month);
    }

    // ─── public survey (no sign-in) ──────────────────────────────────────────

    /// <summary>GET /public/css-surveys/{token} — what the customer sees.</summary>
    public async Task<PublicCssSurveyDto> PublicSurveyAsync(string token, CancellationToken ct)
    {
        var survey = await SurveyByTokenAsync(token, ct);
        var form = await EnsureCssFormAsync(survey.ProjectId, ct);
        var questions = await QuestionsAsync(survey.ProjectId, ct);
        return new PublicCssSurveyDto(survey.Project.Name, Key(survey.PeriodMonth) + "-01", survey.Status == "submitted", ToDto(form),
            questions.Select(q => new PublicCssQuestionDto(q.Id, q.Content, q.IsRequired)).ToList());
    }

    /// <summary>POST /public/css-surveys/{token} — once only; every required question needs a score 1–5.</summary>
    public async Task SubmitSurveyAsync(string token, SubmitCssSurveyRequest r, CancellationToken ct)
    {
        var survey = await SurveyByTokenAsync(token, ct, tracking: true);
        if (survey.Status == "submitted") throw new ConflictException("css_survey.err_already_submitted");
        var errors = new ValidationErrors();
        var respondent = Text.Clean(r.Respondent);
        errors.Required("respondent", respondent);
        errors.MaxLength("respondent", respondent, 150);
        var comment = Text.Clean(r.Comment);
        errors.MaxLength("comment", comment, 2000);
        var questions = await QuestionsAsync(survey.ProjectId, ct);
        var answers = (r.Answers ?? []).Select(a => (Id: Guid.TryParse(a.QuestionId, out var g) ? g : Guid.Empty, a.Score)).ToList();
        if (answers.Any(a => questions.All(q => q.Id != a.Id) || a.Score is not (>= 1 and <= 5))) errors.Add("answers", "css_survey.err_invalid_answers");
        if (questions.Any(q => q.IsRequired && answers.All(a => a.Id != q.Id))) errors.Add("answers", "css_survey.err_missing_required");
        errors.ThrowIfAny();

        survey.Status = "submitted";
        survey.SubmittedAt = DateTime.UtcNow;
        survey.Respondent = respondent;
        survey.Comment = Text.OrNull(comment);
        foreach (var q in questions)
            db.Set<ProjectCssAnswer>().Add(new ProjectCssAnswer
            {
                SurveyId = survey.Id, QuestionId = q.Id, QuestionText = q.Content,
                Score = answers.FirstOrDefault(a => a.Id == q.Id).Score is { } s ? (short)s : null,
            });
        await db.SaveChangesAsync(ct);
    }

    private async Task<ProjectCssSurvey> SurveyByTokenAsync(string token, CancellationToken ct, bool tracking = false)
    {
        var tenantId = token.Length is > 0 and <= 200 ? await surveyTenants.FindTenantAsync(token, ct) : null;
        if (tenantId is null) throw new NotFoundException("css_survey.err_invalid_token");
        tenant.Use(tenantId.Value);
        var query = db.Set<ProjectCssSurvey>().Include(s => s.Project).Where(s => s.Token == token && !s.IsRevoked);
        if (!tracking) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(ct) ?? throw new NotFoundException("css_survey.err_invalid_token");
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private async Task<ProjectScope> WriteAsync(Guid projectId, string permission, CancellationToken ct)
    {
        var scope = await access.OpenAsync(projectId, ct);
        if (!user.Has(permission)) throw new ForbiddenException();
        if (scope.Project.IsDeleted) throw new ConflictException("project_detail.err_deleted");
        return scope;
    }

    /// <summary>The project's own CSS form, copied from Admin › CSS form (with its questions) on first use.</summary>
    private async Task<ProjectCssForm> EnsureCssFormAsync(Guid projectId, CancellationToken ct)
    {
        var form = await db.Set<ProjectCssForm>().AsNoTracking().FirstOrDefaultAsync(f => f.ProjectId == projectId, ct);
        if (form is not null) return form;
        var template = await db.Set<CssTemplate>().AsNoTracking().FirstOrDefaultAsync(ct);
        form = new ProjectCssForm
        {
            ProjectId = projectId, Title = template?.Title ?? string.Empty, Intro = template?.Intro ?? string.Empty,
            ScaleNote = template?.ScaleNote ?? string.Empty, CommentLabel = template?.CommentLabel ?? string.Empty,
        };
        db.Set<ProjectCssForm>().Add(form);
        if (!await db.Set<ProjectCssQuestion>().IgnoreQueryFilters([QueryFilters.SoftDelete]).AnyAsync(q => q.ProjectId == projectId, ct))
            foreach (var q in await db.Set<CssTemplateQuestion>().AsNoTracking().OrderBy(q => q.SortOrder).ToListAsync(ct))
                db.Set<ProjectCssQuestion>().Add(new ProjectCssQuestion { ProjectId = projectId, Content = q.Content, IsRequired = q.IsRequired, SortOrder = q.SortOrder });
        await db.SaveChangesAsync(ct);
        return form;
    }

    private Task<List<ProjectCssQuestion>> QuestionsAsync(Guid projectId, CancellationToken ct)
        => db.Set<ProjectCssQuestion>().AsNoTracking().Where(q => q.ProjectId == projectId).OrderBy(q => q.SortOrder).ToListAsync(ct);

    private static string ParseQuestion(SaveCssQuestionRequest r)
    {
        var label = Text.Clean(r.Label);
        var errors = new ValidationErrors();
        errors.Required("label", label);
        errors.MaxLength("label", label, 500);
        errors.ThrowIfAny();
        return label;
    }

    private static (int Answered, decimal Score) SummarizeCss(ProjectCssSurvey s)
    {
        var answered = s.ProjectCssAnswers.Where(a => a.Score is not null).ToList();
        return (answered.Count, answered.Count > 0 ? Math.Round((decimal)answered.Sum(a => a.Score!.Value) / answered.Count, 2, MidpointRounding.AwayFromZero) : 0);
    }

    private static CssFormDto ToDto(ProjectCssForm f) => new(f.Title, f.Intro, f.ScaleNote, f.CommentLabel);

    private async Task<string> MonthAsync(string? monthParam, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(monthParam)) return await ThisMonthAsync(ct);
        return ProjectTimelineService.IsMonth(monthParam) ? monthParam : throw AppValidationException.Single("month", "validation.invalid_value");
    }

    private async Task<string> ThisMonthAsync(CancellationToken ct) => Key(await clock.TodayAsync(ct));

    private static string Key(DateOnly d) => d.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

    private static DateOnly MonthStart(string month) => DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
