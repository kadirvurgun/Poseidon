# Claude handoff

STATUS: IN_PROGRESS
Güncelleme: 2026-11-18 (Claude Opus 5.5, Codex'ten devralma oturumu)

## Aktif çalışma
- Dal: `foundation/s2-integration-20261118`
- Worktree: `.runtime/worktrees/s2-integration-20261118`
- İçerik: PR16 head `32ff1d0` + continuation dalı (141 commit: 17 dil i18n, 15.11 analiz kaynak importu, aktif şirket bağlamı, onboarding blocker) + tema/filo katalog/profil paketi (eski `1af08e5`, şimdi `387b5d4`) + entegrasyon düzeltmeleri.
- PR #16 (main'e) CI 4/4 yeşil, taslak; main korumalı (yalnız PR ile). Kullanıcı birleştirmesi veya `gh` ile `gh pr ready 16 && gh pr merge 16 --merge` gerekiyor. Birleşince bu dal main'e göre PR açılacak (taban main; PR16 commit'leri o zaman diff'ten düşer).

## 18.11.2026 gerçek doğrulama (bu dalda, yerel)
| Kontrol | Sonuç |
|---|---|
| `dotnet build Poseidon.slnx -c Release` | 0 hata, 0 uyarı (önce 5 derleme hatası düzeltildi) |
| Domain unit | 773/773 |
| Persistence unit | 75/75 |
| Persistence integration (gerçek PG, model parity dahil) | 89/89 |
| Frontend lint / typecheck / build | geçti |
| Frontend component | 1674/1675; tek hata (async history assert) düzeltildi, dosya 82/82 |
| Python scripts | 585 OK, 11 skip, 0 fail |
| API integration | koşuldu; sonucu aşağıda/CI'da |
| Playwright / genuine signed manifest | NOT RUN |

## Bilinen açıklar / kararlar
- Tema: shadcn preset b3Zheoix4U renk/radius tokenları + açık/koyu/sistem + Zümrüt/Okyanus/Grafit. Tam "theme builder" (kullanıcının kendi renk/radius/font seçtiği editör) YOK; Oxanium/Geist/Phosphor geçişi yapılmadı. Sıradaki UI işi olarak planlandı.
- RegulatoryProfile'da iki "Kayıtlı kaynak bağlamı" bloğu var (HEAD aside + ProfileSourceSummary); birleştirilecek.
- Sidebar tamamen tokenlara geçirildi; kontrast ölçümü yapılmadı.
- Codex otomasyonları (Poseidon-HourlyQuota/MetadataMonitor/ProductBatch/ProductReview) 18.11'de devre dışı bırakıldı.

## Sıradaki iş (öncelik sırası)
1. PR16 birleşince: bu dalı main'e PR olarak aç, CI'yı izle (AGENTS.md CI protokolü).
2. THEME-BUILDER-01: uygulama içi tema düzenleyici (shadcn/create benzeri: taban renk, vurgu, radius, mod; canlı önizleme; tarayıcıya kaydet; preset paylaşımı) + component testleri.
3. 15.11 planı dalga 0–1: DEV-020 / T-S2-001 (59.01+52.01 LegalEntity/Vessel sahipliği), ardından DEV-021/022 kabuk-menü, DEV-023/024 Today/Why, DEV-025 erişilebilirlik.
