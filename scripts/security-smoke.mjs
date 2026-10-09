import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createHmac, pbkdf2Sync, randomBytes } from 'node:crypto';

const username = `smoke_${randomBytes(4).toString('hex')}`;
const password = 'SmokeSecurity@123456';
const salt = randomBytes(16);
const hash = pbkdf2Sync(password, salt, 600000, 32, 'sha256');
const sql = query => execFileSync('sqlcmd', [
  '-S', '(localdb)\\KTXDemo', '-E', '-d', 'KTX_Demo', '-b', '-Q', query
], { stdio: 'pipe' });

function totp(base32) {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let bits = 0, value = 0;
  const bytes = [];
  for (const character of base32) {
    value = (value << 5) | alphabet.indexOf(character);
    bits += 5;
    if (bits >= 8) { bits -= 8; bytes.push((value >>> bits) & 255); }
  }
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 30000)));
  const digest = createHmac('sha1', Buffer.from(bytes)).update(counter).digest();
  const offset = digest.at(-1) & 15;
  return (((digest.readUInt32BE(offset) & 0x7fffffff) % 1_000_000)).toString().padStart(6, '0');
}

let cookie = '';
async function api(path, method = 'GET', body) {
  const response = await fetch(`http://127.0.0.1:5100${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', Origin: 'http://localhost:4201', ...(cookie ? { Cookie: cookie } : {}) },
    body: body ? JSON.stringify(body) : undefined
  });
  const setCookies = response.headers.getSetCookie();
  for (const header of setCookies) {
    const pair = header.split(';')[0];
    if (pair.startsWith('KtxDemo.Auth=') || pair.startsWith('KtxDemo.PreAuth=')) {
      const name = pair.split('=')[0];
      cookie = cookie.split('; ').filter(item => item && !item.startsWith(`${name}=`)).join('; ');
      if (!header.includes('expires=Thu, 01 Jan 1970') && !pair.endsWith('='))
        cookie = [cookie, pair].filter(Boolean).join('; ');
    }
  }
  const text = await response.text();
  return { status: response.status, data: text ? JSON.parse(text) : null };
}

sql(`INSERT dbo.UserAccounts (username, full_name, role_name, password_salt, password_hash, password_iterations)
VALUES (N'${username}', N'Security Smoke Test', 'STAFF', 0x${salt.toString('hex')}, 0x${hash.toString('hex')}, 600000)`);
try {
  let result = await api('/api/auth/login', 'POST', { username, password });
  assert.equal(result.status, 200);
  assert.equal(result.data.requiresTwoFactor, false);
  result = await api('/api/settings', 'PUT', { language: 'en', theme: 'dark' });
  assert.equal(result.status, 200);
  result = await api('/api/settings');
  assert.equal(result.data.language, 'en');
  assert.equal(result.data.theme, 'dark');

  result = await api('/api/auth/2fa/setup', 'POST', { password });
  assert.equal(result.status, 200);
  const secret = result.data.secret;
  assert.match(result.data.provisioningUri, /^otpauth:\/\/totp\//);
  result = await api('/api/auth/2fa/confirm', 'POST', { code: totp(secret) });
  assert.equal(result.status, 200);
  assert.equal(result.data.recoveryCodes.length, 8);
  const originalRecovery = result.data.recoveryCodes;
  result = await api('/api/auth/2fa/status');
  assert.equal(result.data.enabled, true);

  await api('/api/auth/logout', 'POST', {});
  result = await api('/api/auth/login', 'POST', { username, password });
  assert.equal(result.data.requiresTwoFactor, true);
  result = await api('/api/auth/me');
  assert.equal(result.status, 401);
  result = await api('/api/auth/2fa/verify', 'POST', { code: originalRecovery[0] });
  assert.equal(result.status, 200);
  assert.equal(result.data.user.username, username);
  result = await api('/api/auth/2fa/recovery/regenerate', 'POST', { password, code: originalRecovery[1] });
  assert.equal(result.status, 200);
  assert.equal(result.data.recoveryCodes.length, 8);
  const newRecovery = result.data.recoveryCodes;
  result = await api('/api/auth/2fa/disable', 'POST', { password, code: newRecovery[0] });
  assert.equal(result.status, 204);
  result = await api('/api/auth/login', 'POST', { username, password });
  assert.equal(result.data.requiresTwoFactor, false);
  const crossOrigin = await fetch('http://127.0.0.1:5100/api/auth/login', {
    method: 'POST', headers: { 'Content-Type': 'application/json', Origin: 'https://untrusted.example' },
    body: JSON.stringify({ username, password })
  });
  assert.equal(crossOrigin.status, 403);
  for (let attempt = 0; attempt < 5; attempt++) {
    result = await api('/api/auth/login', 'POST', { username, password: 'incorrect' });
    assert.equal(result.status, 401);
  }
  result = await api('/api/auth/login', 'POST', { username, password });
  assert.equal(result.status, 429);
  console.log('Security smoke passed: settings, enrollment, pre-auth gate, recovery, disable, CSRF, lockout.');
} finally {
  sql(`DELETE dbo.UserAccounts WHERE username = N'${username}'`);
}
