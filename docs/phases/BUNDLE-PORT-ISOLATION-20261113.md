# Sahipli runtime port izolasyonu — 13.11.2026

Durum: **SOURCE_AUTHORING**, acceptance=false. Kullanıcı kesintisiz geliştirme
ve faz tamamlandığında PR/merge istedi. Merge için aynı hedefin gerekli CI ve
bağımsız QA koşulları geçerlidir. Yeni paket yazılırken test, build ve browser
yürütmeleri **DEFERRED**; kaynak test vakaları yürütülmüş sonuç değildir.

## Gerekçe ve kapsam

Gerçek imzalı observation/browser kapısının mevcut laboratuvarı API5443,
provider8443 ve DB55432 kullanıyordu. İkinci laboratuvar bu portlarla açık
önizlemeyle birlikte çalışamaz. Yeni görev her sahipli çalıştırma için açık,
birbiriyle uyumlu API/provider/database portlarını değişmez bir referansa bağlar.
Eski varsayılanlar korunur; mevcut preview, private profil ve DB değiştirilmez.

Kaynaklar: DEV-OIDC-LAB-01 LAB01-LAB05; OBSERVATION-SIGNED-FIXTURE-01 koşullar1–6;
aktif19.01 paragraflar26/29/30; [otomasyon](../automation.md) ve
[güvenlik temeli](../security-baseline.md). Bu mühendislik görevi ek canonical
story point, onaylı üretim kaynağı veya operasyonel güvenlik kararı üretmez.

## Yazılan backend alt kapsamı

Kaynak commit `06e91936b3ea5b5f75105a7cdf897fd1756de07b`, receiving merge
`ae341862db7e91913e19309ad63ffbd3704cbb94`.
[Backend görev sözleşmesi](../tasks/BUNDLE-PORT-ISOLATION-BACKEND-01.md).

- Açık `DevelopmentLab:OidcPort` yalnız canonical1024..65535 tamsayısını kabul
  eder. Yokluğu mevcut8443 varsayılanını korur; bozuk veya null seçim reddedilir.
- Yerel CA ve port seçimi Development sınırındadır; issuer/authority tam aynı
  localhost HTTPS realm'i ve seçilen portu kullanır.
- Backchannel doğrudan IPv4loopback'e bağlanır. Proxy, redirect ve cookie yok;
  istek hedefi, realm, query/fragment/userinfo ve Host başlığı denetlenir.
- CA, hostname, EKU ve süre doğrulaması korunur. Production için TLS gevşetmesi yok.
- 60 yeni vaka yazıldı; statik kaynak envanteri71. **Çalıştırılan yeni vaka0**.

## Paralel geliştirme ve doğrulama

Python/TypeScript ortak beş alanlı owned-runtime referansı, immutable generation
bağı, compose/driver/provisioner ve browser origin bağlantıları ayrı branch'te
yazılıyor. Tüm portların sahipliği ve çakışması mutasyondan önce denetlenmeli;
farklı çalıştırma veya generation açıkça reddedilmeli. Gerçek fixture'lar ve
oturum/yetki/receipt sınırları korunmalı. Bağımsız QA kaynak ve test sözleşmesini
inceliyor; bütünleşik doğrulama henüz başlamadı.

[Önceki yerel native paket](OBSERVATION-SIGNED-FIXTURE-20261112.md) kendi5e/8a
hedefinde1524ön yüz,1007backendunit,79PG,837API ve117kritik kapsam ile geçti.
Bu sonuç yeni port backend kaynağının PASS kanıtı değildir. Yeni kaynak
tamamlanıp bağımsız incelendikten sonra hedef sabitlenir; anlamlı protokol,
backend/TLS, gerçek isolated runtime/browser ve gerekli regresyon kapıları
çalışır. Ham private/native değerler raporlara alınmaz.

Sonraki adım Python/TS kaynaklarını tamamlamak, birleşik hedefi incelemek ve
gerçek kapıyı çalıştırmaktır. Yeni runtime **NOT_STARTED**; yeni PR/push/merge yok.
Canonical kabul **20/121 iş,103/970SP=%10,62;0/196 ayrı ekran** olarak değişmedi.
