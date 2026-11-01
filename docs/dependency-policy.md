# Güncel sürüm politikası

Kullanıcı tercihi: yeni geliştirmelerde güncel **kararlı** ve karşılıklı uyumlu sürümler. Preview/RC sürümleri varsayılan değildir. Bu karar React 18/PostgreSQL 17+ başlangıç baseline'ından kontrollü teknik güncellemedir; iş kapsamını değiştirmez.

30 Ekim 2026 tarihinde resmi release metadata, NuGet ve npm registry üzerinden doğrulanan sürümler:

| Bileşen | Doğrulanan kararlı sürüm | Projedeki durum |
|---|---|---|
| .NET SDK | 10.0.403 | global.json; yerel .tools/dotnet kurulumu |
| EF Core / dotnet-ef | 10.0.14 | Central Package Management + local tool manifest |
| Npgsql EF sağlayıcısı | 10.0.3 | EF Core 10 ile uyumlu |
| PostgreSQL | 18.6 | Integration container |
| React / React DOM | 19.3.1 | Frontend, exact pin + npm lock |
| TypeScript | 7.0.2 latest; **5.9.3 uygulandı** | typescript-eslint <6.1 ve openapi-typescript ^5.x peer sınırlarının en güncel uyumlu kesişimi; peer kontrolleri kapatılmadı |
| Vite | 8.3.5 | Frontend exact pin |
| Node.js LTS | 26.13.0 | Node registry/dist metadata; yerel arşiv SHA256 doğrulandı; CI aynı sürüm |
| Playwright | 1.64.1 | Desktop/mobile Chromium smoke, exact pin |
| ASP.NET Core OpenAPI / Mvc.Testing | 10.0.14 | Gerçek API host ve contract testleri |
| xUnit v3 | 4.0.2 | Backend test projesi |
| Microsoft.NET.Test.Sdk | 18.11.0 | Backend test projesi |
| Testcontainers.PostgreSql | 4.16.0 | Gerçek PostgreSQL integration testleri |

Sürümler sabitlenir ve lock dosyaları Git'e alınır; her restore'da kontrolsüz `latest` kullanılmaz. Güncel kalmak kontrollü güncelleme PR'ları, build/test ve gerekiyorsa migration/contract değerlendirmesiyle sağlanır. Major güncellemeler de değerlendirilir; destek ve uyumluluk nedeniyle bekletilirse ADR'de gerekçe yazılır. Otomatik merge ancak gerekli testler ve bağımsız inceleme geçince yapılabilir.

Renovate yapılandırması repoda bulunur; bot/app bağlantısı kurulmadan kendiliğinden PR açmaz. Güncelleme kontrolü ve Graphify Python bağımlılıklarının pin/upgrade süreci de CI/otomasyon kurulumu sırasında kapsanmalıdır.

Resmi kaynaklar:

DEV-OIDC-LAB-01 development fixture verification (2026-11-01): official [Keycloak downloads](https://www.keycloak.org/downloads) reports stable26.8.1. The local Docker pull verified `quay.io/keycloak/keycloak:26.8.1@sha256:3f3c5f8e03c614774b4bd9173b88391fefa4153bfc010d8012d59ad825377332`; PostgreSQL18.6 repository digest `sha256:979bccfde35445db618c65107d27d08c6c21339d8dc3ad4c830c6633947be0e4` is also pinned in the explicit lab compose. No floating image tag or new package dependency. This fixture does not select a production IdP. Update pins only with fresh official verification and real browser/API regression evidence.

- https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json
- https://api.nuget.org/v3-flatcontainer/microsoft.entityframeworkcore/index.json
- https://api.nuget.org/v3-flatcontainer/npgsql.entityframeworkcore.postgresql/index.json
- https://www.postgresql.org/support/versioning/
- https://registry.npmjs.org/react/latest
- https://registry.npmjs.org/typescript/latest
- https://registry.npmjs.org/vite/latest
