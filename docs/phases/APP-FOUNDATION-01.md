# APP-FOUNDATION-01 — Çalışan API/React ve tarayıcı kapısı

Tarih: 30 Ekim 2026. Durum: **yerel doğrulandı; bağımsız final QA ve hedef commit remote CI bekliyor**. Mevcut canonical aşama S0; sprint çıkış kapısı geçilmedi.

## Teslim edilen davranış

- .NET 10 gerçek HTTP host: public process liveness ve sabit teknik bağlantı yanıtı. Business/tenant verisi yok; DB bağlantısı veya automatic migration/seed yok. Production OpenAPI sunmaz; cache kapalı ve CORS açılmadı.
- React 19 / TypeScript responsive başlangıç ekranı: TR/EN, loading, bağlantı hatası, tekrar dene, iptal ve timeout. Teknik bağlantı operasyonel readiness veya güvenlik onayı olarak gösterilmez.
- OpenAPI snapshot gerçek host ile karşılaştırılır; frontend tipleri snapshot'tan üretilir ve CI drift'i reddeder.
- Push/PR: backend unit/PostgreSQL/API integration + migration/import, frontend lint/typecheck/component/build, gerçek API Playwright; API/UI build artifact ve tarayıcı hata kanıtı yükleme adımları.
- Repository klasörleri ve main/develop politikası yazıldı. GitHub API ile `main` koruması doğrulandı: strict güncel-base kontrolü, iki zorunlu check (`persistence-and-security`, `frontend-and-browser`), PR zorunlu, adminler dahil, force push/silme kapalı. Bağımsız semantik QA ayrıca kaydedilir; GitHub review sayısı 0'dır. Develop başlangıcı bootstrap merge sonrasına planlandı.

## İzlenebilirlik ve kapsam

[Görev/kabul sözleşmesi](../tasks/APP-FOUNDATION-01.md): DEV-001 / T-S0-001 (18.01 Backlog row 2), DEV-002 / T-S0-002 (row 3); 13.04 UX/layout standardı. Bu bir uygulama altyapısı alt paketidir; canonical rol/vessel shell, OIDC veya bir iş ekranı kabulü değildir. Remote CI ve final QA tamamlanmadan DEV-001/002 accepted yapılmadı.

## Kesin yerel doğrulama

| Kontrol | Sonuç |
|---|---|
| Release build | 0 warning / 0 error |
| Backend unit/guard | 12 passed / 0 failed / 0 skipped |
| PostgreSQL integration | 10 passed / 0 failed / 0 skipped; gerçek Docker PostgreSQL 18.6 |
| HTTP host + OpenAPI integration | 5 passed / 0 failed / 0 skipped; Development ve Production |
| Frontend component | 12 passed / 0 failed / 0 skipped |
| Lint / typecheck / Vite build | Passed; Node 26.13.0 |
| Playwright Chromium desktop + mobil viewport | 4 passed / 0 failed / 0 skipped; gerçek API + production build preview |
| Kaynak import testleri | 4 passed |
| EF migration model tutarlılığı | Passed; pending change yok |
| npm audit (frontend/browser install) | 0 vulnerability |

İlk 4 paralel tarayıcı işçisi bu Windows hostta zaman aşımına uğradı; tek işçiyle bütün senaryolar geçti. Assertions ve timeout sınırları azaltılmadı/artırılmadı; CI da aynı kaynak bütçesiyle tek işçi kullanır. Browser hata senaryosunda yalnız ilk ağ hatası enjekte edilir; retry gerçek API'ye döner. Bu testler Safari/native mobil veya Ship/Tug iş akışı kanıtı değildir.

OpenAPI generator'ın `typescript ^5.x`, lint aracının `<6.1` peer sınırları nedeniyle frontend TypeScript 5.9.3 kullanır; latest 7.0.2 uyumlu değildir. `--force`/peer bypass kullanılmadı. [Sürüm kararı](../dependency-policy.md).

## Açık sınırlar ve sonraki adım

OIDC sağlayıcısı, tenant/role/vessel HTTP policies, login/deny/read kullanıcı audit'i, production deployment, tam S0 ADR/ortam/traceability kapıları açık. Mevcut EF write audit GDPR uyumu olarak sunulmaz. İki hesaplı worker ayrı draft PR #2'de; sürekli 7/24 scheduler aktif değildir.

Sıradaki adım: final bağımsız QA + hedef commit CI; ardından source-backed S0/Identity kararları ve ilk yetkili tenant/vessel dikey dilimi. Kaynak bağımlılıkları kapanmadan uygulama ekranları accepted sayılmaz. Başlatma ve kontrol komutları [geliştirme rehberinde](../development.md).
