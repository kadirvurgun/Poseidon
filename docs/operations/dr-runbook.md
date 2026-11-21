# POSEIDON pilot backup, restore and rollback runbook (DEV-071)

Durum: **DRAFT — AUTHORED_UNVERIFIED.** Yazılım tarafı (betikler + birim testleri) teslim edildi; gerçek prova, ölçülmüş RPO/RTO ve imzalı rollback prosedürü **yoktur**. Bu belge bir DR kabulü, SLA veya sertifika iddiası değildir.

İzlenebilirlik: 18.01 `sheet:Backlog/row:72` DEV-071 / T-S11-003 ("DB + object evidence backup; restore test; RPO/RTO measured; rollback procedure signed"); 23.04 Backup, Restore & DR Runbook `paragraph:5-79`; 23.03 Deployment Runbook pre-deploy checklist `paragraph:60-89`, migration kuralları `paragraph:90-95`, go/no-go tetikleri `paragraph:96-110`; 11.01 `sheet:UX ve Release Gate v46.2/row:14` Security/DR gate; 22.01 `paragraph:113-115`.

## 1. Kapsam ve ilke

- Kapsam (23.04 `paragraph:10`): PostgreSQL, object/evidence storage, config/reference, integration state, audit/outbox.
- İlke (23.04 `paragraph:12`): backup varlığı yeterli değildir; restore testi yapılmadan DR kanıtı oluşmaz. `dr_backup.py` çıktısı açıkça `restoreProof: NOT_PROVEN_BY_BACKUP` yazar.
- Secret'lar sağlayıcı tarafında yönetilir; düz metin dışa aktarılmaz (23.04 `paragraph:31-32`). Betikler bağlantıyı yalnız libpq ortam değişkenlerinden (`PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD`/`PGPASSFILE`, `PGDATABASE`, `PGSSLMODE`) okur; değerleri argümana, manifeste, rapora veya log'a yazmaz.

## 2. Kurtarma hedefleri (onaysız)

| Tier | Örnek | 23.04 önerilen başlangıç hedefi | Durum |
|---|---|---|---|
| T0 Critical | Core DB/auth/evidence | RPO ≤15 dk, RTO ≤4 saat (`paragraph:45-48`) | **Production Council onayı bekliyor** |
| T1 High | Notifications/integration hub | RPO ≤1 saat, RTO ≤8 saat (`paragraph:49-52`) | Onay bekliyor |
| T2 Standard | Analytics/derived projections (pilot metrikleri dahil) | Rebuildable, RTO ≤24 saat (`paragraph:53-56`) | Onay bekliyor |

23.04 `paragraph:57`: bunlar başlangıç tasarım hedefleridir. `dr_rehearsal.py` ölçülen değeri bu hedeflerle yalnız bilgi amaçlı karşılaştırır (`withinProposedTargets`); karar insan onayıdır.

## 3. Yedekleme (`scripts/dr_backup.py`)

```
# Ortam: libpq değişkenleri (secret store'dan enjekte edilir), PATH'te pg_dump/psql.
py scripts/dr_backup.py --out-dir .runtime/dr --object-root <bucket sync/mirror dizini>
```

- `pg_dump --format=custom --no-owner --no-privileges`; dosya SHA-256'sı manifestte.
- Yedek anında `__EFMigrationsHistory` başı, en yeni `audit.AuditEvents.OccurredAt` ve sahip tabloların tam satır sayıları kaydedilir.
- Nesne anlık görüntüsü `poseidon/<tenant>/<id>` düzeninde olmalıdır (API `FileObjects` anahtar biçimi); her nesne kopyalanır, kopya hash'i doğrulanır, manifest `key/size/sha256` içerir. Beklenmeyen anahtar veya symlink reddedilir.
- Sağlayıcı sürümleme/replikasyonu (23.04 `paragraph:26`) bu betiğin yerine geçmez; ikisi birlikte kullanılır.

## 4. Geri yükleme (`scripts/dr_restore.py`)

```
py scripts/dr_restore.py .runtime/dr/backup-<stamp> --confirm-target <PGDATABASE> --object-target <boş dizin>
```

- Önce izole doğrulama ortamına (23.04 `paragraph:62`): hedef adı `restore`/`rehearsal` içermiyorsa `--allow-named-target` olmadan reddedilir; `--confirm-target` `PGDATABASE` ile birebir eşleşmelidir.
- Dump ve tüm nesneler manifest hash'leriyle **yazmadan önce** doğrulanır; uyuşmazlıkta hiçbir şey yazılmaz.
- `pg_restore --clean --if-exists --single-transaction --exit-on-error`.
- Sonuç `RESTORED_UNVALIDATED`: kullanıcı trafiği açılmadan önce bölüm 5 kontrolleri gerekir (23.04 `paragraph:63`).

## 5. Restore provası ve ölçüm (`scripts/dr_rehearsal.py`)

```
py scripts/dr_rehearsal.py .runtime/dr/backup-<stamp> --sample 20 --seed <n>
```

Geçici, yalnız loopback'e açılan `postgres:18.6` konteyneri (rastgele parola yalnız bellekte), restore, kontroller ve her durumda konteyner silme. Rapor `.runtime/dr/rehearsal-<stamp>.json`.

