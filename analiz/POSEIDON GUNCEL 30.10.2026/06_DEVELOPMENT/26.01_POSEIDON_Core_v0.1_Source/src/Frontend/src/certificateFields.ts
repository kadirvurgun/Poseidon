import {calendarDate,type DraftData,type DateBucket} from './api';
export const certificateFields = [
 {key:'type',tr:'Beyan edilen belge tipi',en:'Declared document type'},
 {key:'issuer',tr:'Beyan edilen düzenleyen',en:'Declared issuer'},
 {key:'certificate_no',tr:'Belge numarası (isteğe bağlı)',en:'Document number (optional)'},
 {key:'issue',tr:'Kayıtlı düzenlenme tarihi',en:'Recorded issue date',type:'date'},
 {key:'expiry',tr:'Kayıtlı bitiş tarihi',en:'Recorded expiry date',type:'date'},
 {key:'notes',tr:'Notlar',en:'Notes'},
];
export const dateBucketLabels:Record<DateBucket,[string,string]>={
 NO_EXPIRY_DATE:['Bitiş tarihi girilmemiş','Recorded expiry date not entered'],
 PAST_DATE:['Kayıtlı bitiş tarihi geçmiş','Recorded expiry date passed'],
 TODAY:['Kayıtlı bitiş tarihi bugün','Recorded expiry date today'],
 WITHIN_30_DAYS:['Kayıtlı bitiş tarihine 1–30 gün','Recorded expiry date in 1–30 days'],
 DAYS_31_TO_60:['Kayıtlı bitiş tarihine 31–60 gün','Recorded expiry date in 31–60 days'],
 DAYS_61_TO_90:['Kayıtlı bitiş tarihine 61–90 gün','Recorded expiry date in 61–90 days'],
 AFTER_90_DAYS:['Kayıtlı bitiş tarihine 90 günden fazla','Recorded expiry date in more than 90 days'],
};
export const certificateActions:Record<string,[string,string]>={DRAFT_CREATED:['Taslak oluşturuldu','Draft created'],DRAFT_UPDATED:['Taslak güncellendi','Draft updated'],CERTIFICATE_RECORDED:['İdari kayda alındı','Administratively recorded'],CERTIFICATE_METADATA_UPDATED:['Belge metadatası düzeltildi','Certificate metadata corrected'],CERTIFICATE_REOPENED:['Düzeltme için yeniden açıldı','Reopened for correction'],HISTORICAL_METADATA_CHANGE:['Geçmiş metadata değişikliği','Historical metadata change']};
export function validateCertificate(data:DraftData,lang:'tr'|'en') {
 const result:Record<string,string>={};const t=(tr:string,en:string)=>lang==='tr'?tr:en;
 if(!data.vessel_id)result.vessel_id=t('Yetkili bir gemi seçin.','Choose an authorized vessel.');
 for(const field of certificateFields){const value=data[field.key];if(!value)continue;if(value.length>(field.key==='notes'?4000:200))result[field.key]=t('Alan uzunluk sınırını aşıyor.','Field exceeds its length limit.');if(field.type==='date'&&!calendarDate(value))result[field.key]=t('Gerçek bir YYYY-MM-DD tarihi girin.','Enter a real YYYY-MM-DD date.');}
 if(!result.issue&&!result.expiry&&data.issue&&data.expiry&&data.expiry<data.issue)result.expiry=t('Bitiş tarihi düzenlenme tarihinden önce olamaz.','Expiry cannot precede issue date.');
 return result;
}
