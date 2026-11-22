---
key: getting-started
title: Başlarken
reviewed: false
---
## Bu kılavuz nasıl kullanılır
Bu kılavuz POSEIDON'un nasıl kullanılacağını adım adım anlatır. Uygulamada **?** düğmesiyle açılan sayfa kılavuzlarından üretilir; bu yüzden ikisi birbirinden farklı bir şey söylemez.
- **Başlarken** (bu bölüm) oturum açmayı, doğru şirket ve aracı seçmeyi, sayfa bulmayı ve sayfa yardımını açmayı anlatır.
- **Ortak çalışma modeli** her sayfanın izlediği tek çalışma biçimini açıklar.
- **Rol bölümleri**, rol bazlı el kitabındaki (13.03) her rol için o rolün görevlerini ve kullandığı sayfaları listeler.
- **Sayfa kılavuzları** her sayfayı numaralı adımlarla anlatır. Bir adımın resmi varsa resimde aynı numara yeşil bir işaretle gösterilir.
- Kılavuz **Durumlar sözlüğü** ve **Sorun olursa** bölümleriyle biter.

Resimler, yalnızca sentetik (uydurma) veri içeren bir test ortamından otomatik olarak alınmıştır. "Synthetic Fleet A" veya "DEMO-SHIP" gibi adlar gerçek şirket veya gemi değildir. Her çalıştırmada değişen bazı alanlar resimlerde gri kutuyla örtülmüştür.

Bu kılavuz bir **taslaktır**. Denizcilik alan uzmanı ve UX yazarı henüz gözden geçirmemiştir. Ekranların nasıl çalıştığını anlatır; bir geminin veya römorkörün hazır, emniyetli ya da uyumlu olup olmadığına asla karar vermez. Bu karar her zaman yetkili kişiye, örneğin Kaptana veya Römorkör Kaptanına aittir.

## Oturum açma
1. Kurumunuzun verdiği POSEIDON adresini tarayıcıda açın.
2. **Oturum aç** düğmesine basın. Kurumunuzun giriş sayfasına yönlendirilirsiniz; POSEIDON parolanızı kendisi hiçbir zaman sormaz.
3. Giriş yaptıktan sonra POSEIDON'a dönersiniz. Bir yönetici sizi davet ettiyse önce davet sayfasını görürsünüz; davetin verdiği yetkiyi okuyun, kabul edin ve yeniden oturum açın.
4. İşinizi bitirince sayfanın üstündeki **Oturumu kapat** düğmesini kullanın. Süresi dolan oturum yeniden giriş ister; ekranda kaydedilmemiş girdi otomatik olarak gönderilmez.

## Önce bağlamı kontrol edin
Her kayıt bir şirkete (kiracı) ve çoğunlukla bir araca, yani bir gemiye veya römorköre aittir. Bir şey okumadan veya kaydetmeden önce sayfanın üstündeki bağlam çubuğuna bakın.
1. **Şirket** adını kontrol edin.
2. **Aracı** ve **araç kipini** (Gemi veya Römorkör) kontrol edin. Değiştirmek için **Araç bağlamını değiştir** veya **Araç değiştir** düğmesini kullanın.
3. **Rol bağlamını** kontrol edin. Ne gördüğünüz ve ne yapabileceğiniz, sunucunun bu araç için size verdiği role bağlıdır.

Bağlam yanlışsa önce onu değiştirin. Yanlış bağlamda kaydedilen kayıt, bütün alanları doğru olsa bile yanlış bir kayıttır.

## Sayfa bulma
1. Soldaki menüyü açın. Dar ekranda üstteki menü düğmesiyle açılır.
2. Sayfalar gruplanmıştır: Genel bakış, Gemi operasyonları, Teknik, Mürettebat, Römorkör operasyonları, Uyum ve güvence, Çalışma alanı.
3. Menüyü süzmek için **Menüde ara** alanına yazın. Yalnız açmaya yetkili olduğunuz sayfalar aranır; açamadığınız bir sayfa asla gösterilmez, adı veya sayısı söylenmez.
4. Okuyabildiğiniz kayıtları aramak için **Çalışma alanında ara** (veya `Ctrl K`) kullanın.

## Sayfa yardımını açma
1. Herhangi bir sayfada başlığın yanındaki **?** düğmesine basın ya da bir alana yazmıyorken `Shift` + `?` tuşlarına basın.
2. Yardım paneli üç sekmeyle açılır: **Kılavuz** (bu kılavuzdaki sayfa kılavuzu), **Neden** (bir kaydın gerekçesinin nerede görüleceği) ve **AI** (bu sürümde kapalı).
3. Sekmeler arasında ok tuşlarıyla geçin. Paneli kapatmak için `Escape` tuşuna veya **Yardımı kapat** düğmesine basın; odak **?** düğmesine döner.
4. Paneli açmak sunucuya hiçbir şey göndermez: komut, kayıt okuması veya AI çağrısı başlatmaz.
5. Paneldeki "İlgili sayfalar" yalnız açmaya yetkili olduğunuz sayfaları gösterir.

## Görünüm, dil ve çevrimdışı çalışma
- Görünüm ayarları renk temasını değiştirir. Bir durumun anlamını asla değiştirmez: bilinmeyen veya doğrulanmamış bir değer hiçbir temada yeşil "hazır" görünmez.
- Dil seçici ekran dilini değiştirir. Türkçe ve İngilizce kılavuzlar ürün ekibince yazılmıştır; diğer dillerde İngilizce kılavuz gösterilir ve bu açıkça belirtilir.
- Bağlantı kopunca sayfa **Çevrimdışı** yazar. Gösterilen veri son kopyadır ve eski olabilir. Destekleyen sayfalarda kritik iş sürebilir; kuyruğa alınan değişiklikler bağlantı dönünce gönderilir. Çakışma asla otomatik birleştirilmez: **Önemli çakışma merkezi** sayfasında siz çözersiniz.
