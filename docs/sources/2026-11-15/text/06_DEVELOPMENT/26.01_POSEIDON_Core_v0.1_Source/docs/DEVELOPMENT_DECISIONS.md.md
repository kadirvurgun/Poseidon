# DEVELOPMENT_DECISIONS.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/docs/DEVELOPMENT_DECISIONS.md
SHA256: d14fe228579d38d74fee7f1bdc928e8950de3f7f54b878e8a368f4f24ac68776

## lines:1-40

# Geliştirme kararları — ilk dilim

Tarih: 27 Ekim 2026. Durum: geliştirme uygulaması; üretim onayı değildir.

## Yetki ve kapsam

Kullanıcı ürün sahipliği, analiz ve kodlama görevlerini devretti; güncel teknolojilerin kullanılmasına izin verdi. Resmî .NET ve PostgreSQL araçlarının yalnız bulut çalışma alanına kurulması ayrıca onaylandı. Bunlar üretim yayınına, ücretli hizmete veya gerçek müşteri verisine erişim izni değildir.

## DEV-STACK-001 — güncel kararlı sürümler

Eski kontrollü belgelerdeki React 18 uygulama başlangıç hedefi, son kullanıcı yönlendirmesiyle bu kod diliminde güncellendi. React 19.3.1, TypeScript 7.0.2, Vite 8.3.5 ve Tailwind 4.3.3 kullanıldı; paket sürümleri ve bütünlük değerleri kilit dosyasında tutulur. .NET SDK 10.0.403 / runtime 10.0.14 ve PostgreSQL 18.6 kuruldu. Önizleme, RC veya canary sürüm seçilmedi. Node 24.22.1 test edilen destekli çalışma ortamıdır.

Sürüm ve resmî kaynak kayıtları `src/Frontend/README.md`, backend proje/paket kilitleri ve doğrulama belgelerindedir. Geniş ürünün v46.3 analiz dosyaları, bu dar kod diliminin tamamlandığı veya üretimde çalıştığı iddiasını taşımaz.

## DEV-SCOPE-001 — operasyonel olmayan taslak kayıt

İlk dikey dilim, kimliği doğrulanmış tenant/şirket/gemi kapsamındaki gemi ve sertifika metadata taslaklarıdır. Veri katmanı gerçek PostgreSQL için yazılmıştır; uygulamada bellek veya tarayıcı depolaması kalıcı veritabanı yerine kullanılmaz. Eksik zorunlu bilgi taslakta saklanabilir; onay ve operasyona gönderim engellenir. Belirsiz Yes/No değerleri varsayılan olarak enjekte edilmez.

TugJob iş durumları, hazırlık değerlendirmesi, operasyon izni, sertifika doğrulaması ve mevzuat aktive etme işlemleri kapsam dışıdır. Önceki yedi TugJob durumu önerisi henüz kullanıcı onayı almadı ve bu kodla örtülü olarak kabul edilmiş sayılmaz.

## DEV-DATA-001 — tekil domain ve açık veri sınırı

Ekran başına tablo yerine paylaşılan taslak, şirket, idempotency ve audit kayıtları kullanılır. İlk uygulamada iç kimlikler UUID'dir; görünen iş numarası bu kimlik değildir. PostgreSQL erişimi parametreli Npgsql komutları üzerinden yürütülür. Bu fiziksel uygulama, 2.316 alanın tamamının fiziksel şeması olarak sunulmaz.

Güncelleme, beklenen sürümle koşullu yazılır; eşzamanlı eski sürüm sessizce üzerine yazılmaz. Yazı, audit ve idempotency sonucu aynı transaction içindedir. Silme ve veri saklama süreleri kararlaştırılmadan yıkıcı silme uç noktası eklenmedi.

## DEV-AUTH-001 — gerçek kimlik ve sentetik test ayrımı

Üretim yönü OIDC/OAuth2 bearer token doğrulamasıdır. Tenant/şirket/gemi kapsamı, doğrulanan kimlikten türetilir; istemcinin tenant başlığı yetki kaynağı değildir. Gerçek IdP ve kurum claim yönetimi henüz kurulmadı.

Yalnız açıkça etkinleştirilmiş Development ortamı sentetik fixture oturumlarını kullanabilir. Diğer ortamlarda bu seçenek açılırsa uygulama başlamaz. Paylaşılan sabit admin parolası veya üretim bypass'ı yoktur. Sentetik oturumların uygulama yeniden başladığında sona ermesi taslakların veritabanından silindiği anlamına gelmez.

## DEV-API-001 — kontrollü kaynakla izlenebilir uygulama

Kaynak aday sözleşmelerindeki `/api/v1/vessels`, `/api/v1/certificates` ve `saveMode: DRAFT` yaklaşımı korunur. Ortak sertifika alan adları `type`, `issue`, `expiry` olarak kaynakla eşitlenir. Ek metadata (`certificate_no`, `notes`), taslak ayrıntı/güncelleme ve gönderimi açıkça reddeden uç noktalar ilk dilimin belgelenmiş geliştirme uzantılarıdır. Mevcut tam ürün API taslağının bütünü uygulanmış değildir.

Gerçekleştirilen ilk dilimin kesin isteği, cevabı ve hata durumları `src/Backend/docs/API_CONTRACT.md` içindedir. Bu dosya ile global aday sözleşme arasındaki kapsam ayrımı korunmalıdır.

## DEV-VERIFY-001 — kanıtlar ve açık çalışma ortamı engeli

## lines:41-43

Backend derlemesi, in-process birim/kimlik testleri, frontend testleri ve üretim bundle derlemesi yürütüldü. Gerçek PostgreSQL motorunda soketsiz single-user SQL smoke, şema/trigger/transaction davranışını sınırlı kapsamda kontrol eder; Npgsql veya HTTP entegrasyonu değildir.

Mevcut ortam özel PostgreSQL soketi başlatmayı engelledi; standart izinli başlatma yolu da servis çalışmadan başarısız oldu. Gerçek veritabanı bağlantılı entegrasyon testleri ve canlı tarayıcı kabulü açık tutuldu. Bunların tamamlanması için uygun ve izinli çalışma ortamı gerekir. Test sayıları ve son sonuçlar ilgili `VERIFICATION.md` dosyalarındadır.
