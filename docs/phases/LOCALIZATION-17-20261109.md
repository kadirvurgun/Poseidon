# 17 dil — geliştirme kontrol noktası

Durum: **YAZILDI / ÇEVİRİ DEVAM EDİYOR / TESTLER ERTELENDİ / KABUL EDİLMEDİ**.

Tek i18n dil sahibi, kalıcı tercih, ortak dil seçici, yerel JSON katalogları ve
yükleme başarısızlığında önceki dili koruyan akış yazıldı. Arapça RTL için ortak
yerleşim ve ekipman ağacı klavye yönleri uyarlandı. Dil değişimi taslakları veya
canonical API kodlarını değiştirmemeli.

17 ortak katalog hazır.3169metin alanlı ürün kataloğunun9dili tamamen yazıldı:
Türkçe,İngilizce,İngiliz İngilizcesi,Arapça,Almanca,Fransızca,Hintçe,Çekçe,Rusça.
Macarca ve İtalyanca kısmi çalışmaları korunuyor; kalan diller tamamlanmadı.
Çeviri ajanları gerçek kullanım sınırı bildirdi; hesap/model rotasyonu yapılmadı.

Kaynak AST ayrıştırması kod yazımını destekledi; full build/typecheck/test
çalıştırılmadı. Test kaynakları ve gerçek tarayıcı seçicileri güncellendi;
sonuçları **DEFERRED**, PASS değil. Yeni ürün paketi PR16’dan ayrı tutuluyor.
Canonical103/970SP,%10,62 kabul değişmedi.

[Görev ve kabul koşulları](../tasks/LOCALIZATION-17-01.md).
