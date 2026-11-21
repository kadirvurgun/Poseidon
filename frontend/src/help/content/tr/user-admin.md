---
key: user-admin
title: Kullanıcılar ve yetkiler
roles: [Tenant Admin]
permissions: [membership.read, membership.manage]
related: [audit, companies, fleet]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Kullanıcılar ve yetkiler sayfası, çalışma alanınızdaki **üyelikleri** yönetir. Cevapladığı soru şudur: "Bu kişi hangi gemilerde neyi görebilir ve neyi yapabilir?" Hesabın kendisi, yani kullanıcı adı, parola ve MFA, POSEIDON'da değil merkezi kimlik sağlayıcıda (IdP) tutulur. Bu sayfa yalnız o hesabı bir üyeliğe bağlar ve rol paketi, yetki ve gemi kapsamı verir (13.03 §9).

## Kim kullanır
- **Kiracı yöneticisi (Tenant Admin):** kullanıcı, rol, gemi ve nesne kapsamını en az yetki ilkesiyle atamak, vekaletleri ve askıya almayı yönetmek için (13.03 §9).
- Üyelikleri okuma yetkisi olan diğer kullanıcılar listeyi görebilir ama değiştiremez.

## Başlamadan önce
- Sayfayı görmek için üyelik okuma yetkisi, değişiklik yapmak için üyelik yönetim yetkisi ve tenant geneli yönetim gerekir. Yalnız okuma yetkiniz varsa "Üyelikleri görebilir ama değiştiremezsiniz." mesajı görünür.
- Yeni üye eklemeden önce kişinin hesabını kimlik sağlayıcıda oluşturun ve hesabın **IdP subject** değerini edinin.
- Yalnız kendinizde olan yetkileri verebilirsiniz. Kendi üyeliğinizi başka bir yönetici düzenler.
- Üyelik yönetimi canlı sunucu bağlantısı gerektirir; çevrimdışıyken değişiklik yapılamaz.

## Adım adım
1. **Üyeler** listesinde üyeleri görün. **Durum** ve **Rol paketi** ile süzüp **Uygula** düğmesine basın. Aramak için üyelik veya hesap kimliği başlangıcını ya da tam IdP subject değerini girip **Ara** düğmesine basın.
2. Bir üyeyi **Aç** düğmesiyle açın. **Etkin yetkiler**, **Gemi kapsamı**, **Yetki geçmişi** ve **Vekaletler** bölümlerini inceleyin.
3. Yeni üye için **Üye ekle** düğmesine basın. **IdP issuer** ve **IdP subject** değerlerini girin, en az bir **Rol paketi** seçin, gerekirse **Bireysel yetkiler** ekleyin ve kapsamı belirleyin: tenant geneli ya da en az bir gemi. Ardından **Üyelik oluştur** düğmesine basın.
4. Var olan bir üyenin yetkisini değiştirmek için **Yetkiyi düzenle**, ardından **Yetkiyi kaydet** düğmesine basın. Her kayıt yeni bir yetki revizyonu oluşturur; eski revizyonlar **Yetki geçmişi** bölümünde kalır.
5. Geçici devir için **Bu üyeden vekalet ver** ile vekil, **Başlangıç**, **Bitiş**, **Gerekçe** ve **Devredilen yetkiler** alanlarını doldurun.
6. Erişimi durdurmak için **Yaşam döngüsü** bölümünde **Askıya al**, geri açmak için **Yeniden etkinleştir** düğmesini kullanın. **Üyeliği iptal et** kesindir.

## Durumlar ne anlama gelir
- **Aktif / Askıda / İptal edildi:** üyeliğin yaşam döngüsü. Askıdaki ya da iptal edilmiş üyenin bir sonraki isteği reddedilir.
- **Yetki kaynağı — Burada yönetilir:** yetki bu sayfadan verildi ve buradan değiştirilebilir.
- **Yetki kaynağı — Operatör yapılandırması:** yetki sunucu yapılandırmasından gelir (başlangıç/acil durum) ve burada düzenlenemez.
- **Yetki kaynağı — Yetki yok:** kayıt var ama bir yetki vermiyor.
- **Sizde yok / Bu paketin tüm yetkileri sizde değil:** bu yetkiyi veya paketi veremezsiniz, çünkü kendinizde yok.
- **Kaydedildi. Değişiklik üyenin bir sonraki isteğinde geçerli olur.** Yeni bir üyenin erişimi bir sonraki girişte başlar.

## Yapamazsınız / dikkat
- Kendinizde olmayan bir yetkiyi, gemiyi veya tenant geneli erişimi veremezsiniz.
- Kendi üyeliğinizi değiştiremezsiniz; son aktif yönetici kaldırılamaz.
- Karar, doğrulama ve onay yetkileri hiçbir pakette yoktur. Bunlar yalnız tek tek ve bu yetkiye sahip bir yönetici tarafından verilebilir. Atamaya bağlı işlemler, örneğin römorkör veya kalkış kararları, ayrıca operatör ataması gerektirir. Bir rol paketi vermek kişiyi kaptan veya doğrulayıcı olarak atamaz.
- Kaynak veya vekil olduğunuz bir vekaleti onaylayamazsınız. Vekalet, kaynağın güncel yetkisini aşamaz ve tenant geneli olamaz.
- POSEIDON parola saklamaz ve IdP subject değerlerini listede geri göstermez.

## Sorun olursa
- **Bu kimlik zaten tanımlı ya da yapılandırılmış:** kişi ya zaten üye ya da operatör yapılandırmasından yönetiliyor.
- **Kendi yetkinizi aşıyor:** daha dar bir paket veya kapsam seçin, ya da yetkiyi tutan bir yöneticiden isteyin.
- **Kayıt bu arada değişti:** **Güncel durumu yükle** düğmesine basıp değişikliğinizi yeniden yapın.
- **Sonuç bilinmiyor. Aynı istekle yeniden deneyin ya da yeniden yükleyin.** Aynı komutu güvenle tekrar gönderebilirsiniz.
- **Güvenilen kimlik sağlayıcı değil:** issuer değerini kontrol edin. Hesap başka bir IdP'den geliyorsa sistem yöneticisine başvurun (13.03 §10).

## İlgili sayfalar
