# Claude handoff

STATUS: IN_PROGRESS
in_progress: interaktif oturum aktif — 2026-11-19 02:00'a kadar zamanlanmış oturum yalnız okuma/rapor yapsın, kod değiştirmesin.
Güncelleme: 2026-11-18 akşam (Claude Opus 5.5)

## Main durumu
- PR #16 (Codex S6 foundation) birleşti: `e2e1200`.
- PR #17 (continuation + 17 dil i18n + 15.11 analizleri + tema temeli + entegrasyon düzeltmeleri) birleşti: `b61dbef`. Push ve PR CI koşularında 4/4 iş yeşil (API/PG dahil).
- Secret scan: 10 bulgu bağımsız incelendi, `docs/security/files-source-exceptions.json` 178 kayıt, repo değişkeni `POSEIDON_SOURCE_REVIEW_SHA256` 178 pin.

## Açık işler ve sahipleri
| Paket | Dal / PR | Durum |
|---|---|---|
| THEME-BUILDER-01 | `feature/theme-builder-20261118` / PR #18 (taban main) | Yerelde 1705/1705 vitest, lint/typecheck/build geçti; CI bekleniyor, yeşilse birleştir. |
| DEV-020 / T-S2-001 gemi profili provenance/freshness | `feature/s2-dev020-vessel-profile-20261118` (worktree `.runtime/worktrees/s2-dev020-20261118`) | Geliştirme sürüyor (interaktif oturum alt-ajanı). |
| DEV-021 / T-S2-002 aktif gemi/rol, yetkili geçiş, dirty form | `feature/s2-dev021-context-switch-20261118` (worktree `.runtime/worktrees/s2-dev021-20261118`) | Geliştirme sürüyor. |

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
