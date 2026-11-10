# 17 dil — geliştirme kontrol noktası

Durum: **YAZILDI / ÇEVİRİ DEVAM EDİYOR / TESTLER ERTELENDİ / KABUL EDİLMEDİ**.

Tek i18n dil sahibi, kalıcı tercih, ortak dil seçici, yerel JSON katalogları ve
yükleme başarısızlığında önceki dili koruyan akış yazıldı. Arapça RTL için ortak
yerleşim ve ekipman ağacı klavye yönleri uyarlandı. Dil değişimi taslakları veya
canonical API kodlarını değiştirmemeli.

17 ortak katalog hazır.3169metin alanlı ürün kataloğunun10dili tamamen yazıldı:
Türkçe,İngilizce,İngiliz İngilizcesi,Arapça,Almanca,Fransızca,Hintçe,Çekçe,Rusça,Macarca.
İtalyanca kısmi çalışması korunuyor; kalan diller tamamlanmadı.
Çeviri ajanları gerçek kullanım sınırı bildirdi; hesap/model rotasyonu yapılmadı.

Kaynak AST ayrıştırması kod yazımını destekledi; full build/typecheck/test
çalıştırılmadı. Test kaynakları ve gerçek tarayıcı seçicileri güncellendi;
sonuçları **DEFERRED**, PASS değil. Yeni ürün paketi PR16’dan ayrı tutuluyor.
Canonical103/970SP,%10,62 kabul değişmedi.

[Görev ve kabul koşulları](../tasks/LOCALIZATION-17-01.md).


11.11güncellemesi:950c412 ile günlük görev, tarih sırası, beyan ve durum etiketleri
ayrı JSON namespace’e taşındı;17dil×31etiket. Canonical seçenek değerleri
ve komut gövdeleri korunur. Arapça “beyan edilmedi” erişim reddiyle karıştırılmaz;
sertifika durumu açıkça beyan olarak gösterilir. Kaynak3169metinlik ana katalog
hash’i değişmedi.10tamana katalog ve7eksik ana katalog vardır;17tamüründili
iddiası yoktur. Kalan dictionary/array/template kullanımları da kapatılacak.
Yeni label/RTL/taslak/locale testleri yazıldı; build/typecheck/component/browser
kontrolleri yeni paket kapısına DEFERRED, PASS değil. PR16 recovery ayrı daldadır.
