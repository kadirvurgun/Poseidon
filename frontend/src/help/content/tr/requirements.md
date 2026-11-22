---
key: requirements
title: Gereksinim taslakları
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [sources, audit-ready, audit]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Gereksinim taslakları sayfası, bir kaynak sürümünden çıkan **gereksinimleri** sürümlü taslak olarak tutar ve her gereksinimi tam bir kaynak sürümüne bağlar. Cevapladığı soru şudur: "Bu gereksinim hangi kaynağın hangi sürümüne dayanıyor ve kayıtlı yaşam döngüsü nedir?" Gereksinim, zincirin ilk halkasıdır: Gereksinim → Kontrol → Kanıt → Doğrulama. Zincirde bağlantısız (yetim) kayıt bırakılmamalıdır (13.03 §8).

## Kim kullanır
- **Mevzuat / güvence kullanıcısı (Regulatory / Assurance):** gereksinimi kaynağa bağlar, uygulanabilirliği bağlam girdileriyle değerlendirir ve zincirin eksiksiz olmasını izler (13.03 §8).
- **DPA / HSEQ:** bulgu ve denetim çalışmasında gereksinimin dayanağını okur (13.03 §4).

## Başlamadan önce
- Sayfayı görmek için tenant geneli okuma ve mevzuat taslağı okuma, düzenlemek için ayrıca mevzuat taslağı yazma yetkisi gerekir.
- Alt görünümler kendi yetkilerini ister ve her zaman tenant geneli okuma kapsamı gerekir. **Yaşam döngüsü ve uygulanabilirlik** için uygulanabilirlik okuma; kural düzenlemek için kural yazma, inceleme istemek için kural inceleme, değerlendirmek için uygulanabilirlik değerlendirme yetkisi gerekir; yeterlilik ve olgu teyidi için mevzuat yeterlilik veya olgu teyit yetkisi ile sunucuda kayıtlı bir atama gerekir. **Kontroller ve kanıtlar** için kontrol okuma, kanıt okuma, belge okuma ve uygulanabilirlik okuma; bağlamak için gemide düzenleme hakkıyla kontrol yazma veya kanıt yazma, onay için kontrol onaylama yetkisi gerekir.
- Önce dayanılan kaynağı **Kaynak taslakları** sayfasında kaydedin. Gereksinim yalnız var olan bir kaynak sürümüne bağlanabilir.
- Kaydetme canlı sunucu bağlantısı gerektirir; çevrimdışıyken düzenlemeler sekmede korunur.

## Adım adım
1. Listede gereksinim taslaklarını görün; **Taslaklarda ara** ve **Ara** ile arayın.
2. Yeni gereksinim için **Yeni taslak** düğmesine basın.
3. **Taslak alanları** bölümünde her alan için **Durum** seçin (**Eksik**, **Bilinmiyor**, **Bilinen (beyan)**) ve bilinen değerleri girin: **Düzenleme**, **Sürüm / değişiklik beyanı**, **Beyan edilen yürürlük tarihi**, **Beyan / köken**, **Kaynak gözlem zamanı (UTC farkı ile)**.
4. **Değişmez kaynak atfı** bölümünde **Kaynak bul** alanına yazıp **Kaynaklarda ara** düğmesine basın. **Kaynak taslağı** listesinden kaynağı, ardından **Kesin sürüm** listesinden tam sürümü seçin. **Seçili değişmez kaynak içeriği** ile seçtiğiniz sürümün içeriğini kontrol edin.
5. **Taslağı kaydet** düğmesine basın. Kayıt açıldığında **Kayıtlı yaşam döngüsü** ve **Değişmez geçmiş** bölümleri görünür.
6. Uygulanabilirlik için **Yaşam döngüsü ve uygulanabilirlik**, kontrol ve kanıt bağlantıları için **Kontroller ve kanıtlar** düğmesini kullanın. **Kontroller ve kanıtlar** içindeki **Kritik yetim raporu**, gereksinim bağlantısı olmayan kritik beyanları gösterir; bu kayıtları bir gereksinime bağlayın.

## Durumlar ne anlama gelir
- **Kayıtlı yaşam döngüsü:** sunucunun bağlı kurallardan türettiği durum: **Taslak**, **Kabul edilmiş**, **Gelecekte yürürlüğe girecek**, **Yürürlükte**, **Yeni sürüm var**. Okunamazsa **Kullanılamıyor** görünür; sistem bunu taslak veya yürürlükte diye varsaymaz.
- "Bağlı kurallar farklı yaşam döngüsü durumları kaydediyor. Gösterilen değer bir yazılım önceliğidir; uzman incelemesi gerekir." Bu durumda gösterilen değer bir karar değildir; uzman incelemesi gerekir.
- **Bilinmiyor:** bilgi bilinmiyor. Kritik veri bilinmiyorsa uygulanabilirlik sonucu **İnceleme gerekli** kalır (REVIEW_REQUIRED); "uygulanmaz" veya "hayır" sayılmaz (13.03 §8).
- **Uygulanabilirlik incelemesi gerekli / Gereksinim bağlantısı eksik / Doğrulama yapılmadı:** kontrol-kanıt zincirindeki açık maddelerdir.
- **Tamamlanma bilinmiyor · Doğrulanmamış · Etkinlik bilinmiyor:** tamamlanma, doğrulama ve etkinlik ayrı durumlardır. Tamamlanmış bir kayıt doğrulanmış sayılmaz; doğrulanmış bir kayıt da etkili sayılmaz.

## Yapamazsınız / dikkat
- Bir gereksinimi kaynağın "en güncel" haline bağlayamazsınız; her zaman **Kesin sürüm** seçilir. Kaynak yeni sürüm aldığında eski bağlantı geçmişte korunur.
- Uygulanabilirlik önizlemesi uyum, hazırlık, izin veya hukuki onay oluşturmaz: "ÖNİZLEME / TASLAK. Tahminler uyum, hazırlık, izin veya hukuki onay oluşturmaz."
- Yaşam döngüsü değişikliği için **İnsan incelemesi talep et** kullanılır; kararı kaynak kabulü ve yetkili bağımsız inceleyici verir. Sistem bunu kendisi kapatmaz.
- Klas, bayrak, şirket ve yerel kural kaynaklarını birbirinin yerine varsaymayın (13.03 §8).

## Sorun olursa
- **Kaynak seçimi kullanılamıyor.** **Kaynakları tekrar dene** düğmesine basın.
- **Çatışma. Düzenlemeleriniz korundu. Yalnız vazgeçtikten sonra yeniden yükleyin.** **Kayıtlı taslağı yeniden yükle** ile güncel sürümü açın.
- **Alan durumlarını, sınırları, tarihleri ve kaynak sürümünü kontrol edin.** Kaynak seçtiyseniz kesin sürümü de seçin.
- **Geçmiş kullanılamıyor. Düzenlemeler korundu.** **Geçmişi tekrar dene** düğmesine basın.
- Yaşam döngüsü beklenmedikse bağlı kuralları ve kaynak sürümlerini inceleyin; gerekirse kontrol sahibi veya alan uzmanına (SME) başvurun (13.03 §10).
- Yetki yoksa rol ve tenant kapsamınızı kontrol edip Tenant Admin'e başvurun (13.03 §10).

## İlgili sayfalar
