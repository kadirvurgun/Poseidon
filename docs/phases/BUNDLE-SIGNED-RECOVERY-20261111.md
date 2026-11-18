# PR16 — imzalı koşu düzeltmesi, 11.11.2026

Durum: **GÜNCEL REQUIRED CI YEŞİL / TAM SIGNED VE BAĞIMSIZ SON QA AÇIK / KABUL EDİLMEDİ**.

## Gerçek sonuçlar

-8e0523d push42672628162 ve PR42672636808 için başlayan API işleri başarılı:
  826/826integration,176/176unit,0skip ve117/117gerçekkritikişlem kontrolü.
  Workflow toplamı başarısız: GitHub, oluşturulmayan işler için kendi
  `Internal server error` hatasını kaydetti. PR correlation
  `3d8fabb1-54b2-42b1-a3be-5f8a0917e542`; push correlation
  `f6aa1c5a-c25e-460c-9630-1e6bc28e34fe`. Bu olay için başarısız uygulama
  testi/dosya-satır tanısı uydurulmadı; eksik kontroller PASS sayılmadı.
-69bc92c üzerindeki gerçek174desktop/narrowmanifesti tamamlandı:
  **15PASS/159FAIL**,1659,909s. Desktop15/72;narrow0/87. Özgün sonuçlar korunur.
  Bu159sonuç159bağımsızürünhatası olarak yorumlanmaz; hazırlık ve oturum
  sorunları bazı sonraki vakalara taşınmıştır. Koşu kabul edilmedi.

## Somut kaynak düzeltmesi

Eski sürücü başlangıçta aldığı iki oturum çerezini bütün vakalara kopyalıyordu.
FoundationIdentity, çerez ömrünü doğrulanmış ID token bitişi ve30dakika tavanıyla
sınırlar. Bu güvenlik davranışı korunur.0fb4bee her vakadan önce gerçek provider
girişi yapar; başarısız vakayı yeniden denemez, eski ticket’a dönmez ve ömrü
uzatmaz. Sonuçlar LOGIN/JOURNEY aşamasını yalnız kapalı bir etiketle ayırır.

Login yardımcısı, giriş/çıkış kontrollerine sahip Bugün ekranında gerçek OIDC
girişini tamamlayıp sonra tam hedef adrese normal navigasyon yapar. Anonim
günlük ekranında giriş bağlantısı bulunmadığı gerçek tarayıcıyla gözlendi.
İnceleyici ve yazıcı için güvenliHttpOnlyçerez, gerçek oturum ve tam hedef
adresi **2/2** dar canlı kontrolde doğrulandı. Bu, tam174manifestinin yeni
versiyon üzerinde geçtiği anlamına gelmez.

Protokol/lane/journal/history kontrolleri **36/36**,0skip; bunun içindeki yeni
authprotokolü5testtir, toplamlar tekrar toplanmaz.0fb4bee repo taraması
2652dosya/0bulgu. Yeni hedefinCIvebağımsızQAsonucu henüz yok.

## Ortam ve açık kapılar

Windows eski55432portuna bind işlemini10013ile reddetti. Yalnız exact-owned
development database servisi ephemeral loopback porta taşındı; aynı named
volume, hesaplar, kaynak private generation zinciri ve veriler korundu.
Seed/migration veya güvenlik politikası gevşetmesi yapılmadı. Bu yalnız
önizleme taşıma ayarıdır; yeni signed fixture veya kabul kanıtı değildir.
Kadirvurgun hesabının gerçek browserlogin’i, kendi tenant erişimi ve foreign404
ayrı yeniden doğrulandı.

PR16draft kalır. Yeni sourceCI, güncel bağımsızQA ve temiz/disposable yeni run
kimlikleriyle tam signedmanifest doğrulaması gerekir. Tüketilmiş fixtureIDleri
ve süresi geçmiş kurgusal yetki pencereleri sessizce başarıya çevrilmez.
Canonical103/970SP,%10,62 ve20/121iş kabul değişmedi.


## Güncel CI sonucu — 32ff1d0

`32ff1d08aed38c02cd78a019caf5913d49c26ace` için [PR 42911168567](https://github.com/kadirvurgun/Poseidon/actions/runs/42911168567) ve [push 42911160703](https://github.com/kadirvurgun/Poseidon/actions/runs/42911160703) dört zorunlu işi de başarıyla tamamladı. Her olayın indirilen `api-test-counts` artifact’i 826/826 integration ve 176/176 unit, hata 0, skip 0; gerçek HTTP/write-audit eşleştirmesinde 117/117 kritik işlem gösteriyor. Kapalı sayım özeti ve iki integration/unit artifact SHA256 değerleri durum JSON’unda tutulur. Bu sonuç yeni 17-dil dalını veya tam signed manifestini doğrulamaz.

Özgün 174 koşu ve başarısızlıkları aynen korunur. Güncel tam signed koşu ve bağımsız hedef QA olmadan PR taslak kalır. Bu raporun sonraki metadata commit’i, CI hedefi olan 32ff1d0 ile karıştırılmaz.
