// Kho dữ liệu trong bộ nhớ, nạp từ mock-data/seed (bảng DB, cột snake_case) và ánh xạ sang DTO camelCase
// đúng như BE .NET sẽ trả. Logic tính toán (budget %, thống kê) port từ projectViewModel.ts của DX cũ.
import { readFileSync, existsSync, readdirSync } from 'node:fs';
import { join } from 'node:path';


/** Ngày hiện tại theo giờ Việt Nam (yyyy-MM-dd); MOCK_TODAY ghi đè để demo cố định. */
export function today() {
  if (process.env.MOCK_TODAY) return process.env.MOCK_TODAY;
  return new Date(Date.now() + 7 * 3600 * 1000).toISOString().slice(0, 10);
}

function daysBetween(from, to) {
  return Math.ceil((new Date(`${to}T00:00:00Z`) - new Date(`${from}T00:00:00Z`)) / 86400000);
}

export function createStore(root) {
  const readJson = (path) => JSON.parse(readFileSync(join(root, path), 'utf8'));
  // Nạp mọi bảng trong seed/ (tên file = tên bảng) — bảng mới chỉ cần sinh thêm file, không phải khai báo ở đây
  const tables = readdirSync(join(root, 'seed')).filter((f) => f.endsWith('.json')).map((f) => f.slice(0, -5));
  const db = Object.fromEntries(tables.map((t) => [t, readJson(`seed/${t}.json`)]));

  const byId = (table) => new Map(db[table].map((r) => [r.id, r]));
  const masterById = byId('master_data');
  const clientById = byId('clients');
  const roleById = byId('roles');
  const permissionById = byId('permissions');
  const employeeById = byId('employees');

  const localized = (row, culture) => (culture === 'en' && row.name_en ? row.name_en : row.name);

  function masterRef(id, culture) {
    const row = id ? masterById.get(id) : null;
    if (!row) return null;
    return { id: row.id, code: row.code, name: localized(row, culture), color: row.metadata?.color ?? null };
  }

  function permissionsOfUser(userId) {
    const roleIds = db.user_roles.filter((ur) => ur.user_id === userId).map((ur) => ur.role_id);
    const keys = new Set(
      db.role_permissions.filter((rp) => roleIds.includes(rp.role_id)).map((rp) => permissionById.get(rp.permission_id)?.key),
    );
    keys.delete(undefined);
    return [...keys].sort();
  }

  function userDto(user, culture) {
    const tenant = db.tenants.find((t) => t.id === user.tenant_id);
    const roles = db.user_roles.filter((ur) => ur.user_id === user.id).map((ur) => roleById.get(ur.role_id)).filter(Boolean);
    const employee = user.employee_id ? employeeById.get(user.employee_id) : null;
    return {
      id: user.id,
      email: user.email,
      fullName: user.full_name,
      avatarUrl: user.avatar_url ?? employee?.avatar_url ?? null,
      preferredLocale: user.preferred_locale ?? 'vi',
      employeeId: user.employee_id ?? null,
      tenant: tenant ? { id: tenant.id, code: tenant.code, name: tenant.name } : null,
      roles: roles.map((r) => ({ id: r.id, code: r.code, name: localized(r, culture) })),
    };
  }

  function masterList(types, culture) {
    return db.master_data
      .filter((m) => m.is_active && (types.length === 0 || types.includes(m.type)))
      .sort((a, b) => a.type.localeCompare(b.type) || a.sort_seq - b.sort_seq)
      .map((m) => ({ id: m.id, type: m.type, code: m.code, name: localized(m, culture), color: m.metadata?.color ?? null, sortSeq: m.sort_seq, metadata: m.metadata ?? {} }));
  }

  function ruleDto(r, culture) {
    return {
      key: r.key, module: r.module, valueType: r.value_type, value: r.value, defaultValue: r.default_value, min: r.min, max: r.max,
      status: r.status, name: culture === 'en' && r.name_en ? r.name_en : r.name,
      description: culture === 'en' && r.description_en ? r.description_en : r.description, updatedAt: r.updated_at ?? null,
    };
  }

  function rules(culture = 'vi') {
    return db.system_rules.map((r) => ruleDto(r, culture));
  }

  function ruleValue(key, fallback) {
    const rule = db.system_rules.find((r) => r.key === key);
    if (!rule || rule.status === 0) return fallback;
    return Number(rule.value ?? rule.default_value ?? fallback);
  }

  /** Port getProjectBudgetPercent (projectViewModel.ts). */
  function budgetPercent(project) {
    if (!project.budget) return 0;
    const staff = db.contract_staff.filter((cs) => cs.project_id === project.id && !cs.is_deleted);
    let weighted = 0;
    let qty = 0;
    for (const cs of staff) {
      weighted += (cs.price_per_month || 0) * (cs.quantity || 0);
      qty += cs.quantity || 0;
    }
    const avgPrice = qty > 0 ? weighted / qty : 0;
    const spent = Number(project.actual_cost_fte || 0) * avgPrice;
    const total = project.budget > 1000 ? project.budget : project.budget * avgPrice;
    return total === 0 ? 0 : Math.round(((spent / total) * 100) * 100) / 100;
  }

  function projectDto(p, culture) {
    const client = clientById.get(p.client_id);
    return {
      id: p.id,
      code: p.code,
      name: p.name,
      client: client ? { id: client.id, name: client.name } : null,
      grade: masterRef(p.grade_id, culture),
      status: masterRef(p.status_id, culture),
      riskLevel: masterRef(p.risk_level_id, culture),
      startDate: p.start_date,
      endDate: p.end_date,
      budget: p.budget,
      currencyCode: p.currency_code,
      progressPercent: p.progress_pct,
      contractStaffFte: p.contract_staff_fte,
      actualStaffFte: p.actual_staff_fte,
      budgetUsedPercent: budgetPercent(p),
      isDeleted: p.is_deleted,
      createdAt: p.created_at,
    };
  }

  /**
   * Lọc dự án như project.service.ts#search của DX cũ; người không có project.manage chỉ thấy dự án mình tham gia.
   * @param {{search?:string,statusIds:string[],gradeIds:string[],clientIds:string[],riskLevelIds:string[],startFrom?:string|null,endTo?:string|null,deleted:boolean}} f
   */
  function searchProjects(f, user, permissions) {
    const canManage = permissions.includes('project.manage');
    const myProjects = new Set(db.project_members.filter((m) => m.employee_id === user.employee_id).map((m) => m.project_id));
    const q = (f.search || '').trim().toLowerCase();
    return db.projects
      .filter((p) => (canManage || myProjects.has(p.id)))
      .filter((p) => p.is_deleted === f.deleted)
      .filter((p) => !q || p.name.toLowerCase().includes(q))
      .filter((p) => f.statusIds.length === 0 || f.statusIds.includes(p.status_id))
      .filter((p) => f.gradeIds.length === 0 || f.gradeIds.includes(p.grade_id))
      .filter((p) => f.clientIds.length === 0 || f.clientIds.includes(p.client_id))
      .filter((p) => f.riskLevelIds.length === 0 || f.riskLevelIds.includes(p.risk_level_id))
      .filter((p) => !f.startFrom || (p.start_date && p.start_date >= f.startFrom))
      .filter((p) => !f.endTo || (p.end_date && p.end_date <= f.endTo))
      .sort((a, b) => (b.created_at || '').localeCompare(a.created_at || '') || a.id.localeCompare(b.id));
  }

  /** Port getProjectListStats + isProjectEndingSoon (projectViewModel.ts), so khớp theo code thay vì tên. */
  function projectSummary(rows) {
    const endingSoonDays = ruleValue('PROJECT_ENDING_SOON_DAYS', 30);
    const now = today();
    let running = 0;
    let planning = 0;
    let endingSoon = 0;
    let risky = 0;
    for (const p of rows) {
      const status = masterById.get(p.status_id)?.code;
      const risk = masterById.get(p.risk_level_id)?.code;
      if (status === 'RUNNING') running += 1;
      if (status === 'PLANNING') planning += 1;
      if (risk === 'HIGH') risky += 1;
      if (p.end_date) {
        const left = daysBetween(now, p.end_date);
        if (left >= 0 && (status === 'ENDING' || left <= endingSoonDays)) endingSoon += 1;
      }
    }
    return { total: rows.length, running, endingSoon, planning, risky, endingSoonDays };
  }

  function aiFile(path) {
    const full = join(root, 'ai', path);
    return existsSync(full) ? JSON.parse(readFileSync(full, 'utf8')) : null;
  }

  /**
   * Insight của một màn theo ADR-0001: màn hiện tại trước, rồi các màn cha
   * (dừng khi màn khai báo inherit_insights = false) — cùng quy tắc với insightScreenChain() ở FE.
   */
  function insights(screenKey, culture, screens = {}) {
    const chain = [screenKey];
    let current = screens[screenKey];
    while (current?.parent && current.inherit_insights !== false) {
      chain.push(current.parent);
      current = screens[current.parent];
    }
    return chain.flatMap((key) => {
      const file = aiFile(`insights/${key}.json`);
      const items = file?.insights?.[culture] ?? file?.insights?.vi ?? [];
      return items.map((it, i) => ({ id: `${key}-${i}`, screenKey: key, variant: it.variant, title: it.title, content: it.content }));
    });
  }

  function chatReply(message, culture) {
    const file = aiFile('chat/scenarios.json');
    if (!file) return '';
    const text = String(message || '').toLowerCase();
    const hit = file.scenarios.find((s) => s.keywords.some((k) => text.includes(k)));
    const reply = hit ? hit.reply : file.default_reply;
    return reply[culture] ?? reply.vi;
  }

  function chatGreeting(culture) {
    const file = aiFile('chat/scenarios.json');
    return file?.greeting?.[culture] ?? file?.greeting?.vi ?? '';
  }

  /** Connector Email ở chế độ mock: thư chỉ lưu trong bộ nhớ (db.outbox) và in ra console. */
  function sendMail(mail) {
    db.outbox ??= [];
    db.outbox.push({ ...mail, sent_at: new Date().toISOString() });
    console.log(`[email:mock] -> ${mail.to}: ${mail.subject} ${mail.link ?? ''}`);
  }

  /**
   * Form CSS riêng của dự án: lần đầu đọc thì chép từ biểu mẫu mặc định (Quản trị → Biểu mẫu CSS);
   * sau đó sửa ở màn Chi tiết dự án → Metrics → CSS chỉ áp dụng cho dự án đó.
   */
  function projectCssForm(projectId) {
    db.project_css_forms ??= [];
    db.project_css_questions ??= [];
    let form = db.project_css_forms.find((f) => f.project_id === projectId);
    if (!form) {
      const tpl = db.css_template[0] ?? {};
      form = { project_id: projectId, title: tpl.title ?? '', intro: tpl.intro ?? '', scale_note: tpl.scale_note ?? '', comment_label: tpl.comment_label ?? '' };
      db.project_css_forms.push(form);
      for (const q of db.css_template_questions) {
        db.project_css_questions.push({ id: `${projectId.slice(0, 8)}-${q.id}`, project_id: projectId, content: q.content, is_required: q.is_required, sort_seq: q.sort_seq, is_deleted: false });
      }
    }
    return form;
  }
  function projectCssQuestions(projectId) {
    projectCssForm(projectId);
    return db.project_css_questions.filter((q) => q.project_id === projectId && !q.is_deleted).sort((a, b) => a.sort_seq - b.sort_seq);
  }

  return { db, root, readJson, sendMail, projectCssForm, projectCssQuestions, masterById, masterRef, masterList, permissionsOfUser, userDto, rules, ruleDto, ruleValue, projectDto, searchProjects, projectSummary, insights, chatReply, chatGreeting, localized };
}
