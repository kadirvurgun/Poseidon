---
key: user-admin
title: Kullanıcılar ve yetkiler
roles: [Tenant Admin]
permissions: [membership.read, membership.manage]
related: [audit, companies, fleet]
screenshots: [1, 2, 4]
reviewed: false
---
## Bu sayfa ne işe yarar
Kullanıcılar ve yetkiler sayfası, çalışma alanınızdaki **üyelikleri** yönetir. Cevapladığı soru şudur: "Bu kişi hangi gemilerde neyi görebilir ve neyi yapabilir?" Hesabın kendisi, yani kullanıcı adı, parola ve MFA, POSEIDON'da değil merkezi kimlik sağlayıcıda (IdP) tutulur. Bu sayfa o hesabı bir üyeliğe **davet eder** ve rol paketi, yetki ve gemi kapsamı verir (13.03 §9). Yeni üye davet edilir: kişi oturum açıp daveti kabul edene kadar davet hiçbir yetki vermez.

## Kim kullanır
- **Kiracı yöneticisi (Tenant Admin):** kullanıcı, rol, gemi ve nesne kapsamını en az yetki ilkesiyle atamak, yeni üyeleri davet etmek, vekaletleri ve askıya almayı yönetmek için (13.03 §9).
- Üyelikleri okuma yetkisi olan diğer kullanıcılar listeyi görebilir ama değiştiremez.
- **Davet edilen kişi** yalnız davet bağlantısını açar; bu sayfayı hiç görmez.

## Başlamadan önce
- Sayfayı görmek için üyelik okuma yetkisi, değişiklik yapmak için üyelik yönetim yetkisi ve tenant geneli yönetim gerekir. Yalnız okuma yetkiniz varsa "Üyelikleri görebilir ama değiştiremezsiniz." mesajı görünür.
- Kişinin kimlik sağlayıcıda bir hesabı olmalıdır. **Beklenen IdP subject (isteğe bağlı)** alanı daveti tek bir hesapla sınırlar; boş bırakılırsa bağlantıyla oturum açan kişi daveti kabul edebilir.
- Davet bağlantısını iletmek için güvenilir bir kanal belirleyin (örneğin kuruluşunuzun kendi mesajlaşma aracı). POSEIDON e-posta göndermez.
- Yalnız kendinizde olan yetkileri verebilirsiniz. Kendi üyeliğinizi başka bir yönetici düzenler.
- Üyelik yönetimi canlı sunucu bağlantısı gerektirir; çevrimdışıyken değişiklik yapılamaz.

## Adım adım
1. **Üyeler** listesinde üyeleri görün. **Durum** (örneğin **Davet edildi**) ve **Rol paketi** ile süzüp **Uygula** düğmesine basın. Aramak için üyelik veya hesap kimliği başlangıcını ya da tam IdP subject değerini girip **Ara** düğmesine basın.
2. Bir üyeyi **Aç** düğmesiyle açın. **Etkin yetkiler**, **Gemi kapsamı**, **Davet**, **Yetki geçmişi** ve **Vekaletler** bölümlerini inceleyin.
3. Üye davet etmek için **Üye ekle** düğmesine basın. **IdP issuer** değerini kontrol edin, isterseniz **Beklenen IdP subject (isteğe bağlı)** girin, en az bir **Rol paketi** seçin, gerekirse **Bireysel yetkiler** ekleyin ve kapsamı belirleyin: tenant geneli ya da en az bir gemi. Ardından **Üyelik oluştur** düğmesine basın.
4. Üye **Davet edildi** durumunda ve **Davet bağlantısı** ile açılır. **Davet bağlantısını kopyala** düğmesine basıp bağlantıyı iletin. Bağlantı yalnız şimdi gösterilir; bir kez çalışır ve 24 saat sonra geçersiz olur.
5. Bağlantı kaybolduysa ya da süresi dolduysa **Yeni davet bağlantısı oluştur** düğmesine basın; önceki bağlantı çalışmaz. Daveti geri çekmek için **Daveti iptal et** düğmesini kullanın.
6. Davet edilen kişi bağlantıyı açar, oturum açar, davetin neler verdiğini görür ve **Daveti kabul et** düğmesine basar. Yeniden oturum açtıktan sonra sizin verdiğiniz yetkiyle çalışır; üye artık **Aktif** ve **Davet kabul edildi** görünür.
7. Var olan bir üyenin yetkisini değiştirmek için **Yetkiyi düzenle**, ardından **Yetkiyi kaydet** düğmesine basın. Her kayıt yeni bir yetki revizyonu oluşturur; eski revizyonlar **Yetki geçmişi** bölümünde kalır.
8. Geçici devir için **Bu üyeden vekalet ver** ile vekil, **Başlangıç**, **Bitiş**, **Gerekçe** ve **Devredilen yetkiler** alanlarını doldurun.
9. Erişimi durdurmak için **Yaşam döngüsü** bölümünde **Askıya al**, geri açmak için **Yeniden etkinleştir** düğmesini kullanın. **Üyeliği iptal et** kesindir.

