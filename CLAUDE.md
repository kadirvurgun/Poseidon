# POSEIDON — Claude Code çalışma kılavuzu

@AGENTS.md

AGENTS.md bağlayıcı kurallardır (yetki, CI hata protokolü, güvenlik, seed, kaynak sınırları). Bu dosya Claude'a özgü kısa yol haritası ve Codex döneminden devralınan pratiklerdir.

## Ürün
Gemi ve römorkör (ship/tug) operasyon, uyum ve emniyet platformu. Çok kiracılı (tenant/vessel/job kapsamı sunucuda zorunlu), TR/EN + 17 dil i18n, offline PWA.
- Backend: .NET 10 (`backend/Poseidon.Api|Domain|Persistence|DbMigrator`), EF Core + PostgreSQL, Identity/OIDC.
- Frontend: React + TypeScript + Vite (`frontend/`), shadcn token sistemi (`frontend/src/design-system.css`, `frontend/src/theme/`), i18n `frontend/src/i18n/messages/*.json`.
- Testler: `tests/Poseidon.Domain.Tests`, `Poseidon.Persistence.Tests`, `Poseidon.Api.Tests` (Integration = gerçek PG/Docker), `tests/Poseidon.Browser.Tests` (Playwright), `scripts/test_*.py`.
- Komutlar ve yerel araçlar: `docs/development.md` (repo içi SDK: `$env:DOTNET_ROOT="$PWD/.tools/dotnet"`). Python için `py` kullan (`python` PATH'te yok).

## Gereksinim kaynakları
- Aktif set: `docs/sources/active.json` → `analiz/POSEIDON GUNCEL 30.10.2026` + en son `son analizler 15.11.26` (42 belge).
- 15.11 dalga planı: `docs/tasks/analysis-20261115-01.md` (+ `.plan.json`). PLAN-1511-* kimlikleri türetilmiş takip kimliğidir, canonical SP değildir.
- Kaynak arama: `py scripts/query_sources.py "tam ifade" --limit 4`, sonra atıf yapılan locator'ı oku. `chunks.jsonl`'u bütün olarak okuma.
- Kullanıcı eki: shadcn preset `b3Zheoix4U` (https://ui.shadcn.com/create) ve uygulama içi değiştirilebilir tema → `docs/tasks/theme-foundation-01.md`.

## Durum takibi (her paket sonunda güncelle)
- `docs/claude-handoff.md` — zamanlanmış 5 saatlik oturumların devamlılık dosyası (aktif dal, son iş, sıradaki iş, `STATUS:`).
- `docs/project-status.md` / `.json` ve `docs/phases/<ID>.md` — canonical ilerleme. Kabul = sadece gerçek test + CI + bağımsız QA geçmiş iş. Yazılmış ama doğrulanmamış iş `AUTHORED_UNVERIFIED`, ertelenmiş kontrol `DEFERRED`; asla PASS denmez.
- Canonical sayım: 121 iş / 970 SP / 196 ekran; S0–S7 kabul edildi (67 iş, 530 SP, %54,64; S7 kapısı 20.11.2026 `docs/phases/S7-GATE-20261120.md`; S6 DEV-101/102 hariç).

## Rol rehberleri (eski Codex skill'leri, `.agents/skills/`)
- **backend**: sahip modülde uygula; tenant/vessel/job kapsamı sunucuda; Completed/Verified/Effective ayrı; mutable aggregate'ta concurrency; offline komutta idempotent receipt; her operasyonel varlıkla EF migration + idempotent sentetik seed + domain unit + gerçek PG integration testi.
- **ui**: ekran sözleşmesi + 13.04 design system + prototip; UNKNOWN/doğrulanmamış asla yeşil "hazır" görünmez; loading/empty/error/stale/offline/conflict durumları; component testleri; receipt sonrası kaynak kimliğini URL'de tut.
- **qa**: kabul vakalarını gereksinimden türet; red/izin dışı geçişler, tenant sınırı, UNKNOWN→REVIEW_REQUIRED, insan karar yetkisi; commit ID + komut + exit code + başarısız vakalar raporla.
- **coordinate**: her işe kararlı ID, kaynak locator, kabul vakaları, bağımlılık, sahip rol, dal. Yalnız bağımlılığı hazır işleri seç.

## Git / teslim
- Normal merge commit'leri; force push yok. Main'e doğrudan push edilmez — PR üzerinden.
- `gh` CLI: `C:\Users\avurgun\AppData\Local\Microsoft\WinGet\Links\gh.exe` (18.11.2026 kuruldu; `gh auth login` kullanıcı tarafından yapılmalı). Kimlik yoksa okuma için `Invoke-RestMethod https://api.github.com/repos/kadirvurgun/Poseidon/...` (repo public).
- Codex döneminden kalan çok sayıda `.runtime/worktrees/*` ve `agents/*` dalı var; bunlar tarihsel. Aktif dal `docs/claude-handoff.md`'de yazar.
- Codex'in pratiği olan aşırı ayrıntılı kanıt kaydı (her ölçümü dosyaya yazma, yüzlerce verify dalı) tekrarlanmaz: kısa, doğru, ölçülebilir rapor yeterli.

## Test kadansı
01.11.2026 kullanıcı kararı: küçük paketlerde tam suite koşturma; büyük paket kapısında bir kez tam build/typecheck/test/CI. Hedefli (tek sınıf/trait) testler ve CI hata teşhisi serbest. Kullanıcı 18.11.2026'da "kod yazımları ve testler bitene kadar devam" dedi: paket bittiğinde ilgili hedefli testleri gerçekten çalıştır ve sonucu yaz.
