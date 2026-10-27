#!/usr/bin/env node
/**
 * Sinh toàn bộ thư mục mock-data/ (Q3 — claude/dx-conventions.md).
 *
 *   node tools/mock-data-gen/generate.mjs
 *   DX_DEMO_PASSWORD='MatKhauRieng!2026' node tools/mock-data-gen/generate.mjs
 *
 * - Chạy lại cho ra đúng cùng một kết quả (PRNG có seed cố định, ngày gốc cố định).
 * - Mọi tên người, công ty, email đều hư cấu (email @example.com).
 * - seed/   : mỗi file ứng với một bảng DB (tên cột snake_case) — BE seeder nạp vào tenant demo.
 *   Nhân sự, phòng ban, nghỉ phép, OT, chấm công vào/ra đều là dữ liệu quản lý ngay trong phần mềm
 *   (không đồng bộ từ hệ thống ngoài nào).
 * - ai/, jira/, base/, bitrix/ : giả lập response của các connector còn lại (Q3).
 * - Mật khẩu tài khoản demo lưu dạng hash ASP.NET Core Identity V3 (PBKDF2-HMACSHA512, 100.000 vòng)
 *   để BE nạp thẳng được. Đổi mật khẩu: đặt DX_DEMO_PASSWORD rồi chạy lại.
 */
import { mkdirSync, writeFileSync, rmSync, existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash, pbkdf2Sync } from 'node:crypto';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../mock-data');
const TODAY = '2026-10-20';
const DEMO_PASSWORD = process.env.DX_DEMO_PASSWORD || 'Demo@2026';
const CREATED_AT = '2026-01-05T08:00:00+07:00';

// ─── Tiện ích ───────────────────────────────────────────────────────────────

