# ADR_EVIDENCE_INTAKE.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/docs/ADR_EVIDENCE_INTAKE.md
SHA256: 0b6cdfd30e5507fea2a9791c7baa4fad9e5a931c69b5e0104863ed3222d81118

## lines:1-40

# ADR-EVIDENCE-INTAKE-001 — Karantinalı ek alımı

27 Ekim 2026. Taban: `f20a083b2031f988f63862f0dcd5eb6b758e2e15`.

Bu sözleşme devredilmiş ürün/mühendislik kararlarıdır. Tarihsel olarak onaylanmış fiziksel şema, mevzuat kararı, güvenlik sertifikasyonu veya kullanıcı kabulü değildir. 14.01 ve 15.01 paylaşılan kanıt, sunucu kapsamı, değişmez sürüm, bütünlük ve audit gerektirir; dosya boyutu, saklama süresi, tarayıcı sağlayıcısı ve yükleme protokolünün ayrıntılarını dondurmaz. 14.02 içindeki 41 ilgili ekran alanı ve DEC-DATA kararları açık kalır.

## Sınır

Yalnız üretilmiş zararsız sentetik PDF/PNG/JPEG dosyaları, açıkça etkinleştirilmiş özel Development veya izole Testing depolaması. Varsayılan kapalıdır. Gerçek kullanıcı dosyası kabulü bu geliştirme paketinin kullanım amacı değildir. Hedef yalnız erişilebilen Vessel veya Certificate; sertifikanın gemisi sunucuda çözülür. Mevcut envanter `evidence_ref` düz metni çözülmez, bağlantıya dönüştürülmez.

Yeni `evidence:capture`, hedefe göre bağımsız `inventory:manage` veya `certificate:manage` ile birlikte gereklidir. Uploader olmak hedef erişiminin yerine geçmez. Okuma ve her yazma/tekrar mevcut hedef kapsamını yeniden denetler. Şirket üyeliği veya rol etiketi erişim sağlamaz. Gerçek kimlik sağlayıcısında izin tanımlanmaz.

## Teknik sınırlar

Tek dosya, pozitif 1–5.242.880 bayt; PDF (`.pdf`, `application/pdf`), PNG (`.png`, `image/png`), JPEG (`.jpg/.jpeg`, `image/jpeg`). Bildirilen MIME, uzantı ve sınırlı imza kontrolü uyuşmalıdır. İmza kontrolü dosya ayrıştırma veya güvenlik kanıtı değildir. Dosya açılmaz, çalıştırılmaz, görüntülenmez, arşiv açılmaz; uzaktan URL getirilmez.

Metadata en fazla 16 KiB. Görünen dosya adı 1–128 Unicode karakter; başlık ve bildirilen kaynak 1–200, açıklama 1–2.000. İsteğe bağlı beklenen SHA-256 tam 64 hex karakterdir. Sunucu tenant/şirket/gemi/yükleyen/nesne anahtarını üretir; istemci bunları ya da tarama/doğrulama durumunu yazamaz. Eksik metadata için hayali varsayılan gerçekler üretilmez.

Development tenant kapasitesi 100 MiB, aktör başına 10 açık rezervasyon. Bunlar teknik yerel limitlerdir, denizcilik veya hukuk sınırı değildir. Rezervasyon eşzamanlı güvenli biçimde yapılır; korunmuş sürümler ve temizlenmemiş dosyalar kapasiteden düşülmez. Otomatik son nesne silme kapalıdır.

## Güvenlik ve kurtarma kararı

Dosya adları hiçbir zaman depolama yolu değildir. Linux üzerinde özel dizin tanımlayıcısı ve yalnız sunucunun ürettiği tek bileşenli adlarla exclusive/no-follow açma gerekir. Güvenli ortam doğrulanamıyorsa etkinleştirme reddedilir; uygulama OS izinlerini değiştirmez. Web/static dosya kökleri kullanılamaz.

