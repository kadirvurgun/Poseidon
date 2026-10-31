# OPS-DASH-01 — iki hesap, model politikası ve kalıcı izleme

Kaynak: kullanıcının 31.10.2026 dashboard, iki ayrı hesap, saatlik kota,
yenilenme alarmı, otomatik devam ve uygun model talepleri.
Durum: **yerelde doğrulanan alt paket; bağımsız QA/CI ve kalıcı aktivasyon açık**.
Canonical S0 kabulü değişmedi: 8/970 SP (%0,82), 2/121 görev.

## Teslim edilen davranış

- React `/agents` TR/EN ekranı gerçek loopback `/ops/status` servisini 5 saniyede
  bir okur. Esra Pro ve Kadir Plus görevleri ayrıdır. “Çalışıyor” ile yeni göreve
  kota uygunluğu ayrı belirtilir. Eksik/eski/meşgul metadata hazır kabul edilmez.
- Kadir, arayüz alt paketini `024dc08`; Esra servis alt paketini `bb39655` olarak
  ayrı OAuth profilleri/worktree'lerle üretti. Ana sohbet hesabının değiştirildiği
  iddia edilmez. Credential veya kimlik verileri dashboard'a taşınmaz.
- [Model politikası](../model-policy.md): rutin iş/test yazımı/rutin QA için
  GPT-6 Luna medium; karmaşık geliştirme/güvenlik QA için GPT-6.1 Sol medium.
  Gerçek model ve effort CLI komutuna bağlanır ve oturum kaydında saklanır.
  Desteklenmeyen/bilinmeyen erişimde çağrı harcanmaz; sessiz yükseltme yoktur.
- Kadir üzerinden iki gerçek Luna salt okunur incelemesi tamamlandı; bunlar
  onay üretmedi. Eksik bağlam nedeniyle raporlanan belge hedefleri için QA verisine
  sınırlı gerçek dosya/link kanıtı eklendi. Başarısız inceleme kabul sayılmadı.
- Metadata internal error -32603 için aynı profil/lock içinde tek proactive
  refresh denemesi; diğer/bilinmeyen hatalarda bekleme. Esra'da gerçek düzeltme
  doğrulandı; yeni oturum/credential kopyalama gerekmedi.
- API yalnız GET ve tam loopback Host/route kabul eder. Geçmiş alanları allowlist
  ile sınırlandırılır. Kalıcı driver HTTP'yi model işinden ayrı thread'de tutar.
- Gerçek reset epoch + 60 saniye hedefleri ve Windows wake task yapımı; haftalık
  pencere ayrıca korunur. Saat dolması kota yenilendi kanıtı sayılmaz.
- Sekiz saatlik pencere sınırları restart ile sıfırlanmaz. Güvenli devam,
  dependency, exact commit bağımsız QA, dört CI kontrolü ve merge kapısı korunur.

## Doğrulama

| Kontrol | Son gözlenen sonuç |
| --- | --- |
| .NET solution / gerçek PostgreSQL integration dahil | 43 passed, 0 failed, 0 skipped |
| Arayüz component | 33 passed |
| Lint/typecheck/build | Başarılı |
| Playwright desktop + mobile | 8 passed; 4 gerçek .NET API smoke, 4 sentetik ops contract |
| Model routing ve gerçek komut/receipt bağlama | 6 passed |
| Runtime/HTTP/lease/erken metadata yenileme | 17 passed |
| Sabit gate/QA adaptörü | 15 passed |
| Tam Python ve Windows installer paketi | Önceki tam koşu 118 passed; son eklenen testlerle final koşu sürüyor. Yeni Windows installer 3 passed |
| Canlı loopback / tarayıcı | HTTP 200; iki kart ve Esra “Çalışıyor” canlı doğrulandı |

## Açık kapılar ve sonraki adım

Tam entegrasyon testleri, exact commit bağımsız QA ve uzaktaki dört CI kontrolü
tamamlanınca merge edilecek. Windows `Poseidon-DevelopmentRuntime` ve reset
görevi henüz gerçekten kaydedilmiş sayılmaz; installer preview testi aktivasyon
kanıtı değildir. İlk ADR işi `ee0aea4` için bilinmeyen yarım iş tekrarlanmayacak;
yalnız doğrulanan başlangıç uyarısı hatası operatör kaydıyla reconcile edilip
bağımsız QA yeniden çalıştırılacak. Sonra gerçek PR/CI/merge zinciri izlenecek.

7/24 erişim Windows'un açık, uygun oturumun kullanılabilir ve hostun uyanık
olmasına bağlıdır. WakeToRun donanım/politika desteği gerektirir; kapalı makineyi
başlatma garantisi değildir. Bu paket ürünün GDPR/KVKK veya deniz operasyonu
hazırlık kabulünü oluşturmaz.
