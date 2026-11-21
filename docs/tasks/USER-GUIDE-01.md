# USER-GUIDE-01 — Kullanım kılavuzu ve sayfa içi yardım (analiz + plan)

Durum: **ANALİZ / PLAN** (21.11.2026). Kod yok. Kullanıcı isteği: "uygulama bitince resimli, adım adım kullanım dokümanı; ayrıca her sayfada o sayfanın kullanım kılavuzu. Uygulamayı hiç bilmiyoruz." Türetilmiş ek gereksinim kimliğidir; canonical iş/SP değildir (121 iş / 970 SP değişmez).

## 1. Sorun

Ürünün 53 ana sayfası (`frontend/src/product/enterprise-navigation.ts`) ve 17 dili var. Kullanıcıya bunları nasıl kullanacağını anlatan hiçbir içerik yok:
- Uygulama içinde yardım paneli yok (kodda `HelpPanel` veya benzeri bir bileşen bulunmuyor).
- Elde basılı veya PDF kılavuz yok.
- Playwright altyapısı var (`tests/Poseidon.Browser.Tests`), ama yalnızca hata anında ekran görüntüsü alıyor. Dokümantasyon görüntüsü üretilmiyor.

Bu ürünün dili ayrıca zor: READY, CONDITIONAL, REVIEW_REQUIRED, UNKNOWN; "tamamlandı ≠ doğrulandı ≠ etkili"; "Neden" ağacı; offline kuyruk ve çakışma. Kullanıcı bunları yanlış okursa sonuç sadece kötü bir deneyim olmaz, yanlış emniyet kararı olur. Kılavuz bu yüzden emniyet açısından da gerekli.

## 2. Kaynaklar (aktif set)

| Locator | Ne söylüyor | Bu işe etkisi |
|---|---|---|
| `04_UX_PRODUCT_DESIGN/13.03_POSEIDON_v46.3_Rol_Bazli_Kullanici_El_Kitabi_TR.docx`, paragraf 1–125 | Rol bazlı el kitabı. §1 ortak kullanım modeli (bağlamı doğrula → Today → Neden → kontrollü işlem → kanıt → doğrulama). §2–9: Master, Tug Master, DPA/HSEQ, Chief Engineer, Crew/OOW, Shore Ops, Regulatory, Tenant Admin görevleri ve kullanacakları yüzeyler. §10 hata/yardım akışı (offline, yetki yok, beklenmedik readiness, doğrulanamayan kayıt, uygulama hatası → eskalasyon) | Kılavuzun bölüm iskeleti ve rol bölümleri buradan gelir. Kaynakta olmayan bir iş akışı uydurulmaz. |
| `son analizler 15.11.26/65.03_POSEIDON_Kullanici_Deneyimi_TR_EN_ve_Erisilebilirlik_Tam_Analiz_TR(1).docx`, paragraf 46–64 | Sayfa düzeni sözleşmesi. "Yardım paneli: Neden ve AI ayrı başlıklı sekmeler; panel açılması otomatik komut veya AI çağrısı başlatmaz; mobilde ayrı tam sayfa olabilir." (p57–59). Menü araması gizli menü adı veya sayısı sızdırmaz (p50). 320 px'te taşma olmaz (p69–71). | Sayfa kılavuzu bu panelin bir sekmesi olur. Panel yan etkisizdir ve yetkisiz sayfa adlarını göstermez. |
| `01_BUSINESS_DOMAIN/01.02_Denizcilik_Saha_Bilgisi_ve_Yazilim_Analizi_El_Kitabi_POSEIDON.docx`, paragraf 23 | "Okuma rotası ve kullanım kılavuzu" | Terim sözlüğü ve saha bilgisi kaynağı. |
| AGENTS.md / CLAUDE.md UI kuralları | UNKNOWN asla yeşil "hazır" görünmez; doğrulanmış kanıt veya imza seed edilmez | Kılavuzdaki ekran görüntüleri sentetik veriyle alınır. Sahte "doğrulandı" durumu sahnelenmez. |

## 3. Kapsam

İki çıktı var. İçerik **tek kaynaktan** üretilir, ikisi ayrı ayrı yazılmaz.

**A. Sayfa içi kılavuz (her sayfada).** Sayfa başlığının yanında `?` düğmesi olur; `Shift+?` kısayolu da paneli açar. Panel 65.03 yardım panelidir ve üç sekmesi vardır:
- **Kılavuz:** bu iş paketinde yapılacak sekme.
- **Neden:** mevcut "Why" görünümü buraya bağlanır.
- **AI:** gelecek iş. Bu pakette kapalı kalır.

**B. Kullanım kılavuzu (doküman).** Resimli ve adım adım. Hem HTML (uygulama içinde `/app/help` ve çevrimdışı) hem PDF olarak çıkar. Rol bazlı bölümlerden oluşur.

Kapsam dışı: video eğitim, AI asistan cevapları, gerçek müşteri verisiyle alınmış ekran görüntüsü.

## 4. İçerik modeli (tek kaynak)

