# Pro + Plus Codex worker'ları

Kullanıcı kararı: **Esra Pro**, **Kadir Plus**. İki hesap ayrı, yetkili ChatGPT/Codex oturumları olarak kullanılır. Kota ve abonelikler birleştirilmez; her hesabın kendi erişimi ve limiti geçerlidir.

Bu repo kendi worker katmanını içerir; uygulama menüsündeki hesap seçiminin otomatik failover yaptığı varsayılmaz. Resmi `CODEX_HOME`, keyring ve app-server hesap/kota arayüzleri üzerine kurulmuştur. Otomatik görevlendirme davranışı POSEIDON kodudur; yerleşik bir Codex abonelik birleştirme özelliği değildir.

## İzolasyon ve hesap doğrulama

- `esra-pro` → kullanıcı profilindeki `.poseidon-codex/esra-pro`.
- `kadir-plus` → kullanıcı profilindeki `.poseidon-codex/kadir-plus`.
- Kimlik bilgileri Windows keyring'de; token kopyalama, auth.json okuma veya repoya secret kaydı yok.
- Her alt işlem kendi `CODEX_HOME` değerini alır. Ortak daemon kullanılmaz. Üst oturumdan API key/access token ortam değişkenleri taşınmaz.
- `account/read` üzerinden ChatGPT girişi ve beklenen plan kontrol edilir; yanlış plan, oturum yokluğu veya bilinmeyen kota bilgisi worker dispatch'ini engeller.
- `account/rateLimits/read` primary/secondary pencerelerini okur. Kullanım %100 ise o oturum çalıştırılmaz; kota bilgisi sıfırlanmış gibi gösterilmez.
- Genel Codex/VS Code hesap menüsü ve iş Git hesabı değiştirilmez. GitHub `kadirvurgun` oturumu ayrı bir kimliktir.

Hesap sahibi her profile bir kere kendisi giriş yapmalıdır. Görselde hesapların listelenmesi bu ayrı worker girişlerinin tamamlandığını kanıtlamaz. Script yalnız plan ve kota metadatasını gösterir; hesap e-postasını, token'ını veya raw auth yanıtını yazmaz.

## Komutlar

```powershell
py scripts/codex_accounts.py init
py scripts/codex_accounts.py login esra-pro
py scripts/codex_accounts.py login kadir-plus
py scripts/codex_accounts.py status esra-pro
py scripts/codex_accounts.py status kadir-plus
```

Login resmi Codex device flow'unu kullanır. Hesap seçimini doğru yapın; script daha sonra planı sunucudan doğrular. Token/parola sohbetten veya görev dosyasından verilmez.

İçinde requirement/source locator, kabul kriterleri, rol ve test komutları bulunan bir görev Markdown dosyasıyla:

```powershell
py scripts/codex_accounts.py run path/to/backend-task.md --profile esra-pro
py scripts/codex_accounts.py parallel path/to/backend-task.md path/to/ui-task.md
py scripts/codex_accounts.py resume JOB_ID --profile esra-pro
```

Her görev `HEAD` commit'inden ayrı `agents/<job-id>` branch/worktree oluşturur. Pro varsayılan öncelikli worker, Plus ikinci worker'dır. Paralel komut iki bağımsız task alır; aynı dosya ağacında iki ajan yazmaz. Worktree'ler ve task/checkpoint dosyaları `.runtime/account-workers/` altında, Git dışında tutulur. Account başına tek aktif worker/login kilidi vardır.

## Görev devri

1. Tercih edilen hesabın login/plan/kotası doğrulanır; meşgulse diğer kullanılabilir hesap değerlendirilebilir.
2. CLI `--json` ile bounded bir görev çalıştırır. Başarı için tamamlanmış turn gerekir; exit code 0 tek başına başarı değildir.
3. Yalnız terminal, açıkça sınıflandırılmış kota hatasında ilk işlem tamamen bittikten sonra diğer hesaba aynı worktree/task devredilir. Partial dosyalar korunur; yeni worker önce git status/diff'i inceler.
4. Özel sohbet/session belleği hesaplar arasında kopyalanmaz. Devam bağlamı kontrollü görev metni ve worktree'nin mevcut halidir.
5. İki hesap da uygun değilse durum `waiting_for_quota` / `waiting_for_account` olur. Bu aşamada yeni model çağrısı yapılmaz. Login/kota düzeldikten sonra `resume` aynı işi sürdürür.
6. Ağ/auth/tool hatası, timeout veya protokol belirsizliğinde `blocked` olur; bunlar kota gibi yorumlanıp hesap değiştirilmez. Bitirilmiş/blocked iş otomatik `resume` ile yeniden yürütülmez.
7. Worker process tree'nin sonlandığı doğrulanamazsa `reconcile_required` ve kilitler korunur. Yetkili koordinatör process/worktree/dış etkileri incelemeden stale lock silmez.

Başlangıç sınırları: işlem başına 30 dakika; dispatch/resume başına en fazla iki hesap denemesi; her hesapta bir worker. Sonuç `needs_review` olur; otomatik ürün kabulü veya merge değildir. QA ve CI koordinatörün ayrı kapılarıdır. Üstün abonelik adı görev kalitesini veya kesintisiz 24 saat kapasiteyi garanti etmez.

## Test ve devreye alma durumu

Kontrol testleri sahte CLI/hesap metadatasıyla gerçek Git worktree'leri üzerinde çalışır. Bunlar model çağrısı yapmaz ve canlı hesap kotası tüketen bir failover deneyi değildir. Gerçek profil login/plan doğrulaması, iki küçük görevle paralel çalıştırma ve canlı görev devri ayrı kabul adımlarıdır.

Bu paket **sürekli 7/24 scheduler/servis değildir**. Windows host/servis ve görev kuyruğu planı, günlük çalışma sınırları, açılışta başlatma, süreç öldüğünde reconciliation ve faz raporu tetikleri ayrıca tamamlanmalıdır. İki profile erişim sağlanmadan aktif worker veya failover varmış gibi rapor verilmez.

Resmi kaynaklar:

- [Codex authentication ve credential storage](https://learn.chatgpt.com/docs/auth)
- [App-server account/read, plan ve rateLimits arayüzleri](https://learn.chatgpt.com/docs/app-server)
- [Non-interactive execution](https://learn.chatgpt.com/docs/non-interactive-mode)
