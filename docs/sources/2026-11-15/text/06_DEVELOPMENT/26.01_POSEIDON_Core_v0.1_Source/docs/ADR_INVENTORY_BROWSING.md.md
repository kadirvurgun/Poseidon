# ADR_INVENTORY_BROWSING.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/docs/ADR_INVENTORY_BROWSING.md
SHA256: c02d5585dc5ca404ddeb6a854e012461d96d9cdb24485fb41af2ff445840e45a

## lines:1-40

# ADR INVENTORY-001 — sınırlı envanter taraması

Tarih: 2026-10-27. Taban: d0640b270c81f1db4e190c6f1c090794e08f51ab.
Durum: kullanıcının devrettiği ürün/mühendislik yetkisiyle uygulama kararı.
Bu karar eski kanonik kaynaklarda önceden onaylanmış bir sorgu sözleşmesi değildir.

## Kaynak ayrımı ve kapsam

İletilen kanonik 15.01§3 cursor/stable sort, 16.01 ise GET vessels için optional cursor
ve items/nextCursor belirtir. q/filtre/pageSize/hasMore semantiği bu ADR'nin geliştirme
kararıdır. VesselWrite tam root yazımı taslak değildir: company_id/name/flag/vessel_type
zorunluluğu mevcut eksik DRAFT metadata'ya taşınmaz. IMO isteğe bağlıdır; römorköre
zorlanmaz. Altı root alan ve belgelenmiş notes uzantısı değişmez. status readOnly kaynak
kataloğu donmuş sayılmaz; flag state-machine alanı değildir. 27 ekran alanı root modele
aktarılmaz. Yaşam döngüsü/aktivasyon/silme/sertifika geçerlilik kararı/geçmiş okuma yoktur.

## Sorgu kararı

Her iki gemi listesi (`/api/v1/vessels`, `/bff/api/v1/vessels`) aynı sorguyu kullanır:

| Parametre | Kural |
|---|---|
| q | Trim sonrası en çok 100 UTF-16 karakter; name/imo_no/official_no içinde literal alt dize. |
| flag | Trim sonrası en çok 200; mevcut metin değerinde tam, büyük/küçük harfe duyarlı eşleşme. |
| vessel_type | flag ile aynı; yeni enum/katalog üretilmez. |
| pageSize | Varsayılan 50; 1–100 tam sayı. |
| cursor | Optional opaque continuation, en fazla 512 karakter. |

Bilinmeyen/yinelenen parametre, kontrol karakteri, geçersiz size veya cursor 400 döner.
Trim öncesi q en çok 200, filtreler 400 karakterdir; daha uzun padding reddedilir.
Boş trim edilmiş arama/filtre, filtre yok anlamına gelir. '%' '_' ve '\' wildcard değil
literal karakterdir; SQL parametreleri ve escape uygulanır. Q yalnız ASCII A–Z→a–z
katlanır; PostgreSQL C collation ile eşleşir. Örn. ISTANBUL, istanbul ile eşleşir ama
İSTANBUL ile eşleşmez. Türkçe dilbilimsel collation, accent folding veya tüm Unicode
case-insensitivity iddiası yoktur. UI dili sorgunun sonucunu değiştirmez.

Sıra PostgreSQL UUID artandır. Bir sonraki sayfa son UUID'den büyük kayıtlarla oluşur.
pageSize+1 çekilerek `hasMore` ve `nextCursor` belirlenir. Yanıt `{items,nextCursor,hasMore}`;
son sayfada null/false, boş sayfada []/null/false olur. Toplam sayı hesaplanmaz.

## lines:41-77

Cursor version/after/query-scope digest içerir. Normalize sorgu, filtreler, pageSize,
subject/tenant/company ve güncel gemi grant kümesiyle eşleşmezse 400 olur. Cursor
şifreli/imzalı kimlik belgesi değildir; yeniden üretilebilmesi veya konumun seçilmesi
yetki kazandırmaz. Her sayfada server scope ve creator-or-explicit-vessel-grant SQL
koşulları tekrar uygulanır. Tenant/company seçimi istemci sorgusu değildir. BFF'nin
ayrı cookie/CSRF/scope önkoşulları ve bearer sınırı korunur.

## Uyumluluk ve veri değişimi

`items` alanı korunur; `nextCursor` kanonik sayfalama alanı, `hasMore` yeni response
uzantısıdır. Parametresiz çağrı önceden ilk 500'ü döndürürken artık ilk 50'yi döndürür;
bu gözlenebilir semantik değişikliktir. Tüm in-repo gemi tüketicileri continuation'a
uyarlanmıştır. Dış istemciler tek yanıtı tam envanter saymamalıdır. Sertifika liste API'si
bu dilimde eski 500 sınırı ve mevcut davranışı korur; tam sertifika modülü değildir.

Cursor bir snapshot değildir. Değişmeyen veri kümesinde devam sayfaları eksik/tekrarsızdır.
Cursor öncesine eklenen UUID mevcut devamda görülmez; ilk sayfadan yenileyince görünür.
Sonraki kayıtların güncellenmesi filtre üyeliğini değiştirebilir. UUID aynı kaldığı sürece
sıra değişmez; kullanıcı sayfalar arası point-in-time tutarlılık varsaymamalıdır.

## UI ve test sınırı

Gemi listesinde Ara/filtrele, Filtreleri temizle, İlk/Sonraki sayfa ve Yenile vardır.
Arama/filtre değişikliği uygulandığında cursor sıfırlanır. Sayı sadece yüklenen sayfanın
kayıt sayısıdır. Sertifika yeni taslak gemi seçicisi ayrı server araması ve daha fazla
gemi yükleme kullanır; ilk sayfa/500 sınırına bağlı değildir. Otomatik ilk seçim yoktur;
seçilmiş değer arama sonucu değişince korunur. Dirty-edit terk onayı devam eder.

Fetch abort ve bağımsız request sequence kontrolleri liste/seçici/oturum değişimlerinde
eski cevapları bastırır; abort'u dikkate almayan transport da test edilir. Kaydetme sonrası
listeye dönüş sunucudan yeniden yükler, yerel prepend sırasını sayfalama sonucu saymaz.

Yeni şema/migration yoktur. Mevcut tenant/id primary key keyset'i destekler; contains
araması büyük tenant'larda tarama gerektirebilir. Üretim yük/performance/indeks planı
NOT_RUN'dır; bu dilim üretim kapasitesi taahhüdü değildir. Fixture DDL testleri aynı xUnit
collection içinde sıralanır; gerçek eşzamanlı idempotency/version transaction testleri
Task.WhenAll ile korunur. Audit okuma API'si veya yeni hassas veri görünürlüğü eklenmez.
