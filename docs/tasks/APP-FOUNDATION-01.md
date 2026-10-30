# APP-FOUNDATION-01 — Çalışan uygulama ve CI alt paketi

Owner: backend/UI; bağımsız QA ayrı atanır. Branch: feature/application-foundation; UI worktree: .runtime/ui-foundation. Durum: implementing.

Kaynak: v46.3 18.01 Development Backlog, Backlog row 2 (DEV-001 / T-S0-001), row 3 (DEV-002 / T-S0-002); 13.04 Design System paragraphs 10, 19–25 ve layout standardı. Bu paket DEV-001/002 hazırlık kapsamıdır; bağımlılık/kabul kapıları tamamlanmadan canonical işi accepted yapmaz.

Kabul:
- .NET API gerçek host üzerinde public liveness ve yalnız teknik durum döndürür; iş verisi/credential/DB connection string sunmaz. Kalıcı veriye erişmez ve migration çalıştırmaz.
- React/TypeScript başlangıç ekranı gerçek /api/v1/system/status yanıtını okur; yükleniyor, bağlantı yok, tekrar dene ve teknik bağlantı durumlarını erişilebilir biçimde gösterir. Bağlantı durumu operasyonel readiness veya yetki onayı olarak gösterilmez.
- OpenAPI/contract, backend host integration, frontend component ve gerçek backend Playwright smoke kontrolleri vardır.
- Push/PR CI backend unit/PostgreSQL integration, frontend lint/typecheck/build/component, Playwright ve artifact üretimini zorunlu çalıştırır.
- Yerel çalıştırma, kesin sonuçlar ve açık IAM/audit/scheduler sınırları belgelenir; bağımsız QA yapılır.

Sınırlar: OIDC, tenant API, login/rol ekranları, business mutation, HTTP kullanıcı audit ve production deployment bu alt pakette kabul edilmez. Mevcut seed/migration güvenliği korunur. Public teknik heartbeat kullanıcı eylemi audit tamamlanması değildir.

Contract: GET /api/v1/system/status -> 200 application/json {"service":"poseidon","status":"available"}; Cache-Control no-store. GET /health/live -> 200. Yanlış payload UI'da bağlantı hatasıdır. Backend local http://127.0.0.1:5080, UI 5173; Vite /api proxy. CORS wildcard yok.

Kontroller: dotnet restore/build/test (unit + Integration ayrı); npm ci/lint/typecheck/test/build; npx playwright test (gerçek API + preview); Python import testleri; migration model check; graphify update . --no-cluster.
