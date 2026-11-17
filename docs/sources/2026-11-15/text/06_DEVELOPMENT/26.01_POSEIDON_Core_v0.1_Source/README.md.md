# README.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/README.md
SHA256: 5fd885f723a9c81b3cdf1269cc6919ff6dde0582e11c7f81c18e6d4c74434770

## lines:1-40

# POSEIDON geliştirme çekirdeği 0.1

**Dağıtım hedefi:** Kurumun kendi sunucusunda (on-premises/self-hosted), tarayıcıdan kullanılan web uygulaması; bulut barındırma zorunlu değildir. Kapsam ve açık doğrulamalar: [Dağıtım hedefi](docs/DEPLOYMENT_TARGET.md).

Bu klasör ilk gerçek uygulama kaynak kodunu içerir. Önceki 196 ekranlı HTML prototipinin yerine tamamlanmış ürün olarak sunulmaz. Bu sürümün kapsamı, yetki sınırları içinde gemi/römorkör ve sertifika **metadata taslaklarını oluşturma, listeleme, yeniden açma ve güncellemedir**.

## Güncel doğrulama kanıtı

27 Ekim 2026 doğrulaması, `fc4c9858205a02c8ba3d41b5495d525a73bb4a69` tabanı ve iki dosyalık tarayıcı `fetch` alıcısı düzeltmesi üzerinde yapıldı: gerçek PostgreSQL ile **37 backend testi PASS, 0 SKIP**; **33 frontend testi PASS**; typecheck/build PASS; gerçek API ve PostgreSQL bağlantılı headless Chromium'da **12 kontrol grubu PASS**. Bunlar yalnız ilk metadata taslak diliminin sentetik geliştirme kontrolleridir; bütün ürünün veya üretimin kabulü değildir.

Güncel kapsam ve sınırlar: [27 Ekim doğrulama raporu](docs/VALIDATION_2026-10-27.md) ve [makine tarafından okunabilir sonuç özeti](docs/TEST_RESULTS_SUMMARY.json). `VERIFICATION.md` adlı mevcut kayıtlar tarihsel anlık görüntüler olarak korunur; bu kayıtlardaki eski servis/tarayıcı engelleri yalnız yeni raporda gerçekten sınanan geliştirme kapsamı için aşılmıştır. Dağıtım hedefi belgesindeki önceki servis/tarayıcı bekleme ifadesi de yalnız bu dar geliştirme kapsamı için güncellenmiştir; on-prem hedefi ve üretim kabul kapıları değişmez. Bu paketleme sırasında testler yeniden çalıştırılmadı.

## Neler var?

- ASP.NET Core backend; PostgreSQL üzerinde taslak, sürüm, idempotency ve audit kayıtları.
- React ve TypeScript arayüz; Türkçe/İngilizce görünüm, doğrulama ve çakışma yönetimi.
- Tenant, şirket ve gemi erişim sınırları. Kullanıcının gönderdiği tenant başlığına güvenilmez.
- Eksik zorunlu bilgilerle taslak saklama. Belirsiz Yes/No varsayılanları üretilmez.
- Onay/gönderim işlemleri açıkça engellenir. Taslak kaydetmek sertifikanın geçerli veya geminin operasyona hazır olduğu anlamına gelmez.
- Gerçek OIDC/OAuth2 access token doğrulama altyapısı ve yalnız açıkça etkinleştirilmiş Development ortamında sentetik test oturumu.
- Otomatik testler, bağımlılık kilitleri ve kapsamı açık doğrulama kayıtları.

Silme, belge dosyası yükleme, operasyon serbest bırakma, mevzuat kararı, sertifika doğrulama, AI işlemleri ve çevrimdışı senkronizasyon bu ilk dilimde yoktur. Üretim IdP bağlantısı ve tam kullanıcı giriş/yenileme akışı henüz yapılandırılmamıştır.

## Dosya haritası

| Yol | İçerik |
|---|---|
| `Poseidon.sln` | Backend ve test projeleri |
| `src/Backend/` | API, doğrulama, veri erişimi, migration ve sentetik kimlik |
| `src/Backend/docs/API_CONTRACT.md` | Gerçekleştirilen ilk dilimin HTTP sözleşmesi |
| `src/Backend/README.md` | Backend yapılandırması, sınırlar ve geliştirme kararları |
| `src/Frontend/` | Arayüz, istemci API katmanı ve frontend testleri |
| `src/Frontend/README.md` | Arayüz çalıştırma, sürümler ve resmî teknoloji kaynakları |
| `src/Frontend/VERIFICATION.md` | Frontend kontrol sonuçları ve sınırları |
| `tests/Backend/` | Birim, kimlik ve gerçek PostgreSQL testleri |

## Gereksinimler

- .NET SDK 10.0.403 veya aynı özellik bandında uyumlu güncel kararlı yama (`global.json`).

## lines:41-80

