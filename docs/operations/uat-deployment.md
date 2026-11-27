# UAT ortamı — kurulum ve işletim (UAT-DEPLOY-01)

Tek VPS üzerinde, **yalnız sentetik veriyle** çalışan PO test ortamı. Bu ortam pilot, PROD veya [ortam sözleşmesindeki](environment-contract.md) yetkili UAT ataması değildir. Burada yapılan testler pilot onayı, emniyet/mevzuat onayı veya yayın kararı yerine geçmez.

```
PO tarayıcı ──HTTPS──► VPS: Caddy (TLS, SPA) ─► API (.NET, Staging) ─► PostgreSQL 18
                                   └─► /auth ─► Keycloak (OIDC)  ─┘
main'e merge → "Application foundation" yeşil → UAT deploy: imaj build (GHCR) → SSH → deploy.sh
                                                       └→ "UAT-Fixes: #N" issue'larına yorum + kapatma
```

Dosyalar: [infra/uat/](../../infra/uat/) · Workflow: [uat-deploy.yml](../../.github/workflows/uat-deploy.yml)

## Bilinen sınırlar (ilk sürüm)

| Konu | Durum |
|---|---|
| Dosya yükleme/indirme (belge, fotoğraf, sertifika PDF) | Kapalı: API `Development` dışında HTTPS + versioning'li S3 ve ClamAV ister; bu ortamda yok → `file_storage_unavailable`. Sonraki paket: MinIO + clamd. |
| Excel içe aktarma onayı (commit) | API `Development/Test` dışında 503 döner (kod kuralı). |
| Mürettebat kişisel alanları (kimlik/iletişim/sağlık) | `CrewProfilePolicy` anahtarı tanımlanmadı → 503. Sonraki paket. |
| Römorkör (Tug) | Sentetik seed'de Tug gemisi 2. kiracıda; UAT kullanıcıları 1. kiracıda. Tug ekranları demo verisiyle sınırlı. |
| Karar/onay/doğrulama yetkileri | Varsayılan profillerde **yok** (65 yetki: verify/approve/decide/sign/attest/...). Bkz. "Yetki profilleri". |
| Davet e-postası | Kapalı; yönetici davet bağlantısını elle iletir. |
| Keycloak yönetim konsolu | İnternete kapalı (`/auth/admin` → 404). Kullanıcılar `manage.py` ile yönetilir. |

## 1. Tek seferlik kurulum

### 1.1 Sunucu ve alan adı
- VPS: Ubuntu 24.04, en az 4 vCPU / 16 GB (UAT + Linux CI runner) veya yalnız UAT için 2 vCPU / 8 GB.
- DNS: `uat.<alanadiniz>` için **A kaydı** → VPS IP. Tek kayıt yeterli (Keycloak aynı adreste `/auth` altında).

### 1.2 Deploy anahtarı (yalnız GitHub Actions için)
Kendi bilgisayarınızda:
```powershell
ssh-keygen -t ed25519 -N '""' -C poseidon-uat-deploy -f $HOME\.ssh\poseidon_uat_deploy
```

### 1.3 Sunucuyu hazırla (root ile, bir kez)
```powershell
scp infra/uat/bootstrap-vps.sh infra/uat/manage.py root@VPS_IP:/root/
ssh root@VPS_IP "bash /root/bootstrap-vps.sh '$(Get-Content $HOME\.ssh\poseidon_uat_deploy.pub)'"
```
Betik: güncellemeler, otomatik güvenlik yaması, 4 GB swap, UFW (22/80/443), parola ile SSH kapalı, fail2ban, Docker, `deploy` kullanıcısı, `/opt/poseidon-uat`.

### 1.4 Ortam sırlarını üret (bir kez)
```powershell
ssh root@VPS_IP "install -o deploy -g deploy -m 750 /root/manage.py /opt/poseidon-uat/manage.py && su - deploy -c 'cd /opt/poseidon-uat && python3 manage.py init --domain uat.ALANADI'"
```
Rastgele veritabanı, OIDC istemci, rate-limit ve Keycloak bootstrap sırları yalnız sunucuda (`/opt/poseidon-uat/secrets`, 600) oluşur; hiçbiri ekrana yazılmaz, repoya girmez. `init` ikinci kez çalışmaz (sırlar döndürülmez).

### 1.5 GitHub ayarları
Repo → Settings → Environments → **New environment: `uat`**
- Secrets:
  - `UAT_SSH_KEY`: `poseidon_uat_deploy` dosyasının (uzantısız, özel anahtar) içeriği
  - `UAT_SSH_KNOWN_HOSTS`: `ssh-keyscan -t ed25519 VPS_IP` çıktısı
  - `UAT_FEEDBACK_TOKEN`: geri bildirim reposu için fine-grained PAT (Issues: Read & write; isteğe bağlı kod reposunda Pull requests: Read)
- Variables:
  - `UAT_HOST`: VPS IP veya alan adı
  - `UAT_FEEDBACK_REPO`: `kadirvurgun/Poseidon-UAT`
  - `UAT_BUILD_RUNNER` (isteğe bağlı): imaj build'i self-hosted runner'da yapmak için ör. `self-hosted`

### 1.6 İlk deploy ve kullanıcılar
Actions → **UAT deploy** → Run workflow (ref: `main`). İlk deploy migration + sentetik seed + KURGUSAL demo taslaklarını yükler (karar, doğrulama, imza veya kimlik bilgisi içermez).

