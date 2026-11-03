# Güncel sürüm politikası

## ENTERPRISE-WORKSPACE-01 — 02.11.2026, authored / preview build only

User-approved React/shadcn/ui/ECharts design work retains React19.3.1 and Vite8.3.5.
Official npm stable metadata and peer ranges were inspected; exact versions and
the npm lock are committed-source candidates:

| Dependency | Exact version |
|---|---|
| @radix-ui/react-slot | 1.5.0 |
| class-variance-authority | 0.7.1 |
| clsx | 2.1.1 |
| tailwind-merge | 3.7.0 |
| lucide-react | 1.66.0 |
| echarts | 6.1.0 |
| tailwindcss / @tailwindcss/vite | 4.3.3 / 4.3.3 |

Manual shadcn/ui component source is owned in the frontend; no shadcn runtime
package is required. Pinned npm install and requested-preview TypeScript/Vite
build succeeded without force/legacy peer overrides. Full regression, lint,
security/dependency review and independent QA are **DEFERRED**, not compatibility
acceptance. Install reported13moderate advisories; identities/fixes await that
gate. ECharts is imported modularly into a lazy SVG-renderer chunk; the main
application chunk remains above500kB and needs later bundle review.

Primary metadata: [npm registry](https://registry.npmjs.org/),
[shadcn manual installation](https://ui.shadcn.com/docs/installation/manual),
[ECharts modular imports](https://echarts.apache.org/handbook/en/basics/import/).

## Earlier dependency checkpoints

FILES-SOURCE-COMPLETION-01 authors official AWSSDK.S3 **4.0.104.8** and resolved
AWSSDK.Core **4.0.102.14** using the official NuGet flat-container index/nuspec/package
bytes on2026-11-01. Central version and both API/consumer locks include package SHA512.
The compatible net8 asset is selected for the net10 target; actual locked restore, SDK/API
compatibility and tests are **DEFERRED**, not established by metadata. No provider was set up.
Primary references: [NuGet index](https://api.nuget.org/v3-flatcontainer/awssdk.s3/index.json),
[official SDK PutObject](https://docs.aws.amazon.com/sdkfornet/v4/apidocs/items/S3/TPutObjectRequest.html),
[official GetObject version/checksum contract](https://docs.aws.amazon.com/AmazonS3/latest/API/API_GetObject.html).
The owned S3 lab accepts only an explicitly reviewed local source-built immutable image;
image selection/build/pinning and execution remain a later gate input, not a production vendor choice.

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

## SECURE-FILES-01 authoring — NOT EXECUTED

No SDK/library dependency or lock changes. A gate-only official ClamAV container is pinned to `clamav/clamav:1.5.4@sha256:bda39705e291105049c3936e2803ac90b955172af8030f94f05dce1252df6c6d`. Official registry stable/1.5.4 metadata matched this digest on2026-11-01. Protocol/image docs: https://docs.clamav.net/manual/Usage/ClamdProtocol.html and https://docs.clamav.net/manual/Installing/Docker.html . This is a development/CI integration fixture, not a production scanner choice or executed verification. No image was pulled or started; compatibility and real signature tests remain DEFERRED to AUDIT-FILES-BUNDLE-01.
