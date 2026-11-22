# Yerel uygulama geliştirme

Gerekli araçlar: .NET SDK 10.0.403, Node.js 26.13.0 LTS, npm (Node ile), PostgreSQL integration testleri için çalışan Docker. Lock dosyaları kullanılır; `--force` / `--legacy-peer-deps` kullanılmaz.

Repo kökünden:

```powershell
dotnet tool restore
dotnet restore Poseidon.slnx --locked-mode
dotnet build Poseidon.slnx --configuration Release --no-restore
npm ci --prefix frontend
npm ci --prefix tests/Poseidon.Browser.Tests
npm run generate:api --prefix frontend
```

Bu makinedeki proje içi SDK gerekiyorsa komutlardan önce `$env:DOTNET_ROOT = "$PWD/.tools/dotnet"` ve `$env:PATH = "$env:DOTNET_ROOT;$PWD/.tools/node/node-v26.13.0-win-x64;$env:PATH"` ayarlayın. Genel makine veya iş hesabı ayarlarını değiştirmeyin.

API'yi ayrı terminalde başlatın:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project backend/Poseidon.Api --configuration Release --no-build --no-launch-profile --urls http://127.0.0.1:5080
```

Başka terminalde `npm run dev --prefix frontend`; tarayıcı adresi http://127.0.0.1:5173. Vite `/api` isteklerini yerel API'ye yönlendirir. Production reverse proxy/deployment bu paketin kapsamı değildir. Public bağlantı kontrolü herhangi bir kullanıcı/tenant verisi sunmaz. IAM ve kullanıcı eylem audit'i tamamlanmış değildir.

Kontroller:

```powershell
dotnet test --project tests/Poseidon.Persistence.Tests/Poseidon.Persistence.Tests.csproj --configuration Release --no-build --filter-not-trait 'Category=Integration'
dotnet test --project tests/Poseidon.Domain.Tests/Poseidon.Domain.Tests.csproj --configuration Release --no-build

dotnet test --project tests/Poseidon.Persistence.Tests/Poseidon.Persistence.Tests.csproj --configuration Release --no-build --filter-trait 'Category=Integration'

dotnet test --project tests/Poseidon.Api.Tests/Poseidon.Api.Tests.csproj --configuration Release --no-build --filter-trait 'Category=Integration'
dotnet ef migrations has-pending-model-changes --context PoseidonDbContext --project backend/Poseidon.Persistence --startup-project backend/Poseidon.DbMigrator --configuration Release --no-build
py -m unittest discover -s scripts -p 'test_*.py' -v
npm run lint --prefix frontend
npm run typecheck --prefix frontend
npm test --prefix frontend
npm run build --prefix frontend
```

Playwright öncesinde manuel API/UI süreçlerini kapatın; test kendi iki sunucusunu başlatır ve var olan sunucuyu kullanmaz. İlk kurulumda `cd tests/Poseidon.Browser.Tests; npx playwright install chromium` çalıştırın, sonra repo kökünden `npm test --prefix tests/Poseidon.Browser.Tests`.

Browser suite desktop ve dar mobil viewport'ta Chromium çalıştırır; ilk yolculuk gerçek API, ikinci yolculuk yapay ağ kesintisi ardından gerçek API'ye yeniden bağlantıdır. Bu suite Ship/Tug operasyon regresyonu veya Safari/native mobil kanıtı değildir. Rapor `tests/Poseidon.Browser.Tests/playwright-report/`, hata trace/screenshot'ları `test-results/` altındadır.

### Kullanım kılavuzu (USER-GUIDE-01 UG-4)

Resimli TR/EN kullanım kılavuzu commit edilmez; commit edilmiş sayfa kılavuzlarından (`frontend/src/help/content/{tr,en}/*.md`), kılavuz bölümlerinden (`frontend/src/help/content/{tr,en}/_manual/*.md`) ve UG-2 ekran görüntülerinden (`frontend/src/help/screenshots/`) üretilir:

```powershell
npm ci --prefix tests/Poseidon.Browser.Tests
npm run manual --prefix tests/Poseidon.Browser.Tests       # yalnız HTML
npm run manual:pdf --prefix tests/Poseidon.Browser.Tests   # HTML + PDF (kurulu Playwright Chromium ile)
node --test tests/Poseidon.Browser.Tests/manual/manual.test.ts
```

Çıktı `.runtime/user-manual/poseidon-user-manual-{tr,en}.{html,pdf}` dosyalarıdır (`--out <klasör>`, `--locale tr|en` seçenekleri vardır). HTML tek dosyadır ve ağsız açılır (CSS gömülü, görüntüler data URI); aynı girdilerden bayt bayt aynı çıkar. PDF yazdırma CSS'iyle A4 basılır, sayfa numarası ve PDF ana hatları içerir (PDF bayt düzeyinde deterministik değildir: Chromium oluşturma zamanı yazar). Kılavuz `reviewed:false` olduğu sürece her sayfada TASLAK bandı vardır; insan SME/UX onayı (UG-REV-01) gelmeden onaylı denmez.

MTP'de bütün testleri Integration olan API projesi unit filtresinde sıfır test üretir ve hata verir. Unit aşaması bu nedenle unit test projesini açıkça hedefler; integration aşaması iki projeyi kapsar. Sıfır-test başarısı veya skip istisnası tanımlanmaz.
