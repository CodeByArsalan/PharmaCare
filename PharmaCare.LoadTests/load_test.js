import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// ── CUSTOM METRICS ────────────────────────────────────────
const loginOk      = new Rate('login_success');
const pageOk       = new Rate('page_success');
const loginTime    = new Trend('login_ms', true);
const saleListTime = new Trend('sale_list_ms', true);
const purchaseTime = new Trend('purchase_list_ms', true);
const errors       = new Counter('errors');

// ── OPTIONS ───────────────────────────────────────────────
export const options = {
  scenarios: {
    smoke: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 10  },  // Warm-up
        { duration: '1m',  target: 25  },  // Ramp up
        { duration: '1m',  target: 50  },  // Mid load
        { duration: '1m',  target: 100 },  // Peak load
        { duration: '2m',  target: 100 },  // Hold peak
        { duration: '30s', target: 0   },  // Ramp down
      ],
      gracefulRampDown: '30s',
    },
  },
  thresholds: {
    http_req_duration:  ['p(95)<3000', 'p(99)<6000'],
    http_req_failed:    ['rate<0.05'],
    login_success:      ['rate>0.95'],
    page_success:       ['rate>0.90'],
  },
  insecureSkipTLSVerify: true,
};

// Credentials come from the environment — never commit real logins:
//   k6 run -e K6_EMAIL=you@example.com -e K6_PASSWORD=... load_test.js
const BASE     = __ENV.K6_BASE_URL || 'https://localhost:44302';
const EMAIL    = __ENV.K6_EMAIL    || '';
const PASSWORD = __ENV.K6_PASSWORD || '';

if (!EMAIL || !PASSWORD) {
  throw new Error('Set K6_EMAIL and K6_PASSWORD environment variables (k6 run -e K6_EMAIL=... -e K6_PASSWORD=...).');
}

// ── CSRF EXTRACTION ───────────────────────────────────────
function getCsrf(html) {
  const m = html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
  return m ? m[1] : '';
}

// ── PER-VU SESSION ────────────────────────────────────────
let jar = null;

export function setup() {
  // Verify the app is reachable before starting
  const res = http.get(`${BASE}/Account/Login`, { timeout: '10s' });
  if (res.status !== 200) {
    throw new Error(`App not reachable at ${BASE} — start the app first! Status: ${res.status}`);
  }
  console.log('✅ App is reachable. Starting load test...');
}

export default function () {
  // ── STEP 1: LOGIN ────────────────────────────────────────
  group('1. Login', function () {
    const loginPage = http.get(`${BASE}/Account/Login`, {
      tags: { name: 'GET /Account/Login' },
    });
    check(loginPage, { 'login page loaded': r => r.status === 200 });
    if (loginPage.status !== 200) { errors.add(1); return; }

    const csrf = getCsrf(loginPage.body);
    const t0 = Date.now();

    const loginRes = http.post(`${BASE}/Account/Login`, {
      Email:    EMAIL,
      Password: PASSWORD,
      __RequestVerificationToken: csrf,
    }, {
      redirects: 5,
      tags: { name: 'POST /Account/Login' },
    });

    loginTime.add(Date.now() - t0);
    const ok = loginRes.status === 200 && !loginRes.url.includes('/Login?');
    loginOk.add(ok);
    if (!ok) {
      console.error(`VU${__VU}: Login failed → status=${loginRes.status} url=${loginRes.url}`);
      errors.add(1);
      return;
    }
  });

  sleep(0.5);

  // ── STEP 2: SALES INDEX ──────────────────────────────────
  group('2. Sales Index', function () {
    const t0  = Date.now();
    const res = http.get(`${BASE}/Sale/SalesIndex`, {
      tags: { name: 'GET /Sale/SalesIndex' },
    });
    saleListTime.add(Date.now() - t0);
    const ok = res.status === 200;
    pageOk.add(ok);
    check(res, { 'sales index 200': r => r.status === 200 });
    if (!ok) errors.add(1);
  });

  sleep(0.5);

  // ── STEP 3: ADD SALE FORM ─────────────────────────────────
  group('3. Sale Form Load', function () {
    const res = http.get(`${BASE}/Sale/AddSale`, {
      tags: { name: 'GET /Sale/AddSale' },
    });
    check(res, { 'add sale form 200': r => r.status === 200 });
    if (res.status !== 200) errors.add(1);
  });

  sleep(0.5);

  // ── STEP 4: PURCHASE INDEX ───────────────────────────────
  group('4. Purchase Index', function () {
    const t0  = Date.now();
    const res = http.get(`${BASE}/Purchase/PurchasesIndex`, {
      tags: { name: 'GET /Purchase/PurchasesIndex' },
    });
    purchaseTime.add(Date.now() - t0);
    const ok = res.status === 200;
    pageOk.add(ok);
    check(res, { 'purchase index 200': r => r.status === 200 });
    if (!ok) errors.add(1);
  });

  sleep(0.5);

  // ── STEP 5: DASHBOARD ────────────────────────────────────
  group('5. Dashboard', function () {
    const res = http.get(`${BASE}/`, {
      tags: { name: 'GET /' },
    });
    check(res, { 'dashboard 200': r => r.status === 200 });
    if (res.status !== 200) errors.add(1);
  });

  sleep(0.5);

  // ── STEP 6: REPORTS ──────────────────────────────────────
  group('6. Reports', function () {
    const today     = new Date().toISOString().split('T')[0];
    const monthAgo  = new Date(Date.now() - 30 * 864e5).toISOString().split('T')[0];

    const pages = [
      `/Sale/SalesIndex`,
      `/Purchase/PurchasesIndex`,
      `/Expense/ExpensesIndex`,
      `/CustomerPayment/ReceiptsIndex`,
    ];

    for (const page of pages) {
      const res = http.get(`${BASE}${page}`, {
        tags: { name: `GET ${page}` },
      });
      pageOk.add(res.status === 200);
      check(res, { [`${page} 200`]: r => r.status === 200 });
      if (res.status !== 200) errors.add(1);
      sleep(0.3);
    }
  });

  sleep(__ENV.THINK_TIME ? parseInt(__ENV.THINK_TIME) : 1);
}

// ── SUMMARY ──────────────────────────────────────────────
export function handleSummary(data) {
  const m = data.metrics;
  const summary = {
    test_run: new Date().toISOString(),
    total_requests:  m.http_reqs?.values?.count || 0,
    failed_requests: m.http_req_failed?.values?.passes || 0,
    error_count:     m.errors?.values?.count || 0,
    login_success_rate: (m.login_success?.values?.rate * 100 || 0).toFixed(1) + '%',
    page_success_rate:  (m.page_success?.values?.rate * 100 || 0).toFixed(1) + '%',
    p95_ms:  m.http_req_duration?.values['p(95)']?.toFixed(0) || 'N/A',
    p99_ms:  m.http_req_duration?.values['p(99)']?.toFixed(0) || 'N/A',
    avg_ms:  m.http_req_duration?.values['avg']?.toFixed(0) || 'N/A',
    login_avg_ms:     m.login_ms?.values?.avg?.toFixed(0) || 'N/A',
    sale_list_avg_ms: m.sale_list_ms?.values?.avg?.toFixed(0) || 'N/A',
  };

  console.log('\n========== LOAD TEST RESULTS ==========');
  for (const [k, v] of Object.entries(summary))
    console.log(`  ${k.padEnd(25)}: ${v}`);
  console.log('=======================================');

  return {
    stdout: JSON.stringify(summary, null, 2),
  };
}
