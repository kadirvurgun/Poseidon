const reasons: Record<string, [string, string]> = {
  MissingProvenance: ['Record the source of these inputs.', 'Bu girdilerin kaynağını kaydedin.'],
  MissingTimeZone: ['Select a valid reference time zone.', 'Geçerli bir referans saat dilimi seçin.'],
  MissingCalendarInputs: ['Calendar baseline or interval is missing or invalid.', 'Takvim başlangıcı veya aralığı eksik ya da geçersiz.'],
  UnsupportedCalendarInterval: ['This calendar unit or date range is not supported.', 'Bu takvim birimi veya tarih aralığı desteklenmiyor.'],
  FutureBaseline: ['The declared baseline is after the reference date.', 'Beyan edilen başlangıç, referans tarihinden sonra.'],
  CalendarBoundary: ['Calculated from the declared date and calendar interval.', 'Beyan edilen tarih ve takvim aralığı ile hesaplandı.'],
  MissingMeterAdapterOrReading: ['A meter source, reading or observation version is missing.', 'Sayaç kaynağı, okuması veya gözlem sürümü eksik.'],
  FutureObservation: ['The observation is after the reference clock.', 'Gözlem, referans zamanından sonra.'],
  ReadingFreshnessPolicyUnresolved: ['The reading is historical; its freshness policy needs review.', 'Okuma geçmiş bir zamana ait; güncellik politikası incelenmeli.'],
  MissingOrResetMeterBaseline: ['Check the meter baseline, interval and any meter reset.', 'Sayaç başlangıcını, aralığını ve olası sayaç sıfırlamasını kontrol edin.'],
  DeclaredMeterBoundary: ['Calculated from the declared reading, baseline and interval in the same unit.', 'Aynı birimde beyan edilen okuma, başlangıç ve aralık ile hesaplandı.'],
  MeterRangeExceeded: ['The meter result exceeds the supported numeric range.', 'Sayaç sonucu desteklenen sayısal aralığı aşıyor.'],
  MeterPrecisionExceeded: ['The meter result needs greater precision than can be stored.', 'Sayaç sonucu, saklanabilenden daha fazla hassasiyet gerektiriyor.'],
  MissingConditionAdapterOrObservation: ['A condition observation or its version is missing or unknown.', 'Koşul gözlemi veya sürümü eksik ya da bilinmiyor.'],
  ConditionFreshnessPolicyUnresolved: ['The condition is historical; its freshness policy needs review.', 'Koşul geçmiş bir zamana ait; güncellik politikası incelenmeli.'],
  DeclaredConditionObservation: ['Calculated from the explicitly declared condition observation.', 'Açıkça beyan edilen koşul gözlemi ile hesaplandı.'],
  MissingOrUnsupportedTrigger: ['Select a supported trigger and provide its inputs.', 'Desteklenen bir tetikleyici seçip girdilerini sağlayın.'],
  IncompletePlanContext: ['Job, eligible owner or criticality information needs review.', 'İş, yetkili sorumlu veya kritiklik bilgileri incelenmeli.'],
}
export const dueReason = (code: string, en: boolean) => reasons[code]?.[en ? 0 : 1] ?? (en ? 'Calculation inputs need review.' : 'Hesaplama girdileri incelenmeli.')
export const dueState = (state: string, en: boolean) => state === 'Due' ? (en ? 'Due' : 'Bakım zamanı geldi')
  : state === 'NotDue' ? (en ? 'Not due at the reference time' : 'Referans zamanında bakım zamanı gelmedi')
    : (en ? 'Review required' : 'İnceleme gerekli')