## Durumlar ne anlama gelir
- **Davet edildi:** üyelik davet edilen kişiyi bekler. Hiçbir yetki vermez ve hesabı hiçbir yerde, başka bir kuruluşta da, ayırmaz.
- **Davet bekliyor / Davetin süresi doldu / Davet yenilendi / Davet iptal edildi / Davet kabul edildi:** son davet bağlantısının durumu. Süresi dolmuş, yenilenmiş veya iptal edilmiş bir bağlantı hiçbir zaman kabul edilemez.
- **Aktif / Askıda / İptal edildi:** üyeliğin yaşam döngüsü. Askıdaki ya da iptal edilmiş üyenin bir sonraki isteği reddedilir.
- **Yetki kaynağı — Burada yönetilir:** yetki bu sayfadan verildi ve buradan değiştirilebilir.
- **Yetki kaynağı — Operatör yapılandırması:** yetki sunucu yapılandırmasından gelir (başlangıç/acil durum) ve burada düzenlenemez.
- **Yetki kaynağı — Yetki yok:** kayıt var ama bir yetki vermiyor.
- **Sizde yok / Bu paketin tüm yetkileri sizde değil:** bu yetkiyi veya paketi veremezsiniz, çünkü kendinizde yok.
- **Kaydedildi. Değişiklik üyenin bir sonraki isteğinde geçerli olur.** Davet edilen üyenin erişimi, kabulden sonraki ilk girişte başlar.

## Yapamazsınız / dikkat
- Kendinizde olmayan bir yetkiyi, gemiyi veya tenant geneli erişimi veremezsiniz.
- Kendi üyeliğinizi değiştiremezsiniz; son aktif yönetici kaldırılamaz.
- Bir hesap yalnız bir kuruluşta kabul edilmiş üyelik tutabilir. Davet, başka bir kuruluşa ait bir hesabı hiçbir zaman taşımaz; davet edilen kişi yalnız davetin kabul edilemediğini görür.
- Davet bağlantısını bir anahtar gibi koruyun: beklenen subject girmediyseniz bağlantıyı tutan ve oturum açan herkes daveti kabul edebilir. Ortak kanallarda paylaşmayın. Bağlantıyı her zaman bütün olarak iletin: **#** işaretinden sonraki gizli kısmı yalnız tarayıcı okur, sunucuya ve kayıtlarına hiç ulaşmaz.
- Karar, doğrulama ve onay yetkileri hiçbir pakette yoktur. Bunlar yalnız tek tek ve bu yetkiye sahip bir yönetici tarafından verilebilir. Atamaya bağlı işlemler, örneğin römorkör veya kalkış kararları, ayrıca operatör ataması gerektirir. Bir rol paketi vermek kişiyi kaptan veya doğrulayıcı olarak atamaz.
- Kaynak veya vekil olduğunuz bir vekaleti onaylayamazsınız. Vekalet, kaynağın güncel yetkisini aşamaz ve tenant geneli olamaz.
- POSEIDON parola saklamaz, davet bağlantısının yalnız parmak izini tutar ve IdP subject değerlerini listede geri göstermez.

## Sorun olursa
- **Bu kimlik operatör yapılandırmasında, burada zaten üye ya da burada zaten davet edilmiş.** Var olan üyeyi açın ya da önce eski daveti iptal edin.
- **Davet edilen kişi "Bu davet kabul edilemiyor" görüyor:** bağlantının süresi dolmuş, kullanılmış, yenilenmiş ya da iptal edilmiş olabilir, başka bir hesaba ait olabilir veya kişinin hesabı zaten bir kuruluşa ait olabilir. **Davet** durumunu kontrol edin, gerekirse **Yeni davet bağlantısı oluştur** düğmesine basın.
- **Bağlantı kopyalanamadı:** bağlantı alanını seçip elle kopyalayın.
- **Kendi yetkinizi aşıyor:** daha dar bir paket veya kapsam seçin, ya da yetkiyi tutan bir yöneticiden isteyin.
- **Kayıt bu arada değişti:** **Güncel durumu yükle** düğmesine basıp değişikliğinizi yeniden yapın.
- **Sonuç bilinmiyor. Aynı istekle yeniden deneyin ya da yeniden yükleyin.** Aynı komutu güvenle tekrar gönderebilirsiniz.
- **Güvenilen kimlik sağlayıcı değil:** issuer değerini kontrol edin. Hesap başka bir IdP'den geliyorsa sistem yöneticisine başvurun (13.03 §10).

## İlgili sayfalar