Her sayfa için bir dosya: `frontend/src/help/content/<dil>/<sayfa-anahtarı>.md`. Sayfa anahtarı `enterprise-navigation.ts`'teki `key` değeridir (örnek: `tug-job-board`).

Dosyanın başında sabit alanlar olur: `key`, `title`, `roles` (13.03 rolleri), `permissions` (sayfayı açan yetkiler), `related` (ilişkili sayfa anahtarları), `screenshots`, `reviewed` (`false` / `{by, date}`). Gövdenin bölümleri sabittir:
1. **Bu sayfa ne işe yarar** — tek paragraf; sayfanın cevapladığı ana soru (65.03 p52).
2. **Kim kullanır** — rol ve tipik görev (13.03 §2–9).
3. **Başlamadan önce** — doğru şirket/gemi/römorkör bağlamı, gereken yetki, ön koşul kayıtlar.
4. **Adım adım** — numaralı adımlar. Her adımda ekran görüntüsünde numaralı bir işaret olur.
5. **Durumlar ne anlama gelir** — sayfada görülen rozetler. Örnek: READY, REVIEW_REQUIRED, UNKNOWN; "Tamamlandı ≠ Doğrulandı".
6. **Yapamazsınız / dikkat** — insan karar yetkisi; kaydı yazanla doğrulayanın ayrı kişi olması; offline çakışmanın otomatik birleşmemesi.
7. **Sorun olursa** — 13.03 §10 tablosundan o sayfaya uygun satırlar.
8. **İlgili sayfalar.**

Ek dosyalar:
- `frontend/src/help/content/<dil>/_manual/*.md`: Başlarken (giriş, bağlam seçimi, menü, arama, tema, dil, offline), Ortak kullanım modeli, rol bölümleri, Sözlük, Hata/yardım akışı.
- Doküman (B), bu sayfa dosyalarını ve manual dosyalarını rol sırasıyla birleştirerek üretilir.

## 5. Kararlar

| # | Karar | Gerekçe |
|---|---|---|
| D1 | Tek kaynak Markdown, frontend paketine gömülür | Panel çevrimdışı çalışır (PWA). Doküman ile panel birbirinden sapmaz. |
| D2 | Panel açmak ağ çağrısı yapmaz, komut veya AI başlatmaz | 65.03 p59 |
| D3 | Panel yalnız kullanıcının erişebildiği sayfaların kılavuzunu ve bağlantılarını gösterir | 65.03 p50: gizli menü adı sızmaz. "İlgili sayfalar" listesi kullanıcının yetkisine göre süzülür. |
| D4 | Ekran görüntüleri otomatik üretilir | Playwright betiği OIDC lab'ına karşı çalışır. Veri sentetik seed artı opt-in demo fixture'larıdır (TugDemoFixtures vb.). Görüntüler deterministiktir: sabit tarih ve saat, açık tema, Zümrüt palet, 1440×900 ve 390×844, TR ve EN. Numaralı işaretler CSS ile görüntünün üzerine bindirilir. Altbilgide commit kimliği yazar. Elle ekran görüntüsü alınmaz; ürün değişince görüntü yeniden üretilir. |
| D5 | Ekran görüntüsünde sahte doğrulanmış durum olmaz | Kural gereği doğrulanmış kanıt veya imza seed edilmez. Bir adım doğrulama gösteriyorsa görüntü "beklemede/UNKNOWN" halini gösterir ve sonucu metin anlatır. |
| D6 | Diller: TR ve EN zorunlu; diğer 15 dil taslak | 15 dil makine çevirili taslaktır ve "Çeviri gözden geçirilmedi" etiketi taşır. Eksik kalan sayfa EN'e düşer ve bu açıkça belirtilir. Uygulamadaki i18n yaklaşımıyla aynıdır (`scripts/i18n_untranslated.py`). |
| D7 | İçerik `reviewed:false` başlar | Denizcilik doğruluğu için insan alan uzmanı (SME) ve UX yazarı onayı gerekir. AI bu onayı veremez ve kayıt üretmez. Onaysız sayfada "taslak kılavuz" etiketi görünür. |
| D8 | Kılavuz metni karar vermez | "Bu durumda gemi kalkabilir" gibi cümle yazılmaz. Karar yetkisi Master veya Tug Master'dadır (13.03 §3, AGENTS.md). |

## 6. Kabul ölçütleri

