# Branch ve PR standardı

İzleme: DEV-001 / T-S0-001; kaynak 18.01 Backlog row 2. Backend, frontend, contracts, infra ve docs aynı kontrollü repository içindedir.

- `main`: doğrulanmış teslimler. Yalnız PR üzerinden, hedef commit'in bütün zorunlu kontrolleri ve bağımsız QA kanıtından sonra merge edilir.
- `develop`: sonraki fazın entegrasyon dalı. `feature/*` ve `fix/*` dalları gerektiğinde buraya PR açar; faz kabulünden sonra develop → main PR'ı açılır. İlk altyapı bootstrap'ı main'e alındıktan sonra develop aynı kabul edilmiş commit'ten başlatılır. Uzun süre açık paralel ürün kopyası tutulmaz.
- `feature/*`, `fix/*`, `agents/*`: kısa görev dalları; eşzamanlı yazarlar farklı worktree kullanır. Her PR task/source locator, kabul kriteri ve doğrulama kanıtı taşır.
- Force push, branch silme ve test atlama rutin çözüm değildir. Merge stratejisi normal merge commit; tekrar denenirken geçmiş yeniden yazılmaz.
- Zorunlu GitHub check adları: `persistence-and-security`, `frontend-and-browser`. İlki build, unit/guard, PostgreSQL + API integration, migration, importer ve API artifact'ını; ikincisi contract type drift, lint/typecheck, component/build, gerçek API Playwright ve UI artifact'ını kapsar. Başarısız/bekleyen kontrol kabulü engeller.
- Bağımsız QA, yazarın kendi testinden ayrı kaydedilir. GitHub review sayısı bu semantik incelemenin tek kanıtı değildir; repo izinleri gerçek pilot/uzman onayı vermez.

Branch protection uygulanması ayrıca GitHub API'den doğrulanıp faz raporuna kaydedilir. Bu dokümanın varlığı uzaktaki korumanın etkin olduğunu kanıtlamaz; doğrulanana kadar DEV-001/002'nin tam kabulü açık kalır.
