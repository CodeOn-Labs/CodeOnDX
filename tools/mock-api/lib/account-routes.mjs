// Endpoint tài khoản (D2, D4): quên / đặt lại mật khẩu, nhận lời mời, hồ sơ cá nhân, đổi mật khẩu, ngôn ngữ;
// và trang khảo sát CSS công khai (khách hàng mở link, không cần đăng nhập).
import { randomUUID } from 'node:crypto';
import { ApiError, badRequest, notFound, readJson } from './http.mjs';
import { hashIdentityV3, verifyIdentityV3 } from './auth.mjs';
import { addError, maxLength, nowIso, str, throwIfErrors, EMAIL_PATTERN } from './validate.mjs';

const RESET_TTL_MINUTES = 30;

/**
 * @param {{ store: ReturnType<import('./store.mjs').createStore>, currentUser: Function }} deps
 */
export function accountRoutes({ store, currentUser }) {
  const { db } = store;
  db.invitations ??= [];
  db.password_resets ??= [];
  const setting = (key, fallback) => db.system_settings.find((s) => s.key === key)?.value ?? fallback;

  /** Chính sách mật khẩu: tối thiểu password_min_length ký tự (Cài đặt chung), có chữ hoa, chữ thường, số. */
  function validatePassword(errors, password, confirm) {
    const min = Number(setting('password_min_length', 8));
    if (!password) addError(errors, 'password', 'validation.required');
    else {
      if (password.length < min) addError(errors, 'password', { key: 'account_password.err_too_short', args: { min } });
      if (!/[A-Z]/.test(password) || !/[a-z]/.test(password) || !/\d/.test(password)) addError(errors, 'password', 'account_password.err_weak');
    }
    if (confirm !== undefined && confirm !== password) addError(errors, 'confirmPassword', 'account_password.err_mismatch');
  }

  /** Token đặt mật khẩu: lời mời (tạo tài khoản mới) hoặc yêu cầu quên mật khẩu. */
  function findToken(token) {
    const now = Date.now();
    const invite = db.invitations.find((i) => i.token === token);
    if (invite) {
      if (invite.accepted_at || Date.parse(invite.expires_at) < now) throw new ApiError(410, 'account_token.err_expired');
      return { kind: 'invite', row: invite, email: invite.email };
    }
    const reset = db.password_resets.find((r) => r.token === token);
    if (reset) {
      if (reset.used_at || Date.parse(reset.expires_at) < now) throw new ApiError(410, 'account_token.err_expired');
      return { kind: 'reset', row: reset, email: db.users.find((u) => u.id === reset.user_id)?.email ?? '' };
    }
    throw new ApiError(404, 'account_token.err_invalid');
  }

  function profileDto(user, culture) {
    const employee = user.employee_id ? db.employees.find((e) => e.id === user.employee_id) : null;
    const base = store.userDto(user, culture);
    const department = employee ? db.departments.find((d) => d.id === employee.department_id) : null;
    const team = employee ? db.teams.find((t) => t.id === employee.team_id) : null;
    const position = employee ? db.master_data.find((m) => m.type === 'job_position' && m.code === employee.position_code) : null;
    const now = new Date().toISOString().slice(0, 10);
    const projects = employee
      ? [...new Map(db.assignments.filter((a) => a.employee_id === employee.id && !a.is_deleted && a.start_date <= now && a.end_date >= now).map((a) => [a.project_id, a])).values()]
          .map((a) => db.projects.find((p) => p.id === a.project_id && !p.is_deleted))
          .filter(Boolean)
          .map((p) => ({ id: p.id, code: p.code, name: p.name }))
      : [];
    return {
      ...base,
      phone: employee?.phone ?? null,
      employee: employee
        ? {
            id: employee.id, code: employee.code, position: position ? store.masterRef(position.id, culture) : null, level: store.masterRef(employee.level_id, culture),
            department: department ? { id: department.id, name: department.name } : null, team: team ? { id: team.id, name: team.name } : null,
            joinedDate: employee.joined_date, leftDate: employee.left_date ?? null, isActive: employee.is_active, skills: employee.skills ?? [],
          }
        : null,
      projects,
      lastLoginAt: user.last_login_at ?? null,
    };
  }

  // ─── Khảo sát CSS công khai ───────────────────────────────────────────────

  function findSurvey(url) {
    const token = decodeURIComponent(url.pathname.split('/').at(-1) || '');
    const survey = db.project_css_surveys.find((s) => s.token === token && !s.is_revoked);
    if (!survey) throw new ApiError(404, 'css_survey.err_invalid_token');
    return survey;
  }
  // Form + câu hỏi riêng của dự án (chép từ biểu mẫu mặc định khi dự án chưa có)
  const cssForm = (survey) => store.projectCssForm(survey.project_id);
  const cssQuestions = (survey) => store.projectCssQuestions(survey.project_id);

  return [
    ['POST', /^\/api\/v1\/auth\/forgot-password$/, async ({ req }) => {
      const body = await readJson(req);
      const email = str(body.email).toLowerCase();
      if (!email) throw badRequest({ email: ['validation.required'] });
      if (!EMAIL_PATTERN.test(email)) throw badRequest({ email: ['validation.invalid_email'] });
      // Luôn trả cùng một thông báo dù email có tồn tại hay không — tránh dò tài khoản
      const user = db.users.find((u) => u.normalized_email === email.toUpperCase() && u.is_active);
      if (user) {
        const token = randomUUID().replace(/-/g, '');
        db.password_resets.push({ id: randomUUID(), user_id: user.id, token, expires_at: new Date(Date.now() + RESET_TTL_MINUTES * 60_000).toISOString(), used_at: null });
        store.sendMail({ to: user.email, subject: 'password_reset', link: `/auth/reset-password/${token}` });
      }
      return { key: 'auth_forgot_password.msg_success', args: { minutes: RESET_TTL_MINUTES } };
    }],
    ['GET', /^\/api\/v1\/auth\/reset-password\/[^/]+$/, async ({ url }) => {
      const found = findToken(decodeURIComponent(url.pathname.split('/').at(-1) || ''));
      return { data: { kind: found.kind, email: found.email, expiresAt: found.row.expires_at, passwordMinLength: Number(setting('password_min_length', 8)) } };
    }],
    ['POST', /^\/api\/v1\/auth\/reset-password$/, async ({ req }) => {
      const body = await readJson(req);
      const found = findToken(str(body.token));
      const errors = {};
      validatePassword(errors, typeof body.password === 'string' ? body.password : '', typeof body.confirmPassword === 'string' ? body.confirmPassword : '');
      throwIfErrors(errors);
      const hash = hashIdentityV3(body.password);
      if (found.kind === 'reset') {
        const user = db.users.find((u) => u.id === found.row.user_id);
        Object.assign(user, { password_hash: hash, must_change_password: false });
        found.row.used_at = nowIso();
      } else {
        // Nhận lời mời: tạo tài khoản gắn với hồ sơ nhân sự, vai trò mặc định "member"
        const employee = db.employees.find((e) => e.id === found.row.employee_id);
        const user = {
          id: randomUUID(), tenant_id: db.tenants[0].id, email: employee.email, normalized_email: employee.email.toUpperCase(), full_name: employee.full_name,
          employee_id: employee.id, preferred_locale: setting('default_locale', 'vi'), avatar_url: null, password_hash: hash, email_confirmed: true,
          must_change_password: false, access_failed_count: 0, lockout_end: null, last_login_at: null, is_active: true, created_at: nowIso(),
        };
        db.users.push(user);
        employee.user_id = user.id;
        const member = db.roles.find((r) => r.code === 'member');
        if (member) db.user_roles.push({ user_id: user.id, role_id: member.id });
        found.row.accepted_at = nowIso();
      }
      return { key: found.kind === 'reset' ? 'auth_reset_password.msg_success' : 'auth_accept_invite.msg_success' };
    }],

    // Hồ sơ cá nhân
    ['GET', /^\/api\/v1\/profile$/, async ({ req, culture }) => {
      const ctx = currentUser(req);
      return { data: profileDto(ctx.user, culture) };
    }],
    ['PUT', /^\/api\/v1\/profile$/, async ({ req, culture }) => {
      const ctx = currentUser(req);
      const body = await readJson(req);
      const errors = {};
      const fullName = body.fullName === undefined ? ctx.user.full_name : str(body.fullName);
      if (!fullName) addError(errors, 'fullName', 'validation.required');
      maxLength(errors, 'fullName', fullName, 150);
      const phone = body.phone === undefined ? undefined : str(body.phone);
      if (phone && !/^[0-9+\-\s]{6,20}$/.test(phone)) addError(errors, 'phone', 'validation.invalid_value');
      const locale = body.preferredLocale === undefined ? undefined : str(body.preferredLocale);
      if (locale !== undefined && !['vi', 'en'].includes(locale)) addError(errors, 'preferredLocale', 'validation.invalid_value');
      const avatar = body.avatarUrl === undefined ? undefined : body.avatarUrl;
      if (avatar && (typeof avatar !== 'string' || !avatar.startsWith('data:image/') || avatar.length > 400_000)) addError(errors, 'avatarUrl', 'profile_update.err_avatar_invalid');
      const skills = Array.isArray(body.skills) ? body.skills.map(str).filter(Boolean).slice(0, 30) : undefined;
      throwIfErrors(errors);
      ctx.user.full_name = fullName;
      if (locale) ctx.user.preferred_locale = locale;
      if (avatar !== undefined) ctx.user.avatar_url = avatar || null;
      const employee = ctx.user.employee_id ? db.employees.find((e) => e.id === ctx.user.employee_id) : null;
      if (employee) {
        employee.full_name = fullName;
        if (phone !== undefined) employee.phone = phone || null;
        if (skills) employee.skills = skills;
        if (avatar !== undefined) employee.avatar_url = avatar || null;
      }
      return { key: 'profile_update.msg_success', data: profileDto(ctx.user, culture) };
    }],
    ['POST', /^\/api\/v1\/profile\/change-password$/, async ({ req }) => {
      const ctx = currentUser(req);
      const body = await readJson(req);
      const errors = {};
      const current = typeof body.currentPassword === 'string' ? body.currentPassword : '';
      if (!current) addError(errors, 'currentPassword', 'validation.required');
      else if (!verifyIdentityV3(current, ctx.user.password_hash)) addError(errors, 'currentPassword', 'account_password.err_current_wrong');
      validatePassword(errors, typeof body.password === 'string' ? body.password : '', typeof body.confirmPassword === 'string' ? body.confirmPassword : '');
      if (current && body.password === current) addError(errors, 'password', 'account_password.err_same_as_current');
      throwIfErrors(errors);
      ctx.user.password_hash = hashIdentityV3(body.password);
      return { key: 'profile_change_password.msg_success' };
    }],

    // Khảo sát CSS công khai (không cần đăng nhập)
    ['GET', /^\/api\/v1\/public\/css-surveys\/[^/]+$/, async ({ url }) => {
      const survey = findSurvey(url);
      const project = db.projects.find((p) => p.id === survey.project_id);
      const form = cssForm(survey);
      return {
        data: {
          projectName: project?.name ?? null,
          periodMonth: survey.survey_month,
          submitted: survey.status === 'submitted',
          form: { title: form.title, intro: form.intro ?? '', scaleNote: form.scale_note ?? '', commentLabel: form.comment_label ?? '' },
          questions: cssQuestions(survey).map((q) => ({ id: q.id, label: q.content, required: q.is_required })),
        },
      };
    }],
    ['POST', /^\/api\/v1\/public\/css-surveys\/[^/]+$/, async ({ req, url }) => {
      const survey = findSurvey(url);
      if (survey.status === 'submitted') throw new ApiError(409, 'css_survey.err_already_submitted');
      const body = await readJson(req);
      const errors = {};
      const respondent = str(body.respondent);
      if (!respondent) addError(errors, 'respondent', 'validation.required');
      maxLength(errors, 'respondent', respondent, 150);
      maxLength(errors, 'comment', str(body.comment), 2000);
      const answers = Array.isArray(body.answers) ? body.answers : [];
      const questions = cssQuestions(survey);
      if (answers.some((a) => !questions.some((q) => q.id === a.questionId) || ![1, 2, 3, 4, 5].includes(a.score))) addError(errors, 'answers', 'css_survey.err_invalid_answers');
      if (questions.some((q) => q.is_required && !answers.some((a) => a.questionId === q.id))) addError(errors, 'answers', 'css_survey.err_missing_required');
      throwIfErrors(errors);
      Object.assign(survey, {
        status: 'submitted', submitted_at: nowIso(), respondent, comment: str(body.comment) || null,
        scores: questions.map((q) => ({ questionId: q.id, question: q.content, required: q.is_required, score: answers.find((a) => a.questionId === q.id)?.score ?? null })),
      });
      return { key: 'css_survey_submit.msg_success' };
    }],
  ];
}