| ID | Ölçüt | Doğrulama |
|---|---|---|
| UG-HELP-01 | 53 sayfanın her birinde `?` düğmesi ve `Shift+?` paneli açar. Panel klavyeyle gezilir, Escape kapatır, odak düğmeye döner, 320 px'te tam sayfa açılır. | Component testi + Playwright a11y (axe) |
| UG-HELP-02 | Panel açılınca ağ isteği, komut veya AI çağrısı olmaz. | Testte fetch/izleyici sayacı 0 |
| UG-HELP-03 | Her navigation anahtarı için TR ve EN içerik vardır ve 8 bölüm başlığı eksiksizdir. Yeni sayfa eklenip kılavuzu yazılmazsa CI kırmızı olur. | Koruma testi (`help-coverage.test.ts`) |
| UG-HELP-04 | "İlgili sayfalar" kullanıcının görmediği sayfayı ne adıyla ne sayısıyla gösterir. | Kısıtlı kullanıcıyla (lab-restricted) component ve Playwright testi |
| UG-HELP-05 | İçerikteki durum adları ürünün gerçek durum kodlarıyla eşleşir. Kılavuz UNKNOWN'u "Hayır" veya "Hazır" diye anlatmaz. | Sözlük eşleme testi |
| UG-DOC-01 | Doküman HTML ve PDF olarak üretilir. Bölüm sırası: Başlarken → Ortak model → rol bölümleri → Sözlük → Sorun giderme. İçindekiler ve sayfa bağlantıları vardır. | `scripts/build_user_guide.py` + betik testi |
| UG-DOC-02 | Her "Adım adım" adımının işaretli bir ekran görüntüsü vardır. Görüntüler betikle üretilir ve tekrar üretildiğinde aynı çıkar (piksel farkı eşiği). | Görüntü betiği + karşılaştırma |
| UG-DOC-03 | Görüntülerde yalnız sentetik veri vardır. Gerçek kişi, e-posta, IMO veya şifre görünmez. Doğrulanmış/imzalı durum sahnelenmez. | Seed kaynağı kontrolü + görüntü metni (OCR'sız: DOM metni) tarama testi |
| UG-DOC-04 | Çevrimdışıyken `/app/help` ve panel çalışır. | Offline PWA testi |
| UG-REV-01 | Her sayfa için insan SME/UX onay alanı vardır. Onaysız içerik "taslak" olarak görünür. | İnsan kapısı — AI kapatamaz |

## 7. Plan (fazlar)

| Faz | İş | Bağımlılık | Tahmini büyüklük* |
|---|---|---|---|
| UG-1 | Yardım paneli altyapısı: sekmeler (Kılavuz / Neden / AI kapalı), içerik yükleyici, Markdown render (güvenli, HTML yok), `Shift+?`, mobil tam sayfa; kapsam koruma testi; 3 örnek sayfa (Today, Tug Job Board, Companies) TR/EN | Yok, hemen başlayabilir | 5 |
| UG-2 | Ekran görüntüsü üretim hattı: Playwright betiği, demo fixture yükleme komutu, numaralı işaret bindirme, deterministik ayarlar, CI'da elle tetiklenen iş | UG-1 (işaret konumları içerikten) | 8 |
| UG-3 | 53 sayfanın TR/EN içeriği, 13.03 rol bölümlerine göre gruplar halinde (Gemi, Römorkör, Teknik, HSE, Sertifika/Uyum, Mürettebat/HR, Yönetim/Pilot) | UG-1; ilgili sayfa artık değişmiyor olmalı | 21 |
| UG-4 | Doküman üretici: HTML + PDF, Başlarken, Ortak model, rol bölümleri, Sözlük, Sorun giderme; `/app/help` sayfası | UG-2, UG-3 | 8 |
| UG-5 | 15 dil taslak çevirisi + "gözden geçirilmedi" etiketi | UG-3 | 5 |
| UG-6 | İnsan gözden geçirme (SME + UX yazarı), `reviewed` alanları | UG-3/4 — **insan kapısı** | — |

\* Türetilmiş göreli büyüklük. Canonical SP değildir.

**Sıra:**
1. Kullanıcı/üyelik yönetim ekranı paketi (bkz. handoff), çünkü kılavuzun "Yönetici" bölümü ona bağlı.
2. Ardından UG-1 ve UG-2 paralel başlar.
3. Uygulama özellik olarak donduğunda UG-3 ve UG-4.
4. En son UG-5; UG-6 ise insan onayı bekler.

Kullanıcının "uygulama bitince" isteği gereği tam doküman (UG-4) en sona kalır. Altyapı ve görüntü hattı şimdiden kurulur ki içerik yazımı hızlı ilerlesin.

## 8. Riskler

- **Ekranlar değişmeye devam ediyor.** İçerik eskir. Önlem: kapsam koruma testi ve görüntülerin otomatik yeniden üretimi. Ekran değişen PR'da ilgili kılavuz dosyası da güncellenir (PR şablonu maddesi).
- **Alan doğruluğu.** AI yazdığı metni denizcilik uzmanı onaylamadan "doğru" sayamaz. UG-REV-01 insan kapısıdır.
- **17 dil.** Ana dilde inceleme yok. TR ve EN dışındaki diller taslak olarak etiketlenir.
- **Paket boyutu.** 53 sayfa × 17 dil Markdown birlikte birkaç yüz KB tutar. Dil bazında tembel yükleme yapılır ve önbelleğe alınır.

## 9. Açık insan kararları (AI kapatmaz)

- Kılavuz içeriğini onaylayacak SME ve UX yazarı kim olacak?
- Müşteriye verilecek PDF'te şirket logosu veya marka kullanılacak mı? Varsayılan: yalnız POSEIDON.
