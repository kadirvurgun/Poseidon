# UAT-DEPLOY-01 — PO test ortamı (tek VPS, sentetik veri) ve geri bildirim döngüsü

Durum: **AUTHORED, kısmen yerelde doğrulandı; ilk gerçek deploy yapılmadı.** Canonical sayım değişmez (114/121 iş, 932/970 SP). Bu paket ürün işi değil, operasyon altyapısıdır; pilot/UAT kabul kapılarını (DEV-076…079, 095, 096, 121) kapatmaz.

Dal: `feature/uat-deploy-20261127` (taban `main` ea52ed3f).

## Teslim edilen
- Kaynak: kullanıcı isteği 27.11.2026 (PO'ya test ettirme, gizli repo, issue ile geri bildirim, kapanan issue'larda ortam güncellemesi); kısıt: ücretli GitHub planı yok, Organization yok. Bağlam: [ortam sözleşmesi](../operations/environment-contract.md) (on-prem/self-hosted, aynı imajın terfisi).
- İmajlar: [backend.Dockerfile](../../infra/uat/docker/backend.Dockerfile) (api, migrator; `--locked-mode`), [web.Dockerfile](../../infra/uat/docker/web.Dockerfile) (SPA + Caddy), beyaz liste [.dockerignore](../../.dockerignore).
- Yığın: [compose.uat.yaml](../../infra/uat/compose.uat.yaml): Caddy (TLS, HSTS, `/auth/admin` kapalı), API `Staging` (host portu yok, forwarded headers yalnız Caddy'den), Keycloak 26.8.1 `/auth`, PostgreSQL 18.6 (iç ağ), tek seferlik migrator.
- İşletim: [bootstrap-vps.sh](../../infra/uat/bootstrap-vps.sh), [manage.py](../../infra/uat/manage.py) (sır üretimi, realm, üyelik/menü render, kullanıcı ekleme), [deploy.sh](../../infra/uat/deploy.sh) (yedek → migration → ilk seferde sentetik seed → sağlık → başarısızlıkta önceki imaj), [backup.sh](../../infra/uat/backup.sh).
- CD: [uat-deploy.yml](../../.github/workflows/uat-deploy.yml): yalnız `main` push'unda "Application foundation" başarılıysa aynı SHA'yı build eder (GHCR), SSH ile deploy eder; [notify_feedback.py](../../infra/uat/notify_feedback.py) `UAT-Fixes: #N` satırlarındaki issue'lara deploy sonrası yorum yazar ve kapatır (rollback/ilk deploy'da duyuru yok).
- Geri bildirim reposu şablonları: [feedback-repo/](../../infra/uat/feedback-repo/) (TR hata/öneri formları, kişisel veri uyarısı).
- Rehber: [uat-deployment.md](../operations/uat-deployment.md).

## Güvenlik/kural notları
- Sırlar yalnız VPS'te üretilir (600), env referansıyla verilir; literal `ClientSecret`/`PartitionKey` yok (test ediliyor).
- Varsayılan profiller 65 karar/onay/doğrulama/atama yetkisini **hiç vermez**; `*Authorities` atamaları üretilmez. Ekleme operatörün bilinçli, sentetik-ortam kararıdır.
- Seed, migrator'ın `Development` kapısıyla ve yalnız repodaki sentetik dosyayla, ilk deploy'da bir kez çalışır (belgelenmiş UAT istisnası); doğrulanmış kanıt/imza/kimlik bilgisi içermez.

## Doğrulama (gerçek sonuçlar)
| Kontrol | Sonuç |
|---|---|
| `py -m unittest scripts/test_uat_deploy.py` | 10/10 PASS (Windows, yerel) |
| `bash -n` (4 kabuk betiği) | PASS |
| `docker compose config --quiet` (sahte sırlarla) | PASS |
| İmaj build (api/migrator/web), Caddyfile doğrulama, gerçek VPS deploy, OIDC girişi | **NOT RUN** (yerelde Docker motoru çalışmıyor; VPS henüz yok) |
| Bağımsız QA, PR CI | PENDING |

## Bilinen sınırlar / açık
Dosya özellikleri (S3+ClamAV yok), içe aktarma commit'i (Development dışında 503), `CrewProfilePolicy` anahtarı, Tug gemisinin 2. kiracıda olması, davet e-postası. Gizli repo için self-hosted runner geçişi ayrı PR.

## Sonraki adım
VPS + DNS hazır olunca: rehber §1 → ilk `workflow_dispatch` deploy → giriş/sağlık doğrulaması → PR + bağımsız QA.
