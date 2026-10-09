import express from 'express';
import helmet from 'helmet';
import { rateLimit } from 'express-rate-limit';
import { readFile } from 'node:fs/promises';
import { request as httpRequest } from 'node:http';
import { fileURLToPath } from 'node:url';

const app = express();
app.disable('x-powered-by');
app.use(helmet());

const port = Number(process.env.GATEWAY_PORT || 5100);
const coreUrl = process.env.CORE_API_URL || 'http://127.0.0.1:5101';
const allowedOrigins = new Set((process.env.ALLOWED_ORIGINS ||
  'http://localhost:4200,http://localhost:4201,http://127.0.0.1:4200,http://127.0.0.1:4201')
  .split(',').map(value => value.trim()));

app.use('/api', (req, res, next) => {
  if (['GET', 'HEAD', 'OPTIONS'].includes(req.method)) return next();
  const origin = req.get('origin');
  const fetchSite = req.get('sec-fetch-site');
  if ((origin && !allowedOrigins.has(origin)) ||
      (!origin && fetchSite && fetchSite !== 'same-origin' && fetchSite !== 'none')) {
    return res.status(403).json({ message: 'Nguồn yêu cầu không được phép.' });
  }
  next();
});

const authLimiter = rateLimit({
  windowMs: 5 * 60 * 1000,
  limit: 30,
  standardHeaders: 'draft-8',
  legacyHeaders: false,
  message: { message: 'Quá nhiều lần thử. Vui lòng chờ vài phút.' }
});
app.post(['/api/auth/login', '/api/auth/2fa/verify'], authLimiter);

const localeFiles = {
  vi: fileURLToPath(new URL('./locales/vi.json', import.meta.url)),
  en: fileURLToPath(new URL('./locales/en.json', import.meta.url)),
  zh: fileURLToPath(new URL('./locales/zh.json', import.meta.url))
};
app.get('/api/i18n/:language', async (req, res) => {
  const file = localeFiles[req.params.language];
  if (!file) return res.status(404).json({ message: 'Ngôn ngữ không được hỗ trợ.' });
  try {
    res.set('Cache-Control', 'public, max-age=3600');
    res.type('json').send(await readFile(file, 'utf8'));
  } catch {
    res.status(503).json({ message: 'Không tải được bản dịch.' });
  }
});

app.get('/api/system/health', (_req, res) => res.json({ service: 'ktx-gateway', status: 'ok' }));
app.use('/api', (req, res) => {
  const target = new URL(req.originalUrl, coreUrl);
  const upstream = httpRequest(target, {
    method: req.method,
    headers: { ...req.headers, host: target.host },
    timeout: 30000
  }, response => {
    res.status(response.statusCode || 502);
    for (const [name, value] of Object.entries(response.headers)) {
      if (value !== undefined) res.setHeader(name, value);
    }
    response.pipe(res);
  });
  upstream.on('timeout', () => upstream.destroy(new Error('API timeout')));
  upstream.on('error', () => {
    if (!res.headersSent) res.status(502).json({ message: 'Dịch vụ dữ liệu tạm thời không sẵn sàng.' });
    else res.end();
  });
  req.pipe(upstream);
});

app.listen(port, '127.0.0.1', () => console.log(`Express gateway listening on http://127.0.0.1:${port}`));