DB ile dosya sistemi tek transaction değildir. Önce rezervasyon, sonra sınırlı akış ve bütünlük ölçümü, değişmez karantina nesnesi, en son DB pointer/link/durum/audit/receipt transaction'ı vardır. DB sonucu kaybolursa istemci yeni yükleme başlatmak yerine aynı yükleme kimliğinin durumunu sorgular. Depolanmış içeriğe tekrar POST başarı gibi yanıtlanmaz: dosya adı/boyut/isteğe bağlı istemci hash'i farklı içeriğin aynı olduğu kanıtı değildir.

Son nesneler otomatik temizlenmez. DB commit çevresinde belirsizlik erişilemeyen karantina nesnesi bırakabilir; kabul edilmiş dosyayı silen yarışa tercih edilir. Bu yerel kapasite/kurtarma sınırlamasıdır, tamamlanmış üretim temizleyicisi değildir. Politika eksikliği gerçek kişisel verinin süresiz saklanmasına onay değildir.

## Değerlendirmeler ayrıdır

Teknik alım `RESERVED`, `RECEIVING`, `STORED_QUARANTINED` ve terminal başarısızlık durumlarından oluşur. Tarama sağlayıcısı yoktur: `UNAVAILABLE`. SHA-256 hesaplanması `COMPUTED`, beklenen değerle eşleşme `MATCHED`; bunlar özgünlük veya güvenlik değildir.

`verificationAssessment=NOT_PERFORMED`, `operationalAssessment=NOT_PERFORMED`, `canDownload=false`. Download, HEAD/range içerik, thumbnail, preview, print/export, OCR/AI ve dolaylı signed URL yolları açılmaz. CLEAN, VERIFIED, EFFECTIVE veya READY üretilemez. İnsan doğrulama komutu uygulanmaz.

## Açık kapılar

Gerçek tarayıcı ve güvenilir sonuç kanalı; tarama tazeliği; üretim nesne deposu/şifreleme/anahtarlar/kapasite/izleme; gerçek veri sınıflandırması ve hukuk/saklama/legal hold; kontrollü yayın, sahip/verifier yetkileri; native HTTPS, gerçek IdP, kullanıcıya özel tarayıcı erişimi ve Esra kabulü ayrı kapılardır. Bu temel dört ekranlı Controlled Knowledge and Evidence modülünü tamamlamaz.

## Teknik kaynaklar

## lines:41-65

[Microsoft ASP.NET Core dosya yükleme rehberi](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0), uygulama tarafından seçilen dosya adı, ayrı depolama, boyut/uzantı doğrulaması ve güvenilmeyen içerik için tarama gereksinimini açıklar. Bu paket tarayıcı entegrasyonu yapmaz ve içerik kullanımını engeller.