| 23.04 kontrolü | Betik kontrolü | Not |
|---|---|---|
| Database opens (`paragraph:68`) | `DATABASE_OPENS` | `SELECT 1` |
| Schema/migration version (`paragraph:69`) | `MIGRATION_VERSION` | manifest başı ile eşit |
| Tenant isolation (`paragraph:70-71`) | `TENANT_ISOLATION` = **NOT_RUN** | Uygulama host'u + iki tenant aktörüyle yapılmalı |
| Evidence checksum/provenance (`paragraph:72-73`) | `EVIDENCE_SAMPLE` | seed'li örneklem: nesne SHA-256 + `files.StoredFiles.Checksum` |
| Audit history queryable (`paragraph:74-75`) | `AUDIT_QUERYABLE` | en yeni audit kaydı yedekle aynı |
| PMS/Operations samples (`paragraph:76-77`) | `ROW_COUNTS` | sahip tablo sayıları; iş örneklemi uygulama ile ayrıca |
| Offline sync (`paragraph:78-79`) | `OFFLINE_SYNC` = **NOT_RUN** | replay/duplicate uygulama host'unda |

Ölçüm tanımları:

- **RPO** = simüle olay anı (prova başlangıcı) − geri yüklenen veritabanındaki en yeni audit kaydı. Ayrıca `backupLagSeconds` = dump bitişi − yedekteki en yeni audit.
- **RTO** = restore başlangıcı → restore edilen DB açık ve migration başı manifestle eşleşiyor. API sağlık kontrolü ve kullanıcı trafiği açma ayrı insan adımıdır.

Rapor durumu en iyi ihtimalle `MEASURED_PARTIAL_UNSIGNED`; `acceptance` her zaman `NOT_ACCEPTED` yazılır.

## 6. DR aktivasyonu (23.04 `paragraph:58-64`)

1. Incident Commander DR ilan eder; normal change freeze başlar.
2. Etki belirlenir: tenant/vessel/data window/integrations.
3. Son sağlıklı recovery point seçilir; evidence/data integrity riski değerlendirilir.
4. Restore önce izole doğrulama ortamında (bölüm 4-5), sonra onaylı recovery target'a uygulanır.
5. Auth/tenant/data/evidence/integration smoke geçmeden kullanıcı trafiği açılmaz.
6. Recovery sonrası reconciliation (DEV-070) ve incident report.

## 7. Release rollback / NO-GO tetikleri (23.03 `paragraph:96-110`)

| Tetik | Eylem |
|---|---|
| Build/test/security P0 fail | Deploy etme |
| Migration validation fail | Dur; restore veya fix sonrası yeniden dene |
| Tenant isolation/auth failure | Derhal rollback |
| Critical business smoke fail | Onaylı güvenli degraded mode yoksa rollback |
| Unexplained data mismatch | Yazmaları durdur / reconcile |
| Monitoring blind | Production release yok |

Pre-deploy checklist (23.03 `paragraph:60-89`): release manifest hash'i, onaylı change set, migration forward + rollback/restore planı test edilmiş, secret/config geçerli, **pre-change recovery point doğrulanmış (bölüm 3 + 5)**, dashboard/alert aktif, kritik entegrasyonlar, kullanıcı bildirimi, rollback tetik+sahip+adım hazır.

Migration kuralları (23.03 `paragraph:90-95`): EF Core migration'ları versiyonlu ve tekrarlanabilir; geri alınamayan migration için restore/forward-fix yolu bu runbook'tur (bölüm 4). Breaking değişiklik expand/migrate/contract ile yapılır.

## 8. Rollback adımları

1. Release Manager NO-GO tetiğini kayda geçirir (tetik, saat, sahip).
2. Trafik durdurulur / bakım moduna alınır; offline istemciler kuyruğu korur (idempotent receipt).
3. Uygulama önceki release artefaktına döndürülür (manifest hash'i doğrulanır).
4. Şema geri alınabiliyorsa önceki migration'a dönülür; değilse pre-change recovery point bölüm 4 ile izole hedefe, doğrulama sonrası onaylı hedefe restore edilir.
5. Bölüm 5 kontrolleri + tenant izolasyonu ve kritik smoke insan tarafından doğrulanır.
6. Reconciliation (DEV-070) ve olay raporu; açık bulgular izlenir.

## 9. İmza alanları (insan kaydı — otomasyon dolduramaz)

| Kayıt | Rol | İmza | Tarih |
|---|---|---|---|
| Rollback prosedürü onayı | Owner / Release Manager | **Owner imzası bekleniyor** | — |
| RPO/RTO hedef onayı | Production Council | **Onay bekleniyor** | — |
| İlk gerçek restore provası gözlemi | Ops (yazar ≠ onaylayan) | **Bekleniyor** | — |

## 10. Açık kalemler

- Gerçek prova: Docker/ops ortamında `dr_rehearsal.py` koşusu — **DEFERRED** (bu geliştirme oturumunda Docker paylaşımlı; çalıştırılmadı).
- Tenant isolation ve offline replay restore kontrolleri uygulama host'u ile — **NOT_RUN**.
- WAL/PITR politikası (23.04 `paragraph:23`) ve şifreli yedek deposu sağlayıcı seçimine bağlı — **açık**.
- Pilot telemetri (DEV-072) T2 türetilmiş veridir; retention süresi operatör konfigürasyonudur ve onay bekler.
