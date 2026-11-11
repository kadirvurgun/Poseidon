# Bakım gözlemi komut kurtarması — 12.11.2026

**AUTHORED_UNVERIFIED; acceptance=false.** Kaynak kontrol noktası `972d40ed1b92e7864c09d927886ec3be96c52f4e`.
Yeni test/build/typecheck/lint/browser yürütmesi **DEFERRED / ÇALIŞTIRILMADI**.
Bu yerel teknik alt paket, tam S8 veya canonical DEV-053/056 kabulü değildir.

## Analizdeki yer

DEV-053/T-S8-002,18.01Backlog54 ve mevcut ekipman/bakım/gözlem temeli;
11.01sheet2316 Alan v46.3/row1413 FR46-maintenance-plan-meter_source ve TC46 karşılığı;
15.01paragraph67/71/73 ve13.04paragraph10–24. Kabul edilmiş çapraz
DEV-014/015/019 güvenlik sınırları korunur. [Görev sözleşmesi](../tasks/MAINTENANCE-OBSERVATION-OWNER-RECOVERY-01.md).

## Yazılan davranış

Gözlem formu asıl tenant/actor ve gemi/ekipmana bağlı kalır. Değişen kullanıcı
veya askıya alınan görünüm eski özel DOM'u göremez. Kaynak okuması, odak,
callback kimliği veya aynı kullanıcının yeni CSRF değeri taslağı/komutu silmez.
Güncel gerçek oturum ve owning gözlem yetkisi gönderim öncesi okunur; güncel
CSRF kullanılır. Cevaptan sonra kimlik, kapsam, abort ve unmount yeniden denetlenir.

Kayıp cevap sonrası401/403/404/CSRF/çatışma veya politika reddi önceki belirsiz
komutu temizlemez. Erişim yenilemesi yalnız okur; asıl kullanıcının açık retry
işlemi aynı yol/anahtar/UUID/gövdeyi gönderir. Mevcut backend capture UUID/gövde
eşitliği ve ayrı revocation satırını uygular; actor-scoped generic CommandReceipt
veya yeni onay politikası icat edilmedi. Başarı cevabının kaynak kimliği ve şekli
denetlenir; bozuk200 belirsiz kalır. Doğrulanmış sonuç sonrası okuma hatası yeni
capture/revoke üretmez. Güncel erişim doğrulanmadan eski özel kayıt kimliği gösterilmez.

Üst teknik çalışma alanı işlem sürerken veya sonuç belirsizken navigasyonu ve
discard'ı engeller. Açık discard onayı gözlem bileşenini de temizler; geri dönüş
atılmış özel girdileri yeniden göstermez. Ortak enterprise düğmeleri ve mevcut
17dil mesajları kullanılır. Kalıcı özel browser depolaması, backend iş mutasyonu,
migration, yeni operasyonel kaynak/uzman onayı veya bağımlılık eklenmedi.

## Kanıtın sınırı

UI/adapter/üst ekran testleri kodla birlikte yazıldı. Altı gerçek API/PostgreSQL
vakası izin/politika reddinden sonra aynı gövdeyle kurtarma, yeni gerçek CSRF,
tek immutable satır/business audit, revocation tekrarı ve foreign tenant/asset
sınırlarını sınamak üzere yazıldı. Yeni kayıtlı gerçek-host browser senaryosu
route.fetch ile gerçek commit/replay sonrası kontrollü cevap kaybı ve readback
kesintisini, yeniden gönderim öncesi session okumasını ve tekUUID/ikiPOST'u kapsar.
**Hiçbir yeni test veya build çalıştırılmadı; altı vaka PASS değildir.**

Bağımsız UI ve API kaynak incelemeleri ayrı exact-target JSON/hash ile saklanır;
bunlar derleme, gerçek PG/OIDC/browser veya final kabul kanıtı değildir.
OBS-S01 ilk77732f9hedefinde çocuk okumasının auth reddini üst ekrana bildirmediğini
buldu.278797d üst kapsamı da bir kez yeniler; sürekli ret tekrar-yükleme döngüsüne
girmez. Özgün bulgu ve sonraki test kaynağı korunur. Önceki yerelleştirme
23b87d4hedefindeki1437/1437/lint/typecheck/build sonuçları yeni teknik koda taşınmadı.

## Açık kapılar ve sonraki adım

Browser önkoşulu **PREREQUISITE_OPEN**: genuine disposable yazara observation.write
ve seed rootasset=vessel üzerinde gerçek sentetik kaynak konfigürasyonu gerekir.
Eksikse senaryo hard-fail eder; skip, sahte200, sahte yetki veya fakepolicy yoktur.
Yeni büyük teknik paket toplu yürütmesi, bağımsız final-targetQA ve requiredCI
bekler. Yerelleştirmenin bütünleşik/browser kapıları ve PR16tam genuine signed
kapısı ayrı açıktır; yeni PR/push/uzak merge yapılmadı. Sıradaki iş, bu owning
komut paketini gerçek fixture ile toplu teslim kapısına hazırlamaktır.

Canonical kabul **20/121iş,103/970SP=%10,62;0/196ayrıekran** olarak değişmedi.

Güncel14.02 Data Dictionary2316/row1413 sayısal precision/physical-mapping taslağı REVIEW_REQUIRED kalır. Mevcut gözlem taşımasının kurtarılması bu alanın lookup/birim/üretim politika onayı değildir. Bağımsız UI/browser kaynak incelemesi kapsam içi açık0; API kaynak incelemesinde somut engelleyici bulgu yok. İkisi de test yürütmesi veya final kabul değildir.

AST Graphify bakım güncellemesi exit0:19.310düğüm,39.860kenar.783sıfır-düğümlü kaynak graf dışında kaldı; HTML büyük grafik sınırında atlandı. Bu türetilmiş metadata ürün testi/derlemesi veya kabul kanıtı değildir.