[Linux open/openat sözleşmesi](https://man7.org/linux/man-pages/man2/open.2.html), dizin tanımlayıcısına göre açma ile O_EXCL/O_NOFOLLOW davranışının temelidir. Desteklenmeyen platformlarda daha zayıf pathname kontrolüne düşülmez.

## Son uygulama ayrıntıları

`/api/v1/evidence-intake` ve yalnız etkin BFF için `/bff/api/v1/evidence-intake` ayrı sözleşmedir. Binary akış POST kullanır; mevcut bearer uçları cookie kabul etmez. Browser işlemi ilk await öncesinde bütün reserve → content → status yaşam döngüsünü ortak logout kilidine alır. Her BFF yazısında CSRF ve mevcut scope stamp gerekir. Dosya veya token browser storage'a yazılmaz.

Kapalı metadata alanları `targetType,targetId,filename,declaredMime,expectedBytes,expectedSha256?,title?,source?,description?,capturedAt?`. NFC olmayan dosya adları ve teknik UTF-16 uzunluğu 128'i aşan adlar reddedilir; sunucu sessiz anlam değişikliği yapmaz. Noktalı çift uzantılar, yüzdelik kaçışlar, ayırıcılar, kontrol/bidi karakterleri kabul edilmez. Tarih tam ISO UTC timestamp'tir. CreatedAt DB UTC saatinden, ExpiresAt bundan 15 dakika sonrasından gelir. DetectedMime ve typeResult yalnız sınırlı imza eşleşmesidir.

Yerel adaptör UUID adında exclusive nesneyi doğrudan karantina kökünde oluşturur; staging→rename kullanılmaz. Commit edilene kadar DB object pointer NULL'dur. Dosya ve dizin fsync işleminden sonra pointer/state/audit transaction'ı gelir. Akış başlamadan yazılmış idempotency kaydı bir **niyet kimliği** taşır; başarı receipt'i değildir. Binary retransmission reddedilir, scoped GET gerçek son durumu çözer. Bu, tam byte fingerprint'li yeniden yükleme iddiası değildir.

Aynı evidence şeması tek yerel köke aittir. Başlangıçta kökteki her dosya no-follow/nonblocking açılır; generated ad, regular file, sahiplik/izin/tek link ve kota kaydı denetlenir. DB'deki her STORED_QUARANTINED nesnenin dosya boyutu/SHA-256 eşleşmelidir. Bilinmeyen, eksik veya değişmiş dosya başlangıcı durdurur; otomatik silinmez. Uygulama/source/static kök içinde depolama reddedilir. Bu Linux x64 yerel dosya sistemi adaptörüdür; farklı OS, ağ dosya sistemi veya üretim object store desteği iddia edilmez.

Supersede kapalı `{metadata,reason}` gövdesi ve eski If-Match ile yeni child rezervasyonu oluşturur. Hedef değişmez; gerekçe yalnız restricted saklanır. Child başarılı commit olduğunda eski alımın supersededBy projeksiyonu ve append-only bağlantı olayı oluşur. Eski dosya/metadata/hash ve sürüm değişmez; aynı eski sürümdeki bağlantı olayları eventId tie-breaker ile sıralanır. REJECTED/EXPIRED child yeni düzeltmeyi engellemez; RESERVED/RECEIVING/başarılı child için partial unique koruma vardır. Kesilmiş RECEIVING child belirsiz olduğu için önce kurtarma gerekir; otomatik devam yoktur.

Public history tam altı alandan oluşur; unknown action genel historical metadata etiketiyle sunulur. Notes/filenames/source/reason/actor hash veya raw audit serialize edilmez. ABORTED ve ERROR teknik vocabulary olarak ayrılmıştır; bu pakette istemciye açık abort/scan/release komutu yoktur.

Uygulama/API ve DB trigger sınırında silme/üzerine yazma yoktur. OS sahibi/root yöneticisinin fiziksel dosyayı silmesini önleyen WORM/immutable-filesystem garantisi verilmez; böyle bir altyapı ayarı yapılmadı. Eksik/değişmiş kabul edilmiş dosya yeniden başlangıç mutabakatını durdurur. Beklenen kapasiteye rağmen disk dolması veya disk arızası her zaman mümkün olduğundan gerçek işletim izleme/DR kapısı açık kalır.

## İnceleme düzeltmesi — kaynak varlığı ve yerel dosya seçimi

Alım kimliğiyle yapılan lookup, metadata okunmadan önce tenant/şirket ve güncel gemi owner/explicit-grant koşulunu SQL içinde uygular; sertifika hedefinde yetki aynı ebeveyn gemiden türetilir. Eksik veya erişilemeyen Vessel/Certificate alımı aynı `404`, `not_found`, `Evidence intake not found.` gövdesini döndürür. Hedefe ait ayrı hata mesajları sızdırılmaz. DB, CSRF, yetki ve validation hataları topluca yakalanıp 404'e dönüştürülmez. Bearer ve BFF detail/history/content testleri tam gövde eşitliğini denetler.

Yerel belirsiz alımı açık uyarıyla bırakmak native dosya seçimini de temizler. Aynı dosyanın yeniden seçilmesi yalnız yeni yerel seçimdir; otomatik istek göndermez, önceki sunucu alımını silmez veya başarısız saymaz. Başarılı alım ve açık yerel bırakma sırasında selection generation değiştirilerek input yeniden oluşturulur. Gerçek Chromium testinde aynı dosyanın tekrar seçilebildiği, yeni POST olmadığı ve commit olmuş eski nesnenin karantinada kaldığı denetlenir.
