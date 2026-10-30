# Bilgi grafiği ve RAG hazırlığı

## Birbirinden ayrı iki kullanım

Geliştirici grafiği ajanların kod keşfini daraltır. İleride ürün RAG'i ise yetkili kullanıcıların kontrollü belgelerden kaynak gösteren yanıtlar almasını sağlar. Geliştirici grafiğini müşteriye açmak ürün RAG'i sayılmaz.

Öncelik: mevcut codebase-memory-mcp araçları (search_graph, trace_path, get_code_snippet, query_graph, get_architecture); bu ortamda bu araçlar bulunamazsa yerel Graphify query/path/explain; sonuç yetersizse hedefli kaynak okuma. String/config ve non-code aramalarında doğrudan metin araması kullanılabilir.

## Kaynak aktarımı

`py scripts/import_documents.py --source <controlled-set-folder>`

Orijinaller `sources/poseidon-v46.3/` altında tutulur. Script SHA256, göreli kaynak yolu, belge kimliği ve kaynak sınıfını manifest'e kaydeder. DOCX paragrafları ve XLSX hücreleri metne çıkarılır; orijinal kaynak adresi ve paragrafa/sheet+row'a dayanan locator ile JSONL chunk kayıtları üretilir. Çıktı dosyaları yalnız değişiklik kontrolüyle yenilenir; farklı bir kaynak baseline aynı klasör üzerine aktarılmaz.

Bu aktarım canonical register'ın insan incelemesinin yerini almaz. `90_SOURCE_RESEARCH` ve `99_ARCHIVE` requirement kaynağı değildir. Diğer dosyalar da register okunana kadar `candidate-unverified` kabul edilir. DOCX şekilleri ve XLSX formül hesapları metin çıkarımından doğrulanamaz; kritik karar için orijinal dosya incelenir.

## Graphify

Python paketi `graphifyy==0.9.15`; CLI adı `graphify`. Kurulu araç ve komutlar yerelde kontrol edilmiştir.

```text
graphify extract scripts --code-only --no-cluster --out .
graphify query "import_documents" --budget 1500
graphify update .
```

AST extraction yereldir ve model çağrısı gerektirmez. Üçüncü komut uygulama kodu oluşunca artımlı güncelleme içindir. Anlamsal belge grafiği ayrıca host ajanla veya yapılandırılmış modelle üretilebilir; mevcut oturumla da yapılabilir, zorunlu bir API anahtarı gerektirmez. Headless API extraction ise ayrıca sağlayıcı erişimi ve ücretlendirme gerektirir. Kurulum, bütün analiz setinin anlamsal grafiğinin üretildiği anlamına gelmez.

Makineye özgü Codex hook dosyası git dışında tutulur. Hook'un bu istemcide gerçekten tetiklendiği henüz doğrulanmadığından güncelleme açık komutla çalıştırılır. Token tasarrufu miktarı corpus ve sorguya göre ölçülür; sabit bir tasarruf katsayısı vaat edilmez.

Kaynak: https://github.com/Graphify-Labs/graphify

## Gelecekteki ürün RAG sözleşmesi

- Chunk: source_id, source_sha256, source_path, locator, text ve content_sha256. Embedding/index sürümü, parser sürümü ve ingest job bilgisi ayrıca tutulur.
- Ürüne aktarımda tenant_id, erişim sınıfı, dil, geçerlilik tarihi, belge sürümü ve canonical/superseded durumu zorunlu hale gelir. Doküman tarihinden hukuki yürürlük tarihi tahmin edilmez.
- Arama: yetki filtresi → lexical/vector retrieval → gerekirse graph expansion → kaynak metin doğrulaması → locator içeren yanıt. PostgreSQL vector extension başlangıç adayıdır; henüz seçilmiş/kurulmuş değildir.
- Tenant ve belge yetkileri retrieval öncesinde, kaynak gösterimi sırasında ve cache erişiminde uygulanır. Global regulatory belgeler ile müşteriye ait evidence/PII aynı görünürlükte tutulmaz.
- Graphify INFERRED/AMBIGUOUS kenarları doğrulanmış gereksinim veya mevzuat sayılmaz. AI yanıtı safety release veya yetkili insan imzası üretmez.
- Değişen belge sürümü index ve cache invalidation üretir; silme/retention/legal hold kuralları embedding kayıtlarına da uygulanır.
- Değerlendirme: doğru kaynağı bulma, desteklenmeyen iddia, tarih/sürüm doğruluğu, yetki izolasyonu, Türkçe/İngilizce sorgular ve çelişkili kaynak senaryoları.
# Güncel kaynak seçimi ve düşük bağlamlı arama

`docs/sources/active.json`, kullanıcının 30.10.2026 güncellenmiş setini işaret eder. Önceki indeks değişmez geçmiş olarak korunur. `py scripts/query_sources.py "POL-DEFAULT-01" --limit 4` yerel SQLite FTS ile yalnız ilgili kaynak/locator/hash ve kısa metni döndürür; model veya embedding API çağırmaz. Kod ilişkileri için MCP/Graphify önceliği sürer. `py scripts/verify_sources.py`, kaynak byte hash'lerini ve chunk soy zincirini doğrular; CI'da zorunludur.

Yeni paketteki gömülü ürün kaynağı ve eski test raporları güncel implementation/acceptance kanıtı değildir. Varsayılan kaynak araması bunları ve research/archive sınıflarını dışarıda tutar. Gerektiğinde `--include-references` ile açıkça aranır.
