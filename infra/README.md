# Yerel çalışma ve ortam sınırı

APP-FOUNDATION-01 UI/API bağlantı iskeleti kalıcı veritabanına bağlanmaz. HTTP host migration veya demo seed çalıştırmaz. PostgreSQL integration testleri Docker'da ayrı geçici container kullanır; uygulama verisini değiştirmez.

İlerideki DEV/TEST/UAT/PILOT URL, veri lokasyonu, secrets ve retention kararları DEV-003 kapsamında açık kalır. Bu klasör bir production deployment veya tamamlanmış ortam sözleşmesi değildir.

Migrations ve idempotent sentetik seed için [seed kuralları](../docs/seed-data.md); .NET/React başlatma ve test komutları için [geliştirme rehberi](../docs/development.md).
