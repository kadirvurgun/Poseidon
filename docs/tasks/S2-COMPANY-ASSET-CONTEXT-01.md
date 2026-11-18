# S2-COMPANY-ASSET-CONTEXT-01

Durum: **AUTHORED / TEST EXECUTION DEFERRED / NOT ACCEPTED**. DEV-021 / T-S2-002
kapsamının yeni 15.11 analizleriyle ayrıntılandırılmış şirket ve asset bağlamı alt
paketidir. Canonical görev veya story point eklemez.

## Kontrollü kaynak ve karar

- Aktif 18.01 Backlog `sheet:Backlog/row:22`, SHA256
  `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`:
  görünür vessel/rol topbarı, sunucuda vessel-switch yetkisi ve kaydedilmemiş kritik
  form uyarısı; bağımlılıklar DEV-015 ve DEV-020.
- Yeni 65.03 UX analizi `paragraph:42-66`, SHA256
  `3ce010297c5d74f3686a6054b69690e1b7ca196d8acf4ec50ba077bb21fd72c9`:
  her sayfada aktif şirket, vessel/tug, mode, etkin rol/vekalet, dil ve
  bağlantı/sync bağlamı; eski isteklerin iptali ve yalnız güncel generation
  sonucunun gösterilmesi. Dil veya görünüm iş anlamını ve yetkiyi değiştirmez.
- Yeni 59.01 Fleet master analizi `paragraph:55-72`, SHA256
  `daa1a931b1fa90a4a4f3418700264980ced60691673c919888448da7c1bbce7f`:
  tenant izolasyon kökü; Company/LegalEntity ve Vessel ayrı kimliklerdir; filo
  grubu güvenlik grantı değildir.
- Yeni 54.02 yetki analizi `paragraph:46299`, SHA256
  `dee6c269a0a144bbea87ee57c5cf92c8262297d154083742a094c3c3407d50e6`:
  `activeCompanyId` yalnız doğrulanmış uygulama oturum kapsamıdır; geminin
  operator/legal owner/managing company alanlarından türetilemez. Aktif şirket
  tek başına gemiye veya başka kaynağa erişim hakkı vermez.

Yeni belgeler hedef yazılım sözleşmesidir; çalışan yazılım, hukukî sahiplik,
operasyon izni veya insan onayı olduklarını iddia etmez. `mode`, mevcut server
Vessel kind projectionından Ship/Tug olarak gösterilir; yeni bir operasyonel
state, kabiliyet veya readiness sonucu üretilmez.

## Kabul ölçütleri

| Kimlik | Ölçüt |
|---|---|
| CAC-01 | Server-provisioned membership içindeki activeCompanyId, aynı tenanttaki mevcut Company kaydına çözülür. Request/IdP role/vessel owner alanı şirket veya aktör seçemez. |
| CAC-02 | Yapılandırılmış aktif şirket aynı tenantta çözülemiyorsa kısmi/eski bağlam gösterilmez; okuma güvenli `503 context_unavailable` ile kapanır ve auditlenir. |
| CAC-03 | Aktif şirket ve vessel scope bağımsızdır: vessel seçilmemişken şirket korunabilir; vessel CompanyId aktif şirkete eşit olmak zorunda değildir. Eksik aktif şirket açık Unavailable gösterilir ve vesseldan türetilmez. |
| CAC-04 | Ortak topbar şirket, tam vessel kimliği, Ship/Tug modu, server-configured rol ve bağlantı durumunu ayrı metin/ikonlarla gösterir. Ham actor/company/role ID veya credential göstermez. |
| CAC-05 | Aktör/vessel/session/generation değişince eski company/vessel/rol eşzamanlı gizlenir; geç cevap yeni bağlamı dolduramaz. Denial ve logout eski etiketi geri getiremez. |
| CAC-06 | Yeni iki sunum etiketi 17 yerel katalogda tamdır; Arapça RTL ve mevcut katalog bütünlüğü korunur. Çeviri hiçbir yetki veya domain state üretmez. |

## Uygulama ve yazılmış kontroller

- Backend: `Membership.ActiveCompanyId`, `TrustedActor` taşıması,
  `WorkspaceCompanyDto`, mevcut tenant filtresinden şirket çözümü ve kısmi bağlam
  için fail-closed sonuç. Vessel grant değerlendirmesi ayrı kalır.
- Client wire boundary: bağımsız company/vessel doğrulaması, seçimsiz vessel,
  malformed company reddi ve company-vessel ID eşitliği varsaymama.
- React: şirket/mod sunumu, eski şirketin yeni generationda temizlenmesi, TR/EN
  davranışı ve ID/credential sızdırmama.
- Browser: mocked layout/dirty-switch akışlarının yeni strict sözleşmesi ve gerçek
  register yolculuğunda API şirket etiketi/mod eşliği.
- Backend gerçek PostgreSQL testi: yetkili vesselın gerçek bağlı şirketi ve
  seçimsiz null projection; foreign/denied cevaplar etiket/sayı sızdırmaz.

Kullanıcının faz sonu test kadansı nedeniyle test/build/typecheck/Playwright bu
authoring adımında çalıştırılmadı. Sonraki büyük kapı; kaynak doğrulama, backend
unit/API/PostgreSQL, frontend lint/typecheck/component/build, gerçek browser,
bağımsız exact-target QA ve required CI sonuçlarını aynı hedefte kapatmalıdır.
