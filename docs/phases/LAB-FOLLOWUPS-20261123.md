# LAB-FOLLOWUPS-20261123 — HELP-MANUAL-DOWNLOAD-01 QA notları N1/N2

Kaynak: `docs/phases/HELP-MANUAL-DOWNLOAD-01.md` bağımsız QA (ACCEPT-WITH-NOTES), `docs/phases/ADMIN-POLISH-BUNDLE-20261122.md` takip listesi N1/N2. Kapsam yalnız sentetik DEV-OIDC lab araçları; ürün davranışı değişmez. Dal: `fix/lab-followups-20261123` (`origin/main` 494045ee).

## N1 — `test_lab_profile_acl.py` POSIX testleri Linux CI'da

- Bulgu: `Application foundation` / `persistence-and-security` (ubuntu) içindeki `Document import tests` adımı zaten `scripts/test_*.py` setini koşturuyor; main run 45090000671 günlüğünde üç POSIX testi (`test_posix_owned_pair_made_owner_only_and_read_back`, `test_posix_wrong_owner_refuses_mode_change`, `test_posix_readback_rejects_group_world_or_foreign_uid`) `ok`. Eksik olan, bir atlamanın (skip) fark edilmemesiydi.
- Değişiklik: aynı işte yeni adım `Lab profile POSIX boundary tests on Linux (no skips allowed)`: `test_lab_profile_acl` + `test_lab_profile_ca` çalışır, `lab_profile_posix ran=… skipped=…` satırı yazar; başarısızlık, hata veya **herhangi bir skip** adımı kırmızı yapar. İş adları değişmedi.

## N2 — `lab_profile_ca.py` sabit paket eksikse fail-closed

- Önceden `POSIX_DEPENDENCIES` içindeki kitaplık yoksa sessizce atlanıyordu; dinamik yükleyici bunu sistem NSPR/NSS'inden karşılayabilirdi.
- Şimdi `require_bundle()` yüklemeden önce tam seti denetler: NSPR/NSS bağımlılıkları, `libnss3.so` + `libsmime3.so` ve NSS_Init'in kendi dizininden açtığı `libsoftokn3.so` + `libfreeblpriv3.so` (Windows: `nss3.dll`). Her biri düzenli dosya olmalı ve gerçek yolu doğrudan pinned paket dizininde olmalı (dışarıya symlink reddedilir). Eksikse hiçbir kitaplık yüklenmeden `nss_bundle_incomplete`.
- Liste, gerçek Playwright `firefox-1555` Linux paketinin içeriğiyle doğrulandı (Help screenshots run 45075400512 tanılama listesi: bu dokuz kitaplığın hepsi mevcut).
- CLI artık stderr'e yalnız sabit kod yazar (`lab_profile_ca: nss_bundle_incomplete`; serbest metinli/başka tür hatada `lab_profile_ca: failed`). İstisna metni hâlâ dışarı sızmaz; `strict-browser.ts` stdio'yu yok sayar.
- Yeni `scripts/test_lab_profile_ca.py` (9 test): her bir POSIX kitaplığı eksikken ve Windows `nss3.dll` eksikken `CDLL` hiç çağrılmadan red; kitaplık yerine dizin; paket dışına symlink (yalnız POSIX); tam pakette yalnız paket yollarının sırayla `RTLD_GLOBAL` ile yüklenmesi; CLI sabit kod / metin sızmaması / sessiz başarı.

## Doğrulama

| Kontrol | Sonuç |
| --- | --- |
| `py -m unittest test_lab_profile_ca test_lab_profile_acl` (Windows, `scripts/`) | 29 test OK, 3 skip (Windows'ta POSIX-yalnız testler) |
| `py -m unittest discover -s scripts -p 'test_*.py'` (Windows) | 669 test OK, 14 skip |
| Linux yerel koşu | Yapılmadı: WSL/Docker başka oturumda; yeniden başlatılmadı. Linux kanıtı CI adımıdır. |
| Branch CI | Aşağıda / teslim raporunda |

Help screenshots iş akışı bu dalda tetiklenmez (yalnız `screenshots/**`); NSS yükleme değişikliği bir sonraki ekran görüntüsü koşusunda gerçek paketle uçtan uca doğrulanacak (DEFERRED).
