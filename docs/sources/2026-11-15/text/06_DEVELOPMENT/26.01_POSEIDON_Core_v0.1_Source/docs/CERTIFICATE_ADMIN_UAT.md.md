# CERTIFICATE_ADMIN_UAT.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/docs/CERTIFICATE_ADMIN_UAT.md
SHA256: 342d257a237d6e41f467bb609bf5ec5f3fa839e786ab02a92ae4ec8d96e6d71d

## lines:1-40

# UAT — İdari belge kayıtları ve tarih takibi

Durum: **USER_ACCEPTANCE_PENDING / PRIVATE_ACCESS_NOT_ESTABLISHED**.
Bu belge Esra kabulü değildir. Yerel çalıştırma/görev/Library bağlantısı, Android veya
web üzerinden erişilebilen owner-authenticated uygulama adresi değildir. G7 açıkken
USER_ACCEPTED veya kullanıcıya hazır test erişimi var denmez.

## Mevcut sentetik ortamda yeniden üretme

Mevcut özel Development API/PostgreSQL kurulumunu kullanın; yeni hosting/tünel/ağ,
hesap/IdP veya credential oluşturmayın. Yeni003 migration yalnız ayrılmış sentetik
ortamda mevcut Development initializer ile uygulanır. Gerçek kurulum ayrı kapıdır.

```sh
node tests/Backend/certificate-uat.cjs
```

Betik loopback API'ye bağlanır; yalnız certificate-manager sentetik fixture'ıyla bir
DRAFT tug ve yedi belge kaydı oluşturur: vessel-only eksik taslak; expiry yok;
geçmiş/bugün/+30/+31/+91 gün örnekleri. Tarih örnekleri API'nin döndürdüğü UTC gününe
göre açık sentetik girdilerdir, ürünün otomatik alan varsayılanı değildir. Betik token
ve veri tabanı çıktısı basmaz. Yeniden çalıştırma yeni örnekler üretir.

Development girişinde `Certificate manager (synthetic)` seçin ve **Belge kayıtları**
sekmesini açın. Bu fixture inventory:manage vermez; parent DRAFT seçilebilir.
RECORDED parent/readonly/karşı şirket/issuer ayrımı ve izin iptali için C# TestServer
fixture'ları vardır; gerçek hesap/grant yaratılmaz. Kullanıcının özel test ortamı
sağlandığında roller yetkili fixture yönetimiyle sunulmalıdır; şu anda erişim URL'si yoktur.

## Esra adımları — bütün kullanıcı sonuçları BEKLİYOR

| No | İşlem ve beklenen sonuç |
|---|---|
| C01 | Yalnız gemi seçip kaydet; type/issuer/tarih varsayılanı yok, kayda alma eksik alanlarla engelli |
| C02 | Type+issuer gir, idari kayda al; UUID aynı, tarihsiz NO_EXPIRY_DATE; valid/ready/verified yok |
| C03 | Yetkili DRAFT/RECORDED gemi seç; parent reopen belge state/sürümünü değiştirmez |
| C04 | Readonly/writer/inventory-manager/certificate-manager; bağımsız grant server'da denetlenir |
| C05 | Karşı tenant/company/ungranted parent; listede yok, detail/history404; creator tek başına yetmez |
| C06 | Aynı key aynı işlem bir event; değiştirilmiş body/action409; iptal edilmiş erişim/grant replay'i engeller |
| C07 | İki editör; eksik If-Match428, eski sürüm412; gizli üzerine yazma yok |

## lines:41-80

| C08 | State/verification/readiness/evidence alias'ları veya duplicate keys422; submit403 |
| C09 | Geçersiz takvim/timestamp/blank/range422; leap day ve eşit tarihler metadata olarak kabul |
| C10 | Dün/bugün/+1/+30/+31/+60/+61/+90/+91/boş; tam bucket sınırları; browser timezone değiştirmez |
| C11 | Sabit saatle gece yarısı: cursor aynı asOfDate; refresh yeni gün; audit/state/version değişmez |
| C12 | Gelecek issue yalnız metadata uyarısı; far-future expiry yine değerlendirme yapılmadı |
| C13 | Kayıtlı expiry düzeltmesi permission+reason+version; 'renewed' denmez; required alan için önce reopen |
| C14 | Aynı number ikinci UUID ile mümkün; vessel değişimi yetki olsa da422 |
| C15 | Literal %,_,\ ve >500 sayfalama; scope/query/date cursor tutarlılığı; invalid query400 |
| C16 | History yalnız altı sabit alan; values/actor/reason/notes/correlation yok; her sayfada güncel parent access |
| C17 | Notes veya girilen metin Evidence/Available/Verified/PSC-ready üretmez; valid-count yok |
| C18 | Eski v1 çalışır; v2/RECORDED legacy write/replay409; migration stable IDs/audit/replay/FK/grants korur |
| C19 | DB/network hata başarı değildir; aynı key korunur; expiry/logout/CSRF/scope değişiminde otomatik yazı yok |
| C20 | TR/EN mobile/tablet/desktop; boş/hata/conflict, dirty iptal/geri, hızlı arama, safe text, tarih bağlamı |

Bulgu: arşiv SHA/build, adım, dil/viewport, sentetik UUID, beklenen/görülen davranış.
Gizli token, müşteri verisi veya ekran görüntüsü yüklemeyin. Defect→fix→retest sonrası
Esra'nın açık onayıyla USER_ACCEPTED kaydedilir; otomatik PASS bu statüyü değiştirmez.

## Otomatik kanıt kapsamı

`CertificateAdminTests`: lifecycle/date-boundary/midnight/strict input/legacy.
`CertificateSecurityTests`: independent grant/replay/concurrency/parent isolation/
601 kayıt/history/atomic rollback. `CertificateMigrationTests`: gerçek001/002→003
ve immutable audit/idempotency/FK. `CertificateBffTests`: cookie/CSRF/scope/grant.
Frontend certificate testleri gerçek browser sertifikasyonu değil, component/API
fixture testleridir. `bff-browser.cjs` gerçek Chromium+React+ASP.NET+PostgreSQL ile
TR/EN×390/768/1440 yolculuğunu, farklı Hawaii/Kiritimati browser timezone'larını ve
sabit server tarihini dener. Sahte IdP/ephemeral ticket/anahtar; HTTPS ve redirect
transportu Playwright–stdio–TestServer köprüsüdür. Native TLS/gerçek IdP/private user
access bu koşularla doğrulanmaz. `responsive.cjs` eski ekran/klavye regresyonudur.

```sh
dotnet test tests/Backend/Poseidon.Backend.Tests.csproj
npm --prefix src/Frontend test
npm --prefix src/Frontend run typecheck
npm --prefix src/Frontend run build
```

BFF browser testi için önce ayrı `VITE_ENABLE_BFF=true` dist; ardından
POSEIDON_BFF_BROWSER_DIST ve ayrılmış POSEIDON_TEST_POSTGRES gerekir. Kurulu SDK,

## lines:81-85

NuGet/Node/Chromium'u yeniden kurmayın. Ortam sırları veya DB arşive dahil değildir.

G1–G7 (catalogues, evidence, verification authority, survey/renewal, readiness/PSC,
notifications/offline, gerçek deployment/UAT) ADR'de açık tutulur. İlk idari register
ve tarih takibi, yedi ekranlı tam sertifika/sörvey veya bütün ürün tamamlandı demek değildir.
