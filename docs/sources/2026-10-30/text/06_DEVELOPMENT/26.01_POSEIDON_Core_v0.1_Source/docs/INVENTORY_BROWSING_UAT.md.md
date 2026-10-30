# INVENTORY_BROWSING_UAT.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/docs/INVENTORY_BROWSING_UAT.md
SHA256: de2f5bd78772455a16b55cdc8f69dda2d0df9fd1e41f4e17060b4034ecf3bc90

## lines:1-32

# Esra UAT — kısmi envanter tarama dilimi

Durum: **KULLANICI KABULÜ BEKLİYOR**. Otomatik testler Esra'nın kabulü değildir.
Bu teslim tam Gemi/Römorkör Envanteri modülü veya Sertifika Geçerlilik modülü değildir.

Mevcut özel Development kurulumunda yalnız sentetik örneklerle veya ayrıca kurulmuş
ve yetkilendirilmiş kurum ortamında test edin. Bu teslim public test URL'si, gerçek
hesap/IdP, credential, hosting veya deployment üretmez. BFF varsayılan kapalıdır.
Kurulum yönergeleri mevcut README/BFF_UI_BOUNDARY belgelerindedir.

| Adım | Beklenen davranış | Esra sonucu |
|---|---|---|
| TR/EN giriş; gemi listesini aç | Yalnız yetkili kayıtlar; yüklenen sayfa sayısı görünür | BEKLİYOR |
| Ad, IMO veya resmî numara ara ve Ara/filtrele seç | Sonuç sunucudan; IMO boş römorkör kaydı kaybolmaz/IMO zorunlu olmaz | BEKLİYOR |
| Bayrak/tip mevcut metin değeriyle filtrele | Tam metin eşleşmesi; yeni katalog/state oluşmaz | BEKLİYOR |
| `%`, `_`, `\` içeren sentetik numara/ad ara | Wildcard yerine literal eşleşme | BEKLİYOR |
| Sonraki sayfa; sonra aramayı değiştir/temizle | İlk sayfadan başlar; önceki sonuç/sıra karışmaz | BEKLİYOR |
| İlk sayfa/Yenile | Güncel veri; sayfalar arası snapshot garantisi yok | BEKLİYOR |
| Bir taslağı aç/değiştir; listeye dön/yeniden aç | DRAFT, sürüm ve mevcut yetkiler korunur; onaysız dirty edit kaybolmaz | BEKLİYOR |
| Sertifika yeni taslağında ilk sayfada olmayan gemiyi ara/yükle/seç | Otomatik varsayılan yok; arama değişiminde seçilen gemi ve yazılmış alanlar korunur | BEKLİYOR |
| Hızlı ardışık aramalar veya sayfa değişimleri | Geç gelen eski yanıt yeni sonucu değiştirmez | BEKLİYOR |
| Oturum/kapsam değişimi veya expiry | Eski hesap satırları temizlenir; otomatik yazma/tekrar gönderim olmaz | BEKLİYOR |
| 390/768/1440 görünüm, TR/EN, Tab/Enter | Kontroller/odak taşmaz; alan etiketleri okunur | BEKLİYOR |

Arama ASCII case-fold kullanır: ISTANBUL→istanbul, fakat Türkçe İ/ı eşlemesi veya
aksan duyarsızlığı vaat edilmez. Bayrak/tip tam ve harfe duyarlı metindir.
Yetki dışı bir tenant/company/grant için kayıt sayısı veya tarihçe açığa çıkmamalıdır.
Geçmiş okuma, silme, aktivasyon, sertifika geçerli/geçersiz/operasyona hazır kararı yoktur.

Bulgu kaydı: adım, TR/EN, viewport, sentetik veri örneği, beklenen/görülen davranış;
gizli token veya gerçek müşteri verisi eklemeyin. Esra sonucu ve kabul tarihi kullanıcı
onayından sonra ayrıca kaydedilecek; bu dosya otomatik PASS'e çevrilmez.
