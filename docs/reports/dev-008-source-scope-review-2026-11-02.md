# DEV-008 bağımsız kaynak kapsamı incelemesi — 02.11.2026

**Karar: APPROACH ACCEPTABLE AS PLAN; mevcut çıktı GAP / NOT ACCEPTED.**

S0 işi, kaynaklara bağlanmış ve bağımsız incelenmiş açık bir sorumlu-rol kaynak
sahipliği planı olarak karşılanabilir. İncelenen kaynaklar bu S0 kriterinde named
live kişi, tenant membership, eligible actor ataması veya operasyonel assignment
API/UI'sini zorunlu kılmıyor. Tam authoritative corpus doğrulaması ve gerçek
Regulatory SME yayın/pilot onayları sonraki ayrı kapılardır. Bu ayrım, row9'daki
“atanır” veya “version/validation status” alanlarını boş bırakma izni değildir.

Mevcut [ownership planı](../tasks/source-ownership-plan-01.md) açıkça **proposed**
ve **pending interpretation/review** diyor. Önerilen roller ve serbest metin
“version pending” ifadeleri, kabul edilmiş bir plan ataması/versiyon kaydı yerine
geçmez. Bu inceleme scope yorumunu sağlar; planı düzeltmez, DEV-008'i kabul etmez,
legal validation, yayın izni veya ürün QA'sı üretmez. Herhangi bir test/validator,
build veya CI çalıştırılmadı.

## İncelenen exact kaynaklar ve çıkarım

Etkin baseline [poseidon-v46.3-20261030](../sources/active.json). Yalnız sınırlandırılmış
yerel kaynak retrieval/locator okumaları yapıldı. Kaynak metinleri güncel hukukun
doğru yorumu olarak dışarıdan doğrulanmadı; burada incelenen konu repo gereksinim
sınırıdır.

| Kaynak / locator | Kaynakta söylenen | Bu inceleme için sonucu |
|---|---|---|
| **18.01 Backlog / row9**, DEV-008, T-S0-008,5SP,S0,owner Regulatory Lead,dep DEV-007 | Başlık “Pilot regulatory source ownership planı”; AC: ISM/SOLAS/STCW/MLC/MARPOL/Flag/Class pilot kapsamı için source owner, version, validation status ve reviewer atanır. | Bütün yedi kategori için somut atama/versiyon/durum kaydı gerekir. Başlık yalnız öneri listesine indirgemeye yetmez; AC'de named-person/live-membership/runtime handler ifadesi de yoktur. |
| **18.01 RACI / rows1,4** | Kolonlar PO/BA/Architect/Backend/Frontend/QA/DevOps/Regulatory SME/UX/Pilot Customer; applicability için PO A,BA R,Regulatory SME A/R. | Kaynak sorumlulukları rol düzeyinde ifade ediyor. RACI, bir deployment hesabına/grant'a veya adlandırılmış gerçek kişiye atama değildir. |
| **08.01 Kaynak Register / row1**, rows2–6,15,17–19 | Owner kolonu; değerler IMO,ILO,UAB DGM,IACS/Türk Loydu/Türk Loydu. Kaynak kimliği,instrument,lifecycle,URL ve controlled note bulunuyor. | Buradaki Owner kaynak yayımlayıcısı/otoritesidir; bunu internal owner/reviewer rolü veya tenant actor diye kullanmak hatalıdır. Plan issuer ile internal responsibility'yi ayırmalıdır. |
| **08.01 Açık Hukuki Doğrulama / rows2–7** | VAL-01–06 OPEN; ISM/flag,Turkish crew/class/ocean-vs-harbour/MLC belirsizlikleri; SME sahip rolleri ve false-applicability guardrail'leri tanımlı. | Kaynak setinin kendisi hukuki doğrulamanın açık olmasına izin veren candidate envanteridir. Bir planın validation status'u OPEN/UNVERIFIED olabilir; CURRENT lifecycle bunu VERIFIED yapmaz. |
| **18.01 Backlog / row74**, DEV-073,S11,dep DEV-008,040,041 | Pilot vessel profile için authoritative source/version reviewer sign-off; açık ambiguity REVIEW_REQUIRED ve tracked issue kalır. | Gerçek pilot source/version sign-off'u DEV-008'den sonraki açık ayrı iştir. S0 planını DEV-073'ün tamamlanması şartına çevirmek bağımlılık sırasını tersine çevirir. |
| **18.01 Backlog / row94**, DEV-093,S11,dep DEV-008,084,087 | Flag/class/company/local source ownership assigned; unknown rules REVIEW_REQUIRED; unsupported legal claim yok. | Daha somut Tug operasyon source/applicability review ayrı sonraki kapsamdır; S0 planı otomatik operasyonel izin değildir. |
| **00.01 START HERE / paragraph116** | Regulatory source register release/final legal claim öncesinde source/version refresh gate'inden geçer. | S0 sorumluluk planı review ile release/final legal corpus refresh farklı kapılardır. |
| **18.01 DoR DoD Gates / rows3–5** | DoD acceptance/code review/tests/docs/current trace; Pilot Gate regulatory SME review dahil PASS; Production Gate regulatory source ownership current. | Planın kendi test/review/doğruluğu gerekir; pilot/production otoritesi plan yapısal kontrolünden türemez. |
| **09.01 / paragraphs398–406,411–424** | Source version/lifecycle kaydı,SME review, golden/diff,second authorized reviewer publication; Author draft yapar,SME review eder,Approver publish eder;admin/AI bu otoriteyi alamaz. | Live rule publication ve separation-of-duties runtime sınırı gerçektir; S0 engineering planı bunları atlayamaz veya onların onayıymış gibi kullanılamaz. |
| **19.01 / paragraphs125–126,174** | Exact flag/class/local/company SMS source/version/reviewer; kaynaksız yasal default yok; pilot ship/tug review complete,unknown explicit. | Gerçek pilot/operasyon kapsamına geçerken exact authoritative bağların ayrıca tamamlanması gerekir. S0'daki declared candidate version bunun yerine geçmez. |

