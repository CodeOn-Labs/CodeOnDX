using System;
using System.Collections.Generic;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Infrastructure.Persistence;

public partial class DxDbContext : DbContext
{
    public DxDbContext(DbContextOptions<DxDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AccountToken> AccountTokens { get; set; }

    public virtual DbSet<AiInsightConfig> AiInsightConfigs { get; set; }

    public virtual DbSet<AiInsightResult> AiInsightResults { get; set; }

    public virtual DbSet<AiPromptTemplate> AiPromptTemplates { get; set; }

    public virtual DbSet<AttendanceRecord> AttendanceRecords { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Client> Clients { get; set; }

    public virtual DbSet<CssTemplate> CssTemplates { get; set; }

    public virtual DbSet<CssTemplateQuestion> CssTemplateQuestions { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<EffortAllocation> EffortAllocations { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<Holiday> Holidays { get; set; }

    public virtual DbSet<ImportJob> ImportJobs { get; set; }

    public virtual DbSet<LeaveBalance> LeaveBalances { get; set; }

    public virtual DbSet<LeaveRequest> LeaveRequests { get; set; }

    public virtual DbSet<MasterDatum> MasterData { get; set; }

    public virtual DbSet<MonthlyReport> MonthlyReports { get; set; }

    public virtual DbSet<MonthlyReportColumn> MonthlyReportColumns { get; set; }

    public virtual DbSet<MonthlyReportEntry> MonthlyReportEntries { get; set; }

    public virtual DbSet<MonthlyReportMember> MonthlyReportMembers { get; set; }

    public virtual DbSet<OvertimeRequest> OvertimeRequests { get; set; }

    public virtual DbSet<PcvTemplateItem> PcvTemplateItems { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Project> Projects { get; set; }

    public virtual DbSet<ProjectAssignment> ProjectAssignments { get; set; }

    public virtual DbSet<ProjectAssignmentPeriod> ProjectAssignmentPeriods { get; set; }

    public virtual DbSet<ProjectChangeRequest> ProjectChangeRequests { get; set; }

    public virtual DbSet<ProjectContractStaff> ProjectContractStaffs { get; set; }

    public virtual DbSet<ProjectCostCenter> ProjectCostCenters { get; set; }

    public virtual DbSet<ProjectCssAnswer> ProjectCssAnswers { get; set; }

    public virtual DbSet<ProjectCssForm> ProjectCssForms { get; set; }

    public virtual DbSet<ProjectCssQuestion> ProjectCssQuestions { get; set; }

    public virtual DbSet<ProjectCssSurvey> ProjectCssSurveys { get; set; }

    public virtual DbSet<ProjectDocument> ProjectDocuments { get; set; }

    public virtual DbSet<ProjectDocumentGroup> ProjectDocumentGroups { get; set; }

    public virtual DbSet<ProjectIssueLink> ProjectIssueLinks { get; set; }

    public virtual DbSet<ProjectLink> ProjectLinks { get; set; }

    public virtual DbSet<ProjectMember> ProjectMembers { get; set; }

    public virtual DbSet<ProjectMilestone> ProjectMilestones { get; set; }

    public virtual DbSet<ProjectPcvAssessment> ProjectPcvAssessments { get; set; }

    public virtual DbSet<ProjectPcvItem> ProjectPcvItems { get; set; }

    public virtual DbSet<ProjectPcvResult> ProjectPcvResults { get; set; }

    public virtual DbSet<ProjectQualityMetric> ProjectQualityMetrics { get; set; }

    public virtual DbSet<ProjectRisk> ProjectRisks { get; set; }

    public virtual DbSet<ProjectStatusReport> ProjectStatusReports { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RolePermission> RolePermissions { get; set; }

    public virtual DbSet<SystemRule> SystemRules { get; set; }

    public virtual DbSet<Team> Teams { get; set; }

    public virtual DbSet<Tenant> Tenants { get; set; }

    public virtual DbSet<TenantIntegration> TenantIntegrations { get; set; }

    public virtual DbSet<TenantRule> TenantRules { get; set; }

    public virtual DbSet<TenantSetting> TenantSettings { get; set; }

    public virtual DbSet<TenantUser> TenantUsers { get; set; }

    public virtual DbSet<TimesheetEntry> TimesheetEntries { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserClaim> UserClaims { get; set; }

    public virtual DbSet<UserLogin> UserLogins { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<UserToken> UserTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_account_tokens");

            entity.ToTable("account_tokens", "dx");

            entity.HasIndex(e => new { e.TenantId, e.Email }, "ix_account_tokens_tenant_id_email");

            entity.HasIndex(e => e.TokenHash, "uq_account_tokens_token_hash").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.ConsumedAt).HasColumnName("consumed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Email)
                .HasMaxLength(256)
                .HasColumnName("email");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.Purpose)
                .HasMaxLength(20)
                .HasColumnName("purpose");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(128)
                .HasColumnName("token_hash");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Employee).WithMany(p => p.AccountTokens)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_account_tokens_employee_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.AccountTokens)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_account_tokens_tenant_id");

            entity.HasOne(d => d.User).WithMany(p => p.AccountTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_account_tokens_user_id");
        });

        modelBuilder.Entity<AiInsightConfig>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_ai_insight_configs");

            entity.ToTable("ai_insight_configs", "dx");

            entity.HasIndex(e => new { e.TenantId, e.ScreenKey }, "ix_ai_insight_configs_tenant_id_screen_key").HasFilter("is_active");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.AllowedRoleIds)
                .HasDefaultValueSql("'{}'::uuid[]")
                .HasComment("Empty array = visible to every role.")
                .HasColumnName("allowed_role_ids");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DataPrompt).HasColumnName("data_prompt");
            entity.Property(e => e.DataSources)
                .HasDefaultValueSql("'{}'::text[]")
                .HasColumnName("data_sources");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(300)
                .HasColumnName("name");
            entity.Property(e => e.ScreenKey)
                .HasMaxLength(100)
                .HasColumnName("screen_key");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Tenant).WithMany(p => p.AiInsightConfigs)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_ai_insight_configs_tenant_id");
        });

        modelBuilder.Entity<AiInsightResult>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_ai_insight_results");

            entity.ToTable("ai_insight_results", "dx");

            entity.HasIndex(e => e.ConfigId, "ix_ai_insight_results_config_id");

            entity.HasIndex(e => new { e.TenantId, e.ConfigId, e.ScopeKey, e.Locale }, "uq_ai_insight_results_cache_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.ConfigId).HasColumnName("config_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.GeneratedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("generated_at");
            entity.Property(e => e.InputHash)
                .HasMaxLength(128)
                .HasColumnName("input_hash");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Model)
                .HasMaxLength(100)
                .HasColumnName("model");
            entity.Property(e => e.ScopeKey)
                .HasMaxLength(200)
                .HasDefaultValueSql("''::character varying")
                .HasColumnName("scope_key");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");

            entity.HasOne(d => d.Config).WithMany(p => p.AiInsightResults)
                .HasForeignKey(d => d.ConfigId)
                .HasConstraintName("fk_ai_insight_results_config_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.AiInsightResults)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_ai_insight_results_tenant_id");
        });

        modelBuilder.Entity<AiPromptTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_ai_prompt_templates");

            entity.ToTable("ai_prompt_templates", "dx");

            entity.HasIndex(e => new { e.TenantId, e.Scope }, "uq_ai_prompt_templates_tenant_id_scope_active")
                .IsUnique()
                .HasFilter("is_active");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.Scope)
                .HasMaxLength(20)
                .HasColumnName("scope");
            entity.Property(e => e.Template).HasColumnName("template");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Tenant).WithMany(p => p.AiPromptTemplates)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_ai_prompt_templates_tenant_id");
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_attendance_records");

            entity.ToTable("attendance_records", "dx");

            entity.HasIndex(e => new { e.TenantId, e.WorkDate }, "ix_attendance_records_tenant_id_work_date");

            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.WorkDate }, "uq_attendance_records_tenant_id_employee_id_work_date").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.CheckIn).HasColumnName("check_in");
            entity.Property(e => e.CheckOut).HasColumnName("check_out");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.Note)
                .HasMaxLength(1000)
                .HasColumnName("note");
            entity.Property(e => e.Source)
                .HasMaxLength(10)
                .HasDefaultValueSql("'app'::character varying")
                .HasColumnName("source");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.WorkDate).HasColumnName("work_date");

            entity.HasOne(d => d.Employee).WithMany(p => p.AttendanceRecords)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_attendance_records_employee_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.AttendanceRecords)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_attendance_records_tenant_id");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_audit_logs");

            entity.ToTable("audit_logs", "dx");

            entity.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId }, "ix_audit_logs_tenant_id_entity");

            entity.HasIndex(e => new { e.TenantId, e.OccurredAt }, "ix_audit_logs_tenant_id_occurred_at").IsDescending(false, true);

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(10)
                .HasColumnName("action");
            entity.Property(e => e.Changes)
                .HasColumnType("jsonb")
                .HasColumnName("changes");
            entity.Property(e => e.EntityId)
                .HasMaxLength(100)
                .HasColumnName("entity_id");
            entity.Property(e => e.EntityType)
                .HasMaxLength(100)
                .HasColumnName("entity_type");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(64)
                .HasColumnName("ip_address");
            entity.Property(e => e.OccurredAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("occurred_at");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.TraceId)
                .HasMaxLength(64)
                .HasColumnName("trace_id");
            entity.Property(e => e.UserAgent)
                .HasMaxLength(500)
                .HasColumnName("user_agent");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_audit_logs_tenant_id");
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_clients");

            entity.ToTable("clients", "dx");

            entity.HasIndex(e => new { e.TenantId, e.Name }, "ix_clients_tenant_id_name");

            entity.HasIndex(e => new { e.TenantId, e.Code }, "uq_clients_tenant_id_code")
                .IsUnique()
                .HasFilter("(NOT is_deleted)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.ContactEmail)
                .HasMaxLength(256)
                .HasColumnName("contact_email");
            entity.Property(e => e.ContactName)
                .HasMaxLength(200)
                .HasColumnName("contact_name");
            entity.Property(e => e.CountryId).HasColumnName("country_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DefaultCurrencyCode)
                .HasMaxLength(3)
                .IsFixedLength()
                .HasColumnName("default_currency_code");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IndustryId).HasColumnName("industry_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(300)
                .HasColumnName("name");
            entity.Property(e => e.ShortName)
                .HasMaxLength(100)
                .HasColumnName("short_name");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Country).WithMany(p => p.ClientCountries)
                .HasForeignKey(d => d.CountryId)
                .HasConstraintName("fk_clients_country_id");

            entity.HasOne(d => d.Industry).WithMany(p => p.ClientIndustries)
                .HasForeignKey(d => d.IndustryId)
                .HasConstraintName("fk_clients_industry_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Clients)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_clients_tenant_id");
        });

        modelBuilder.Entity<CssTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_css_templates");

            entity.ToTable("css_templates", "dx");

            entity.HasIndex(e => e.TenantId, "uq_css_templates_tenant_id").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.CommentLabel)
                .HasMaxLength(500)
                .HasDefaultValueSql("''::character varying")
                .HasColumnName("comment_label");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Intro)
                .HasDefaultValueSql("''::text")
                .HasColumnName("intro");
            entity.Property(e => e.ScaleNote)
                .HasMaxLength(1000)
                .HasDefaultValueSql("''::character varying")
                .HasColumnName("scale_note");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Title)
                .HasMaxLength(300)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Tenant).WithOne(p => p.CssTemplate)
                .HasForeignKey<CssTemplate>(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_css_templates_tenant_id");
        });

        modelBuilder.Entity<CssTemplateQuestion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_css_template_questions");

            entity.ToTable("css_template_questions", "dx");

            entity.HasIndex(e => e.TemplateId, "ix_css_template_questions_template_id").HasFilter("(NOT is_deleted)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Content)
                .HasMaxLength(1000)
                .HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.IsRequired)
                .HasDefaultValue(true)
                .HasColumnName("is_required");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.TemplateId).HasColumnName("template_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Template).WithMany(p => p.CssTemplateQuestions)
                .HasForeignKey(d => d.TemplateId)
                .HasConstraintName("fk_css_template_questions_template_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.CssTemplateQuestions)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_css_template_questions_tenant_id");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_departments");

            entity.ToTable("departments", "dx");

            entity.HasIndex(e => new { e.TenantId, e.Code }, "uq_departments_tenant_id_code").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.ManagerEmployeeId).HasColumnName("manager_employee_id");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.ManagerEmployee).WithMany(p => p.Departments)
                .HasForeignKey(d => d.ManagerEmployeeId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_departments_manager_employee_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Departments)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_departments_tenant_id");
        });

        modelBuilder.Entity<EffortAllocation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_effort_allocations");

            entity.ToTable("effort_allocations", "dx");

            entity.HasIndex(e => new { e.TenantId, e.PeriodMonth }, "ix_effort_allocations_tenant_id_period_month");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId, e.PeriodMonth }, "ix_effort_allocations_tenant_id_project_id_period_month");

            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.ProjectId, e.PositionId, e.PeriodMonth }, "uq_effort_allocations_line").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.ActualEffort)
                .HasPrecision(4, 2)
                .HasDefaultValue(1m)
                .HasColumnName("actual_effort");
            entity.Property(e => e.AllocationRate)
                .HasDefaultValue((short)100)
                .HasColumnName("allocation_rate");
            entity.Property(e => e.ContractEffort)
                .HasPrecision(4, 2)
                .HasColumnName("contract_effort");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.Note)
                .HasMaxLength(100)
                .HasColumnName("note");
            entity.Property(e => e.OvertimeEffort)
                .HasPrecision(4, 2)
                .HasColumnName("overtime_effort");
            entity.Property(e => e.PerformanceCoefficient)
                .HasPrecision(4, 2)
                .HasDefaultValue(1m)
                .HasColumnName("performance_coefficient");
            entity.Property(e => e.PerformanceLevelId).HasColumnName("performance_level_id");
            entity.Property(e => e.PeriodMonth).HasColumnName("period_month");
            entity.Property(e => e.PositionId).HasColumnName("position_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.TeamId).HasColumnName("team_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Employee).WithMany(p => p.EffortAllocations)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_effort_allocations_employee_id");

            entity.HasOne(d => d.PerformanceLevel).WithMany(p => p.EffortAllocationPerformanceLevels)
                .HasForeignKey(d => d.PerformanceLevelId)
                .HasConstraintName("fk_effort_allocations_performance_level_id");

            entity.HasOne(d => d.Position).WithMany(p => p.EffortAllocationPositions)
                .HasForeignKey(d => d.PositionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_effort_allocations_position_id");

            entity.HasOne(d => d.Project).WithMany(p => p.EffortAllocations)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_effort_allocations_project_id");

            entity.HasOne(d => d.Team).WithMany(p => p.EffortAllocations)
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("fk_effort_allocations_team_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.EffortAllocations)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_effort_allocations_tenant_id");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_employees");

            entity.ToTable("employees", "dx");

            entity.HasIndex(e => e.StatusId, "ix_employees_status_id");

            entity.HasIndex(e => new { e.TenantId, e.DepartmentId }, "ix_employees_tenant_id_department_id");

            entity.HasIndex(e => new { e.TenantId, e.TeamId }, "ix_employees_tenant_id_team_id");

            entity.HasIndex(e => new { e.TenantId, e.Code }, "uq_employees_tenant_id_code")
                .IsUnique()
                .HasFilter("(NOT is_deleted)");

            entity.HasIndex(e => new { e.TenantId, e.UserId }, "uq_employees_tenant_id_user_id")
                .IsUnique()
                .HasFilter("(user_id IS NOT NULL)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(1000)
                .HasColumnName("avatar_url");
            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.Email)
                .HasMaxLength(256)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(200)
                .HasColumnName("full_name");
            entity.Property(e => e.HireDate).HasColumnName("hire_date");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.LevelId).HasColumnName("level_id");
            entity.Property(e => e.Phone)
                .HasMaxLength(32)
                .HasColumnName("phone");
            entity.Property(e => e.PositionId).HasColumnName("position_id");
            entity.Property(e => e.Skills)
                .HasDefaultValueSql("'{}'::text[]")
                .HasColumnName("skills");
            entity.Property(e => e.StatusId).HasColumnName("status_id");
            entity.Property(e => e.TeamId).HasColumnName("team_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.TerminationDate).HasColumnName("termination_date");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Department).WithMany(p => p.Employees)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("fk_employees_department_id");

            entity.HasOne(d => d.Level).WithMany(p => p.EmployeeLevels)
                .HasForeignKey(d => d.LevelId)
                .HasConstraintName("fk_employees_level_id");

            entity.HasOne(d => d.Position).WithMany(p => p.EmployeePositions)
                .HasForeignKey(d => d.PositionId)
                .HasConstraintName("fk_employees_position_id");

            entity.HasOne(d => d.Status).WithMany(p => p.EmployeeStatuses)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_employees_status_id");

            entity.HasOne(d => d.Team).WithMany(p => p.Employees)
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("fk_employees_team_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Employees)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_employees_tenant_id");

            entity.HasOne(d => d.User).WithMany(p => p.Employees)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_employees_user_id");
        });

        modelBuilder.Entity<Holiday>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_holidays");

            entity.ToTable("holidays", "dx");

            entity.HasIndex(e => new { e.TenantId, e.HolidayDate }, "uq_holidays_tenant_id_holiday_date").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.HolidayDate).HasColumnName("holiday_date");
            entity.Property(e => e.IsPaid)
                .HasDefaultValue(true)
                .HasColumnName("is_paid");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Holidays)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_holidays_tenant_id");
        });

        modelBuilder.Entity<ImportJob>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_import_jobs");

            entity.ToTable("import_jobs", "dx");

            entity.HasIndex(e => new { e.TenantId, e.CreatedAt }, "ix_import_jobs_tenant_id_created_at").IsDescending(false, true);

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Errors)
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("errors");
            entity.Property(e => e.FailedRows).HasColumnName("failed_rows");
            entity.Property(e => e.FileName)
                .HasMaxLength(300)
                .HasColumnName("file_name");
            entity.Property(e => e.FilePath)
                .HasMaxLength(1000)
                .HasColumnName("file_path");
            entity.Property(e => e.FinishedAt).HasColumnName("finished_at");
            entity.Property(e => e.Kind)
                .HasMaxLength(30)
                .HasColumnName("kind");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.SucceededRows).HasColumnName("succeeded_rows");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.TotalRows).HasColumnName("total_rows");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ImportJobs)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_import_jobs_tenant_id");
        });

        modelBuilder.Entity<LeaveBalance>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_leave_balances");

            entity.ToTable("leave_balances", "dx");

            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Year }, "uq_leave_balances_tenant_id_employee_id_year").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.AdjustedDays)
                .HasPrecision(5, 2)
                .HasColumnName("adjusted_days");
            entity.Property(e => e.CarriedOverDays)
                .HasPrecision(5, 2)
                .HasColumnName("carried_over_days");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.EntitledDays)
                .HasPrecision(5, 2)
                .HasColumnName("entitled_days");
            entity.Property(e => e.Note)
                .HasMaxLength(1000)
                .HasColumnName("note");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Year).HasColumnName("year");

            entity.HasOne(d => d.Employee).WithMany(p => p.LeaveBalances)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_leave_balances_employee_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.LeaveBalances)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_leave_balances_tenant_id");
        });

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_leave_requests");

            entity.ToTable("leave_requests", "dx");

            entity.HasIndex(e => e.LeaveTypeId, "ix_leave_requests_leave_type_id");

            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.FromDate }, "ix_leave_requests_tenant_id_employee_id_from_date");

            entity.HasIndex(e => new { e.TenantId, e.Status }, "ix_leave_requests_tenant_id_status").HasFilter("((status)::text = 'pending'::text)");

            entity.HasIndex(e => new { e.TenantId, e.Code }, "uq_leave_requests_tenant_id_code").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.AttachmentUrl)
                .HasMaxLength(1000)
                .HasColumnName("attachment_url");
            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DecidedAt).HasColumnName("decided_at");
            entity.Property(e => e.DecidedBy).HasColumnName("decided_by");
            entity.Property(e => e.DecisionNote)
                .HasMaxLength(1000)
                .HasColumnName("decision_note");
            entity.Property(e => e.DurationType)
                .HasMaxLength(20)
                .HasDefaultValueSql("'full_day'::character varying")
                .HasColumnName("duration_type");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.FromDate).HasColumnName("from_date");
            entity.Property(e => e.LeaveTypeId).HasColumnName("leave_type_id");
            entity.Property(e => e.Reason)
                .HasMaxLength(2000)
                .HasColumnName("reason");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.ToDate).HasColumnName("to_date");
            entity.Property(e => e.TotalHours)
                .HasPrecision(6, 2)
                .HasColumnName("total_hours");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.DecidedByNavigation).WithMany(p => p.LeaveRequests)
                .HasForeignKey(d => d.DecidedBy)
                .HasConstraintName("fk_leave_requests_decided_by");

            entity.HasOne(d => d.Employee).WithMany(p => p.LeaveRequests)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_leave_requests_employee_id");

            entity.HasOne(d => d.LeaveType).WithMany(p => p.LeaveRequests)
                .HasForeignKey(d => d.LeaveTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_leave_requests_leave_type_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.LeaveRequests)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_leave_requests_tenant_id");
        });

        modelBuilder.Entity<MasterDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_master_data");

            entity.ToTable("master_data", "dx");

            entity.HasIndex(e => new { e.TenantId, e.Type, e.Code }, "uq_master_data_tenant_id_type_code").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsSystem).HasColumnName("is_system");
            entity.Property(e => e.Metadata)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("metadata");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.NameEn)
                .HasMaxLength(200)
                .HasColumnName("name_en");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Tenant).WithMany(p => p.MasterData)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_master_data_tenant_id");
        });

        modelBuilder.Entity<MonthlyReport>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_monthly_reports");

            entity.ToTable("monthly_reports", "dx");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId, e.PeriodMonth }, "uq_monthly_reports_tenant_id_project_id_period_month").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.ClientName)
                .HasMaxLength(255)
                .HasColumnName("client_name");
            entity.Property(e => e.CompanyName)
                .HasMaxLength(255)
                .HasColumnName("company_name");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Language)
                .HasMaxLength(5)
                .HasDefaultValueSql("'vi'::character varying")
                .HasColumnName("language");
            entity.Property(e => e.OrderNumber)
                .HasMaxLength(100)
                .HasColumnName("order_number");
            entity.Property(e => e.PeriodMonth).HasColumnName("period_month");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ResponsiblePerson)
                .HasMaxLength(255)
                .HasColumnName("responsible_person");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.WorkName)
                .HasMaxLength(255)
                .HasColumnName("work_name");

            entity.HasOne(d => d.Project).WithMany(p => p.MonthlyReports)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_monthly_reports_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.MonthlyReports)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_monthly_reports_tenant_id");
        });

        modelBuilder.Entity<MonthlyReportColumn>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_monthly_report_columns");

            entity.ToTable("monthly_report_columns", "dx");

            entity.HasIndex(e => new { e.ReportId, e.ColumnKey }, "uq_monthly_report_columns_report_id_column_key").IsUnique();

            entity.HasIndex(e => new { e.ReportId, e.Source }, "uq_monthly_report_columns_report_id_source")
                .IsUnique()
                .HasFilter("(source IS NOT NULL)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.ColumnKey)
                .HasMaxLength(50)
                .HasColumnName("column_key");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DataType)
                .HasMaxLength(10)
                .HasDefaultValueSql("'text'::character varying")
                .HasColumnName("data_type");
            entity.Property(e => e.Label)
                .HasMaxLength(50)
                .HasColumnName("label");
            entity.Property(e => e.LabelJa)
                .HasMaxLength(50)
                .HasColumnName("label_ja");
            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.Source)
                .HasMaxLength(20)
                .HasColumnName("source");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Report).WithMany(p => p.MonthlyReportColumns)
                .HasForeignKey(d => d.ReportId)
                .HasConstraintName("fk_monthly_report_columns_report_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.MonthlyReportColumns)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_monthly_report_columns_tenant_id");
        });

        modelBuilder.Entity<MonthlyReportEntry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_monthly_report_entries");

            entity.ToTable("monthly_report_entries", "dx");

            entity.HasIndex(e => e.EmployeeId, "ix_monthly_report_entries_employee_id");

            entity.HasIndex(e => new { e.ReportId, e.EmployeeId, e.WorkDate }, "uq_monthly_report_entries_cell").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Content)
                .HasMaxLength(2000)
                .HasColumnName("content");
            entity.Property(e => e.ContentJa)
                .HasMaxLength(2000)
                .HasColumnName("content_ja");
            entity.Property(e => e.ContentJaSource)
                .HasMaxLength(10)
                .HasColumnName("content_ja_source");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CustomValues)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("custom_values");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.Hours)
                .HasPrecision(4, 2)
                .HasColumnName("hours");
            entity.Property(e => e.HoursSource)
                .HasMaxLength(10)
                .HasColumnName("hours_source");
            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.TimeSource)
                .HasMaxLength(10)
                .HasColumnName("time_source");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.WorkDate).HasColumnName("work_date");

            entity.HasOne(d => d.Employee).WithMany(p => p.MonthlyReportEntries)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_monthly_report_entries_employee_id");

            entity.HasOne(d => d.Report).WithMany(p => p.MonthlyReportEntries)
                .HasForeignKey(d => d.ReportId)
                .HasConstraintName("fk_monthly_report_entries_report_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.MonthlyReportEntries)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_monthly_report_entries_tenant_id");
        });

        modelBuilder.Entity<MonthlyReportMember>(entity =>
        {
            entity.HasKey(e => new { e.ReportId, e.EmployeeId }).HasName("pk_monthly_report_members");

            entity.ToTable("monthly_report_members", "dx");

            entity.HasIndex(e => e.EmployeeId, "ix_monthly_report_members_employee_id");

            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsRemoved).HasColumnName("is_removed");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");

            entity.HasOne(d => d.Employee).WithMany(p => p.MonthlyReportMembers)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_monthly_report_members_employee_id");

            entity.HasOne(d => d.Report).WithMany(p => p.MonthlyReportMembers)
                .HasForeignKey(d => d.ReportId)
                .HasConstraintName("fk_monthly_report_members_report_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.MonthlyReportMembers)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_monthly_report_members_tenant_id");
        });

        modelBuilder.Entity<OvertimeRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_overtime_requests");

            entity.ToTable("overtime_requests", "dx");

            entity.HasIndex(e => e.ProjectId, "ix_overtime_requests_project_id");

            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.WorkDate }, "ix_overtime_requests_tenant_id_employee_id_work_date");

            entity.HasIndex(e => new { e.TenantId, e.Status }, "ix_overtime_requests_tenant_id_status").HasFilter("((status)::text = 'pending'::text)");

            entity.HasIndex(e => new { e.TenantId, e.Code }, "uq_overtime_requests_tenant_id_code").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DayType)
                .HasMaxLength(10)
                .HasColumnName("day_type");
            entity.Property(e => e.DecidedAt).HasColumnName("decided_at");
            entity.Property(e => e.DecidedBy).HasColumnName("decided_by");
            entity.Property(e => e.DecisionNote)
                .HasMaxLength(1000)
                .HasColumnName("decision_note");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.Hours)
                .HasPrecision(5, 2)
                .HasColumnName("hours");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.RatePercent).HasColumnName("rate_percent");
            entity.Property(e => e.Reason)
                .HasMaxLength(2000)
                .HasColumnName("reason");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.WorkDate).HasColumnName("work_date");

            entity.HasOne(d => d.DecidedByNavigation).WithMany(p => p.OvertimeRequests)
                .HasForeignKey(d => d.DecidedBy)
                .HasConstraintName("fk_overtime_requests_decided_by");

            entity.HasOne(d => d.Employee).WithMany(p => p.OvertimeRequests)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_overtime_requests_employee_id");

            entity.HasOne(d => d.Project).WithMany(p => p.OvertimeRequests)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_overtime_requests_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.OvertimeRequests)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_overtime_requests_tenant_id");
        });

        modelBuilder.Entity<PcvTemplateItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_pcv_template_items");

            entity.ToTable("pcv_template_items", "dx");

            entity.HasIndex(e => new { e.TenantId, e.SortOrder }, "ix_pcv_template_items_tenant_id_sort_order").HasFilter("(NOT is_deleted)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Category)
                .HasMaxLength(200)
                .HasColumnName("category");
            entity.Property(e => e.Content)
                .HasMaxLength(1000)
                .HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Tenant).WithMany(p => p.PcvTemplateItems)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_pcv_template_items_tenant_id");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_permissions");

            entity.ToTable("permissions", "dx");

            entity.HasIndex(e => e.Key, "uq_permissions_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.GroupKey)
                .HasMaxLength(32)
                .HasColumnName("group_key");
            entity.Property(e => e.Key)
                .HasMaxLength(64)
                .HasColumnName("key");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.NameEn)
                .HasMaxLength(200)
                .HasColumnName("name_en");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_projects");

            entity.ToTable("projects", "dx");

            entity.HasIndex(e => new { e.TenantId, e.ClientId }, "ix_projects_tenant_id_client_id");

            entity.HasIndex(e => new { e.TenantId, e.EndDate }, "ix_projects_tenant_id_end_date").HasFilter("(NOT is_deleted)");

            entity.HasIndex(e => new { e.TenantId, e.StatusId }, "ix_projects_tenant_id_status_id").HasFilter("(NOT is_deleted)");

            entity.HasIndex(e => new { e.TenantId, e.Code }, "uq_projects_tenant_id_code").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Budget)
                .HasPrecision(18, 2)
                .HasColumnName("budget");
            entity.Property(e => e.ClientId).HasColumnName("client_id");
            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CurrencyCode)
                .HasMaxLength(3)
                .IsFixedLength()
                .HasColumnName("currency_code");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.GradeId).HasColumnName("grade_id");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(300)
                .HasColumnName("name");
            entity.Property(e => e.ProgressPercent).HasColumnName("progress_percent");
            entity.Property(e => e.RiskLevelId).HasColumnName("risk_level_id");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.StatusId).HasColumnName("status_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Client).WithMany(p => p.Projects)
                .HasForeignKey(d => d.ClientId)
                .HasConstraintName("fk_projects_client_id");

            entity.HasOne(d => d.Grade).WithMany(p => p.ProjectGrades)
                .HasForeignKey(d => d.GradeId)
                .HasConstraintName("fk_projects_grade_id");

            entity.HasOne(d => d.RiskLevel).WithMany(p => p.ProjectRiskLevels)
                .HasForeignKey(d => d.RiskLevelId)
                .HasConstraintName("fk_projects_risk_level_id");

            entity.HasOne(d => d.Status).WithMany(p => p.ProjectStatuses)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_projects_status_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Projects)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_projects_tenant_id");
        });

        modelBuilder.Entity<ProjectAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_assignments");

            entity.ToTable("project_assignments", "dx");

            entity.HasIndex(e => e.ContractStaffId, "ix_project_assignments_contract_staff_id");

            entity.HasIndex(e => new { e.TenantId, e.EmployeeId }, "ix_project_assignments_tenant_id_employee_id").HasFilter("(NOT is_deleted)");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId }, "ix_project_assignments_tenant_id_project_id").HasFilter("(NOT is_deleted)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.ContractStaffId).HasColumnName("contract_staff_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.FteAllocated)
                .HasPrecision(4, 2)
                .HasDefaultValue(1m)
                .HasColumnName("fte_allocated");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.LevelId).HasColumnName("level_id");
            entity.Property(e => e.PositionId).HasColumnName("position_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.SlotIndex).HasColumnName("slot_index");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.ContractStaff).WithMany(p => p.ProjectAssignments)
                .HasForeignKey(d => d.ContractStaffId)
                .HasConstraintName("fk_project_assignments_contract_staff_id");

            entity.HasOne(d => d.Employee).WithMany(p => p.ProjectAssignments)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_assignments_employee_id");

            entity.HasOne(d => d.Level).WithMany(p => p.ProjectAssignmentLevels)
                .HasForeignKey(d => d.LevelId)
                .HasConstraintName("fk_project_assignments_level_id");

            entity.HasOne(d => d.Position).WithMany(p => p.ProjectAssignmentPositions)
                .HasForeignKey(d => d.PositionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_assignments_position_id");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectAssignments)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_assignments_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectAssignments)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_assignments_tenant_id");
        });

        modelBuilder.Entity<ProjectAssignmentPeriod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_assignment_periods");

            entity.ToTable("project_assignment_periods", "dx");

            entity.HasIndex(e => e.AssignmentId, "ix_project_assignment_periods_assignment_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.FteAllocated)
                .HasPrecision(4, 2)
                .HasColumnName("fte_allocated");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Assignment).WithMany(p => p.ProjectAssignmentPeriods)
                .HasForeignKey(d => d.AssignmentId)
                .HasConstraintName("fk_project_assignment_periods_assignment_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectAssignmentPeriods)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_assignment_periods_tenant_id");
        });

        modelBuilder.Entity<ProjectChangeRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_change_requests");

            entity.ToTable("project_change_requests", "dx");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId, e.Code }, "uq_project_change_requests_tenant_id_project_id_code").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.ImpactAreas)
                .HasDefaultValueSql("'{}'::text[]")
                .HasColumnName("impact_areas");
            entity.Property(e => e.ImpactLevel)
                .HasMaxLength(20)
                .HasColumnName("impact_level");
            entity.Property(e => e.ImpactNote)
                .HasMaxLength(2000)
                .HasColumnName("impact_note");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.Source)
                .HasMaxLength(20)
                .HasColumnName("source");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Title)
                .HasMaxLength(300)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Url)
                .HasMaxLength(2000)
                .HasColumnName("url");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectChangeRequests)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_change_requests_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectChangeRequests)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_change_requests_tenant_id");
        });

        modelBuilder.Entity<ProjectContractStaff>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_contract_staff");

            entity.ToTable("project_contract_staff", "dx");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId }, "ix_project_contract_staff_tenant_id_project_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.FtePerPerson)
                .HasPrecision(4, 2)
                .HasDefaultValue(1m)
                .HasColumnName("fte_per_person");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.LevelId).HasColumnName("level_id");
            entity.Property(e => e.MonthlyRate)
                .HasPrecision(18, 2)
                .HasColumnName("monthly_rate");
            entity.Property(e => e.PositionId).HasColumnName("position_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.Quantity)
                .HasDefaultValue((short)1)
                .HasColumnName("quantity");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Level).WithMany(p => p.ProjectContractStaffLevels)
                .HasForeignKey(d => d.LevelId)
                .HasConstraintName("fk_project_contract_staff_level_id");

            entity.HasOne(d => d.Position).WithMany(p => p.ProjectContractStaffPositions)
                .HasForeignKey(d => d.PositionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_contract_staff_position_id");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectContractStaffs)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_contract_staff_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectContractStaffs)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_contract_staff_tenant_id");
        });

        modelBuilder.Entity<ProjectCostCenter>(entity =>
        {
            entity.HasKey(e => e.ProjectId).HasName("pk_project_cost_centers");

            entity.ToTable("project_cost_centers", "dx");

            entity.HasIndex(e => new { e.TenantId, e.DepartmentId }, "ix_project_cost_centers_tenant_id_department_id");

            entity.Property(e => e.ProjectId)
                .ValueGeneratedNever()
                .HasColumnName("project_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Department).WithMany(p => p.ProjectCostCenters)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_cost_centers_department_id");

            entity.HasOne(d => d.Project).WithOne(p => p.ProjectCostCenter)
                .HasForeignKey<ProjectCostCenter>(d => d.ProjectId)
                .HasConstraintName("fk_project_cost_centers_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectCostCenters)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_cost_centers_tenant_id");
        });

        modelBuilder.Entity<ProjectCssAnswer>(entity =>
        {
            entity.HasKey(e => new { e.SurveyId, e.QuestionId }).HasName("pk_project_css_answers");

            entity.ToTable("project_css_answers", "dx");

            entity.HasIndex(e => e.QuestionId, "ix_project_css_answers_question_id");

            entity.Property(e => e.SurveyId).HasColumnName("survey_id");
            entity.Property(e => e.QuestionId).HasColumnName("question_id");
            entity.Property(e => e.QuestionText)
                .HasMaxLength(1000)
                .HasColumnName("question_text");
            entity.Property(e => e.Score).HasColumnName("score");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");

            entity.HasOne(d => d.Question).WithMany(p => p.ProjectCssAnswers)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_css_answers_question_id");

            entity.HasOne(d => d.Survey).WithMany(p => p.ProjectCssAnswers)
                .HasForeignKey(d => d.SurveyId)
                .HasConstraintName("fk_project_css_answers_survey_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectCssAnswers)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_css_answers_tenant_id");
        });

        modelBuilder.Entity<ProjectCssForm>(entity =>
        {
            entity.HasKey(e => e.ProjectId).HasName("pk_project_css_forms");

            entity.ToTable("project_css_forms", "dx");

            entity.Property(e => e.ProjectId)
                .ValueGeneratedNever()
                .HasColumnName("project_id");
            entity.Property(e => e.CommentLabel)
                .HasMaxLength(500)
                .HasDefaultValueSql("''::character varying")
                .HasColumnName("comment_label");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Intro)
                .HasDefaultValueSql("''::text")
                .HasColumnName("intro");
            entity.Property(e => e.ScaleNote)
                .HasMaxLength(1000)
                .HasDefaultValueSql("''::character varying")
                .HasColumnName("scale_note");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Title)
                .HasMaxLength(300)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Project).WithOne(p => p.ProjectCssForm)
                .HasForeignKey<ProjectCssForm>(d => d.ProjectId)
                .HasConstraintName("fk_project_css_forms_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectCssForms)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_css_forms_tenant_id");
        });

        modelBuilder.Entity<ProjectCssQuestion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_css_questions");

            entity.ToTable("project_css_questions", "dx");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId }, "ix_project_css_questions_tenant_id_project_id").HasFilter("(NOT is_deleted)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Content)
                .HasMaxLength(1000)
                .HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.IsRequired)
                .HasDefaultValue(true)
                .HasColumnName("is_required");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectCssQuestions)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_css_questions_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectCssQuestions)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_css_questions_tenant_id");
        });

        modelBuilder.Entity<ProjectCssSurvey>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_css_surveys");

            entity.ToTable("project_css_surveys", "dx");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId, e.PeriodMonth }, "uq_project_css_surveys_live_month")
                .IsUnique()
                .HasFilter("(NOT is_revoked)");

            entity.HasIndex(e => e.Token, "uq_project_css_surveys_token").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsRevoked).HasColumnName("is_revoked");
            entity.Property(e => e.PeriodMonth).HasColumnName("period_month");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.Respondent)
                .HasMaxLength(200)
                .HasColumnName("respondent");
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("sent_at");
            entity.Property(e => e.SentBy).HasColumnName("sent_by");
            entity.Property(e => e.SentTo)
                .HasMaxLength(256)
                .HasColumnName("sent_to");
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasDefaultValueSql("'sent'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Token)
                .HasMaxLength(128)
                .HasColumnName("token");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectCssSurveys)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_css_surveys_project_id");

            entity.HasOne(d => d.SentByNavigation).WithMany(p => p.ProjectCssSurveys)
                .HasForeignKey(d => d.SentBy)
                .HasConstraintName("fk_project_css_surveys_sent_by");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectCssSurveys)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_css_surveys_tenant_id");
        });

        modelBuilder.Entity<ProjectDocument>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_documents");

            entity.ToTable("project_documents", "dx");

            entity.HasIndex(e => e.GroupId, "ix_project_documents_group_id");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId }, "ix_project_documents_tenant_id_project_id").HasFilter("(NOT is_deleted)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(300)
                .HasColumnName("name");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Url)
                .HasMaxLength(2000)
                .HasColumnName("url");

            entity.HasOne(d => d.Group).WithMany(p => p.ProjectDocuments)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_documents_group_id");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectDocuments)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_documents_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectDocuments)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_documents_tenant_id");
        });

        modelBuilder.Entity<ProjectDocumentGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_document_groups");

            entity.ToTable("project_document_groups", "dx");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId }, "ix_project_document_groups_tenant_id_project_id").HasFilter("(NOT is_deleted)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectDocumentGroups)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_document_groups_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectDocumentGroups)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_document_groups_tenant_id");
        });

        modelBuilder.Entity<ProjectIssueLink>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_issue_links");

            entity.ToTable("project_issue_links", "dx");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId }, "ix_project_issue_links_tenant_id_project_id").HasFilter("(NOT is_deleted)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Category)
                .HasMaxLength(20)
                .HasColumnName("category");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.IssueType)
                .HasMaxLength(20)
                .HasColumnName("issue_type");
            entity.Property(e => e.Name)
                .HasMaxLength(300)
                .HasColumnName("name");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Url)
                .HasMaxLength(2000)
                .HasColumnName("url");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectIssueLinks)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_issue_links_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectIssueLinks)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_issue_links_tenant_id");
        });

        modelBuilder.Entity<ProjectLink>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_links");

            entity.ToTable("project_links", "dx");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId }, "ix_project_links_tenant_id_project_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.LinkTypeId).HasColumnName("link_type_id");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Url)
                .HasMaxLength(2000)
                .HasColumnName("url");

            entity.HasOne(d => d.LinkType).WithMany(p => p.ProjectLinks)
                .HasForeignKey(d => d.LinkTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_links_link_type_id");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectLinks)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_links_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectLinks)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_links_tenant_id");
        });

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.HasKey(e => new { e.ProjectId, e.EmployeeId }).HasName("pk_project_members");

            entity.ToTable("project_members", "dx");

            entity.HasIndex(e => new { e.TenantId, e.EmployeeId }, "ix_project_members_tenant_id_employee_id");

            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsManager).HasColumnName("is_manager");
            entity.Property(e => e.PositionId).HasColumnName("position_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Employee).WithMany(p => p.ProjectMembers)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_members_employee_id");

            entity.HasOne(d => d.Position).WithMany(p => p.ProjectMembers)
                .HasForeignKey(d => d.PositionId)
                .HasConstraintName("fk_project_members_position_id");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectMembers)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_members_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectMembers)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_members_tenant_id");
        });

        modelBuilder.Entity<ProjectMilestone>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_milestones");

            entity.ToTable("project_milestones", "dx");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId, e.PlannedDate }, "ix_project_milestones_tenant_id_project_id").HasFilter("(NOT is_deleted)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.ActualDate).HasColumnName("actual_date");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Justification)
                .HasMaxLength(2000)
                .HasColumnName("justification");
            entity.Property(e => e.Name)
                .HasMaxLength(300)
                .HasColumnName("name");
            entity.Property(e => e.PlannedDate).HasColumnName("planned_date");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectMilestones)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_milestones_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectMilestones)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_milestones_tenant_id");
        });

        modelBuilder.Entity<ProjectPcvAssessment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_pcv_assessments");

            entity.ToTable("project_pcv_assessments", "dx");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId, e.PeriodMonth }, "uq_project_pcv_assessments_month").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.AssessedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("assessed_at");
            entity.Property(e => e.AssessedBy).HasColumnName("assessed_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.PeriodMonth).HasColumnName("period_month");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.AssessedByNavigation).WithMany(p => p.ProjectPcvAssessments)
                .HasForeignKey(d => d.AssessedBy)
                .HasConstraintName("fk_project_pcv_assessments_assessed_by");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectPcvAssessments)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_pcv_assessments_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectPcvAssessments)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_pcv_assessments_tenant_id");
        });

        modelBuilder.Entity<ProjectPcvItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_pcv_items");

            entity.ToTable("project_pcv_items", "dx");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId }, "ix_project_pcv_items_tenant_id_project_id").HasFilter("(NOT is_deleted)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Category)
                .HasMaxLength(200)
                .HasColumnName("category");
            entity.Property(e => e.Content)
                .HasMaxLength(1000)
                .HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.TemplateItemId).HasColumnName("template_item_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectPcvItems)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_pcv_items_project_id");

            entity.HasOne(d => d.TemplateItem).WithMany(p => p.ProjectPcvItems)
                .HasForeignKey(d => d.TemplateItemId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_project_pcv_items_template_item_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectPcvItems)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_pcv_items_tenant_id");
        });

        modelBuilder.Entity<ProjectPcvResult>(entity =>
        {
            entity.HasKey(e => new { e.AssessmentId, e.ItemId }).HasName("pk_project_pcv_results");

            entity.ToTable("project_pcv_results", "dx");

            entity.HasIndex(e => e.ItemId, "ix_project_pcv_results_item_id");

            entity.Property(e => e.AssessmentId).HasColumnName("assessment_id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.Note)
                .HasMaxLength(2000)
                .HasColumnName("note");
            entity.Property(e => e.Result)
                .HasMaxLength(10)
                .HasColumnName("result");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");

            entity.HasOne(d => d.Assessment).WithMany(p => p.ProjectPcvResults)
                .HasForeignKey(d => d.AssessmentId)
                .HasConstraintName("fk_project_pcv_results_assessment_id");

            entity.HasOne(d => d.Item).WithMany(p => p.ProjectPcvResults)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_pcv_results_item_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectPcvResults)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_pcv_results_tenant_id");
        });

        modelBuilder.Entity<ProjectQualityMetric>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_quality_metrics");

            entity.ToTable("project_quality_metrics", "dx");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId, e.Kind, e.PeriodMonth }, "uq_project_quality_metrics_month").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DefectCount).HasColumnName("defect_count");
            entity.Property(e => e.Kind)
                .HasMaxLength(10)
                .HasColumnName("kind");
            entity.Property(e => e.PeriodMonth).HasColumnName("period_month");
            entity.Property(e => e.ProductSize)
                .HasPrecision(12, 2)
                .HasColumnName("product_size");
            entity.Property(e => e.ProductSizeUnit)
                .HasMaxLength(20)
                .HasColumnName("product_size_unit");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectQualityMetrics)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_quality_metrics_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectQualityMetrics)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_quality_metrics_tenant_id");
        });

        modelBuilder.Entity<ProjectRisk>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_project_risks");

            entity.ToTable("project_risks", "dx");

            entity.HasIndex(e => e.OwnerEmployeeId, "ix_project_risks_owner_employee_id");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId, e.Code }, "uq_project_risks_tenant_id_project_id_code").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Category)
                .HasMaxLength(20)
                .HasColumnName("category");
            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DueDate).HasColumnName("due_date");
            entity.Property(e => e.IdentifiedDate).HasColumnName("identified_date");
            entity.Property(e => e.Impact).HasColumnName("impact");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.OwnerEmployeeId).HasColumnName("owner_employee_id");
            entity.Property(e => e.Probability).HasColumnName("probability");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ResponsePlan).HasColumnName("response_plan");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'open'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.Strategy)
                .HasMaxLength(20)
                .HasColumnName("strategy");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Title)
                .HasMaxLength(300)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.OwnerEmployee).WithMany(p => p.ProjectRisks)
                .HasForeignKey(d => d.OwnerEmployeeId)
                .HasConstraintName("fk_project_risks_owner_employee_id");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectRisks)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_project_risks_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectRisks)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_risks_tenant_id");
        });

        modelBuilder.Entity<ProjectStatusReport>(entity =>
        {
            entity.HasKey(e => e.ProjectId).HasName("pk_project_status_reports");

            entity.ToTable("project_status_reports", "dx");

            entity.Property(e => e.ProjectId)
                .ValueGeneratedNever()
                .HasColumnName("project_id");
            entity.Property(e => e.AiGeneratedAt).HasColumnName("ai_generated_at");
            entity.Property(e => e.AiSummary).HasColumnName("ai_summary");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Items)
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("items");
            entity.Property(e => e.OverallStatus)
                .HasMaxLength(10)
                .HasDefaultValueSql("'GOOD'::character varying")
                .HasColumnName("overall_status");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Project).WithOne(p => p.ProjectStatusReport)
                .HasForeignKey<ProjectStatusReport>(d => d.ProjectId)
                .HasConstraintName("fk_project_status_reports_project_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ProjectStatusReports)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project_status_reports_tenant_id");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_refresh_tokens");

            entity.ToTable("refresh_tokens", "dx");

            entity.HasIndex(e => e.ExpiresAt, "ix_refresh_tokens_expires_at");

            entity.HasIndex(e => e.FamilyId, "ix_refresh_tokens_family_id");

            entity.HasIndex(e => e.UserId, "ix_refresh_tokens_user_id");

            entity.HasIndex(e => e.TokenHash, "uq_refresh_tokens_token_hash").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedByIp)
                .HasMaxLength(64)
                .HasColumnName("created_by_ip");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.FamilyId).HasColumnName("family_id");
            entity.Property(e => e.ReplacedById).HasColumnName("replaced_by_id");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.RevokedReason)
                .HasMaxLength(50)
                .HasColumnName("revoked_reason");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(128)
                .HasColumnName("token_hash");
            entity.Property(e => e.UserAgent)
                .HasMaxLength(500)
                .HasColumnName("user_agent");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.ReplacedBy).WithMany(p => p.InverseReplacedBy)
                .HasForeignKey(d => d.ReplacedById)
                .HasConstraintName("fk_refresh_tokens_replaced_by_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_refresh_tokens_tenant_id");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_refresh_tokens_user_id");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_roles");

            entity.ToTable("roles", "dx");

            entity.HasIndex(e => new { e.TenantId, e.Code }, "uq_roles_tenant_id_code").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.IsSystem).HasColumnName("is_system");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.NameEn)
                .HasMaxLength(200)
                .HasColumnName("name_en");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Roles)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_roles_tenant_id");
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => new { e.RoleId, e.PermissionId }).HasName("pk_role_permissions");

            entity.ToTable("role_permissions", "dx");

            entity.HasIndex(e => e.PermissionId, "ix_role_permissions_permission_id");

            entity.HasIndex(e => e.TenantId, "ix_role_permissions_tenant_id");

            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.PermissionId).HasColumnName("permission_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");

            entity.HasOne(d => d.Permission).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.PermissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_role_permissions_permission_id");

            entity.HasOne(d => d.Role).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("fk_role_permissions_role_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_role_permissions_tenant_id");
        });

        modelBuilder.Entity<SystemRule>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("pk_system_rules");

            entity.ToTable("system_rules", "dx");

            entity.Property(e => e.Key)
                .HasMaxLength(64)
                .HasColumnName("key");
            entity.Property(e => e.DefaultValue)
                .HasColumnType("jsonb")
                .HasColumnName("default_value");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.DescriptionEn)
                .HasMaxLength(1000)
                .HasColumnName("description_en");
            entity.Property(e => e.MaxValue).HasColumnName("max_value");
            entity.Property(e => e.MinValue).HasColumnName("min_value");
            entity.Property(e => e.Module)
                .HasMaxLength(32)
                .HasColumnName("module");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.NameEn)
                .HasMaxLength(200)
                .HasColumnName("name_en");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.ValueType)
                .HasMaxLength(16)
                .HasColumnName("value_type");
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_teams");

            entity.ToTable("teams", "dx");

            entity.HasIndex(e => new { e.TenantId, e.DepartmentId }, "ix_teams_tenant_id_department_id");

            entity.HasIndex(e => new { e.TenantId, e.Code }, "uq_teams_tenant_id_code").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LeaderEmployeeId).HasColumnName("leader_employee_id");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Department).WithMany(p => p.Teams)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_teams_department_id");

            entity.HasOne(d => d.LeaderEmployee).WithMany(p => p.Teams)
                .HasForeignKey(d => d.LeaderEmployeeId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_teams_leader_employee_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Teams)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_teams_tenant_id");
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_tenants");

            entity.ToTable("tenants", "dx");

            entity.HasIndex(e => e.Code, "uq_tenants_code").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DefaultLocale)
                .HasMaxLength(10)
                .HasDefaultValueSql("'vi'::character varying")
                .HasColumnName("default_locale");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.TimeZone)
                .HasMaxLength(64)
                .HasDefaultValueSql("'Asia/Ho_Chi_Minh'::character varying")
                .HasColumnName("time_zone");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<TenantIntegration>(entity =>
        {
            entity.HasKey(e => new { e.TenantId, e.IntegrationKey }).HasName("pk_tenant_integrations");

            entity.ToTable("tenant_integrations", "dx");

            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.IntegrationKey)
                .HasMaxLength(32)
                .HasColumnName("integration_key");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled");
            entity.Property(e => e.LastSyncAt).HasColumnName("last_sync_at");
            entity.Property(e => e.SecretsEncrypted).HasColumnName("secrets_encrypted");
            entity.Property(e => e.Settings)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("settings");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UseMock)
                .HasDefaultValue(true)
                .HasColumnName("use_mock");

            entity.HasOne(d => d.Tenant).WithMany(p => p.TenantIntegrations)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_tenant_integrations_tenant_id");
        });

        modelBuilder.Entity<TenantRule>(entity =>
        {
            entity.HasKey(e => new { e.TenantId, e.RuleKey }).HasName("pk_tenant_rules");

            entity.ToTable("tenant_rules", "dx");

            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.RuleKey)
                .HasMaxLength(64)
                .HasColumnName("rule_key");
            entity.Property(e => e.IsEnabled)
                .HasDefaultValue(true)
                .HasColumnName("is_enabled");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Value)
                .HasColumnType("jsonb")
                .HasColumnName("value");

            entity.HasOne(d => d.RuleKeyNavigation).WithMany(p => p.TenantRules)
                .HasForeignKey(d => d.RuleKey)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_tenant_rules_rule_key");

            entity.HasOne(d => d.Tenant).WithMany(p => p.TenantRules)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_tenant_rules_tenant_id");
        });

        modelBuilder.Entity<TenantSetting>(entity =>
        {
            entity.HasKey(e => new { e.TenantId, e.Key }).HasName("pk_tenant_settings");

            entity.ToTable("tenant_settings", "dx");

            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Key)
                .HasMaxLength(64)
                .HasColumnName("key");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Value)
                .HasDefaultValueSql("''::text")
                .HasColumnName("value");

            entity.HasOne(d => d.Tenant).WithMany(p => p.TenantSettings)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_tenant_settings_tenant_id");
        });

        modelBuilder.Entity<TenantUser>(entity =>
        {
            entity.HasKey(e => new { e.TenantId, e.UserId }).HasName("pk_tenant_users");

            entity.ToTable("tenant_users", "dx");

            entity.HasIndex(e => e.UserId, "ix_tenant_users_user_id");

            entity.HasIndex(e => e.UserId, "uq_tenant_users_user_id_default")
                .IsUnique()
                .HasFilter("is_default");

            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsDefault)
                .HasDefaultValue(true)
                .HasColumnName("is_default");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Tenant).WithMany(p => p.TenantUsers)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_tenant_users_tenant_id");

            entity.HasOne(d => d.User).WithOne(p => p.TenantUser)
                .HasForeignKey<TenantUser>(d => d.UserId)
                .HasConstraintName("fk_tenant_users_user_id");
        });

        modelBuilder.Entity<TimesheetEntry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_timesheet_entries");

            entity.ToTable("timesheet_entries", "dx");

            entity.HasIndex(e => e.SourceRequestId, "ix_timesheet_entries_source_request_id").HasFilter("(source_request_id IS NOT NULL)");

            entity.HasIndex(e => e.TaskTypeId, "ix_timesheet_entries_task_type_id");

            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.WorkDate }, "ix_timesheet_entries_tenant_id_employee_id_work_date");

            entity.HasIndex(e => new { e.TenantId, e.ProjectId, e.WorkDate }, "ix_timesheet_entries_tenant_id_project_id_work_date").HasFilter("(project_id IS NOT NULL)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Description)
                .HasMaxLength(2000)
                .HasColumnName("description");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.Hours)
                .HasPrecision(5, 2)
                .HasColumnName("hours");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.RejectionReason)
                .HasMaxLength(1000)
                .HasColumnName("rejection_reason");
            entity.Property(e => e.Source)
                .HasMaxLength(20)
                .HasDefaultValueSql("'manual'::character varying")
                .HasColumnName("source");
            entity.Property(e => e.SourceRequestId).HasColumnName("source_request_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'draft'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TaskTypeId).HasColumnName("task_type_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.WorkDate).HasColumnName("work_date");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.TimesheetEntries)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("fk_timesheet_entries_approved_by");

            entity.HasOne(d => d.Employee).WithMany(p => p.TimesheetEntries)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_timesheet_entries_employee_id");

            entity.HasOne(d => d.Project).WithMany(p => p.TimesheetEntries)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_timesheet_entries_project_id");

            entity.HasOne(d => d.TaskType).WithMany(p => p.TimesheetEntries)
                .HasForeignKey(d => d.TaskTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_timesheet_entries_task_type_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.TimesheetEntries)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_timesheet_entries_tenant_id");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_users");

            entity.ToTable("users", "dx");

            entity.HasIndex(e => e.NormalizedEmail, "uq_users_normalized_email").IsUnique();

            entity.HasIndex(e => e.NormalizedUserName, "uq_users_normalized_user_name").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("id");
            entity.Property(e => e.AccessFailedCount).HasColumnName("access_failed_count");
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(1000)
                .HasColumnName("avatar_url");
            entity.Property(e => e.ConcurrencyStamp).HasColumnName("concurrency_stamp");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Email)
                .HasMaxLength(256)
                .HasColumnName("email");
            entity.Property(e => e.EmailConfirmed).HasColumnName("email_confirmed");
            entity.Property(e => e.FullName)
                .HasMaxLength(200)
                .HasColumnName("full_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.LockoutEnabled)
                .HasDefaultValue(true)
                .HasColumnName("lockout_enabled");
            entity.Property(e => e.LockoutEnd).HasColumnName("lockout_end");
            entity.Property(e => e.MustChangePassword).HasColumnName("must_change_password");
            entity.Property(e => e.NormalizedEmail)
                .HasMaxLength(256)
                .HasColumnName("normalized_email");
            entity.Property(e => e.NormalizedUserName)
                .HasMaxLength(256)
                .HasColumnName("normalized_user_name");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(32)
                .HasColumnName("phone_number");
            entity.Property(e => e.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
            entity.Property(e => e.PreferredLocale)
                .HasMaxLength(10)
                .HasColumnName("preferred_locale");
            entity.Property(e => e.SecurityStamp).HasColumnName("security_stamp");
            entity.Property(e => e.TwoFactorEnabled).HasColumnName("two_factor_enabled");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UserName)
                .HasMaxLength(256)
                .HasColumnName("user_name");
        });

        modelBuilder.Entity<UserClaim>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_user_claims");

            entity.ToTable("user_claims", "dx");

            entity.HasIndex(e => e.UserId, "ix_user_claims_user_id");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClaimType).HasColumnName("claim_type");
            entity.Property(e => e.ClaimValue).HasColumnName("claim_value");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.UserClaims)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_user_claims_user_id");
        });

        modelBuilder.Entity<UserLogin>(entity =>
        {
            entity.HasKey(e => new { e.LoginProvider, e.ProviderKey }).HasName("pk_user_logins");

            entity.ToTable("user_logins", "dx");

            entity.HasIndex(e => e.UserId, "ix_user_logins_user_id");

            entity.Property(e => e.LoginProvider)
                .HasMaxLength(128)
                .HasColumnName("login_provider");
            entity.Property(e => e.ProviderKey)
                .HasMaxLength(128)
                .HasColumnName("provider_key");
            entity.Property(e => e.ProviderDisplayName).HasColumnName("provider_display_name");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.UserLogins)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_user_logins_user_id");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId }).HasName("pk_user_roles");

            entity.ToTable("user_roles", "dx");

            entity.HasIndex(e => new { e.TenantId, e.RoleId }, "ix_user_roles_tenant_id_role_id");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("fk_user_roles_role_id");

            entity.HasOne(d => d.Tenant).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_user_roles_tenant_id");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_user_roles_user_id");
        });

        modelBuilder.Entity<UserToken>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.LoginProvider, e.Name }).HasName("pk_user_tokens");

            entity.ToTable("user_tokens", "dx");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.LoginProvider)
                .HasMaxLength(128)
                .HasColumnName("login_provider");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");
            entity.Property(e => e.Value).HasColumnName("value");

            entity.HasOne(d => d.User).WithMany(p => p.UserTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_user_tokens_user_id");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