Sonra sunucuda:
```bash
su - deploy -c 'cd /opt/poseidon-uat && python3 manage.py add-user kadir --profile admin'
su - deploy -c 'cd /opt/poseidon-uat && python3 manage.py add-user po.ad --profile po --first-name Ad --last-name Soyad'
```
Her komut **bir kez** geçici parola yazar; ilk girişte değiştirilmesi zorunludur. Parolayı PO'ya e-posta dışı güvenli bir kanaldan iletin.

### 1.7 Geri bildirim reposu
```powershell
gh repo create kadirvurgun/Poseidon-UAT --private --description "POSEIDON UAT geri bildirim"
# infra/uat/feedback-repo/ içeriğini (README.md + .github/ISSUE_TEMPLATE) bu repoya kopyalayıp push edin
gh label create hata --repo kadirvurgun/Poseidon-UAT --color d73a4a
gh label create oneri --repo kadirvurgun/Poseidon-UAT --color 0e8a16
gh label create uat --repo kadirvurgun/Poseidon-UAT --color 1d76db
gh api -X PUT repos/kadirvurgun/Poseidon-UAT/collaborators/PO_GITHUB_KULLANICI -f permission=push
```
PO yalnız bu repoya eklenir; kod reposuna erişimi olmaz. README'deki `UAT_ALAN_ADI` yerine gerçek adresi yazın.

## 2. Günlük akış

1. PO, `Poseidon-UAT` reposunda "Hata bildirimi" / "Öneri" açar.
2. Düzeltme PR'ının açıklamasına veya commit mesajına satır eklenir: `UAT-Fixes: #12` (birden çok: `UAT-Fixes: #12, #15`). `Fixes`/`Closes` **kullanılmaz**: GitHub issue'yu merge anında, ortam güncellenmeden kapatır.
3. PR → `main` merge → "Application foundation" yeşil → **UAT deploy** otomatik çalışır.
4. Deploy sağlıklı biterse bot ilgili issue'lara "UAT ortamında yayında: sürüm `uat-YYYYMMDD-abc1234`" yazar ve kapatır. PO tekrar dener; sorun sürerse Reopen.
5. Sürüm: `https://uat.ALANADI/version.txt`.

## 3. İşletim

| İş | Komut (sunucuda, `deploy` kullanıcısıyla, `/opt/poseidon-uat`) |
|---|---|
| Durum | `POSEIDON_TAG=$(cat deployed-tag) docker compose -f compose.uat.yaml ps` |
| Loglar | `POSEIDON_TAG=$(cat deployed-tag) docker compose -f compose.uat.yaml logs --tail 200 api` |
| Kullanıcılar | `python3 manage.py list-users` / `add-user` |
| Elle yedek | `bash backup.sh manual` (gece 02:30 UTC otomatik; deploy öncesi `pre-<tag>`; 14 gün) |
| Deploy geçmişi | `cat deploy-history.log` |

**Geri dönüş:** Actions → UAT deploy → Run workflow → `ref` = önceki iyi commit SHA. Migration'lar ileri yönlüdür; şema uyumsuzsa önce yedekten geri yükleyin:
```bash
T=$(cat deployed-tag); dc() { POSEIDON_TAG=$T docker compose -f compose.uat.yaml "$@"; }
dc stop api web
dc exec -T postgres pg_restore -U poseidon -d poseidon --clean --if-exists < backups/poseidon-<zaman>-pre-<tag>.dump
dc up -d
```
Sağlık kontrolü başarısız olursa `deploy.sh` önceki imajları otomatik yeniden başlatır (veritabanı yeni şemada kalır) ve workflow kırmızı biter.

## 4. Yetki profilleri

`state/profiles.json` ilk render'da kaynak koddaki kapalı yetki listesinden üretilir:
- `admin`: kullanıcı yönetimi dahil sıradan yetkiler (`membership.manage`).
- `po`: admin'den kullanıcı/kayıt saklama yönetimi çıkarılmış hâli; kiracı genelinde okuma/yazma.
- `viewer`: yalnız okuma.
- `excludedAuthorityPermissions`: verify/approve/decide/sign/attest/close/release/owner/override vb. **hiçbir profile varsayılan olarak verilmez** ve hiçbir atama (`*Authorities`) üretilmez. Bir akışın uçtan uca denenmesi için birine eklemek, bu SENTETİK ortam için bilinçli bir operatör kararıdır; gerçek bir imza/onay değildir. Düzenleme sonrası `python3 manage.py render` ve API yeniden başlatma gerekir.

## 5. CI dakikaları ve gizli repo

Ücretsiz planda gizli repolar ayda 2.000 Actions dakikası alır (Windows ×2). Repoyu gizliye çekmeden önce:
1. VPS'e Linux self-hosted runner kurun (Repo → Settings → Actions → Runners → New self-hosted runner; `deploy`'dan ayrı bir kullanıcıyla, `--labels poseidon-linux`). Aynı anda tek iş çalışır.
2. `backend-foundation.yml` Linux işlerinin `runs-on` değerini runner etiketine taşıyın (ayrı PR) ve `UAT_BUILD_RUNNER` değişkenini ayarlayın.
3. Windows işlerinin süresini ölçüp kotaya sığıp sığmadığına karar verin.
Self-hosted runner gizli repoda yalnız güvenilir katkıcıların kodunu çalıştırır; fork PR'larından gelen iş çalıştırmayın.
