# Göreve göre model ve kullanım politikası

Kaynak: kullanıcının 31.10.2026 model maliyeti ve hız talebi; OPS-MODEL-01.
Çalıştırıcı [politikayı](../automation/model-policy.json) açıkça uygular.

Son hesap yönlendirme kararı: geliştirme ve QA dahil **önce Esra Pro**.
Kadir Plus yalnız Esra'nın doğrulanmış bütün metadata koşulları sağlanırken
kota penceresi %85 rezerv sınırına ulaştığında, kendi kotası uygunsa kullanılır.
Esra meşgul/kimliği veya bağlantısı belirsizken Kadir'e geçilmez.

| İş | Model | Düşünme düzeyi |
| --- | --- | --- |
| Dar kapsamlı rutin düzenleme, test yazımı, rutin bağımsız QA | GPT-6 Luna | medium |
| Karmaşık geliştirme, güvenlik ve sınıflandırılmamış görev incelemesi | GPT-6.1 Sol | medium |

Standart hız kullanılır. Yeni görevler açıkça sınıflandırılmadan ucuz inceleme
yoluna alınmaz. Otomatik pahalı modele yükseltme yoktur. Erişim doğrulanamıyorsa
görev bekler; farklı modele sessiz geçilmez. Bağımsız QA'nın bulguları somut
kabul kriterleri ve test kanıtlarıyla değerlendirilir; daha ucuz model seçmek
kabul eşiğini düşürmez. Başarısız inceleme merge izni değildir.

Her hesabın kendi model kataloğu kontrol edilir; katalogda görünmek canlı erişim
kanıtı değildir. Kadir Plus ile GPT-6 Luna/medium üzerinden iki gerçek salt okunur
inceleme 31.10.2026'da tamamlandı. Esra Pro'nun kataloğunda aynı model görünmektedir;
Esra üzerinden Luna çalıştırıldığı henüz iddia edilmez. Önceden başlamış işler
yeni politika uğruna kesilmez; geçmiş oturumların modeli bilinmiyorsa uydurulmaz.

Unit, integration, Vitest ve Playwright testlerini çalıştırmak model çağrısı
değildir. Ajan test yazarken veya sonuçları incelerken kota tüketebilir.
CLI'nin kümülatif input/cached/output sayıları hesap kotasının kalan token miktarı
olarak gösterilmez. Dashboard, planın 5 saatlik/haftalık pencerelerini ve gerçek
model seçimini ayrı gösterir. Her profil kendi doğrulanmış kotasıyla değerlendirilir.

[Resmî fiyatlandırma](https://learn.chatgpt.com/docs/pricing) ve
[model kataloğu](https://learn.chatgpt.com/docs/models), 31.10.2026 tarihinde
incelendi. 5.6 Sol, sürüm numarası daha eski diye daha ucuz kabul edilmez.
Kredi fiyat farkı, aboneliğin dahil kotasında aynı oranda tasarruf garantisi değildir.