SHA-256 kaynak kimlikleri:

- 18.01: `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.
- 08.01: `c36c72dec31efe45d1f894214b10708a017d0c8a5dfb96fe0d3e1498d44145da`.
- 00.01: `4cf7f9872a50d81d7cbc15c748965e139555bdb3bc5b859706331fb273b683b0`.
- 09.01: `8a338adb39f2ec62ff149d0c5e75c8df266f03107b7de5630b52685143bc3f0c`.
- 19.01: `063823ea7d453adc91033c6eabb17f60906b4e6b9b4595be884eb12e02040a49`.

23.03 için sınırlı Regulatory araması bu konuda doğrudan ek kriter döndürmedi;
bu kaynak, S0 owner planına yeni bir live appointment zorunluluğu üretmek için
kullanılmadı. Dış IMO/ILO/UAB/Class URL'leri yalnız candidate kaynak kayıtları
olarak okundu; güncel legal validation yapıldığı ileri sürülmüyor.

## S0 planı için eksiksiz sayılabilecek minimum içerik

Aşağıdaki alanlar row9'un uygulanabilir bir encoding'idir; JSON/validator kullanımı
engineering tercihtir, kaynak tarafından ayrıca dayatılmış bir format değildir.

1. **Kapsam ve source identity:** yedi kategorinin hepsi; her seçili kaynak için
   source ID,instrument,issuer ve exact candidate dosya/hash/sheet/row. Örneğin
   ISM→SRC-ISM/row2,Class→SRC-IACS-M79/17,SRC-TL-OCEAN-TOW/18,SRC-TL-BP/19.
   Flag/class seçimi koşulluysa seçme kuralı ve çözülmemiş pilot binding açık olsun;
   keyfi pilot flag/class veya evrensel rule pack seçilmesin.
2. **Assigned plan owner ve reviewer:** açık internal accountable role ve review
   role; yalnız issuer “IMO” veya “SME TBD” yetmez. Roller bir plan decision'ıyla
   gerçekten benimsenmiş sorumluluklar olarak kaydedilsin. Bu kayıt bir runtime
   membership/grant veya kişinin gerçek yeterlik onayı olarak serileştirilmesin.
3. **Version:** candidate'da gerçekten beyan edilmiş instrument/version metni
   source locator'ına aynen bağlansın; candidate baseline/hash/locator sürümü
   ayrıca korunsun. “SOLAS1974 as amended”, “MLC2006 as amended”,“2023/19”,
   “Chapter58…2022” gibi mevcut deklarasyon ile **authoritative edition/amendment
   resolution status** ayrı olsun. Sadece “version pending” boşluğu yeterli
   değildir; hiç edition belirtilmeyen kaynağa tarih/edition uydurulmaz.
4. **Validation status:** her kaynakta açık UNVERIFIED/OPEN/REVIEW_REQUIRED gibi
   dürüst durum, dayanak ve açık version/applicability sorusunun takibi bulunsun.
   Candidate `Lifecycle=CURRENT` kopyalanabilir bir source assertion'dır; hukuki
   doğrulama durumu değildir. VERIFIED/CURRENT_LEGAL gibi yeni bir onay üretilemez.
5. **Review/adoption kaydı:** engineering/source-scope reviewer, exact target,
   karar/tarih ve sorumluluk planının hangi revizyonunu review ettiği açık olsun.
   Bu inceleme yalnız scope yorumudur; sonradan düzeltilen plan revizyonunun
   otomatik approval receipt'i olarak kullanılamaz.
6. **Activation/exit sınırı:** unresolved exact legal edition/flag/class/reviewer
   qualification girdileri için owner/next step ve DEV-073/093/release-refresh
   bağımlılığı korunmalı. Plan review'ü operational publication,verified evidence,
   permit veya pilot sign-off'u etkinleştirmesin.

Bu biçimde **declared version var ama authoritative exact edition hâlâ unresolved**
olabilir: kaynak row9 “validation status” ister; VERIFIED sonuç şartını söylemez.
Ancak “version bilinmiyor” diye alanı kaldırmak veya candidate belge hash'ini
mevzuatın exact yürürlükteki edition'ı diye göstermek kriteri karşılamaz.

## Mevcut planın somut GAP listesi

| Alan | Mevcut durum | Kabul öncesi gereken |
|---|---|---|
| Kategori kapsamı | Yedi kategori ve candidate source row'ları mevcut. | Seçili source-ID envanterini açıkça dondur; Flag/Class koşulluluğunu koru. |
| Owner/reviewer | Proposed role sütunları var; plan ataması/review kararı pending. | İncelenmiş/adopt edilmiş exact plan responsibility kayıtları; gerçek runtime atama iddiası yok. |
| Version | Instrument ve bazı tarih/numaralar var; exact edition pending serbest metniyle iç içe. | Declared candidate metin/provenance sürümü ile authoritative resolution status ayrı ve eksiksiz kayıt. |
| Validation status | “Candidate CURRENT; no validation” gibi metin var. | Lifecycle assertion'dan ayrı, source başına açık tutarlı durum ve open issue/next step. |
| Plan review | Bağımsız interpretation/review PENDING yazıyor. | Düzeltilmiş exact plan revizyonunun bağımsız engineering/source-scope review kanıtı. |
| Legal activation | Açık ve yasaklar doğru biçimde belirtilmiş. | Aynen korunmalı; plan acceptance ile hukuki/operasyonel approval üretilemez. |

**Live operational assignment yazılımı bu S0 plan yorumu için önkoşul değildir.**
Sonraki gerçek source-governance/qualification/operasyon kaynağı güncel eligible
owner/reviewer binding'i gerektiriyorsa bu ayrıca yapılabilir yazılım ve gerçek
yetkili binding işidir; bu rapor o gereksinimi kaldırmaz.

## Parent için uygulanabilir sonraki adım

Mevcut planı yukarıdaki alanlarla makinece okunabilir şekilde ifade etmek,
source-byte/locator/candidate declaration doğrulaması ve duplicate/missing
category/issuer-owner karışması/false-VERIFIED/yanlış version eşlemesi/activation
negatif testleri yazmak uygundur. Validator yapısal/source uyumu ölçer; legal
truth, reviewer yetkisi veya acceptance üretmez. Yazım tamamlanınca daha sonra
tek koordineli gate ve düzeltilmiş exact planın bağımsız incelemesi gerekir.
DEV-007 bağımlılığı ile S0 exit review ayrıca açık kalır.

Mevcut kabul puanı değişmedi. Bu rapor sahibi ürün/plan düzeltmesi yapmadı;
yalnız bağımsız scope kararı ve somut açık alanları kaydetti.
