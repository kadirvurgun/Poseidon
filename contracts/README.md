# API sözleşmeleri

`openapi.v1.json`, .NET API'nin Development ortamındaki `/openapi/v1.json` çıktısıdır. Host adresi sözleşmeye gömülmez. API integration testi, gerçek host çıktısını bu dosyayla yapısal olarak karşılaştırır; sözleşme drift'i CI'ı durdurur.

Güncelleme: API'yi Development ortamında başlatın, `/openapi/v1.json` çıktısını UTF-8 olarak bu dosyaya kaydedin ve API integration testlerini çalıştırın. Frontend consumer tiplerini ve ilgili component/browser testlerini aynı değişiklikte güncelleyin.

Public system status yalnız teknik bağlantıdır. Tenant verisi, kullanıcı yetkisi, DB readiness veya gemi emniyet kararı değildir. OpenAPI Production ortamında sunulmaz.
