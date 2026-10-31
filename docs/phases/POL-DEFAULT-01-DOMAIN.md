# POL-DEFAULT-01-DOMAIN — Taslak zorunlu veri sözleşmesi

Kaynak: güncel 14.02 Özet row19, 13.06 Yönetici Özeti row18, DATA_DICTIONARY_UPDATE_NOTE ve 49.01 paragraph8. Kullanıcı onaylı genel politika: belirsiz default yok, zorunlu bilgi eksikken taslak saklanabilir; gönderim veri kapısı geçilemez.

Esra profili kod commit `fb8d302` üretti. `Poseidon.Domain` ve unit test projesi eklendi. Typed Missing/Unknown/Known değerler metin, bool ve decimal için ayrıdır. Null/boş metin eksiktir; bilinen false ve zero korunur. Trust edilen field definitions boş/duplicate/yanlış tip durumlarında reddedilir; ham istekten required tanımı bind edilmez. Sonuç immutable blocking field/reason listesi ve yalnız RequiredDataGateSatisfied bilgisidir; submit yetkisi veya operasyonel Ready kararı değildir.

16 domain testi + 12 persistence unit testi geçti; Release build 0 warning/error, locked restore geçti. Yeni unit-only proje yüzünden MTP Integration filtresinin zero-test vermemesi için CI unit ve integration projelerini açıkça hedefler; skip/ignore exit code eklenmedi. Yeni entity/API yoktur; sahte seed veya migration gerekmez.

Durum: kaynak kod yerel doğrulandı; bağımsız hedef QA ve remote CI bekliyor. API/OIDC/tenant ve gerçek form persistence entegrasyonu, tam alan kataloğu ve HSE yaşam döngüsü açık kalır. Bu alt paket canonical story veya ekran kabul sayısını artırmaz.

[Task/kabul](../tasks/POL-DEFAULT-01-DOMAIN.md), [domain kararı](../decisions/ADR-DRAFT-REQUIRED-DATA.md).