- Node.js 24; güncel 27 Ekim çalışma zamanı doğrulamasında 24.19.0 kullanıldı (tarihsel kaynak kontrolünde 24.22.1).
- PostgreSQL 17 veya üstü; güncel çalışma zamanı doğrulamasında 17.11 kullanıldı. Önceki SQL tek-kullanıcılı kontrolü 18.6 üzerinde tarihsel kanıttır.
- Yalnız sentetik veri için ayrı geliştirme veritabanı ve `poseidon_test` veya `poseidon_test_*` adında ayrı test veritabanı.

Araçları yalnız resmî kaynaklarından edinin: [.NET](https://dotnet.microsoft.com/download/dotnet/10.0), [Node.js](https://nodejs.org/en/download), [PostgreSQL](https://www.postgresql.org/download/). Bu kaynak paketi kurulum dosyası, veritabanı dosyası, node_modules veya gerçek parola içermez.

## 1. Kaynağı doğrula

Komutlar bu README'nin bulunduğu dizinde çalıştırılır:

```sh
dotnet restore Poseidon.sln --locked-mode --disable-parallel -m:1
dotnet build Poseidon.sln --no-restore -m:1
dotnet test tests/Backend/Poseidon.Backend.Tests.csproj --no-restore -m:1
npm --prefix src/Frontend ci
npm --prefix src/Frontend test
npm --prefix src/Frontend run typecheck
npm --prefix src/Frontend run build
```

`POSEIDON_TEST_POSTGRES` verilmemişse gerçek veritabanı testleri **SKIPPED** olur; bu bir başarı sonucu değildir. Test veritabanına bağlantı sağlandıktan sonra aynı test komutu tekrar çalıştırılmalıdır. Testler şema ve sentetik kayıtlar oluşturabilir; müşteri veya üretim veritabanı kullanmayın.

## 2. Yerel sentetik geliştirme oturumunu çalıştır

Önce kendi PostgreSQL kurulumunuzda boş bir `poseidon_dev` veritabanı hazırlayın. Aşağıdaki bağlantı değerleri şablondur; gerçek parolayı bu dosyaya yazmayın veya kaynak kontrolüne eklemeyin.

```sh
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__Poseidon='Host=127.0.0.1;Database=poseidon_dev;Username=YOUR_LOCAL_USER;Password=YOUR_LOCAL_PASSWORD'
export SyntheticIdentity__Enabled=true
export Database__Initialize=true
dotnet run --project src/Backend/Poseidon.Backend.csproj --urls http://127.0.0.1:5080
```

Yeni terminalde:

```sh
VITE_ENABLE_DEV_SESSION=true POSEIDON_API_URL=http://127.0.0.1:5080 npm --prefix src/Frontend run dev
```

## lines:81-98

Arayüzün terminalde gösterdiği yerel adresi açın. Sentetik A ve B oturumları gerçek müşteri hesapları değildir. Test oturumları servis yeniden başladığında geçersizleşir; taslak verileri PostgreSQL'de kalır. Development sunucularını internete açmayın.

Production ortamında sentetik kimlik ve otomatik şema başlatma etkinleştirilemez. Üretim için gerçek IdP, yetki politikaları, HTTPS, kısıtlı veritabanı rolü, kontrollü migration ve bağımsız güvenlik incelemesi gerekir.

## 3. Gerçek veritabanı testleri

```sh
export POSEIDON_TEST_POSTGRES='Host=127.0.0.1;Database=poseidon_test;Username=YOUR_TEST_USER;Password=YOUR_TEST_PASSWORD'
dotnet test tests/Backend/Poseidon.Backend.Tests.csproj --no-restore -m:1
```

Bu testler farklı tenant/şirket/gemi kapsamlarını, tekrar gönderimleri, eski sürümle güncellemeyi, eşzamanlı yazmaları ve audit hatasında geri almayı sınar. Çalıştırılmaları tamamlanmadan API–PostgreSQL entegrasyonu doğrulanmış sayılmaz.

## Doğrulama sınırı

Bu teslimde kaynak derlemesi, birim ve in-process kimlik kontrolleri ile frontend testleri ayrı raporlanır. Frontend testlerindeki HTTP taklitleri test dosyalarındadır; uygulamada sahte API veya tarayıcı depolama yedeği yoktur. Gerçek PostgreSQL servis bağlantısı ve 12 headless Chromium kontrol grubu, 27 Ekim tarihli raporda yalnız ilk sentetik geliştirme dilimi için doğrulanmıştır. Tam manuel görsel/erişilebilirlik/mobil/çoklu tarayıcı incelemesi; üretim OIDC/HTTPS dağıtımı; veritabanı/host yeniden başlatma ve yedekten dönüş; operasyonel ve alan uzmanı kabulü henüz yapılmamıştır. Ayrıntılı sonuçlar ve tarihsel kanıtların sınırı için [güncel raporu](docs/VALIDATION_2026-10-27.md) okuyun.

Ürün durumu geliştirme çekirdeğidir; FINAL, CERTIFIED veya PRODUCTION READY değildir.
