// Xác thực giả lập theo đúng hợp đồng của BE (D2): access token ngắn hạn trong body,
// refresh token xoay vòng trong cookie httpOnly. Kiểm tra mật khẩu theo định dạng hash ASP.NET Core Identity V3.
import { createHmac, pbkdf2Sync, randomBytes, timingSafeEqual } from 'node:crypto';
import { ApiError, parseCookies, unauthorized } from './http.mjs';

const ACCESS_TTL_SECONDS = 15 * 60;
const REFRESH_TTL_SECONDS = 7 * 24 * 3600;
export const REFRESH_COOKIE = 'dx_rt';

const accessTokens = new Map(); // token -> { userId, exp }
const refreshTokens = new Map(); // token -> { userId, exp }
const failedLogins = new Map(); // ip -> { count, until }

export function verifyIdentityV3(password, hashBase64) {
  try {
    const buf = Buffer.from(hashBase64, 'base64');
    if (buf[0] !== 0x01) return false;
    const prf = buf.readUInt32BE(1);
    const iterations = buf.readUInt32BE(5);
    const saltLength = buf.readUInt32BE(9);
    const salt = buf.subarray(13, 13 + saltLength);
    const expected = buf.subarray(13 + saltLength);
    const digest = prf === 2 ? 'sha512' : prf === 1 ? 'sha256' : 'sha1';
    const actual = pbkdf2Sync(String(password), salt, iterations, expected.length, digest);
    return actual.length === expected.length && timingSafeEqual(actual, expected);
  } catch {
    return false;
  }
}

/** Tạo hash Identity V3 (PBKDF2-HMACSHA512, 100.000 vòng, salt 16 byte) — dùng khi đặt / đổi mật khẩu. */
export function hashIdentityV3(password) {
  const salt = randomBytes(16);
  const subkey = pbkdf2Sync(String(password), salt, 100_000, 32, 'sha512');
  const header = Buffer.alloc(13);
  header.writeUInt8(0x01, 0);
  header.writeUInt32BE(2, 1);
  header.writeUInt32BE(100_000, 5);
  header.writeUInt32BE(salt.length, 9);
  return Buffer.concat([header, salt, subkey]).toString('base64');
}

/** 5 lần sai trong 5 phút trên cùng IP → khoá 5 phút (429). */
export function checkLoginRate(ip) {
  const entry = failedLogins.get(ip);
  if (entry && entry.until > Date.now() && entry.count >= 5) throw new ApiError(429, 'error.too_many_requests');
}
export function recordLoginFailure(ip) {
  const entry = failedLogins.get(ip);
  const fresh = !entry || entry.until < Date.now();
  failedLogins.set(ip, { count: fresh ? 1 : entry.count + 1, until: Date.now() + 5 * 60 * 1000 });
}
export function clearLoginFailures(ip) {
  failedLogins.delete(ip);
}

export function issueTokens(userId) {
  const accessToken = randomBytes(32).toString('base64url');
  const refreshToken = randomBytes(48).toString('base64url');
  accessTokens.set(accessToken, { userId, exp: Date.now() + ACCESS_TTL_SECONDS * 1000 });
  refreshTokens.set(refreshToken, { userId, exp: Date.now() + REFRESH_TTL_SECONDS * 1000 });
  return { userId, accessToken, refreshToken, expiresIn: ACCESS_TTL_SECONDS };
}

export function refreshCookie(token, secure) {
  const attrs = [`${REFRESH_COOKIE}=${token}`, 'Path=/api/v1/auth', 'HttpOnly', 'SameSite=Strict', `Max-Age=${token ? REFRESH_TTL_SECONDS : 0}`];
  if (secure) attrs.push('Secure');
  return attrs.join('; ');
}

/** Xoay vòng: refresh token cũ bị huỷ ngay khi dùng. */
export function rotateRefresh(req) {
  const token = parseCookies(req)[REFRESH_COOKIE];
  const entry = token ? refreshTokens.get(token) : null;
  if (!entry || entry.exp < Date.now()) throw unauthorized('auth_refresh.err_session_expired');
  refreshTokens.delete(token);
  return issueTokens(entry.userId);
}

export function revoke(req) {
  const token = parseCookies(req)[REFRESH_COOKIE];
  if (token) refreshTokens.delete(token);
  const bearer = bearerOf(req);
  if (bearer) accessTokens.delete(bearer);
}

function bearerOf(req) {
  const header = String(req.headers.authorization || '');
  return header.startsWith('Bearer ') ? header.slice(7) : null;
}

/** Trả userId hoặc ném 401. Nhận cả access token của mock lẫn JWT do BE .NET cấp (chế độ hybrid của frontend). */
export function requireUserId(req) {
  const token = bearerOf(req);
  const entry = token ? accessTokens.get(token) : null;
  if (entry && entry.exp >= Date.now()) return entry.userId;
  const backendUserId = token ? verifyBackendJwt(token) : null;
  if (backendUserId) return backendUserId;
  throw unauthorized();
}

// ─── JWT của BE .NET (frontend chạy DX_API_MODE=hybrid) ─────────────────────
// Đăng nhập / refresh đi BE thật, các màn chưa có endpoint BE vẫn gọi mock với cùng access token.
// Mock kiểm chữ ký HS256 bằng đúng khoá BE (Jwt:SigningKey), cùng issuer / audience / hạn — không tin token chưa ký.
// sub = users.id; mock-data/seed và DB demo (tools/db-seed) dùng chung id nên tra được người dùng trong mock.

let backendJwt = null; // { key: Buffer, issuer, audience } | null

/** Gọi một lần lúc khởi động. Trả mô tả để in log. */
export function configureBackendJwt({ signingKey, issuer, audience }) {
  backendJwt = signingKey ? { key: Buffer.from(signingKey, 'utf8'), issuer, audience } : null;
  return backendJwt !== null;
}

function verifyBackendJwt(token) {
  if (!backendJwt) return null;
  const parts = token.split('.');
  if (parts.length !== 3) return null;
  try {
    const header = JSON.parse(Buffer.from(parts[0], 'base64url').toString('utf8'));
    if (header.alg !== 'HS256') return null;
    const expected = createHmac('sha256', backendJwt.key).update(`${parts[0]}.${parts[1]}`).digest();
    const actual = Buffer.from(parts[2], 'base64url');
    if (actual.length !== expected.length || !timingSafeEqual(actual, expected)) return null;
    const payload = JSON.parse(Buffer.from(parts[1], 'base64url').toString('utf8'));
    const now = Date.now() / 1000;
    if (typeof payload.exp !== 'number' || payload.exp < now) return null;
    if (typeof payload.nbf === 'number' && payload.nbf > now + 30) return null;
    if (backendJwt.issuer && payload.iss !== backendJwt.issuer) return null;
    if (backendJwt.audience && payload.aud !== backendJwt.audience && !(Array.isArray(payload.aud) && payload.aud.includes(backendJwt.audience))) return null;
    return typeof payload.sub === 'string' ? payload.sub : null;
  } catch {
    return null;
  }
}
