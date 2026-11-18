# Claude handoff

STATUS: IN_PROGRESS
in_progress: interaktif oturum aktif — 2026-11-19 02:00'a kadar zamanlanmış oturum yalnız okuma/rapor yapsın, kod değiştirmesin.
Güncelleme: 2026-11-18 akşam (Claude Opus 5.5)

## Main durumu
- PR #16 (Codex S6 foundation) birleşti: `e2e1200`.
- PR #17 (continuation + 17 dil i18n + 15.11 analizleri + tema temeli + entegrasyon düzeltmeleri) birleşti: `b61dbef`. Push ve PR CI koşularında 4/4 iş yeşil (API/PG dahil).
- Secret scan: 10 bulgu bağımsız incelendi, `docs/security/files-source-exceptions.json` 178 kayıt, repo değişkeni `POSEIDON_SOURCE_REVIEW_SHA256` 178 pin.

## Açık işler ve sahipleri (18.11 gece)
Main: PR #16, #17, #18 (tema oluşturucu) birleşti (`d7479d6`).

| Paket | Dal / PR | Durum |
|---|---|---|
| DEV-020 gemi profili provenance/freshness | `feature/s2-dev020-vessel-profile-20261118` / PR #19 | QA PASS-WITH-NOTES düzeltildi; CI browser smoke `profile-repair.spec.ts` eski mock → düzeltiliyor |
| DEV-021 bağlam geçişi | `feature/s2-dev021-context-switch-20261118` / PR #20 | QA düzeltildi, CI bekleniyor |
| DEV-022 role menü/arama | `feature/s2-dev022-role-menu-20261118` / PR #21 (taban #20) | QA PASS-WITH-NOTES; etiketle arama düzeltmesi yapılıyor |
| DEV-023 Today/Why | `feature/s2-dev023-today-why-20261118` | geliştiriliyor |
| DEV-024 kritik durum | `feature/s2-dev024-critical-state-20261118` (taban DEV-020) | geliştiriliyor |
| DEV-025 erişilebilirlik | `feature/s2-dev025-accessibility-20261118` (taban DEV-022) | geliştiriliyor |
| PLAN-1511-5601 kapsam haritası | PR #22 | CI bekleniyor; lider kararları LEAD-5601-D01..D05 |
| PLAN-1511-5803 endpoint profilleri | `feature/plan-5803-endpoint-profiles-20261118` | geliştiriliyor |
| PLAN-1511-5402 action scope | `feature/plan-5402-action-scope-20261118` | geliştiriliyor |
| PLAN-1511-5202 üyelik | `feature/plan-5202-membership-20261118` | geliştiriliyor |
| PLAN-1511-5802 belge/kanıt | `feature/plan-5802-document-evidence-20261118` | geliştiriliyor |
| PLAN-1511-5501-SLICE metrik sözleşmesi | `feature/plan-5501-metric-contract-20261118` | geliştiriliyor |

Birleştirme sırası: #22 → #19 → #20 → #21 (taban main'e çevir) → DEV-024/025 (tabanları birleşince main'e) → 5803/5402/5202/5802/5501 (her biri main ile birleştirilip CI + bağımsız QA).
## Sıradaki iş (sıra)
1. Yukarıdaki PR'ların CI'sını izle; kırmızıysa AGENTS.md CI protokolü (teşhis → tek push → bekle; durma limiti).
2. DEV-022 / T-S2-003 role-first menü + yetkili arama (gizli menü/sayı sızıntısı yok) — 54.02, 61.03, 65.03.
3. DEV-023 / T-S2-004 Today priority/source/Why/owner/next action; DEV-024 / T-S2-005 kritik state source/reason/freshness/blocker.
4. DEV-025 / T-S2-006 erişilebilirlik (DEV-022 sonrası paralel).
5. S2 tamamlanınca: bağımsız QA (poseidon-qa skill) + `docs/project-status.*` canonical güncelleme; sonra 15.11 plan dalga 1 (52.02, 54.01, 54.02, 58.02, 63.06, 65.03).

## Kararlar
- Kullanıcı soru istemiyor; kararlar analiz/araştırmaya göre verilir ve buraya yazılır.
- Main korumalı: her iş PR ile; required checks + bağımsız inceleme sonrası `gh pr merge --merge --match-head-commit <sha>`.
- `gh` kurulu ve oturum açık: `C:\Users\avurgun\AppData\Local\Microsoft\WinGet\Links\gh.exe`.
