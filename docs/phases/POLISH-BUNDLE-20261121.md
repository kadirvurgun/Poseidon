# POLISH-BUNDLE-20261121 — saat kaynağı ve tema tamamlama

Durum: **AUTHORED, bağımsız QA ACCEPT-WITH-NOTES, kapı CI bekleniyor**. Yazılım kalite paketi; canonical iş/SP değiştirmez (114/121 iş, 932/970 SP, %96,08 aynı). Pilot işleri (DEV-076–079/095/096/121) açık kalır.

Dal: `integration/polish-bundle-20261121` = main 84d4cd6e + `refactor/timeprovider-20261121` (e37ed8c0) + `feature/theme-complete-20261121` (4dadc56f) + N1 düzeltmesi.

## TIME-SOURCE-01 — enjekte saat
- 95 dosyada 142 doğrudan `DateTime(Offset).UtcNow` okuması → DI `TimeProvider` (`RequestClock.cs` `HttpContext.UtcNow()`, `PoseidonDbContext.Clock`, domain metotlarında açık `now/savedAt`).
- Denetim/güvenlik olayı `OccurredAt` gerçek saatte kalır (`PoseidonDbContext.AuditRecordingClock`; ilk CI `AUDIT_TIMESTAMP_INVALID` @ `CriticalWriteCoverageCapture.cs:146` ile bulundu, e37ed8c0'da düzeltildi).
- QA N1 düzeltildi: `AuditHistoryEndpoints.cs` sayfa kesimi ve imleç üst sınırı `OccurredAt` ile aynı saat (AuditRecordingClock).
- Koruma: `tests/Poseidon.Api.Tests/WallClockGuardTests.cs` (3 test; tam sayılı `TimeProvider.System` izin listesi).
- Açık notlar: N2 `MembershipStateReader` sistem saati (salt okuma), N3 `using static`/alias ve Stopwatch kapsam dışı, N4 CrewEndpoints satır başına `UtcNow()`.

## THEME-TOKEN-SWEEP-01 / THEME-CHART-01 / radius
- ~220 sabit renk token'a; ECharts `frontend/src/theme/chart-theme.ts` ile tema okur, değişimde yeniden boyar, okunamazsa çizmez; 121 radius `--radius-delta` ile builder'a bağlı.
- Koruma: `frontend/src/theme/hardcoded-colors.test.ts` (çok satırlı bildirim + custom property isimli renk dahil, QA notları 4dadc56f).
- Güvenlik anlamı: UNKNOWN/REVIEW_REQUIRED/warning/danger/info ayrı; yeşil/başarı tonu eklenmedi; durum asla `--primary`'ye eşlenmez.
- DEFERRED: gerçek tarayıcı görsel/kontrast/klavye/320px kapısı; preset fontları (Oxanium/Geist), Phosphor ikonları, Sera stili (dosya/lisans yok); 196 ekran tema kabulü.

## Doğrulama
| Kontrol | Sonuç |
|---|---|
| TimeProvider push CI 44906148772 (e37ed8c0) | success (4/4 iş) |
| Tema push CI 44907632197 (4dadc56f) | success |
| Bağımsız QA saat (e37ed8c0) | ACCEPT-WITH-NOTES; build 0/0, Domain 1622/1622, Persistence 149/149, Api 466/466 (Integration hariç) |
| Bağımsız QA tema (8e270264) | ACCEPT-WITH-NOTES; typecheck/lint 0, tema testleri 64/64; tam vitest 10 hata = yerel Node 22 ortamı (9 tabanda da var) + 1 yük zaman aşımı |
| Paket yerel (N1 sonrası) | build 0 hata; Api 466/466 (Integration hariç) |
| PostgreSQL entegrasyon yerel | DEFERRED (Docker yok); CI kanıtı |
| Paket push + PR CI | bekleniyor |
