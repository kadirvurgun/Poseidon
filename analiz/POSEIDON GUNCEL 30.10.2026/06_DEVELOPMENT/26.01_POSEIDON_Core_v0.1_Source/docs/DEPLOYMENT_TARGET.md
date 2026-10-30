# Dağıtım hedefi: kurum içi sunucu ve tarayıcı

## Onaylanan yön

POSEIDON, kurumun kendi sunucusunda çalıştırılabilen (on-premises/self-hosted), kullanıcıların web tarayıcısıyla eriştiği bir uygulama olarak geliştirilecektir. Bulut barındırma zorunlu değildir. Bulut çalışma alanı yalnız geliştirme/test, özel GitHub deposu yalnız kaynak kontrolü içindir; kaynak yüklemek uygulamayı internette yayımlamaz.

## Mevcut kodla uyum

- React arayüzü zaten bir tarayıcı istemcisidir; API çağrıları aynı kökendeki `/api/v1` yoluna gider.
- ASP.NET Core API ve PostgreSQL kurum sunucusunda veya kurumun yönettiği ayrı iç sunucularda çalıştırılabilir. Hedef kurulumda derlenmiş arayüz ile API aynı HTTPS kökeninden, uygun web sunucusu/ters vekil üzerinden sunulmalıdır; bu dağıtım yapılandırması henüz kurulup doğrulanmadı.
- Üretim kimliği için yapılandırılabilir OIDC sağlayıcısı gerekir. Bu sağlayıcı da kurum içinde barındırılabilir; kod belirli bir bulut kimlik hizmetine bağlı değildir. HTTPS metadata, audience ve kurum yetki claim'leri ayrıca yapılandırılmalı ve doğrulanmalıdır.
- Paket indirme ve güvenlik denetimlerinde kullanılan npm/NuGet bağlantıları geliştirme/derleme bağımlılıklarıdır; uygulama verisini buluta taşıyan bir çalışma zamanı hizmeti değildir. Tamamen ağdan yalıtılmış kurulum için paket/araç tedarik süreci ayrıca hazırlanmalıdır.

## Henüz tamamlanmayanlar ve sınırlar

Bu belge hedef mimariyi belirtir; üretime hazır kurulum veya tamamlanmış saha testi iddiası değildir. Gerçek PostgreSQL servis entegrasyonu, canlı tarayıcı kabulü, üretim OIDC giriş/yenileme akışı, HTTPS/ters vekil, yedekleme-geri yükleme ve güvenlik/iş kabulü açık doğrulama adımlarıdır.

Tarayıcı kullanımı bulut kullanımı anlamına gelmez; ancak istemcinin kurum sunucusuna erişmesi gerekir. Bağlantısız gemi kullanımı, çevrimdışı kayıt ve gemi-kara senkronizasyonu bu sürümde uygulanmadı. Tarayıcıda kalıcı veri yedeği yoktur.

Sentetik oturumlar yalnız açıkça etkinleştirilmiş Development ortamına aittir; üretimde kullanılmaz. Gerçek parolalar, erişim belirteçleri, müşteri verileri ve veritabanı dosyaları kaynak deposuna eklenmez. Bu çalışma herhangi bir bulut yayını veya gerçek müşteri verisi kullanımına izin vermez.
