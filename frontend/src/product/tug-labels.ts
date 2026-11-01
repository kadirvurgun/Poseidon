// Stable source codes are wire values; these labels never change the lexical contract.
const labels: Record<string, [string, string]> = {
  Customer: ['Müşteri', 'Customer'], Agent: ['Acente', 'Agent'], Internal: ['İç talep', 'Internal'], Contract: ['Sözleşme', 'Contract'], Emergency: ['Acil', 'Emergency'],
  'Harbour Towage': ['Liman römorkajı', 'Harbour towage'], Escort: ['Eskort', 'Escort'], 'Berthing Assist': ['Yanaşma yardımı', 'Berthing assistance'], 'Unberthing Assist': ['Ayrılma yardımı', 'Unberthing assistance'], Standby: ['Hazır bekleme', 'Standby'], 'Emergency Towage': ['Acil römorkaj', 'Emergency towage'], 'Sea Towage': ['Deniz römorkajı', 'Sea towage'],
  Normal: ['Normal', 'Normal'], High: ['Yüksek', 'High'], Critical: ['Kritik', 'Critical'], Draft: ['Taslak', 'Draft'],
  Unknown: ['Bilinmiyor', 'Unknown'], Unavailable: ['Kullanılamıyor', 'Unavailable'], Missing: ['Eksik', 'Missing'],
  LegacyMeasurementUnverified: ['Eski profil ölçümü, doğrulanmamış', 'Legacy profile measurement, unverified'], UserDeclaredUnverified: ['Kullanıcı beyanı, doğrulanmamış', 'User declaration, unverified'], SyntheticUnverified: ['Sentetik, doğrulanmamış', 'Synthetic, unverified'],
  DraftDeclarationsOnly: ['Yalnız taslak beyanlar', 'Draft declarations only'], MissingStart: ['Başlangıç eksik', 'Start missing'], EndNotSupplied: ['Bitiş verilmemiş', 'End not supplied'], InvalidOrder: ['Zaman sırası geçersiz', 'Invalid time order'], Defined: ['Girilen aralık tanımlı', 'Entered window defined'],
  Separate: ['Ayrı zaman aralıkları', 'Separate windows'], BoundaryTouch: ['Sınır teması', 'Boundary touch'], Intersecting: ['Örtüşen aralıklar', 'Intersecting windows'], InvalidWindow: ['Geçersiz aralık', 'Invalid window'],
  'RequestedWindowOnly;NoApprovedPlan': ['Yalnız talep aralığı; onaylı plan yok', 'Requested window only; no approved plan'],
  'DraftPlanUnverified;NoOperationalApproval': ['Taslak plan doğrulanmamış; operasyon onayı yok', 'Unverified draft plan; no operational approval'],
  UnavailableControlledAssignmentAdapter: ['Kontrollü atama verisi kullanılamıyor', 'Controlled assignment data unavailable'], CurrentDraftAssignmentsOnly: ['Yalnız güncel taslak atamaları', 'Current draft assignments only'],
  'VisiblePageEnteredDataOnly;NoBufferOrOperationalAssessment': ['Yalnız görünür sayfadaki zamanlar; tampon veya operasyon değerlendirmesi yok', 'Visible page times only; no buffer or operational assessment'],
  AssignmentCoverageUnavailable: ['Atama kapsamı kullanılamıyor', 'Assignment coverage unavailable'], DraftAssignmentsUnverified: ['Taslak atamalar doğrulanmamış', 'Draft assignments unverified'], VerifiedBPCertificateCrewUnavailable: ['Doğrulanmış BP, sertifika ve ekip girdileri eksik', 'Verified BP, certificate and crew inputs unavailable'], OperationalConflictInputsUnavailable: ['Operasyonel çakışma girdileri kullanılamıyor', 'Operational conflict inputs unavailable'],
  VerifiedMeasurementCertificateCrewAndAssignmentAdaptersUnavailable: ['Doğrulanmış ölçüm, sertifika, ekip ve operasyonel atama girdileri kullanılamıyor', 'Verified measurement, certificate, crew and operational assignment inputs unavailable'],
  RequestChangedSincePlan: ['Talep plan kaydından sonra değişti; inceleme gerekir', 'Request changed since this plan; review required'],
  CompleteDraft: ['Taslağı tamamlayın', 'Complete draft'], AuthorizedPlanningReviewRequired: ['Yetkili planlama incelemesi gerekir', 'Authorized planning review required'], AuthorizedHumanConflictReviewRequired: ['Çakışma için yetkili insan incelemesi gerekir', 'Authorized human conflict review required'], HumanReviewRequired: ['Yetkili insan incelemesi gerekir', 'Authorized human review required'], BoundaryReviewRequired: ['Sınır teması incelemesi gerekir', 'Boundary touch review required'],
  'D-STATE-01': ['Operasyonel geçiş sözleşmesi açık (D-STATE-01)', 'Operational transition contract unresolved (D-STATE-01)'],
  request_source: ['Talep kaynağı', 'Request source'], assisted_vessel: ['Hizmet verilecek gemi', 'Assisted vessel'], job_type: ['İş tipi', 'Job type'], requested_start: ['Talep başlangıcı', 'Requested start'], operation_area: ['Operasyon alanı', 'Operation area'], required_tug_count: ['Gerekli römorkör sayısı', 'Required tug count'], priority: ['Öncelik', 'Priority'], request_owner: ['Talep sorumlusu', 'Request owner'],
  requestSource: ['Talep kaynağı', 'Request source'], jobType: ['İş tipi', 'Job type'], requestedStart: ['Talep başlangıcı', 'Requested start'], requestedEnd: ['Talep bitişi', 'Requested end'], requiredBollardPull: ['Gerekli BP', 'Required BP'], requiredTugCount: ['Gerekli römorkör sayısı', 'Required tug count'], requestOwner: ['Talep sorumlusu', 'Request owner'],
}
export function tugLabel(code: string, language: 'tr' | 'en'): string {
  if (code.startsWith('Missing:')) return `${language === 'en' ? 'Missing' : 'Eksik'}: ${tugLabel(code.slice(8), language)}`
  return labels[code]?.[language === 'en' ? 1 : 0] ?? code
}
