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
| Gerçek host (AWS EC2 m7i-flex.large, eu-north-1, Ubuntu 26.04, Docker 29.1.3/Compose 2.40.3): bootstrap | PASS (UFW 22/80/443, parola SSH kapalı, swap 4G, deploy kullanıcısı) |
| İmaj build (api/migrator/web) host üzerinde, `274701c` | PASS (`uat-20261127-274701c`) |
| Deploy: migration + ilk sentetik seed + demo taslakları + sağlık | PASS (deploy.sh exit 0) |
| Genel HTTPS (Let's Encrypt, `poseidon.13-49-27-245.sslip.io`) | `/health/live` 200, `/app` 200, OIDC discovery issuer eşleşiyor, `/api/v1/auth/login` → Keycloak 302 (PKCE/PAR) |
| Keycloak yönetim konsolu / master realm dışarıdan | 404 / 404 |
| Yedek (`backup.sh`) + gece cron | PASS (poseidon + keycloak dump) |
| Keycloak DB init betiği (yeniden yazım) postgres:18.6 geçici konteyner | PASS (rol kendi DB'sine giriş yaptı) |
| Tarayıcıda insan girişi ve ekran gezintisi | PENDING (kullanıcı) |
| CI repo secret taraması | `b43d30f`: 3 yanlış pozitif (değişken adları) → `7a3d15c7` ile düzeltildi; CI tekrar PENDING |
| Bağımsız QA | PENDING |

İlk gerçek kurulumda bulunup düzeltilenler: Docker resmi deposu Ubuntu 26.04'ü desteklemiyor (Ubuntu paketleri), sağlık sondası `AllowedHosts` nedeniyle 400, cron kurulumunun `set -e` ile düşmesi, Caddy'de üst düzey `respond`'un `handle`'dan sonra sıralanması nedeniyle yönetim konsolunun açık kalması.

## Bilinen sınırlar / açık
Dosya özellikleri (S3+ClamAV yok), içe aktarma commit'i (Development dışında 503), `CrewProfilePolicy` anahtarı, Tug gemisinin 2. kiracıda olması, davet e-postası. Gizli repo için self-hosted runner geçişi ayrı PR.

## Sonraki adım
VPS + DNS hazır olunca: rehber §1 → ilk `workflow_dispatch` deploy → giriş/sağlık doğrulaması → PR + bağımsız QA.
