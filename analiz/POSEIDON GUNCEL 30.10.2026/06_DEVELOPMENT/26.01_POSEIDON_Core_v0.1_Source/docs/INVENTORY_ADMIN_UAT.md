# Esra UAT — İdari gemi/römorkör envanteri

Durum: **USER_ACCEPTANCE_PENDING / PRIVATE_ACCESS_NOT_ESTABLISHED**.
Otomatik teknik PASS, kullanıcı kabulü veya kullanıcının açabildiği test adresi değildir.
Bu paket idari envanterdir; bütün düzenleyici gemi yönetimi/v46.3 ürünü değildir.
Esra sonucu ve kabul tarihi yalnız kullanıcının gerçek testinden sonra kaydedilecektir.

## Tekrarlanabilir sentetik fixture

Var olan özel Development API / PostgreSQL kurulumunu kullanın; ortamı bu belgeyle
oluşturmayın veya internete açmayın. README'deki Development sentetik ayarları dışında
üretim için fallback yoktur. `inventory-manager` yalnız sentetik Development fixture'ı;
gerçek hesap/IdP veya geniş şirket görünürlüğü sağlamaz. Eski tenant-a yalnız draft yazarıdır.

Mevcut loopback Development API çalışırken:

```sh
node tests/Backend/inventory-uat.cjs
```

Betik bir eksik taslak ve IMO'suz, optional profile alanları dolu kayıtlı sentetik tug
oluşturur; yalnız UUID/durum raporlar, token yazdırmaz. Yeniden çalıştırma yeni sentetik
kayıtlar üretir; gerçek müşteri ortamında kullanılmaz. API loopback ile sınırlıdır.
Frontend Development sentetik listesinde `Inventory manager (synthetic)` seçilip
**İdari envanter** sekmesi açılır. Bu localhost talimatı Esra'ya verilmiş erişilebilir
Android/web URL'si değildir. BFF modunda bu seçim görünmez.

Yetkili salt-okunur, izin iptali, farklı issuer ve şirket/tenant senaryoları mevcut
C# TestServer fixture'larında tekrarlanır; gerçek kullanıcı grant'i oluşturulmaz.
Gerçek özel UAT adresi sağlandığında bu roller test ortamının yetkili fixture yönetimi
ile sunulmalıdır; şu anda kullanıcıya açılmış rol/hesap veya URL yoktur.

## Beklenen manuel senaryolar — tüm Esra sonuçları BEKLİYOR

| No | İşlem | Beklenen |
|---|---|---|
| U01 | İsimsiz/modsuz/IMO'suz taslak oluştur, yeniden aç | Boşlar kalır; şirket sunucudan; eksik idari alanlar açık; otomatik SHIP yok |
| U02 | Ad/bayrak/alt tip + SHIP doldur, kaydet, Envantere al | Aynı UUID, RECORDED; operasyon/mevzuat onayı yok |
| U03 | TUG seç, IMO olmadan kayda al | Başarılı; tip/BP/sertifikalı kapasite uydurulmaz |
| U04 | Registry/profile/class/operator/contact/reference/not alanlarını kaydet | Sayı/birimler ve sıfır korunur; metin referans, atama/kanıt doğrulaması değildir |
| U05 | Kayıtlı metadata düzelt | Manager düzeltir; zorunlu alan silemez; mode kilitli; draft-only kullanıcı sunucuda403 |
| U06 | Gerekçeyle yeniden aç, mode değiştir, yeniden kayda al | Aynı gemi/certificate bağlantısı; sürüm/event artar; gerekçe tarihçede yok |
| U07 | Yetkili readonly ve aynı şirket erişimsiz okuyucu | İlki detail/history; yazılar403. İkincisi listede yok; nesne/history404 |
| U08 | Tenant/company/mode/state/cursor/CSRF/scope manipülasyonu | Yetki kazanılmaz; geçersiz girdi/oturum reddedilir |
| U09 | İki editör, eski If-Match, aynı/farklı key, ağ kesintisi |412 / exactly-once /409; izin iptalinde replay yok; ağ hatasında sahte başarı yok |
| U10 | History HTTP yanıtını incele | Yalnız eventId/time/version/action/alan adları/SELF-OTHER; gizli değer/actor/reason yok |
| U11 | Eski draft istemcisiyle v1/v2/RECORDED dene | v1 çalışır; v2 yazı409; RECORDED read/write409; submit403; certificate bağlantısı çalışır |
| U12 | TR/EN,390/768/1440; dirty iptal, geri dönüş, expiry/logout/relogin | Değişiklikler onaysız atılmaz; eski scope yanıtı gelmez; otomatik yazı retry yok |
| U13 | Eski DB fixture'ını migrate et | Kimlikler/audit/idempotency/sahiplik korunur; hepsi DRAFT; eski UPDATE v2'yi bozamaz |
| U14 | Exact build + özel tarayıcı erişimiyle Esra testi | Ancak gerçek bulgu/retest ve açık kullanıcı onayından sonra USER_ACCEPTED |

Hata raporu: build arşiv SHA'sı, adım, dil/viewport, sentetik kayıt UUID'si,
beklenen ve görülen davranış. Token, gerçek kullanıcı/veri veya screenshot yüklemeyin.

## Otomatik tekrar çalıştırma

Kurulu SDK/NuGet/Node ortamını tekrar kurmayın. POSEIDON_TEST_POSTGRES yalnız ayrılmış
sentetik PostgreSQL için operatör tarafından sağlanır; gerçek DB veya sır arşivde yoktur.

```sh
dotnet test tests/Backend/Poseidon.Backend.Tests.csproj
npm --prefix src/Frontend test
npm --prefix src/Frontend run typecheck
npm --prefix src/Frontend run build
```

Tam BFF Chromium senaryosu ayrıca `VITE_ENABLE_BFF=true` ile ayrı dist üretir;
`POSEIDON_BFF_BROWSER_DIST` bu dist'i, `POSEIDON_BFF_BROWSER_REPORT` çıktı JSON'unu gösterir.
`BffBrowserTests` mevcut Playwright/Chromium'u kullanır. In-process fake IdP, ephemeral
keys/tickets ve gerçek PostgreSQL vardır. HTTPS Playwright→stdio→TestServer köprüsünde
sanaldır; native TLS/gerçek IdP/kurum private access kanıtı değildir.

`tests/Frontend` yerine frontend mevcut `src/Frontend/tests/responsive.cjs` betiği
legacy ekranların altı viewport/dil regresyonunu ayrıca çalıştırır. Görüntüler teslim
edilmez. Cihaz/ekran okuyucu/offline/tam WCAG kabulü ayrıca bekler.

### Ek hata ve çıkış kontrolleri

- Oluşturma sunucuda commit olduktan sonra yanıt kaybolsun; draft:write iptalinde
  aynı anahtarlı retry403 alsın. Ekran yeni create'e açılmaz. İzin geri gelince aynı
  anahtar tek kayıt/tek event'i getirir. Bu durum gerçek DB'li BFF tarayıcı fixture'ında
  kontrollü response-loss ve test-only grant değişimiyle tekrarlanır.
- Çıkışın CSRF veya logout yanıtını geciktirin. Save/Record/Reopen düğmeleri ve
  handler'ları yeni yazı başlatmamalı. Önceden başlayan yazı için çıkışın o yazıyı
  iptal ettiğini söylemeyin; gerçek sonucunu bekleyin.
- İlk oluşturma beklerken oturum sona ersin. Ekran başarı göstermez, özgün anahtarı
  korur. Yeniden giriş/ayrılma uyarısı olası commit'i açıklar; Esra sonucu BEKLİYOR.
