---
key: sources
title: Kaynak taslakları
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Kaynak taslakları sayfası, kuruluşunuzun dayandığı mevzuat ve kural kaynaklarını (sözleşme, bayrak, klas, şirket veya yerel kural) **sürümlü taslak kayıtlar** olarak tutar. Cevapladığı soru şudur: "Bu gereksinim hangi resmî kaynağın hangi sürümüne dayanıyor?" Sayfanın üstündeki uyarı geçerlidir: "Kuruluşa özel TASLAK · Beyan edilmiş ve doğrulanmamış. Tarihler ve özet değerleri hukuki yetki vermez. Kaynak referansları metin olarak saklanır." (13.03 §8).

## Kim kullanır
- **Mevzuat / güvence kullanıcısı (Regulatory / Assurance):** resmî ve sürümlü kaynağı kayda alır, yürürlük tarihini ve yaşam döngüsünü belirtir (13.03 §8).
- **DPA / HSEQ:** denetim ve bulgu çalışmalarında hangi kaynağın hangi sürümüne bakıldığını görmek için okur (13.03 §4).

## Başlamadan önce
- Sayfayı görmek için tenant geneli okuma ve mevzuat taslağı okuma yetkisi gerekir. Taslak oluşturmak ve kaydetmek için ayrıca mevzuat taslağı yazma yetkisi gerekir; bu yetki yoksa **Yeni taslak** düğmesi görünmez.
- Alt görünümler kendi yetkilerini ister ve her zaman tenant geneli okuma kapsamı gerekir. **Yaşam döngüsü ve uygulanabilirlik** için uygulanabilirlik okuma; kural düzenlemek için kural yazma, inceleme istemek için kural inceleme, değerlendirmek için uygulanabilirlik değerlendirme yetkisi gerekir; yeterlilik ve olgu teyidi için mevzuat yeterlilik veya olgu teyit yetkisi ile sunucuda kayıtlı bir atama gerekir. **Kontroller ve kanıtlar** için kontrol okuma, kanıt okuma, belge okuma ve uygulanabilirlik okuma; bağlamak için gemide düzenleme hakkıyla kontrol yazma veya kanıt yazma, onay için kontrol onaylama yetkisi gerekir.
- Kaynağın resmî metnini ve sürüm/değişiklik bilgisini elinizde bulundurun. POSEIDON kaynağın metnini indirmez veya doğrulamaz; siz beyan edersiniz.
- Kaydetme canlı sunucu bağlantısı gerektirir. Çevrimdışıyken düzenlemeleriniz sekmede korunur ama kaydedilmez.

## Adım adım
1. Listede kayıtlı kaynak taslaklarını görün. Aramak için **Taslaklarda ara** alanına yazıp **Ara** düğmesine basın. Sayfalar arasında **Önceki** ve **Sonraki** ile gezinin.
2. Yeni kaynak için **Yeni taslak** düğmesine basın.
3. **Taslak alanları** bölümünde her alan için önce **Durum** seçin: **Eksik**, **Bilinmiyor** veya **Bilinen (beyan)**. Değeri yalnız **Bilinen (beyan)** seçince girebilirsiniz. Alanlar: **Başlık**, **Beyan edilen otorite**, **Kaynak referansı (yalnız metin)**, **Sürüm / değişiklik beyanı**, **Beyan edilen yürürlük tarihi**, **Beyan / köken**, **Kaynak gözlem zamanı (UTC farkı ile)**.
4. **Taslağı kaydet** düğmesine basın. Başarılı kayıtta "Taslak kaydedildi. Doğrulanmadı." mesajı görünür; **Kayıtlı taslağı ve geçmişi aç** ile kaydı açın.
5. Var olan bir taslağı listeden açın, değiştirin ve yeniden kaydedin. Her kayıt yeni bir **Sürüm** oluşturur; eski sürümler **Değişmez geçmiş** bölümünde içerik ve **Bütünlük özeti (yetki değil)** ile kalır.
6. Uygulanabilirlik kurallarına veya kontrol-kanıt zincirine geçmek için listenin üstündeki **Yaşam döngüsü ve uygulanabilirlik** ya da **Kontroller ve kanıtlar** düğmelerini kullanın. Geri dönmek için **Kayıtlara dön** düğmesine basın.

## Durumlar ne anlama gelir
- **DRAFT:** listede ve kayıt başlığında görünen saklanan durum. Bu sayfadaki her kaynak kaydı bir taslaktır; resmî kabul veya yürürlük kararı değildir.
- **Eksik:** alan için henüz bilgi girilmedi. Zorunlu bilgi eksikken sonraki adımlar (örneğin uygulanabilirlik değerlendirmesi) engellenir.
- **Bilinmiyor:** bilgi arandı ama bilinmiyor. Bilinmiyor değeri sistem tarafından "hayır" veya "uygulanmaz" diye yorumlanmaz; kritik veri bilinmiyorsa sonuç incelemeye kalır (REVIEW_REQUIRED).
- **Bilinen (beyan):** değeri siz beyan ettiniz. Beyan doğrulanmış bilgi değildir.
- **Bütünlük özeti (yetki değil):** sürümün içeriğinin değişmediğini gösterir; hukuki geçerlilik kanıtı değildir.

## Yapamazsınız / dikkat
- Kaydedilmiş bir sürümü değiştiremezsiniz. Kaynak değiştiğinde yeni sürüm kaydedin; gereksinimler eski sürüme bağlı kalır ve geçmiş korunur (13.03 §1).
- Klas, bayrak, şirket ve yerel kural kaynaklarını birbirinin yerine varsaymayın; her birini ayrı kaynak olarak kaydedin (13.03 §8).
- Bu sayfa bir kaynağı resmî olarak kabul etmez ve kimseye uyum, izin veya hazırlık sonucu vermez. Kaynak kabulü ve bağımsız inceleme insan yetkisindedir.
- Yürürlük tarihi ve özet değerleri hukuki yetki vermez.

## Sorun olursa
- **Yetkiniz yok.** Rolünüzü ve tenant kapsamınızı kontrol edin; gerekiyorsa Tenant Admin'e başvurun (13.03 §10).
- **Çatışma. Düzenlemeleriniz korundu. Yalnız vazgeçtikten sonra yeniden yükleyin.** Başka biri kaydı sizden önce değiştirdi. **Kayıtlı taslağı yeniden yükle** ile güncel sürümü açın ve değişikliğinizi yeniden yapın.
- **Alan durumlarını, sınırları, tarihleri ve kaynak sürümünü kontrol edin.** Bir alan değeri veya tarih biçimi geçersiz.
- **Hizmete ulaşılamıyor. Taslağınız korundu.** Bağlantı dönünce **Sabit komutu tekrar dene** ile aynı komutu yeniden gönderin.
- **Taslağınızı koruyarak oturum açın.** Oturumunuz bitti; **Oturum aç** bağlantısını kullanın.
- Uygulama hatası sürerse correlation ID, ekran, zaman ve adımlarla destek ekibine bildirin (13.03 §10).

## İlgili sayfalar
