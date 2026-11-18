# Claude handoff

STATUS: IN_PROGRESS
Güncelleme: 2026-11-19 ~00:30 (interaktif oturum 5 saatlik kotanın %90'ında durdu; kilit kaldırıldı)

## KOTA KURALI (zorunlu)
Her büyük adımdan önce `C:\Users\avurgun\.claude-poseidon\statusline\cache\usage.json` oku (`fiveHourPct`, `resetsAt`). `fiveHourPct >= 97` ise yeni iş başlatma, durumu bu dosyaya yaz, commit et ve çık. `>= 90` ise yeni alt-ajan başlatma, yalnız mevcut işi bitir. Alt-ajanları aynı anda en fazla 3 tut.

## Disk / Docker (önce kontrol et)
- C: diski 18.11 gece 0 bayta düştü → Docker Desktop daemon kilitlendi (`docker info` timeout). Eski worktree bin/obj silinerek ~1,9 GB açıldı. Başlarken `(Get-PSDrive C).Free` < 5 GB ise önce yer aç (yalnız yeniden üretilebilir şeyler: eski `.runtime/worktrees/*` bin/obj, TestResults, indirilen CI artifact'ları). Docker hâlâ yanıt vermiyorsa yerel PG testlerini çalıştırma; CI kanıtı yeterli, kullanıcıdan Docker restart iste.

## Main ve PR durumu
Main: #16 #17 #18 #20 #23 birleşik (son: `4d63012`).

| PR | İçerik | Durum / sıradaki adım |
|---|---|---|
| **#36** | S2 entegrasyon paketi: #31 #24 #19 #21 #26 #27 #22 #35 (HEAD 42c9c25) | CI koşuyor. Push+PR koşusu yeşilse `gh pr merge 36 --merge --match-head-commit <sha>`; branch koruması "up to date" ister — main ilerlediyse main'i birleştir, push, CI bekle. Birleşince #19 #21 #22 #24 #26 #27 #31 #35 otomatik Merged olur. |
| #29 | DEV-024 kritik durum (taban DEV-020 dalı) | QA FAIL düzeltildi (944759e). #36 birleşince tabanı main'e çevir (`gh pr edit 29 --base main`), main'i birleştir, push, **bağımsız yeniden QA**, CI. |
| #32 | axe-core kapısı (taban DEV-025 dalı) | #36 sonrası tabanı main'e çevir, main'i birleştir, CI. |
| #28 | 52.02 üyelik | QA PASS-WITH-NOTES; düzeltme ajanı yarıda durdu: worktree `.runtime/worktrees/plan-5202-20261118` (9f05cc9, origin'den 3 commit ileri, 1 dosya commit'lenmemiş). Değişikliği incele, tamamla (vekalet kapsamı+gerekçe, security-suspend resume yetkisi, last-admin eşzamanlılık testi, 503/cookie testleri), commit, push. |
| #30 | 58.02 belge zinciri | QA düzeltmeleri commit'li ve push edildi (8cca242) — ajan durdurulmadan önce son commit docs idi; testleri (trait PLAN-1511-5802, ModelParity, MigrationIdOrdering) CI/yerelde doğrula; eksik kalan F1–F5 maddesi varsa tamamla. |
| #33 | 52.01 tüzel kişi/İK | QA düzeltmeleri push edildi (94dd49b). Docker'lı testler yerelde koşmadı → CI'ya bak. |
| #34 | 54.01 tipli politika | Açık; bağımsız QA yapılmadı → QA başlat. |
| #25 | 58.03 uç profil sicili | En son birleşecek; diğer PR'ların yeni uçlarına profil ekle (scripts/endpoint_profiles.py). |
| (dal) | `fix/same-key-replay-race-followup-20261118` worktree `.runtime/worktrees/replay-race-followup-20261118` | Ajan yarıda durdu: 15 dosya commit'lenmemiş (HumanReview/RegulatoryQualification/Notification aynı-anahtar replay yarışı). İncele, testleri çalıştır, commit, push, PR (taban: #31 birleştiyse main). |

## Sıradaki iş (sıra)
1. #36'yı birleştir (S2'nin 5/6 işi main'e girer).
2. #29'u (DEV-024) tamamla → S2 6/6 main'de.
3. **S2 sprint kapısı:** main üzerinde tam backend/persistence/API/frontend/Playwright + bağımsız S2 QA (poseidon-qa). Geçerse `docs/project-status.json` → S2 accepted 6 iş / 42 SP; toplam 145/970 = %14,9 (PR ile).
4. İkinci paket: #28 #30 #33 #34 (+#32), sonra #25.
5. Sonra S3 backlog (DEV-026…) + 15.11 dalga 1 kalanları (63.06, 65.03), dalga 2 (59.01, 60.03).

## PR KURALI (kullanıcı, 19.11.2026)
İş başına ayrı PR AÇMA. Paketler özellik dallarında geliştirilir (push edilebilir, CI koşar) ama PR yalnız **entegrasyon dalı** için açılır: `integration/<konu>-<tarih>` dalında birden çok paket birleştirilir, tek büyük PR → tek CI → main. Mevcut tekil PR'lar (#25 #28 #29 #30 #32 #33 #34) ikinci entegrasyon PR'ında toplanacak; o birleşince otomatik Merged olurlar (ayrıca yeni tekil PR açma). Bağımsız QA paket başına yine yapılır (alt-ajan), PR başına değil.

## Kararlar
- Kullanıcı soru istemiyor; kararlar analiz/araştırma ile verilir, buraya ve görev dokümanlarına yazılır.
- Her PR: CI yeşil + bağımsız QA (yazar ≠ inceleyici alt-ajan). Main korumalı: güncel dal şart → çok PR'ı entegrasyon paketiyle birleştir.
- Ajanlara: yalnız mutlak yol, kendi başlatmadığı süreci öldürme, Playwright için benzersiz port.
- Secret scan istisnaları: yazar + bağımsız inceleyici paketi; pin ekleme adımı kullanıcı onayı gerektirebilir.