function mulberry32(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
const rand = mulberry32(20261020);
const pick = (arr) => arr[Math.floor(rand() * arr.length)];
const between = (min, max) => min + Math.floor(rand() * (max - min + 1));
const round = (n, d = 2) => Math.round(n * 10 ** d) / 10 ** d;

/** UUID v5-like ổn định theo tên (để id giữa các file luôn khớp nhau). */
function uid(name) {
  const h = createHash('sha1').update(`codeon-dx:${name}`).digest('hex');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-5${h.slice(13, 16)}-${((parseInt(h.slice(16, 17), 16) & 0x3) | 0x8).toString(16)}${h.slice(17, 20)}-${h.slice(20, 32)}`;
}

function addDays(iso, days) {
  const d = new Date(`${iso}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}
function diffDays(a, b) {
  return Math.round((new Date(`${b}T00:00:00Z`) - new Date(`${a}T00:00:00Z`)) / 86400000);
}
function isWeekend(iso) {
  const day = new Date(`${iso}T00:00:00Z`).getUTCDay();
  return day === 0 || day === 6;
}
function stripVi(s) {
  return s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D');
}

/** Hash định dạng ASP.NET Core Identity V3 (PasswordHasher<TUser>, .NET 7+). */
function identityV3Hash(password, saltSeed) {
  const prf = 2; // HMACSHA512
  const iterations = 100_000;
  const salt = createHash('sha256').update(`salt:${saltSeed}`).digest().subarray(0, 16);
  const subkey = pbkdf2Sync(password, salt, iterations, 32, 'sha512');
  const header = Buffer.alloc(13);
  header.writeUInt8(0x01, 0);
  header.writeUInt32BE(prf, 1);
  header.writeUInt32BE(iterations, 5);
  header.writeUInt32BE(salt.length, 9);
  return Buffer.concat([header, salt, subkey]).toString('base64');
}

const files = new Map();
function out(path, data) {
  files.set(path, data);
}

// ─── Tenant, RBAC ──────────────────────────────────────────────────────────

const TENANT_ID = uid('tenant:demo');
out('seed/tenants.json', [{
  id: TENANT_ID,
  code: 'demo',
  name: 'CodeOn Demo',
  default_locale: 'vi',
  time_zone: 'Asia/Ho_Chi_Minh',
  is_active: true,
  created_at: CREATED_AT,
}]);

const PERMISSIONS = [
  ['timesheet.edit', 'timesheet', 'Ghi công cá nhân', 'Log own timesheet'],
  ['timesheet.team', 'timesheet', 'Xem công số của nhóm', 'View team timesheets'],
  ['timesheet.all_view', 'timesheet', 'Xem tất cả công số', 'View all timesheets'],
  ['timesheet.all_edit', 'timesheet', 'Sửa công số của người khác', 'Edit others\' timesheets'],
  ['timesheet.approve', 'timesheet', 'Duyệt công số', 'Approve timesheets'],
  ['project.view', 'project', 'Xem dự án', 'View projects'],
  ['project.create', 'project', 'Tạo dự án', 'Create projects'],
  ['project.manage', 'project', 'Quản lý dự án', 'Manage projects'],
  ['staffing', 'project', 'Phân bổ nhân sự', 'Staffing'],
  ['employee.view', 'employee', 'Xem nhân sự', 'View employees'],
  ['employee.manage', 'employee', 'Quản lý nhân sự', 'Manage employees'],
  ['effort.view', 'effort', 'Xem công số', 'View effort'],
  ['effort.edit', 'effort', 'Sửa công số', 'Edit effort'],
  ['pcv.manage', 'quality', 'Quản lý PCV', 'Manage PCV'],
  ['css.manage', 'quality', 'Quản lý CSS', 'Manage CSS'],
  ['report.basic', 'report', 'Báo cáo tổng quan', 'Basic reports'],
  ['report.finance', 'report', 'Báo cáo tài chính', 'Finance reports'],
  ['report.quality', 'report', 'Báo cáo chất lượng', 'Quality reports'],
  ['admin.roles', 'admin', 'Quản lý vai trò', 'Manage roles'],
  ['admin.settings', 'admin', 'Cài đặt hệ thống', 'System settings'],
  ['admin.integrations', 'admin', 'Quản lý tích hợp', 'Manage integrations'],
  ['admin.insights', 'admin', 'Quản lý AI Insight', 'Manage AI insights'],
];
out('seed/permissions.json', PERMISSIONS.map(([key, group, name, name_en], i) => ({
  id: uid(`permission:${key}`), key, group, name, name_en, sort_seq: i + 1,
})));

const ROLES = [
  { code: 'admin', name: 'Admin', name_en: 'Admin', perms: PERMISSIONS.map((p) => p[0]) },
  {
    code: 'project_admin', name: 'Quản trị Hệ thống/Dự án', name_en: 'System/Project administrator',
    perms: ['timesheet.edit', 'timesheet.team', 'timesheet.all_view', 'timesheet.all_edit', 'timesheet.approve',
      'project.view', 'project.create', 'project.manage', 'staffing', 'employee.view', 'effort.view', 'effort.edit',
      'pcv.manage', 'css.manage', 'report.basic', 'report.quality'],
  },
  { code: 'member', name: 'Member', name_en: 'Member', perms: ['timesheet.edit', 'project.view'] },
  {
    code: 'resource_finance', name: 'Quản lý Tài nguyên & Ngân sách', name_en: 'Resource & budget manager',
    perms: ['employee.manage', 'employee.view', 'project.manage', 'project.view', 'staffing', 'timesheet.approve',
      'timesheet.team', 'effort.view', 'report.finance'],
  },
];
out('seed/roles.json', ROLES.map((r) => ({
  id: uid(`role:${r.code}`), tenant_id: TENANT_ID, code: r.code, name: r.name, name_en: r.name_en,
  is_system: true, created_at: CREATED_AT,
})));
out('seed/role_permissions.json', ROLES.flatMap((r) => r.perms.map((key) => ({
  role_id: uid(`role:${r.code}`), permission_id: uid(`permission:${key}`),
}))));

// ─── Master data ───────────────────────────────────────────────────────────

const MASTER = {
  project_status: [
    ['RUNNING', 'Đang chạy', 'Running', { color: 'green' }],
    ['ENDING', 'Sắp KT', 'Ending soon', { color: 'amber' }],
    ['PLANNING', 'Kế hoạch', 'Planning', { color: 'violet' }],
    ['COMPLETED', 'Hoàn thành', 'Completed', { color: 'slate' }],
  ],
  project_level: [
    ['S', 'S', 'S', { color: 'red', rank: 1 }],
    ['A', 'A', 'A', { color: 'amber', rank: 2 }],
    ['B', 'B', 'B', { color: 'pr', rank: 3 }],
    ['C', 'C', 'C', { color: 'slate', rank: 4 }],
  ],
  risk_level: [
    ['HIGH', 'Cao', 'High', { color: 'red' }],
    ['MEDIUM', 'Trung bình', 'Medium', { color: 'amber' }],
    ['LOW', 'Thấp', 'Low', { color: 'green' }],
  ],
  hr_level: [
    ['INTERN', 'Intern', 'Intern', { rank: 1, color: 'slate' }],
    ['FRESHER', 'Fresher', 'Fresher', { rank: 2, color: 'emerald' }],
    ['JUNIOR', 'Junior', 'Junior', { rank: 3, color: 'blue' }],
    ['MID', 'Mid', 'Mid', { rank: 4, color: 'indigo' }],
    ['SENIOR', 'Senior', 'Senior', { rank: 5, color: 'violet' }],
    ['LEAD', 'Lead', 'Lead', { rank: 6, color: 'amber' }],
    ['PRINCIPAL', 'Principal', 'Principal', { rank: 7, color: 'rose' }],
  ],
  job_position: [
    ['PO', 'PO', 'PO', {}], ['PM', 'PM', 'PM', {}], ['BRSE', 'BrSE', 'BrSE', {}],
    ['DEV_BE', 'Dev BE', 'Backend developer', {}], ['DEV_FE', 'Dev FE', 'Frontend developer', {}],
    ['DEV_MOBILE', 'Dev Mobile', 'Mobile developer', {}], ['DEV_AI', 'Dev AI', 'AI engineer', {}],
    ['TESTER', 'Tester', 'Tester', {}], ['COMTOR', 'Comtor', 'Interpreter', {}], ['INFRA', 'Infra', 'Infrastructure', {}],
  ],
  task_type: [
    ['PROJECT', 'Dự án', 'Project', {}], ['MEETING', 'Họp', 'Meeting', {}], ['RESEARCH', 'Nghiên cứu', 'Research', {}],
    ['DOCUMENT', 'Tài liệu', 'Documentation', {}], ['ACTIVITY', 'Các hoạt động', 'Activities', {}], ['OTHER', 'Khác', 'Other', {}],
    // Do hệ thống tự sinh từ đơn đã duyệt — không chọn tay khi ghi công
    ['OT', 'Làm thêm giờ (OT)', 'Overtime', { system: true }], ['LEAVE', 'Nghỉ phép', 'Leave', { system: true }],
  ],
  employee_status: [
    ['PROBATION', 'Thử việc', 'Probation', { color: 'blue', working: true }],
    ['ACTIVE', 'Đang làm việc', 'Active', { color: 'green', working: true }],
    ['MATERNITY_LEAVE', 'Nghỉ thai sản', 'Maternity leave', { color: 'amber', working: false }],
    ['UNPAID_LEAVE', 'Nghỉ không lương dài hạn', 'Long unpaid leave', { color: 'slate', working: false }],
    ['RESIGNED', 'Đã nghỉ việc', 'Resigned', { color: 'red', working: false }],
  ],
  leave_type: [
    ['ANNUAL', 'Nghỉ phép năm', 'Annual leave', { color: 'green', paid: true, deduct_balance: true }],
    ['SICK', 'Nghỉ ốm', 'Sick leave', { color: 'amber', paid: false, deduct_balance: false, need_attachment: true }],
    ['COMPENSATORY', 'Nghỉ bù', 'Compensatory leave', { color: 'teal', paid: true, deduct_balance: false }],
    ['PERSONAL_PAID', 'Nghỉ việc riêng có lương (cưới, tang…)', 'Paid personal leave (wedding, bereavement…)', { color: 'violet', paid: true, deduct_balance: false }],
    ['UNPAID', 'Nghỉ không lương', 'Unpaid leave', { color: 'slate', paid: false, deduct_balance: false }],
  ],
  link_type: [
    ['JIRA', 'Jira', 'Jira', { kind: 'ticket' }], ['BACKLOG', 'Backlog', 'Backlog', { kind: 'ticket' }],
    ['GITHUB', 'GitHub', 'GitHub', { kind: 'source' }], ['GITLAB', 'GitLab', 'GitLab', { kind: 'source' }],
    ['GOOGLE_DRIVE', 'Google Drive', 'Google Drive', { kind: 'storage' }],
  ],
  // Quốc gia / ngành nghề của khách hàng (màn admin_client_list)
  country: [
    ['VN', 'Việt Nam', 'Vietnam', {}], ['JP', 'Nhật Bản', 'Japan', {}], ['SG', 'Singapore', 'Singapore', {}],
    ['US', 'Hoa Kỳ', 'United States', {}], ['KR', 'Hàn Quốc', 'South Korea', {}],
  ],
  industry: [
    ['RETAIL', 'Bán lẻ', 'Retail', {}], ['FINANCE', 'Tài chính – Bảo hiểm', 'Finance & insurance', {}],
    ['MANUFACTURING', 'Sản xuất', 'Manufacturing', {}], ['LOGISTICS', 'Vận tải – Kho vận', 'Logistics', {}],
    ['SOFTWARE', 'Phần mềm', 'Software', {}], ['FOOD', 'Thực phẩm', 'Food & beverage', {}],
  ],
  currency: [['USD', 'USD', 'USD', { symbol: '$' }], ['JPY', 'JPY', 'JPY', { symbol: '¥' }], ['VND', 'VND', 'VND', { symbol: '₫' }]],
  // Mức hiệu suất dùng ở lưới Công số (D12 — cấu hình theo tenant). metadata.coefficient = hệ số nhân vào
  // TT effort khi tính Actual1 (xem frontend/src/modules/effort/utils/effort-calculations.ts). Bộ mặc định của tenant demo.
  performance_level: [
    ['TRAINEE', 'Học việc', 'Trainee', { coefficient: 0, color: 'slate' }],
    ['PARTIAL', 'Hỗ trợ một phần', 'Partial contribution', { coefficient: 0.5, color: 'amber' }],
    ['PROBATION', 'Thử việc', 'Probation', { coefficient: 0.8, color: 'blue' }],
    ['FULL', 'Chính thức', 'Full contribution', { coefficient: 1, color: 'emerald' }],
  ],
};
const masterRows = [];
const M = {}; // M.project_status.RUNNING = id
for (const [type, rows] of Object.entries(MASTER)) {
  M[type] = {};
  rows.forEach(([code, name, name_en, metadata], i) => {
    const id = uid(`master:${type}:${code}`);
    M[type][code] = id;
    masterRows.push({
      id, tenant_id: TENANT_ID, type, code, name, name_en, description: null, metadata, sort_seq: i + 1, is_active: true,
      updated_at: '2026-10-01T08:00:00+07:00',
    });
  });
}
out('seed/master_data.json', masterRows);

// ─── Tổ chức ───────────────────────────────────────────────────────────────

// Phòng ban → team do quản trị tự tạo trong màn "Phòng ban & Team" (admin_department_list)
const DEPARTMENTS = [
  { code: 'DEV1', name: 'Phòng Phát triển 1', teams: ['Team Web', 'Team Mobile'] },
  { code: 'DEV2', name: 'Phòng Phát triển 2', teams: ['Team Enterprise', 'Team QA'] },
  { code: 'AIS', name: 'Phòng Giải pháp AI', teams: ['Team AI', 'Team Data'] },
];
const departments = DEPARTMENTS.map((d, i) => ({
  id: uid(`department:${d.code}`), tenant_id: TENANT_ID, code: d.code, name: d.name, description: null,
  manager_employee_id: null, sort_seq: i + 1, is_active: true, created_at: CREATED_AT,
}));
const teams = DEPARTMENTS.flatMap((d) => d.teams.map((t, i) => ({
  id: uid(`team:${d.code}:${i}`), tenant_id: TENANT_ID, department_id: uid(`department:${d.code}`),
  code: `${d.code}-${i + 1}`, name: t, leader_employee_id: null, sort_seq: i + 1, is_active: true, created_at: CREATED_AT,
})));

const FAMILY = ['Nguyễn', 'Trần', 'Lê', 'Phạm', 'Hoàng', 'Vũ', 'Đặng', 'Bùi', 'Đỗ', 'Hồ', 'Ngô', 'Dương'];
const MIDDLE = ['Văn', 'Thị', 'Minh', 'Đức', 'Thu', 'Ngọc', 'Quang', 'Hải', 'Thanh', 'Anh', 'Hoài', 'Gia'];
const GIVEN = ['An', 'Bình', 'Châu', 'Dũng', 'Giang', 'Hà', 'Hiếu', 'Hùng', 'Khánh', 'Lan', 'Linh', 'Long', 'Mai',
  'Nam', 'Ngân', 'Phong', 'Phúc', 'Quân', 'Quỳnh', 'Sơn', 'Tâm', 'Thảo', 'Trang', 'Tuấn', 'Vy', 'Yến', 'Đạt', 'Huy'];
const POSITIONS = ['DEV_BE', 'DEV_BE', 'DEV_FE', 'DEV_FE', 'DEV_MOBILE', 'TESTER', 'TESTER', 'BRSE', 'COMTOR', 'PM', 'DEV_AI', 'INFRA'];
const POSITION_TITLE = Object.fromEntries(MASTER.job_position.map(([c, n]) => [c, n]));
const LEVELS = ['FRESHER', 'JUNIOR', 'JUNIOR', 'MID', 'MID', 'MID', 'SENIOR', 'SENIOR', 'LEAD'];
const SKILLS = {
  DEV_BE: ['.NET', 'C#', 'PostgreSQL', 'Node.js', 'Java', 'Docker'],
  DEV_FE: ['Vue', 'React', 'TypeScript', 'Tailwind CSS'],
  DEV_MOBILE: ['Flutter', 'Swift', 'Kotlin'],
  TESTER: ['Manual test', 'Playwright', 'JMeter'],
  BRSE: ['Tiếng Nhật N2', 'Phân tích nghiệp vụ'],
  COMTOR: ['Tiếng Nhật N1'],
  PM: ['Scrum', 'Jira', 'Quản lý rủi ro'],
  DEV_AI: ['Python', 'LLM', 'RAG'],
  INFRA: ['AWS', 'Kubernetes', 'Terraform'],
};

const usedNames = new Set();
const usedSlugs = new Set();
const employees = [];
const EMP_COUNT = 48;
for (let i = 0; i < EMP_COUNT; i++) {
  let full;
  do { full = `${pick(FAMILY)} ${pick(MIDDLE)} ${pick(GIVEN)}`; } while (usedNames.has(full));
  usedNames.add(full);
  const parts = stripVi(full).toLowerCase().split(' ');
  let slug = `${parts[2]}.${parts[0][0]}${parts[1][0]}`;
  let n = 1;
  while (usedSlugs.has(slug)) slug = `${parts[2]}.${parts[0][0]}${parts[1][0]}${++n}`;
  usedSlugs.add(slug);
  const dept = DEPARTMENTS[i % 3];
  const team = teams.filter((t) => t.department_id === uid(`department:${dept.code}`))[i % 2];
  const position = i < 3 ? 'PM' : POSITIONS[i % POSITIONS.length];
  const level = i < 3 ? 'LEAD' : pick(LEVELS);
  const leftDate = i === 45 ? '2026-08-31' : null;
  employees.push({
    id: uid(`employee:${i}`),
    tenant_id: TENANT_ID,
    user_id: null,
    code: `NV${String(i + 1).padStart(4, '0')}`,
    full_name: full,
    email: `${slug}@example.com`,
    phone: `09${String(between(10000000, 99999999))}`,
    position: POSITION_TITLE[position],
    position_code: position,
    level_id: M.hr_level[level],
    level_code: level,
    department_id: uid(`department:${dept.code}`),
    team_id: team.id,
    status: leftDate ? 'RESIGNED' : i === 40 ? 'MATERNITY_LEAVE' : i >= 46 ? 'PROBATION' : 'ACTIVE',
    is_active: !leftDate,
    joined_date: i >= 46 ? addDays(TODAY, -between(10, 50)) : addDays('2019-03-01', between(0, 2200)),
    left_date: leftDate,
    avatar_url: null,
    skills: (SKILLS[position] || []).filter(() => rand() > 0.35).slice(0, 3),
    created_at: CREATED_AT,
  });
}

// Trưởng phòng = 3 PM đầu tiên; trưởng nhóm = nhân sự đầu tiên của team
departments.forEach((d, i) => { d.manager_employee_id = employees[i].id; });
teams.forEach((t) => { t.leader_employee_id = employees.find((e) => e.team_id === t.id && e.is_active)?.id ?? null; });
out('seed/departments.json', departments);
out('seed/teams.json', teams);

// Tài khoản demo — mỗi vai trò một người, gắn với 1 nhân sự
const DEMO_USERS = [
  { key: 'admin', email: 'admin@example.com', role: 'admin', emp: 0 },
  { key: 'pm', email: 'pm@example.com', role: 'project_admin', emp: 1 },
  { key: 'finance', email: 'finance@example.com', role: 'resource_finance', emp: 2 },
  { key: 'member', email: 'member@example.com', role: 'member', emp: 5 },
];
const users = DEMO_USERS.map((u) => {
  const emp = employees[u.emp];
  const id = uid(`user:${u.key}`);
  emp.user_id = id;
  return {
    id, tenant_id: TENANT_ID, email: u.email, normalized_email: u.email.toUpperCase(),
    full_name: emp.full_name, employee_id: emp.id, preferred_locale: 'vi', avatar_url: null,
    password_hash: identityV3Hash(DEMO_PASSWORD, u.email), email_confirmed: true, must_change_password: false,
    access_failed_count: 0, lockout_end: null, last_login_at: null, is_active: true, created_at: CREATED_AT,
  };
});
out('seed/users.json', users);
out('seed/user_roles.json', DEMO_USERS.map((u) => ({ user_id: uid(`user:${u.key}`), role_id: uid(`role:${u.role}`) })));
out('seed/employees.json', employees);

// ─── Khách hàng ────────────────────────────────────────────────────────────

const CLIENTS = [
  ['HOSHINO', 'Hoshino Trading', 'Hoshino', 'JP', 'RETAIL', 'Sato Haruki', 'JPY'], ['MINATO', 'Minato Digital', 'Minato', 'JP', 'SOFTWARE', 'Ito Mei', 'JPY'],
  ['AOBA', 'Aoba Insurance', 'Aoba', 'JP', 'FINANCE', 'Kato Ren', 'JPY'], ['KAWASE', 'Kawase Robotics', 'Kawase', 'JP', 'MANUFACTURING', 'Mori Yuto', 'USD'],
  ['NISHIKI', 'Nishiki Foods', 'Nishiki', 'JP', 'FOOD', 'Abe Sora', 'JPY'], ['SONGHONG', 'Sông Hồng Retail', 'Sông Hồng', 'VN', 'RETAIL', 'Trần Bảo Ngọc', 'VND'],
  ['LAMVIEN', 'Lâm Viên Tech', 'Lâm Viên', 'VN', 'SOFTWARE', 'Lê Quang Huy', 'USD'], ['PHUONGNAM', 'Phương Nam Logistics', 'Phương Nam', 'VN', 'LOGISTICS', 'Võ Thanh Tâm', 'VND'],
  // Khách hàng đã ngừng hợp tác / đã xoá mềm — để thử bộ lọc trạng thái và thùng rác
  ['TANAKA', 'Tanaka Logistics', 'Tanaka', 'JP', 'LOGISTICS', 'Tanaka Riku', 'JPY', false], ['BACHDANG', 'Bạch Đằng Media', 'Bạch Đằng', 'VN', null, null, 'VND', true, true],
];
const clients = CLIENTS.map(([code, name, short_name, country, industry, contact_name, currency, is_active = true, is_deleted = false]) => ({
  id: uid(`client:${code}`), tenant_id: TENANT_ID, code, name, short_name, country,
  country_id: M.country[country], industry_id: industry ? M.industry[industry] : null, contact_name,
  contact_email: `contact@${code.toLowerCase()}.example.com`, default_currency: currency, is_active, is_deleted, created_at: CREATED_AT,
}));

// ─── Dự án ─────────────────────────────────────────────────────────────────

// [mã, khách, tên, cấp, start offset (ngày so với TODAY), thời lượng (ngày), rủi ro, % budget mục tiêu, deleted]
const PROJECTS = [
  ['P01', 'HOSHINO', 'Cổng thanh toán B2B', 'S', -300, 420, 'HIGH', 92],
  ['P02', 'HOSHINO', 'Hệ thống quản lý kho', 'A', -200, 230, 'MEDIUM', 81],
  ['P03', 'MINATO', 'Ứng dụng đặt lịch khám', 'A', -150, 330, 'LOW', 44],
  ['P04', 'MINATO', 'Chatbot chăm sóc khách hàng', 'B', -90, 115, 'MEDIUM', 63],
  ['P05', 'AOBA', 'Cổng bồi thường bảo hiểm', 'S', -400, 520, 'HIGH', 88],
  ['P06', 'AOBA', 'Ứng dụng đại lý di động', 'B', -60, 240, 'LOW', 22],
  ['P07', 'KAWASE', 'Giám sát dây chuyền IoT', 'A', -250, 280, 'MEDIUM', 77],
  ['P08', 'KAWASE', 'Phân tích dữ liệu bảo trì', 'B', 30, 180, 'LOW', 0],
  ['P09', 'NISHIKI', 'Website thương mại điện tử', 'B', -120, 145, 'LOW', 58],
  ['P10', 'NISHIKI', 'Hệ thống POS chuỗi cửa hàng', 'A', -330, 300, 'MEDIUM', 97],
  ['P11', 'SONGHONG', 'Ứng dụng khách hàng thân thiết', 'C', -45, 200, 'LOW', 18],
  ['P12', 'SONGHONG', 'Báo cáo doanh thu thời gian thực', 'B', 15, 150, 'LOW', 0],
  ['P13', 'LAMVIEN', 'Nền tảng học trực tuyến', 'A', -180, 360, 'HIGH', 70],
  ['P14', 'LAMVIEN', 'Kiểm thử tự động', 'C', -30, 120, 'LOW', 12],
  ['P15', 'PHUONGNAM', 'Quản lý vận tải', 'S', -260, 290, 'HIGH', 86],
  ['P16', 'PHUONGNAM', 'Ứng dụng tài xế', 'B', -100, 300, 'MEDIUM', 35],
  ['P17', 'HOSHINO', 'Chuyển đổi hệ thống kế toán', 'A', -500, 400, 'LOW', 99],
  ['P18', 'MINATO', 'Trợ lý AI tra cứu tài liệu', 'B', 45, 210, 'MEDIUM', 0],
  ['P19', 'AOBA', 'Bảo trì hệ thống lõi', 'C', -365, 380, 'LOW', 66],
  ['P20', 'KAWASE', 'Ứng dụng kiểm định chất lượng', 'B', -75, 100, 'MEDIUM', 54],
  ['P21', 'NISHIKI', 'Hệ thống đặt hàng nhà cung cấp', 'C', -20, 180, 'LOW', 9],
  ['P22', 'LAMVIEN', 'Cổng thông tin sinh viên', 'B', -420, 380, 'LOW', 100],
  ['P23', 'SONGHONG', 'Hệ thống khuyến mãi (đã huỷ)', 'C', -200, 90, 'LOW', 40, true],
  ['P24', 'PHUONGNAM', 'Bản đồ tuyến giao hàng (đã huỷ)', 'C', -150, 60, 'MEDIUM', 30, true],
];
const ROLE_PRICE = { PM: 4200, BRSE: 3800, DEV_BE: 3200, DEV_FE: 3000, DEV_MOBILE: 3100, DEV_AI: 3600, TESTER: 2400, COMTOR: 2200, INFRA: 3300 };

const projects = [];
const contractStaff = [];
const assignments = [];
const assignmentSchedules = [];
const projectMembers = [];
const projectLinks = [];
let empCursor = 3;

for (const [code, clientCode, title, grade, startOffset, duration, risk, budgetPct, deleted] of PROJECTS) {
  const id = uid(`project:${code}`);
  const start = addDays(TODAY, startOffset);
  const end = addDays(start, duration);
  const daysLeft = diffDays(TODAY, end);
  let status = 'RUNNING';
  if (startOffset > 0) status = 'PLANNING';
  else if (daysLeft < 0) status = 'COMPLETED';
  else if (daysLeft <= 30 && rand() > 0.4) status = 'ENDING';
  const client = clients.find((c) => c.code === clientCode);

  // Kế hoạch nhân sự hợp đồng
  const roleSet = grade === 'S' ? ['PM', 'BRSE', 'DEV_BE', 'DEV_FE', 'TESTER'] : grade === 'A'
    ? ['BRSE', 'DEV_BE', 'DEV_FE', 'TESTER'] : grade === 'B' ? ['DEV_BE', 'DEV_FE', 'TESTER'] : ['DEV_BE', 'TESTER'];
  if (title.includes('AI') || title.includes('Chatbot')) roleSet.splice(1, 0, 'DEV_AI');
  if (title.includes('di động') || title.includes('Ứng dụng')) roleSet.push('DEV_MOBILE');
  let contractFte = 0;
  let weightedPrice = 0;
  let totalQty = 0;
  const csRows = roleSet.map((role, i) => {
    const qty = role.startsWith('DEV') ? (grade === 'S' ? 3 : grade === 'A' ? 2 : 1) : 1;
    const ftePer = role === 'PM' || role === 'COMTOR' ? 0.5 : 1;
    const row = {
      id: uid(`contract_staff:${code}:${i}`), tenant_id: TENANT_ID, project_id: id,
      role_code: role, level_id: M.hr_level[role === 'PM' || role === 'BRSE' ? 'SENIOR' : pick(['JUNIOR', 'MID', 'SENIOR'])],
      quantity: qty, fte_per_person: ftePer, start_date: start, end_date: end, price_per_month: ROLE_PRICE[role] ?? 3000,
      is_deleted: false,
    };
    contractFte += qty * ftePer;
    weightedPrice += row.price_per_month * qty;
    totalQty += qty;
    return row;
  });
  contractStaff.push(...csRows);
  const avgPrice = totalQty > 0 ? weightedPrice / totalQty : 0;
  const months = Math.max(1, duration / 30);
  const budget = Math.round((contractFte * avgPrice * months) / 1000) * 1000;
  const actualCostFte = budgetPct > 0 ? round((budgetPct / 100) * budget / avgPrice, 2) : 0;

  // Người thực tế (dự án chưa bắt đầu thì chưa gán)
  let actualFte = 0;
  if (status !== 'PLANNING') {
    for (const cs of csRows) {
      for (let q = 0; q < cs.quantity; q++) {
        // Để trống 1 slot ở vài dự án rủi ro cao → "Vị trí trống"
        if (risk === 'HIGH' && q === cs.quantity - 1 && cs.quantity > 1) continue;
        const emp = employees[empCursor % EMP_COUNT];
        empCursor += 1;
        const fte = rand() > 0.8 ? 0.5 : cs.fte_per_person;
        const aStart = addDays(start, between(0, 20));
        const aEnd = end;
        const aid = uid(`assignment:${code}:${cs.id}:${q}`);
        assignments.push({
          id: aid, tenant_id: TENANT_ID, project_id: id, employee_id: emp.id, contract_staff_id: cs.id, slot_index: q,
          role_in_project: cs.role_code, level_id: cs.level_id, fte_allocated: fte, start_date: aStart, end_date: aEnd,
          is_deleted: false,
        });
        assignmentSchedules.push({
          id: uid(`schedule:${aid}`), tenant_id: TENANT_ID, assignment_id: aid, start_date: aStart, end_date: aEnd, fte_allocated: fte,
        });
        if (TODAY >= aStart && TODAY <= aEnd) actualFte += fte;
        if (!projectMembers.some((m) => m.project_id === id && m.employee_id === emp.id)) {
          projectMembers.push({ project_id: id, employee_id: emp.id, role: cs.role_code });
        }
      }
    }
  }
  // Member demo (employee 5) tham gia 3 dự án đang chạy để thấy danh sách "dự án của tôi"
  if (['P03', 'P04', 'P09'].includes(code)) {
    const emp = employees[5];
    const cs = csRows.find((c) => c.role_code === 'DEV_FE') || csRows[0];
    const aid = uid(`assignment:${code}:member-demo`);
    assignments.push({
      id: aid, tenant_id: TENANT_ID, project_id: id, employee_id: emp.id, contract_staff_id: cs.id, slot_index: 99,
      role_in_project: cs.role_code, level_id: cs.level_id, fte_allocated: 0.5, start_date: start, end_date: end, is_deleted: false,
    });
    assignmentSchedules.push({ id: uid(`schedule:${aid}`), tenant_id: TENANT_ID, assignment_id: aid, start_date: start, end_date: end, fte_allocated: 0.5 });
    actualFte += 0.5;
    projectMembers.push({ project_id: id, employee_id: emp.id, role: cs.role_code });
  }

  projects.push({
    id, tenant_id: TENANT_ID, code: `PRJ-${code.slice(1)}`,
    name: `${client.short_name} – ${title}`,
    client_id: client.id, grade_id: M.project_level[grade], status_id: M.project_status[status], risk_level_id: M.risk_level[risk],
    description: `Dự án ${title.toLowerCase()} cho ${client.name}.`,
    start_date: start, end_date: end, budget, currency_code: 'USD',
    progress_pct: status === 'COMPLETED' ? 100 : Math.max(0, Math.min(99, Math.round((diffDays(start, TODAY) / duration) * 100))),
    contract_staff_fte: round(contractFte, 1), actual_staff_fte: round(actualFte, 1), actual_cost_fte: actualCostFte,
    is_deleted: !!deleted, created_at: `${addDays(start, -14)}T09:00:00+07:00`, updated_at: `${addDays(start, -14)}T09:00:00+07:00`,
  });
  projectLinks.push(
    { id: uid(`link:${code}:jira`), tenant_id: TENANT_ID, project_id: id, link_type_id: M.link_type.JIRA, provider: 'jira', name: 'Jira', url: `https://jira.example.com/browse/${code}` },
    { id: uid(`link:${code}:git`), tenant_id: TENANT_ID, project_id: id, link_type_id: M.link_type.GITLAB, provider: 'gitlab', name: 'GitLab', url: `https://gitlab.example.com/codeon/${code.toLowerCase()}` },
  );
}
for (const c of clients) c.projects_count = projects.filter((p) => p.client_id === c.id && !p.is_deleted).length;
out('seed/clients.json', clients);
out('seed/projects.json', projects);
out('seed/contract_staff.json', contractStaff);
out('seed/assignments.json', assignments);
out('seed/project_assignment_schedules.json', assignmentSchedules);
out('seed/project_members.json', projectMembers);
out('seed/project_links.json', projectLinks);

// Milestone cho dự án đang chạy
const milestones = [];
for (const p of projects.filter((x) => !x.is_deleted && x.status_id !== M.project_status.PLANNING)) {
  const names = ['Chốt yêu cầu', 'Hoàn thành thiết kế', 'Bàn giao Sprint 1', 'Kiểm thử tích hợp', 'Nghiệm thu UAT'];
  const total = diffDays(p.start_date, p.end_date);
  names.forEach((name, i) => {
    const planned = addDays(p.start_date, Math.round((total * (i + 1)) / (names.length + 0.5)));
    const done = planned < TODAY;
    const late = done && rand() > 0.7;
    milestones.push({
      id: uid(`milestone:${p.id}:${i}`), tenant_id: TENANT_ID, project_id: p.id, name, sort_seq: i + 1,
      planned_date: planned, actual_date: done ? addDays(planned, late ? between(3, 12) : between(-3, 0)) : null,
      justification: late ? 'Khách hàng đổi yêu cầu giữa sprint' : null, is_deleted: false,
    });
  });
}
out('seed/project_milestones.json', milestones);

// ─── Chấm công (3 tuần gần nhất, ngày làm việc) ───────────────────────────

const timesheets = [];
const activeAssign = assignments.filter((a) => a.start_date <= TODAY && a.end_date >= TODAY);
const tsEmployees = employees.filter((e) => e.is_active).slice(0, 20);
for (let d = -20; d <= 0; d++) {
  const date = addDays(TODAY, d);
  if (isWeekend(date)) continue;
  for (const emp of tsEmployees) {
    const mine = activeAssign.filter((a) => a.employee_id === emp.id);
    let remaining = 8;
    mine.slice(0, 2).forEach((a, idx) => {
      const hours = idx === 0 && mine.length > 1 ? 5 : Math.min(remaining, 8 * a.fte_allocated);
      if (hours <= 0) return;
      remaining -= hours;
      timesheets.push({
        id: uid(`ts:${emp.id}:${date}:${a.project_id}`), tenant_id: TENANT_ID, employee_id: emp.id, project_id: a.project_id,
        task_type_id: M.task_type.PROJECT, work_date: date, hours, description: 'Phát triển tính năng theo sprint',
        status: d < -6 ? 'approved' : 'draft', source: 'manual', source_request_id: null,
      });
    });
    if (remaining >= 1) {
      timesheets.push({
        id: uid(`ts:${emp.id}:${date}:meeting`), tenant_id: TENANT_ID, employee_id: emp.id, project_id: null,
        task_type_id: M.task_type.MEETING, work_date: date, hours: 1, description: 'Họp nội bộ', status: d < -6 ? 'approved' : 'draft',
        source: 'manual', source_request_id: null,
      });
    }
  }
}
// (seed/timesheets.json được ghi sau khi sinh đơn nghỉ phép / OT — dòng công tự sinh từ đơn đã duyệt)

// ─── Công số (Effort) tháng 2026-10 ────────────────────────────────────────

const effortCenters = projects.filter((p) => !p.is_deleted).map((p, i) => ({
  id: uid(`effort_center:${p.id}`), tenant_id: TENANT_ID, project_id: p.id, department_id: departments[i % 3].id,
}));
out('seed/effort_project_centers.json', effortCenters);
const EFFORT_ROLE = { DEV_BE: 'Dev BE', DEV_FE: 'Dev FE', DEV_MOBILE: 'Dev Mobile', DEV_AI: 'Dev AI', TESTER: 'Tester', PM: 'PO', BRSE: 'PO', COMTOR: 'Comtor', INFRA: 'Infra' };
const effort = [];
for (const a of activeAssign) {
  const emp = employees.find((e) => e.id === a.employee_id);
  const key = `${a.employee_id}:${a.project_id}:${EFFORT_ROLE[a.role_in_project] || 'Dev BE'}`;
  if (effort.some((x) => x._key === key)) continue;
  const perf = emp.level_code === 'FRESHER' ? 0.8 : 1.0;
  effort.push({
    _key: key,
    id: uid(`effort:${key}:2026-10`), tenant_id: TENANT_ID, employee_id: a.employee_id, project_id: a.project_id,
    year_month: '2026-10-01', team_id: emp.team_id, role: EFFORT_ROLE[a.role_in_project] || 'Dev BE', note: null,
    hd: round(a.fte_allocated, 2), tt_effort: round(a.fte_allocated, 2), ot_effort: rand() > 0.85 ? 0.1 : 0,
    // performance = hệ số của mức hiệu suất (performance_level.metadata.coefficient) tại thời điểm nhập
    performance: perf, performance_level_id: perf === 1 ? M.performance_level.FULL : M.performance_level.PROBATION, allocate_rate: 100,
  });
}
out('seed/effort_allocations.json', effort.map(({ _key, ...rest }) => rest));

// ─── Chất lượng: PCV, CSS, Defect/Leakage ─────────────────────────────────

const PCV_CRITERIA = [
  ['Lập kế hoạch', 'Kế hoạch dự án được cập nhật trong tháng'],
  ['Lập kế hoạch', 'Danh sách rủi ro được rà soát và có người phụ trách'],
  ['Yêu cầu', 'Yêu cầu thay đổi được ghi nhận và phê duyệt'],
  ['Thiết kế', 'Tài liệu thiết kế được review trước khi code'],
  ['Phát triển', 'Mọi merge request đều được review'],
  ['Kiểm thử', 'Có test case cho chức năng mới'],
  ['Kiểm thử', 'Lỗi nghiêm trọng được xử lý trước khi bàn giao'],
  ['Bàn giao', 'Biên bản bàn giao được khách hàng xác nhận'],
  ['Cấu hình', 'Mã nguồn và tài liệu được quản lý phiên bản'],
  ['Báo cáo', 'Báo cáo tuần được gửi khách hàng đúng hạn'],
];
const pcvTemplate = PCV_CRITERIA.map(([group, content], i) => ({
  id: uid(`pcv_template:${i}`), tenant_id: TENANT_ID, group_name: group, content, sort_seq: i + 1, is_deleted: false,
}));
out('seed/pcv_template_items.json', pcvTemplate);
const pcvItems = [];
const pcvAssessments = [];
const pcvResults = [];
const pcvProjects = projects.filter((p) => !p.is_deleted && p.status_id === M.project_status.RUNNING).slice(0, 6);
for (const p of pcvProjects) {
  const items = pcvTemplate.map((t, i) => ({
    id: uid(`pcv_item:${p.id}:${i}`), tenant_id: TENANT_ID, project_id: p.id, group_name: t.group_name, content: t.content, sort_seq: t.sort_seq, is_deleted: false,
  }));
  pcvItems.push(...items);
  for (const month of ['2026-08-01', '2026-09-01']) {
    const aid = uid(`pcv_assessment:${p.id}:${month}`);
    pcvAssessments.push({ id: aid, tenant_id: TENANT_ID, project_id: p.id, assess_month: month, assessed_by: users[1].id, assessed_at: `${addDays(month, 32).slice(0, 8)}05T10:00:00+07:00` });
    items.forEach((it, i) => {
      const r = rand();
      const result = r > 0.82 ? 'fail' : r > 0.07 ? 'pass' : null;
      pcvResults.push({ assessment_id: aid, item_id: it.id, result, error_note: result === 'fail' ? 'Chưa có bằng chứng thực hiện trong tháng' : null });
    });
  }
}
out('seed/project_pcv_items.json', pcvItems);
out('seed/project_pcv_assessments.json', pcvAssessments);
out('seed/project_pcv_results.json', pcvResults);

const CSS_QUESTIONS = [
  ['Chất lượng sản phẩm bàn giao', true], ['Tiến độ bàn giao so với kế hoạch', true], ['Khả năng giao tiếp của đội dự án', true],
  ['Mức độ chủ động đề xuất cải tiến', false], ['Mức độ hài lòng chung', true],
];
out('seed/css_template.json', [{
  id: uid('css_template'), tenant_id: TENANT_ID, title: 'Khảo sát mức độ hài lòng của khách hàng',
  intro: 'Cảm ơn Quý khách đã dành thời gian đánh giá dự án.\nKết quả khảo sát giúp đội dự án cải thiện chất lượng dịch vụ trong các tháng tiếp theo.',
  scale_note: '1: Rất không hài lòng · 2: Không hài lòng · 3: Bình thường · 4: Hài lòng · 5: Rất hài lòng',
  comment_label: 'Quý khách có ý kiến hoặc đề xuất nào khác cho đội dự án không?',
}]);
out('seed/css_template_questions.json', CSS_QUESTIONS.map(([content, required], i) => ({
  id: uid(`css_q:${i}`), tenant_id: TENANT_ID, template_id: uid('css_template'), content, is_required: required, sort_seq: i + 1,
})));
const cssSurveys = [];
for (const p of pcvProjects.slice(0, 4)) {
  cssSurveys.push({
    id: uid(`css_survey:${p.id}:2026-09`), tenant_id: TENANT_ID, project_id: p.id, survey_month: '2026-09-01',
    token: createHash('sha256').update(`css:${p.id}`).digest('hex'), status: 'submitted',
    submitted_at: '2026-10-03T09:30:00+07:00',
    scores: CSS_QUESTIONS.map(([content], i) => ({ question: content, score: i === 3 && rand() > 0.5 ? null : between(3, 5) })),
  });
}
out('seed/project_css_surveys.json', cssSurveys);

const quality = [];
for (const p of pcvProjects) {
  for (const month of ['2026-07-01', '2026-08-01', '2026-09-01']) {
    quality.push(
      { id: uid(`quality:${p.id}:${month}:defect`), tenant_id: TENANT_ID, project_id: p.id, metric_month: month, kind: 'defect', defect_count: between(8, 40), product_size: between(4, 12), product_size_unit: 'KLOC' },
      { id: uid(`quality:${p.id}:${month}:leakage`), tenant_id: TENANT_ID, project_id: p.id, metric_month: month, kind: 'leakage', defect_count: between(0, 6), product_size: between(4, 12), product_size_unit: 'KLOC' },
    );
  }
}
out('seed/project_quality_metrics.json', quality);

// ─── Hệ thống: quy tắc, cài đặt, AI insight, tích hợp ─────────────────────

// Quy tắc hệ thống: khoá (key) cố định trong code, quản trị chỉ chỉnh giá trị + bật/tắt (màn admin_rule_list).
// value_type 'number' có min/max (BE kiểm tra lại khi lưu), 'boolean' là công tắc.
const RULES = [
  // key, module, value_type, value, min, max, tên vi, tên en, mô tả vi, mô tả en
  ['TIMESHEET_MIN_HOURS', 'timesheet', 'number', 0.25, 0, 24, 'Công số tối thiểu', 'Minimum hours', 'Số giờ tối thiểu của một dòng công', 'Minimum hours of one timesheet line'],
  ['TIMESHEET_MAX_HOURS', 'timesheet', 'number', 12, 0, 24, 'Công số tối đa', 'Maximum hours', 'Số giờ tối đa được ghi trong một ngày', 'Maximum hours that can be logged in one day'],
  ['TIMESHEET_WEEKLY_MAX_HOURS', 'timesheet', 'number', 60, 1, 168, 'Công số tối đa trong tuần', 'Weekly maximum hours', 'Giới hạn số giờ chấm công tối đa trong một tuần (bao gồm cả OT)', 'Maximum hours logged in a week (including overtime)'],
  ['TIMESHEET_EDIT_LIMIT_DAYS', 'timesheet', 'number', 7, 0, 365, 'Giới hạn ngày chỉnh sửa', 'Edit limit (days)', 'Số ngày tối đa cho phép nhân sự chỉnh sửa công số trong quá khứ', 'How many past days employees may still edit their timesheet'],
  ['TIMESHEET_LOCK_DATE_OF_MONTH', 'timesheet', 'number', 5, 1, 31, 'Ngày khóa công tháng trước', 'Previous month lock day', 'Khóa hoàn toàn chỉnh sửa công số của tháng trước từ ngày này của tháng hiện tại', 'From this day of the current month, the previous month timesheet is locked'],
  ['TIMESHEET_ALLOW_FUTURE', 'timesheet', 'boolean', false, null, null, 'Cho phép nhập công tương lai', 'Allow future entries', 'Cho phép nhân sự điền công số cho các ngày trong tương lai', 'Allow employees to log time for future dates'],
  ['TIMESHEET_FTE_HOURS', 'timesheet', 'number', 8, 1, 24, 'Số giờ quy đổi 1 FTE', 'Hours per FTE', 'Số giờ làm việc tương đương 1 FTE (Full-Time Equivalent) trong một ngày', 'Working hours equal to 1 FTE (full-time equivalent) per day'],
  ['PROJECT_ENDING_SOON_DAYS', 'project', 'number', 30, 1, 365, 'Ngày sắp kết thúc dự án', 'Ending soon (days)', 'Số ngày trước khi kết thúc để coi dự án là "sắp kết thúc"', 'Days before the end date at which a project is considered "ending soon"'],
  ['PROJECT_BUDGET_WARNING_THRESHOLD', 'project', 'number', 75, 1, 100, 'Ngưỡng cảnh báo ngân sách', 'Budget warning threshold', 'Phần trăm ngân sách đã sử dụng để kích hoạt cảnh báo (VD: 75 = 75%)', 'Percentage of budget used that triggers a warning (e.g. 75 = 75%)'],
  ['HR_OVERLOAD_ALLOCATION_THRESHOLD', 'hr', 'number', 100, 1, 1000, 'Ngưỡng cảnh báo quá tải', 'Overload threshold', 'Tỷ lệ phân bổ dự án (%) bắt đầu kích hoạt cảnh báo quá tải nhân sự', 'Allocation percentage at which an employee is flagged as overloaded'],
  ['HR_MIN_ALLOCATION_THRESHOLD', 'hr', 'number', 20, 1, 1000, 'Ngưỡng định mức tối thiểu', 'Minimum allocation threshold', 'Tỷ lệ phân bổ (%) dưới mức này coi là nhân sự đang chờ việc', 'Allocation percentage below which an employee is considered available'],
  ['HR_UTILIZATION_TARGET_PERCENT', 'hr', 'number', 85, 1, 100, 'Tỷ lệ sử dụng mục tiêu', 'Utilization target', 'Tỷ lệ sử dụng nhân sự mục tiêu của công ty (%)', 'Company target for staff utilization (%)'],
  ['LEAVE_ANNUAL_DAYS', 'leave', 'number', 12, 0, 60, 'Số ngày phép năm', 'Annual leave days', 'Số ngày phép năm cơ bản của một nhân viên', 'Base annual leave days per employee'],
  ['LEAVE_SENIORITY_STEP_YEARS', 'leave', 'number', 5, 1, 50, 'Thâm niên cộng phép', 'Seniority step (years)', 'Cứ đủ số năm làm việc này được cộng thêm 1 ngày phép', 'One extra leave day for every this many years of service'],
  ['LEAVE_CARRY_OVER_MAX_DAYS', 'leave', 'number', 5, 0, 60, 'Phép năm cũ chuyển sang', 'Carry-over days', 'Số ngày phép năm cũ tối đa được chuyển sang năm mới', 'Maximum unused leave days carried over to the next year'],
  ['LEAVE_MIN_NOTICE_DAYS', 'leave', 'number', 1, 0, 60, 'Báo trước khi nghỉ', 'Notice period (days)', 'Số ngày báo trước tối thiểu khi xin nghỉ phép năm', 'Minimum days of notice for an annual leave request'],
  ['OT_MAX_HOURS_PER_DAY', 'overtime', 'number', 4, 0, 24, 'OT tối đa mỗi ngày', 'Max overtime per day', 'Số giờ OT tối đa mỗi ngày', 'Maximum overtime hours per day'],
  ['OT_MAX_HOURS_PER_MONTH', 'overtime', 'number', 40, 0, 300, 'OT tối đa mỗi tháng', 'Max overtime per month', 'Số giờ OT tối đa mỗi tháng', 'Maximum overtime hours per month'],
  ['OT_MAX_HOURS_PER_YEAR', 'overtime', 'number', 200, 0, 1000, 'OT tối đa mỗi năm', 'Max overtime per year', 'Số giờ OT tối đa mỗi năm', 'Maximum overtime hours per year'],
  ['ATTENDANCE_LATE_GRACE_MINUTES', 'attendance', 'number', 15, 0, 240, 'Phút đi muộn bỏ qua', 'Late grace (minutes)', 'Số phút đi muộn được bỏ qua sau giờ bắt đầu làm việc', 'Minutes after the start time that are not counted as late'],
  ['ATTENDANCE_EDIT_LIMIT_DAYS', 'attendance', 'number', 7, 0, 365, 'Hạn tự bổ sung giờ vào/ra', 'Self-correction limit (days)', 'Số ngày nhân viên được tự bổ sung giờ vào/ra đã qua', 'How many past days employees may add missing check-in/out times'],
  // Ngưỡng tô màu chỉ số chất lượng ở tab Metrics (D12 — mỗi tenant tự đặt; công thức chỉ số giữ trong code)
  ['QUALITY_PCV_GREEN_MIN', 'quality', 'number', 90, 0, 100, 'PCV đạt (xanh) từ', 'PCV good (green) from', 'Điểm PCV (%) từ mức này trở lên tô xanh', 'PCV score (%) at or above this is shown green'],
  ['QUALITY_PCV_ORANGE_MIN', 'quality', 'number', 70, 0, 100, 'PCV cảnh báo (cam) từ', 'PCV warning (orange) from', 'Điểm PCV (%) từ mức này đến dưới mức xanh tô cam; thấp hơn tô đỏ', 'PCV score (%) from this up to the green level is orange; below is red'],
  ['QUALITY_CSS_GREEN_MIN', 'quality', 'number', 4.5, 1, 5, 'CSS đạt (xanh) từ', 'CSS good (green) from', 'Điểm CSS (thang 1–5) từ mức này trở lên tô xanh', 'CSS score (1–5) at or above this is shown green'],
  ['QUALITY_CSS_ORANGE_MIN', 'quality', 'number', 3.5, 1, 5, 'CSS cảnh báo (cam) từ', 'CSS warning (orange) from', 'Điểm CSS từ mức này đến dưới mức xanh tô cam; thấp hơn tô đỏ', 'CSS score from this up to the green level is orange; below is red'],
  ['QUALITY_EE_GREEN_MAX', 'quality', 'number', 100, 1, 500, 'EE tốt (xanh) đến', 'EE good (green) up to', 'EE (%) không vượt mức này tô xanh', 'EE (%) not above this is shown green'],
  ['QUALITY_EE_ORANGE_MAX', 'quality', 'number', 110, 1, 500, 'EE cảnh báo (cam) đến', 'EE warning (orange) up to', 'EE (%) trên mức xanh đến mức này tô cam; cao hơn tô đỏ', 'EE (%) above green up to this is orange; above is red'],
  ['QUALITY_DEFECT_RATE_GOOD_MIN', 'quality', 'number', 4, 0, 100, 'Defect/MM tốt từ', 'Defect/MM good from', 'Tỷ lệ defect/MM dưới mức này tô cam nhạt (test chưa đủ)', 'Defects per MM below this are light orange (too little testing)'],
  ['QUALITY_DEFECT_RATE_GOOD_MAX', 'quality', 'number', 5, 0, 100, 'Defect/MM tốt đến', 'Defect/MM good up to', 'Tỷ lệ defect/MM trong khoảng tốt tô xanh', 'Defects per MM within the good range are green'],
  ['QUALITY_DEFECT_RATE_WARN_MAX', 'quality', 'number', 6, 0, 100, 'Defect/MM cảnh báo đến', 'Defect/MM warning up to', 'Trên mức tốt đến mức này tô vàng; cao hơn tô đỏ', 'Above the good range up to this is yellow; higher is red'],
  ['QUALITY_LEAKAGE_RATE_GOOD_MIN', 'quality', 'number', 0.45, 0, 10, 'Leakage/MM tốt từ', 'Leakage/MM good from', 'Tỷ lệ lỗi lọt/MM dưới mức này tô cam nhạt', 'Leaked defects per MM below this are light orange'],
  ['QUALITY_LEAKAGE_RATE_GOOD_MAX', 'quality', 'number', 0.5, 0, 10, 'Leakage/MM tốt đến (không gồm)', 'Leakage/MM good below', 'Từ mức "tốt từ" đến dưới mức này tô xanh', 'From the good-from level up to (not including) this is green'],
  ['QUALITY_LEAKAGE_RATE_WARN_MAX', 'quality', 'number', 0.55, 0, 10, 'Leakage/MM cảnh báo đến', 'Leakage/MM warning up to', 'Đến mức này tô vàng; cao hơn tô đỏ', 'Up to this is yellow; higher is red'],
  ['INSIGHT_FTE_GAP_MIN', 'insight', 'number', 0.5, 0, 10, 'Thiếu FTE so với hợp đồng từ', 'FTE shortfall from', 'Cảnh báo khi FTE đang phân bổ thấp hơn FTE hợp đồng của dự án từ mức này trở lên', 'Warn when the assigned FTE is below the contract FTE by at least this much'],
  ['INSIGHT_MILESTONE_UPCOMING_DAYS', 'insight', 'number', 7, 1, 90, 'Nhắc mốc sắp đến hạn trước (ngày)', 'Upcoming milestone reminder (days)', 'Nhắc các mốc chưa hoàn thành có ngày dự kiến trong số ngày tới', 'Remind unfinished milestones due within this many days'],
  ['INSIGHT_LEAVE_REMAINING_DAYS', 'insight', 'number', 8, 0, 60, 'Nhắc dùng phép khi còn từ (ngày)', 'Leave reminder from (days left)', 'Từ tháng nhắc phép, nhắc nhân sự còn số ngày phép năm từ mức này trở lên', 'From the reminder month, remind people with at least this many annual leave days left'],
  ['INSIGHT_LEAVE_REMINDER_MONTH', 'insight', 'number', 10, 1, 12, 'Tháng bắt đầu nhắc dùng phép', 'Leave reminder month', 'Từ tháng này trong năm bắt đầu nhắc nhân sự còn nhiều phép', 'Month of the year from which the leave reminder shows'],
  ['INSIGHT_EFFORT_OVER_MM', 'insight', 'number', 1, 0.5, 3, 'Công số cảnh báo vượt (MM)', 'Effort overload from (MM)', 'Cảnh báo nhân sự có công số thực tế + OT trong tháng vượt mức này', 'Warn when actual + overtime effort of a person in the month exceeds this'],
  ['INSIGHT_MAX_NAMES', 'insight', 'number', 3, 1, 10, 'Số tên liệt kê tối đa', 'Names listed at most', 'Số dự án / nhân sự được nêu tên trong một thông báo', 'How many projects / people a notice names'],
];
out('seed/system_rules.json', RULES.map(([key, module, value_type, value, min, max, name, name_en, description, description_en]) => ({
  id: uid(`rule:${key}`), tenant_id: TENANT_ID, key, module, value_type, value, default_value: value, min, max,
  name, name_en, description, description_en, status: 1, updated_at: '2026-10-01T08:00:00+07:00',
})));
// Cài đặt chung của tenant (màn admin_setting) — giá trị luôn là chuỗi, BE kiểm tra định dạng từng khoá.
out('seed/system_settings.json', [
  ['company_name', 'CodeOn Demo'], ['company_logo', ''], ['default_locale', 'vi'], ['time_zone', 'Asia/Ho_Chi_Minh'],
  ['currency', 'USD'], ['work_week', 'MON-FRI'], ['fiscal_year_start_month', '1'],
  ['work_start_time', '08:30'], ['work_end_time', '17:30'], ['lunch_break_minutes', '60'],
  ['notification_email', 'admin@example.com'], ['monthly_report_ja_enabled', 'false'],
  ['allow_app_check_in', 'true'], ['password_min_length', '8'], ['invitation_expiry_hours', '48'],
].map(([key, value]) => ({ tenant_id: TENANT_ID, key, value })));

const INSIGHT_CONFIGS = [
  ['project_list', 'Tổng quan rủi ro danh mục dự án', ['projects']],
  ['project_list', 'Dự án cần chú ý ngân sách', ['projects']],
  ['project_detail', 'Sức khoẻ dự án đang xem', ['projects', 'assignments']],
  ['project_detail_metrics_timeline', 'Milestone trễ hạn', ['milestones']],
  ['timesheet_week', 'Tổng quan tuần', ['timesheets']],
  ['timesheet_day', 'Gợi ý ghi công hôm nay', ['timesheets']],
  ['effort', 'Nhân sự rảnh / quá tải theo phòng ban', ['effort']],
  ['leave_request', 'Tình hình nghỉ phép & OT', ['leave_requests', 'ot_requests']],
  ['attendance', 'Đi muộn / thiếu giờ vào ra', ['attendance_records']],
  ['employee_list', 'Phân bổ nhân sự', ['employees', 'assignments']],
];
out('seed/ai_insights.json', INSIGHT_CONFIGS.map(([screen_key, name, data_sources], i) => ({
  id: uid(`insight:${i}`), tenant_id: TENANT_ID, screen_key, name, data_sources, allowed_roles: [], status: 'active',
  data_prompt: `Phân tích dữ liệu ${data_sources.join(', ')} và nêu tối đa 3 nhận định quan trọng nhất.`,
})));
out('seed/ai_prompt_templates.json', [
  { id: uid('prompt:chat'), tenant_id: TENANT_ID, scope: 'chat', status: 'active', name: 'Chat mặc định', template: 'Bạn là trợ lý AI của {{company_name}}. Trả lời bằng {{locale_name}}, chỉ dựa trên dữ liệu được cung cấp.' },
  { id: uid('prompt:insight'), tenant_id: TENANT_ID, scope: 'insight', status: 'active', name: 'Insight mặc định', template: 'Màn hình: {{screen_key}}. Dữ liệu: {{context}}. Trả JSON {"insights":[{"variant","title","content"}]}.' },
]);
out('seed/integrations.json', [
  ['Jira', false], ['Base', false], ['Bitrix', false],
].map(([key, enabled]) => ({ tenant_id: TENANT_ID, key, enabled, use_mock: true, last_sync_at: null })));

// ─── Nghỉ phép & OT (quản lý ngay trong phần mềm) ─────────────────────────
// Nhân viên tạo đơn → người có quyền timesheet.approve duyệt → đơn đã duyệt tự sinh dòng công
// (task LEAVE / OT, status approved, source = leave_request / ot_request) trong Nhật ký công việc.

const HOLIDAYS_2026 = [
  ['2026-01-01', 'Tết Dương lịch'], ['2026-04-30', 'Ngày Giải phóng miền Nam'], ['2026-05-01', 'Quốc tế Lao động'],
  ['2026-09-02', 'Quốc khánh'],
];
out('seed/holidays.json', HOLIDAYS_2026.map(([date, name]) => ({
  id: uid(`holiday:${date}`), tenant_id: TENANT_ID, holiday_date: date, name, is_paid: true,
})));
const isHoliday = (iso) => HOLIDAYS_2026.some(([d]) => d === iso);
const approverUser = users[1]; // pm@example.com có quyền timesheet.approve
const activeEmployees = employees.filter((e) => e.is_active && e.status !== 'MATERNITY_LEAVE');

// Quỹ phép năm 2026: 12 ngày + 1 ngày mỗi 5 năm thâm niên, chuyển tối đa 5 ngày từ 2025
const leaveBalances = employees.filter((e) => e.is_active).map((e) => {
  const years = Math.floor(diffDays(e.joined_date, '2026-01-01') / 365);
  return {
    id: uid(`leave_balance:${e.id}:2026`), tenant_id: TENANT_ID, employee_id: e.id, year: 2026,
    entitled_days: 12 + Math.max(0, Math.floor(years / 5)), carried_over_days: e.status === 'PROBATION' ? 0 : between(0, 5),
    adjusted_days: 0, note: null,
  };
});
out('seed/leave_balances.json', leaveBalances);

const leaveRequests = [];
const LEAVE_REASONS = {
  ANNUAL: ['Việc gia đình', 'Nghỉ ngơi cá nhân', 'Về quê'], SICK: ['Ốm, có giấy khám'], COMPENSATORY: ['Nghỉ bù làm thêm cuối tuần'],
  PERSONAL_PAID: ['Đám cưới người thân'], UNPAID: ['Giải quyết việc cá nhân'],
};
const TYPE_POOL = ['ANNUAL', 'ANNUAL', 'ANNUAL', 'ANNUAL', 'SICK', 'COMPENSATORY', 'PERSONAL_PAID', 'UNPAID'];
for (let i = 0; i < 28; i++) {
  const emp = activeEmployees[(i * 5) % 24];
  let from = addDays(TODAY, between(-45, 20));
  while (isWeekend(from) || isHoliday(from)) from = addDays(from, 1);
  const half = rand() > 0.75;
  const days = half ? 1 : pick([1, 1, 1, 2, 3]);
  let to = from;
  for (let k = 1; k < days; k++) { to = addDays(to, 1); while (isWeekend(to)) to = addDays(to, 1); }
  const typeCode = pick(TYPE_POOL);
  const past = to < TODAY;
  const status = past ? (rand() > 0.12 ? 'approved' : 'rejected') : pick(['pending', 'pending', 'approved', 'cancelled']);
  const created = `${addDays(from, -between(2, 10))}T09:${String(between(0, 59)).padStart(2, '0')}:00+07:00`;
  leaveRequests.push({
    id: uid(`leave:${i}`), tenant_id: TENANT_ID, code: `NP-${String(i + 1).padStart(4, '0')}`, employee_id: emp.id,
    leave_type_id: M.leave_type[typeCode], from_date: from, to_date: to,
    duration_type: half ? pick(['half_day_am', 'half_day_pm']) : 'full_day',
    total_hours: half ? 4 : days * 8, reason: pick(LEAVE_REASONS[typeCode]), attachment_url: null,
    status, approver_user_id: status === 'pending' || status === 'cancelled' ? null : approverUser.id,
    decided_at: status === 'approved' || status === 'rejected' ? `${addDays(created.slice(0, 10), 1)}T10:00:00+07:00` : null,
    decision_note: status === 'rejected' ? 'Trùng lịch bàn giao dự án, đề nghị đổi ngày' : null, created_at: created,
  });
}
// Tài khoản member demo: 1 đơn đã duyệt, 1 đơn chờ duyệt
const memberEmp = employees[5];
leaveRequests.push(
  {
    id: uid('leave:member:1'), tenant_id: TENANT_ID, code: 'NP-0029', employee_id: memberEmp.id, leave_type_id: M.leave_type.ANNUAL,
    from_date: '2026-10-09', to_date: '2026-10-09', duration_type: 'full_day', total_hours: 8, reason: 'Việc gia đình', attachment_url: null,
    status: 'approved', approver_user_id: approverUser.id, decided_at: '2026-10-06T10:00:00+07:00', decision_note: null, created_at: '2026-10-05T08:30:00+07:00',
  },
  {
    id: uid('leave:member:2'), tenant_id: TENANT_ID, code: 'NP-0030', employee_id: memberEmp.id, leave_type_id: M.leave_type.ANNUAL,
    from_date: '2026-10-29', to_date: '2026-10-30', duration_type: 'full_day', total_hours: 16, reason: 'Về quê', attachment_url: null,
    status: 'pending', approver_user_id: null, decided_at: null, decision_note: null, created_at: '2026-10-19T16:10:00+07:00',
  },
);
out('seed/leave_requests.json', leaveRequests);

const otRequests = [];
const liveRunning = projects.filter((p) => !p.is_deleted && p.status_id === M.project_status.RUNNING);
for (let i = 0; i < 24; i++) {
  const emp = activeEmployees[(i * 7) % 24];
  const date = addDays(TODAY, between(-40, 5));
  const dayKind = isHoliday(date) ? 'holiday' : isWeekend(date) ? 'weekend' : 'weekday';
  const hours = dayKind === 'weekday' ? between(1, 3) : between(4, 8);
  const startHour = dayKind === 'weekday' ? 18 : 9;
  const mine = assignments.find((a) => a.employee_id === emp.id && a.start_date <= date && a.end_date >= date);
  const status = date < TODAY ? (rand() > 0.1 ? 'approved' : 'rejected') : 'pending';
  const created = `${addDays(date, -1)}T15:00:00+07:00`;
  otRequests.push({
    id: uid(`ot:${i}`), tenant_id: TENANT_ID, code: `OT-${String(i + 1).padStart(4, '0')}`, employee_id: emp.id,
    project_id: mine?.project_id ?? liveRunning[i % liveRunning.length].id, work_date: date,
    start_time: `${String(startHour).padStart(2, '0')}:00`, end_time: `${String(startHour + hours).padStart(2, '0')}:00`, hours,
    day_kind: dayKind, rate_percent: dayKind === 'holiday' ? 300 : dayKind === 'weekend' ? 200 : 150,
    reason: pick(['Hỗ trợ release cuối sprint', 'Xử lý lỗi gấp từ khách hàng', 'Chuẩn bị demo nghiệm thu']),
    status, approver_user_id: status === 'pending' ? null : approverUser.id,
    decided_at: status === 'pending' ? null : `${date}T08:30:00+07:00`,
    decision_note: status === 'rejected' ? 'Không thuộc phạm vi hợp đồng, đề nghị sắp xếp lại kế hoạch' : null, created_at: created,
  });
}
out('seed/ot_requests.json', otRequests);

// Đơn đã duyệt → dòng công tự sinh (thay cho đồng bộ từ hệ thống ngoài của bản cũ)
for (const r of leaveRequests.filter((x) => x.status === 'approved')) {
  for (let d = r.from_date; d <= r.to_date; d = addDays(d, 1)) {
    if (isWeekend(d) || isHoliday(d)) continue;
    timesheets.push({
      id: uid(`ts:leave:${r.id}:${d}`), tenant_id: TENANT_ID, employee_id: r.employee_id, project_id: null,
      task_type_id: M.task_type.LEAVE, work_date: d, hours: r.duration_type === 'full_day' ? 8 : 4,
      description: r.reason, status: 'approved', source: 'leave_request', source_request_id: r.id,
    });
  }
}
for (const r of otRequests.filter((x) => x.status === 'approved')) {
  timesheets.push({
    id: uid(`ts:ot:${r.id}`), tenant_id: TENANT_ID, employee_id: r.employee_id, project_id: r.project_id,
    task_type_id: M.task_type.OT, work_date: r.work_date, hours: r.hours, description: r.reason,
    status: 'approved', source: 'ot_request', source_request_id: r.id,
  });
}
out('seed/timesheets.json', timesheets);

// ─── Chấm công vào/ra (quản lý ngay trong phần mềm) ───────────────────────
// Nguồn: 'app' (nút Vào/Ra trên phần mềm), 'import' (file Excel/CSV từ máy chấm công), 'manual' (quản lý sửa tay, bắt buộc ghi chú).

const onLeave = (empId, date) => leaveRequests.some((r) => r.employee_id === empId && r.status === 'approved'
  && r.duration_type === 'full_day' && r.from_date <= date && r.to_date >= date);
const attendance = [];
for (const e of activeEmployees.slice(0, 20)) {
  for (let day = 1; day <= 20; day++) {
    const date = `2026-10-${String(day).padStart(2, '0')}`;
    if (isWeekend(date) || isHoliday(date) || onLeave(e.id, date)) continue;
    const r = rand();
    const late = r > 0.88;
    // Giờ làm 08:30, cho phép muộn 15 phút → vào sau 08:45 là đi muộn
    const inMinutes = 8 * 60 + (late ? between(46, 100) : between(5, 44));
    const inTime = `${String(Math.floor(inMinutes / 60)).padStart(2, '0')}:${String(inMinutes % 60).padStart(2, '0')}`;
    const missingOut = date === TODAY || r < 0.03;
    const source = r < 0.06 ? 'manual' : r < 0.35 ? 'import' : 'app';
    attendance.push({
      id: uid(`attendance:${e.id}:${date}`), tenant_id: TENANT_ID, employee_id: e.id, work_date: date,
      check_in: inTime, check_out: missingOut ? null : `${between(17, 19)}:${String(between(30, 59)).padStart(2, '0')}`,
      source, note: source === 'manual' ? 'Quên chấm công, quản lý bổ sung theo xác nhận' : null,
      updated_by: source === 'manual' ? approverUser.id : null,
    });
  }
}
out('seed/attendance_records.json', attendance);

// ─── AI (giả lập kết quả LLM theo screen_key) ─────────────────────────────

const live = projects.filter((p) => !p.is_deleted);
const highRisk = live.filter((p) => p.risk_level_id === M.risk_level.HIGH && p.status_id !== M.project_status.COMPLETED);
const endingSoon = live.filter((p) => p.end_date >= TODAY && diffDays(TODAY, p.end_date) <= 30);
const planning = live.filter((p) => p.status_id === M.project_status.PLANNING);
const names = (list) => list.slice(0, 3).map((p) => p.name).join(', ');
const insight = (screen, vi, en) => out(`ai/insights/${screen}.json`, { screen_key: screen, generated_by: 'mock', insights: { vi, en } });

insight('project_list', [
  { variant: 'red', title: `${highRisk.length} dự án rủi ro cao`, content: `${names(highRisk)} đang ở mức rủi ro Cao, cần rà soát nhân sự và ngân sách trong tuần này.` },
  { variant: 'amber', title: `${endingSoon.length} dự án sắp kết thúc`, content: `${names(endingSoon)} kết thúc trong 30 ngày tới — chuẩn bị kế hoạch bàn giao và giải phóng nhân sự.` },
  { variant: 'purple', title: `${planning.length} dự án đang lập kế hoạch`, content: `Cần chốt nhân sự hợp đồng cho ${names(planning)} trước ngày bắt đầu.` },
], [
  { variant: 'red', title: `${highRisk.length} high-risk projects`, content: `${names(highRisk)} are at High risk; review staffing and budget this week.` },
  { variant: 'amber', title: `${endingSoon.length} projects ending soon`, content: `${names(endingSoon)} end within 30 days — prepare handover and release staff.` },
  { variant: 'purple', title: `${planning.length} projects in planning`, content: `Confirm contract staffing for ${names(planning)} before their start dates.` },
]);
insight('project_detail', [
  { variant: 'amber', title: 'Thiếu 1 vị trí Dev BE', content: 'Hợp đồng yêu cầu 3 Dev BE nhưng mới gán 2 người; FTE thực tế thấp hơn hợp đồng 1.0.' },
  { variant: 'blue', title: 'Burn rate ổn định', content: 'Chi phí 4 tuần gần nhất bám sát kế hoạch, chưa có dấu hiệu vượt ngân sách.' },
], [
  { variant: 'amber', title: 'One Backend developer slot open', content: 'Contract requires 3 backend developers but only 2 are assigned; actual FTE is 1.0 below contract.' },
  { variant: 'blue', title: 'Stable burn rate', content: 'Cost over the last 4 weeks tracks the plan; no sign of budget overrun.' },
]);
insight('project_detail_metrics_timeline', [
  { variant: 'red', title: '1 milestone quá hạn', content: '"Kiểm thử tích hợp" đã quá ngày kế hoạch 6 ngày và chưa có lý do.' },
], [
  { variant: 'red', title: '1 overdue milestone', content: '"Integration testing" is 6 days past its planned date with no justification.' },
]);
insight('timesheet_week', [
  { variant: 'teal', title: 'Tuần này: 38,5h / 40h', content: 'Bạn còn thiếu 1,5h; thứ Sáu chưa ghi công.' },
  { variant: 'purple', title: 'Phân bổ tuần', content: 'Dự án chiếm 82%, họp 12%, nghiên cứu 6% — tương đương tuần trước.' },
], [
  { variant: 'teal', title: 'This week: 38.5h / 40h', content: 'You are 1.5h short; Friday has no entries yet.' },
  { variant: 'purple', title: 'Weekly allocation', content: 'Projects 82%, meetings 12%, research 6% — similar to last week.' },
]);
insight('timesheet_day', [
  { variant: 'blue', title: 'Áp dụng lại công hôm qua?', content: 'Hôm qua bạn ghi 5h cho dự án chính và 3h cho dự án thứ hai, 1h họp.' },
], [
  { variant: 'blue', title: 'Reuse yesterday\'s entries?', content: 'Yesterday you logged 5h on your main project, 3h on the second project and 1h of meetings.' },
]);
insight('effort', [
  { variant: 'green', title: '5 nhân sự còn rảnh ở Phòng Phát triển 1', content: 'Tổng công số rảnh 2,5 MM — có thể bổ sung cho dự án đang thiếu Dev BE.' },
  { variant: 'red', title: '2 nhân sự vượt 100%', content: 'Hai nhân sự Phòng Phát triển 2 có công số thực tế cộng OT vượt 1,0 MM trong tháng 10.' },
], [
  { variant: 'green', title: '5 people available in Development 1', content: '2.5 MM of free effort — could cover projects short of backend developers.' },
  { variant: 'red', title: '2 people above 100%', content: 'Two Development 2 members exceed 1.0 MM including overtime in October.' },
]);
const pendingLeave = leaveRequests.filter((r) => r.status === 'pending').length;
const pendingOt = otRequests.filter((r) => r.status === 'pending').length;
const lateCount = attendance.filter((a) => a.check_in > '08:45').length;
const missingOutCount = attendance.filter((a) => !a.check_out && a.work_date < TODAY).length;
insight('leave_request', [
  { variant: 'amber', title: `${pendingLeave} đơn nghỉ & ${pendingOt} đơn OT chờ duyệt`, content: 'Duyệt trước ngày nghỉ để nhật ký công việc và báo cáo tháng cập nhật kịp.' },
  { variant: 'blue', title: 'Phép năm còn nhiều vào cuối năm', content: 'Một số nhân sự còn trên 8 ngày phép — nhắc lên kế hoạch nghỉ để tránh dồn vào tháng 12.' },
], [
  { variant: 'amber', title: `${pendingLeave} leave & ${pendingOt} overtime requests pending`, content: 'Approve before the leave date so timesheets and monthly reports stay up to date.' },
  { variant: 'blue', title: 'Large leave balances late in the year', content: 'Several people still have more than 8 days left — remind them to plan leave before December.' },
]);
insight('attendance', [
  { variant: 'red', title: `${lateCount} lượt đi muộn trong tháng`, content: 'Vào sau 08:45 (đã trừ 15 phút cho phép). Xem chi tiết theo nhân sự ở bảng bên trái.' },
  { variant: 'amber', title: `${missingOutCount} ngày thiếu giờ ra`, content: 'Nhắc nhân sự bổ sung giờ ra trong 7 ngày, quá hạn cần quản lý sửa tay kèm ghi chú.' },
], [
  { variant: 'red', title: `${lateCount} late arrivals this month`, content: 'Checked in after 08:45 (15-minute grace applied). See per-person details in the table.' },
  { variant: 'amber', title: `${missingOutCount} days missing check-out`, content: 'Remind people to add their check-out within 7 days; after that a manager must edit it with a note.' },
]);
insight('employee_list', [
  { variant: 'amber', title: '6 nhân sự quá tải', content: 'Phân bổ trên 100% FTE — cân nhắc chuyển bớt việc sang nhân sự đang chờ việc.' },
  { variant: 'green', title: '8 nhân sự chờ việc', content: 'Phân bổ dưới 20% — ưu tiên gán vào dự án sắp bắt đầu.' },
], [
  { variant: 'amber', title: '6 people overloaded', content: 'Allocated above 100% FTE — consider moving work to people on the bench.' },
  { variant: 'green', title: '8 people on the bench', content: 'Allocated below 20% — prioritise them for upcoming projects.' },
]);

out('ai/chat/scenarios.json', {
  default_reply: {
    vi: 'Đây là câu trả lời giả lập (chế độ mock). Khi bật AI thật (Integrations:Ai:UseMock = false), câu hỏi sẽ được gửi tới LLM kèm dữ liệu của màn hình hiện tại.',
    en: 'This is a mocked answer. With the real AI enabled (Integrations:Ai:UseMock = false), your question is sent to the LLM together with the current screen\'s data.',
  },
  greeting: { vi: 'Tôi là trợ lý AI. Tôi có thể giúp gì cho bạn?', en: 'I am your AI assistant. How can I help?' },
  scenarios: [
    {
      keywords: ['rủi ro', 'risk', 'chú ý', 'attention'],
      reply: {
        vi: `**${highRisk.length} dự án rủi ro cao:**\n${highRisk.map((p) => `- ${p.name}`).join('\n')}\n\nĐề xuất: rà soát nhân sự còn trống và chi phí 4 tuần gần nhất.`,
        en: `**${highRisk.length} high-risk projects:**\n${highRisk.map((p) => `- ${p.name}`).join('\n')}\n\nSuggestion: review open staffing slots and the last 4 weeks of cost.`,
      },
    },
    {
      keywords: ['chậm', 'tiến độ', 'late', 'delay', 'progress'],
      reply: {
        vi: `Các dự án sắp đến hạn: ${names(endingSoon)}. Kiểm tra tab Metrics › Timeline để xem milestone trễ.`,
        en: `Projects close to their deadline: ${names(endingSoon)}. Check Metrics › Timeline for late milestones.`,
      },
    },
    {
      keywords: ['nhân sự', 'thiếu', 'rảnh', 'staff', 'available', 'bench'],
      reply: {
        vi: 'Hiện có 8 nhân sự phân bổ dưới 20% và 6 nhân sự trên 100%. Xem chi tiết ở màn Nhân sự.',
        en: '8 people are allocated below 20% and 6 above 100%. See the Employees screen for details.',
      },
    },
    {
      keywords: ['ngân sách', 'budget', 'chi phí', 'cost'],
      reply: {
        vi: `Dự án dùng trên 75% ngân sách: ${names(live.filter((p) => p.actual_cost_fte > 0).slice(0, 3))}.`,
        en: `Projects above 75% of budget: ${names(live.filter((p) => p.actual_cost_fte > 0).slice(0, 3))}.`,
      },
    },
  ],
});
out('ai/translate/vi-ja.json', {
  'Phát triển tính năng theo sprint': 'スプリントに沿った機能開発',
  'Họp nội bộ': '社内会議',
  'Kiểm thử tích hợp': '結合テスト',
  'Sửa lỗi': 'バグ修正',
  'Nghiên cứu công nghệ': '技術調査',
});

// ─── Jira / Base / Bitrix24 (nguồn tích hợp ở Quản trị → Tích hợp) ────────
// Tên trường gần với API thật để sau này thay bằng dữ liệu thật ít phải sửa; địa chỉ đều là example.com.

const syncProjects = live.slice(0, 6);
const JIRA_STATUSES = ['To Do', 'In Progress', 'In Review', 'Done'];
const JIRA_SUMMARIES = ['Màn danh sách hiển thị sai số trang', 'Bổ sung xuất Excel báo cáo tháng', 'Tối ưu truy vấn dashboard', 'Lỗi đăng nhập khi hết phiên'];
out('jira/issues.json', syncProjects.flatMap((p, i) => [0, 1].map((k) => {
  const key = `${p.code.replace(/[^A-Z0-9]/gi, '').toUpperCase()}-${101 + k}`;
  return {
    key, summary: JIRA_SUMMARIES[(i + k) % JIRA_SUMMARIES.length], status: JIRA_STATUSES[(i + k * 2) % JIRA_STATUSES.length],
    issue_type: k === 0 ? 'Bug' : 'Story', assignee: employees[(i * 2 + k) % EMP_COUNT].email.split('@')[0],
    url: `https://jira.example.com/browse/${key}`, updated: `${addDays(TODAY, -i - k)}T09:00:00Z`,
  };
})));
const BASE_STATUSES = ['Đang làm', 'Chờ duyệt', 'Hoàn thành'];
out('base/tasks.json', syncProjects.map((p, i) => ({
  id: 5000 + i, name: `${p.code} - Chuẩn bị báo cáo tiến độ tuần`, project: p.name, status: BASE_STATUSES[i % BASE_STATUSES.length],
  url: `https://base.example.com/wework/task/${5000 + i}`, updated_at: `${addDays(TODAY, -i)}T08:30:00Z`,
})));
const BITRIX_STAGES = ['Mới', 'Đang đàm phán', 'Đã gửi báo giá', 'Thành công'];
out('bitrix/deals.json', syncProjects.slice(0, 4).map((p, i) => ({
  id: 300 + i, title: `Gia hạn hợp đồng bảo trì ${p.code}`, stage: BITRIX_STAGES[i % BITRIX_STAGES.length],
  url: `https://bitrix.example.com/crm/deal/details/${300 + i}/`, date_modify: `${addDays(TODAY, -2 * i)}T10:00:00Z`,
})));

// ─── Chi tiết dự án: quản trị dự án, báo cáo tháng, khảo sát CSS đang chờ ──
// PRNG riêng để thêm dữ liệu mới không làm đổi dữ liệu đã sinh ở trên.
const rand2 = mulberry32(20261021);
const pick2 = (arr) => arr[Math.floor(rand2() * arr.length)];
const between2 = (min, max) => min + Math.floor(rand2() * (max - min + 1));
const mgmtProjects = projects.filter((p) => !p.is_deleted && p.status_id !== M.project_status.PLANNING).slice(0, 8);
// Công số các tháng trước (06–09/2026) chép từ tháng 10 cho dự án đã chạy từ tháng đó, dao động nhẹ —
// để EE, Defect / Leakage (MM tính tiền) có số liệu nhiều tháng.
const effortHistory = [];
for (const month of ['2026-06-01', '2026-07-01', '2026-08-01', '2026-09-01']) {
  for (const row of effort) {
    const p = projects.find((x) => x.id === row.project_id);
    if (!p || !p.start_date || p.start_date > addDays(month, 27)) continue;
    const { _key, ...rest } = row;
    const tt = Math.max(0, Math.round((row.tt_effort + (rand2() - 0.5) * 0.2) * 100) / 100);
    effortHistory.push({ ...rest, id: uid(`effort:${_key}:${month}`), year_month: month, tt_effort: tt });
  }
}
out('seed/effort_allocations.json', [...effort.map(({ _key, ...rest }) => rest), ...effortHistory]);

const pmOf = (p) => projectMembers.find((m) => m.project_id === p.id && m.role === 'PM')?.employee_id ?? projectMembers.find((m) => m.project_id === p.id)?.employee_id ?? null;
const membersOf = (p) => [...new Set(projectMembers.filter((m) => m.project_id === p.id).map((m) => m.employee_id))];
const audit = (p, days) => ({ created_at: `${addDays(TODAY, -days)}T09:00:00+07:00`, updated_at: `${addDays(TODAY, -Math.max(0, days - 3))}T09:00:00+07:00`, created_by: users[1].id, updated_by: users[1].id, is_deleted: false });

// Khảo sát CSS tháng 10 đã gửi link nhưng khách chưa trả lời (để thử trang /khao-sat-css/:token)
for (const p of pcvProjects.slice(0, 3)) {
  cssSurveys.push({
    id: uid(`css_survey:${p.id}:2026-10`), tenant_id: TENANT_ID, project_id: p.id, survey_month: '2026-10-01',
    token: createHash('sha256').update(`css:${p.id}:2026-10`).digest('hex'), status: 'sent', sent_to: `contact@${p.code.toLowerCase()}.example.com`,
    sent_at: `${addDays(TODAY, -2)}T10:00:00+07:00`, submitted_at: null, respondent: null, comment: null, scores: [],
  });
}
out('seed/project_css_surveys.json', cssSurveys);

const DOC_GROUPS = [['Tài liệu yêu cầu', 'Đặc tả, user story, biên bản họp với khách hàng'], ['Thiết kế', 'Kiến trúc, thiết kế màn hình, thiết kế CSDL'], ['Kiểm thử', 'Kế hoạch test, test case, báo cáo test']];
const DOCS = {
  'Tài liệu yêu cầu': ['Đặc tả yêu cầu phần mềm', 'Danh sách user story', 'Biên bản họp kick-off'],
  'Thiết kế': ['Tài liệu kiến trúc hệ thống', 'Thiết kế màn hình (Figma)'],
  'Kiểm thử': ['Kế hoạch kiểm thử', 'Bộ test case hồi quy'],
};
const docGroups = [];
const documents = [];
const risks = [];
const changeRequests = [];
const issueLinks = [];
const RISK_SEEDS = [
  ['Khách hàng thay đổi yêu cầu muộn', 'customer', 4, 3, 'mitigate', 'Chốt phạm vi theo từng sprint, CR phải qua phê duyệt'],
  ['Thiếu nhân sự kiểm thử giai đoạn cuối', 'resource', 3, 4, 'mitigate', 'Đặt trước 1 tester từ team QA từ tháng sau'],
  ['Thư viện thanh toán bên thứ ba ngừng hỗ trợ', 'technical', 2, 5, 'transfer', 'Đàm phán hỗ trợ dài hạn với nhà cung cấp'],
  ['Trễ mốc bàn giao UAT', 'schedule', 3, 3, 'avoid', 'Tách phạm vi UAT thành 2 đợt'],
  ['Chi phí hạ tầng cloud vượt dự toán', 'cost', 2, 3, 'accept', 'Theo dõi chi phí hằng tuần'],
  ['Tỷ lệ lỗi lọt sau bàn giao tăng', 'quality', 3, 4, 'mitigate', 'Bổ sung review code bắt buộc và test hồi quy tự động'],
];
mgmtProjects.forEach((p, pi) => {
  DOC_GROUPS.forEach(([name, description], gi) => {
    const groupId = uid(`doc_group:${p.id}:${gi}`);
    docGroups.push({ id: groupId, tenant_id: TENANT_ID, project_id: p.id, name, description, sort_seq: gi + 1, ...audit(p, 60 - gi) });
    DOCS[name].forEach((docName, di) => {
      documents.push({ id: uid(`doc:${p.id}:${gi}:${di}`), tenant_id: TENANT_ID, project_id: p.id, group_id: groupId, name: `${p.code} – ${docName}`, url: `https://drive.example.com/${p.code.toLowerCase()}/${gi}-${di}`, ...audit(p, 50 - di) });
    });
  });
  const members = membersOf(p);
  RISK_SEEDS.slice(0, 3 + (pi % 4)).forEach(([name, category, probability, impact, strategy, plan], ri) => {
    const status = ri === 0 && pi % 3 === 0 ? 'resolved' : pick2(['open', 'open', 'in_progress']);
    risks.push({
      id: uid(`risk:${p.id}:${ri}`), tenant_id: TENANT_ID, project_id: p.id, code: `R-${String(ri + 1).padStart(2, '0')}`, name, description: null, category,
      probability, impact, strategy, response_plan: plan, owner_id: members[ri % Math.max(1, members.length)] ?? pmOf(p),
      due_date: addDays(TODAY, between2(-10, 40)), status, identified_at: addDays(TODAY, -between2(15, 90)), ...audit(p, 30 - ri),
    });
  });
  for (let ci = 0; ci < 1 + (pi % 3); ci++) {
    changeRequests.push({
      id: uid(`cr:${p.id}:${ci}`), tenant_id: TENANT_ID, project_id: p.id, code: `CR-${String(ci + 1).padStart(2, '0')}`,
      file_name: [`Bổ sung xuất báo cáo Excel`, `Thay đổi luồng phê duyệt`, `Thêm ngôn ngữ tiếng Anh`][ci], url: `https://drive.example.com/${p.code.toLowerCase()}/cr-${ci + 1}`,
      source: ci === 1 ? 'internal' : 'customer', impact_level: pick2(['low', 'medium', 'high']), impact_areas: ci === 0 ? ['scope', 'schedule'] : ci === 1 ? ['quality'] : ['scope', 'cost'],
      impact_note: ci === 0 ? 'Cần thêm khoảng 0,5 MM, lùi mốc UAT 1 tuần' : null, ...audit(p, 20 - ci),
    });
  }
  [['parent', 'task', 'Epic: Quản lý đơn hàng'], ['sprint', 'task', 'Sprint 12'], ['milestone', 'bug', 'Bug UAT đợt 1']].forEach(([type, kind, name], ii) => {
    issueLinks.push({ id: uid(`issue:${p.id}:${ii}`), tenant_id: TENANT_ID, project_id: p.id, type, kind, name, url: `https://jira.example.com/browse/${p.code.replace('-', '')}-${100 + ii}`, ...audit(p, 25 - ii) });
  });
});
out('seed/project_document_groups.json', docGroups);
out('seed/project_documents.json', documents);
out('seed/project_risks.json', risks);
out('seed/project_change_requests.json', changeRequests);
out('seed/project_issue_links.json', issueLinks);

// Báo cáo tháng (作業報告書 兼 納品書 — mẫu báo cáo cho khách, cấu hình được) tháng 9, 10 cho 3 dự án đang chạy.
// Mỗi báo cáo: thông tin chung + danh sách cột (cột hệ thống lấy dữ liệu tự động + cột bổ sung do PO thêm) + ô theo nhân sự × ngày.
// Giờ làm lấy tự động từ Nhật ký công việc đã duyệt, giờ bắt đầu/kết thúc từ Chấm công vào/ra (BE tính khi đọc),
// trừ ô PO đã sửa tay (source = 'manual'). Seed chỉ lưu nội dung công việc + cột bổ sung.
const monthlyReports = [];
const monthlyReportCells = [];
const WORK = ['Phát triển tính năng theo sprint', 'Sửa lỗi', 'Kiểm thử tích hợp', 'Họp nội bộ', 'Nghiên cứu công nghệ'];
const MR_COLUMNS = [
  { id: 'date', label: 'Ngày', label_ja: '日付', type: 'text', source: 'date' },
  { id: 'start', label: 'Bắt đầu', label_ja: '開始', type: 'text', source: 'checkIn' },
  { id: 'end', label: 'Kết thúc', label_ja: '終了', type: 'text', source: 'checkOut' },
  { id: 'hours', label: 'Số giờ', label_ja: '作業時間', type: 'number', source: 'hours' },
  { id: 'content', label: 'Nội dung công việc', label_ja: '作業内容', type: 'text', source: 'content' },
  { id: 'ticket', label: 'Ticket', label_ja: 'チケット', type: 'text' },
];
for (const p of pcvProjects.slice(0, 3)) {
  const client = clients.find((c) => c.id === p.client_id);
  for (const month of ['2026-09', '2026-10']) {
    const reportId = uid(`monthly_report:${p.id}:${month}`);
    const memberIds = membersOf(p).slice(0, 4);
    monthlyReports.push({
      id: reportId, tenant_id: TENANT_ID, project_id: p.id, report_month: `${month}-01`,
      client_name: client?.name ?? '', company_name: 'CodeOn Demo', order_no: `PO-${p.code}-${month.replace('-', '')}`,
      work_name: p.name, responsible: users[1].full_name, language: 'vi',
      columns: MR_COLUMNS, member_ids: memberIds, removed_employee_ids: [],
      updated_at: `${month}-28T17:00:00+07:00`, updated_by: users[1].id,
    });
    const days = [];
    for (let d = 1; d <= 31; d++) {
      const iso = `${month}-${String(d).padStart(2, '0')}`;
      if (new Date(`${iso}T00:00:00Z`).getUTCMonth() + 1 !== Number(month.slice(5)) || iso > TODAY) break;
      if (!isWeekend(iso)) days.push(iso);
    }
    for (const empId of memberIds) {
      for (const iso of days) {
        if (rand2() < 0.15) continue;
        monthlyReportCells.push({
          report_id: reportId, employee_id: empId, work_date: iso, hours: null, source: 'timesheet', start_time: null, end_time: null, time_source: 'attendance',
          content: pick2(WORK), custom: { ticket: `${p.code.replace('-', '')}-${between2(100, 180)}` },
        });
      }
    }
  }
}
out('seed/monthly_reports.json', monthlyReports);
out('seed/monthly_report_cells.json', monthlyReportCells);

// ─── Ghi file ─────────────────────────────────────────────────────────────

for (const dir of ['seed', 'ai', 'jira', 'base', 'bitrix', 'github', 'gitlab', 'google-drive']) {
  const p = join(ROOT, dir);
  if (existsSync(p)) rmSync(p, { recursive: true, force: true });
}
for (const [path, data] of files) {
  const full = join(ROOT, path);
  mkdirSync(dirname(full), { recursive: true });
  writeFileSync(full, `${JSON.stringify(data, null, 2)}\n`, 'utf8');
}
mkdirSync(join(ROOT, 'email/outbox'), { recursive: true });
writeFileSync(join(ROOT, 'email/outbox/.gitkeep'), '');

console.log(`mock-data: ${files.size} file JSON → ${ROOT}`);
if (!process.env.DX_DEMO_PASSWORD) {
  console.warn('Đang dùng mật khẩu demo mặc định. Trước khi mở ra Internet: đặt DX_DEMO_PASSWORD rồi chạy lại.');
}
