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

## CI durumu — PR #17 (bu dal → main)
- PR #16 18.11.2026'da birleşti (merge `e2e1200`).
- PR #17 açık. İlk push+PR koşuları aynı iki adımda kırmızıydı (deterministik):
  1. `frontend-and-browser / Real API browser smoke`: i18n JSON import attribute, `import.meta.glob` ve describe içi `test.use({trace})` → `9b25dc1` ile düzeltildi (yerelde `playwright test --list` 116 test yüklüyor). Sonucu CI'da kontrol et.
  2. `persistence-and-security / Scan every tracked repository input`: 10 bulgu (8'i `docs/sources/2026-11-15/` metin kopyalarındaki analiz dokümanı örnek DB şifreleri, 2'si `scripts/oidc_lab.py:148,172` çalışma anında üretilen lab şifreleri). **İNSAN ONAYI GEREKİR.** İstisna sadece bağımsız inceleme + korumalı `vars.POSEIDON_SOURCE_REVIEW_SHA256` pini ile geçer (`docs/security/source-review-v2.md`). AI kendi istisnasını yazıp onaylayamaz (izin sistemi de engelliyor). Bu adım için push döngüsüne GİRME; kullanıcı kararını bekle. Diğer kırmızı adımlar için CI protokolü geçerli.

## Sıradaki iş (öncelik sırası)
1. PR #17 CI: browser smoke sonucunu oku; kırmızıysa protokole göre teşhis et. Secret scan kullanıcı onayını bekliyor.
2. THEME-BUILDER-01: uygulama içi tema düzenleyici (shadcn/create benzeri: taban renk, vurgu, radius, mod; canlı önizleme; tarayıcıya kaydet; preset paylaşımı) + component testleri.
3. 15.11 planı dalga 0–1: DEV-020 / T-S2-001 (59.01+52.01 LegalEntity/Vessel sahipliği), ardından DEV-021/022 kabuk-menü, DEV-023/024 Today/Why, DEV-025 erişilebilirlik.
