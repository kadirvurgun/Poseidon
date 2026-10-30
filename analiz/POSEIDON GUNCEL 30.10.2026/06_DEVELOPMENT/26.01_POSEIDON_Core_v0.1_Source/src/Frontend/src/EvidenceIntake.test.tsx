import {describe,it,expect,vi} from 'vitest';
import {render,screen,fireEvent,waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {Api,BffApi,ApiError,type EvidenceRecord,type EvidenceOperation} from './api';
import {EvidenceIntake} from './EvidenceIntake';
const file=()=>new File(['%PDF-1.7\nbenign generated fixture'],'sample.pdf',{type:'application/pdf'});
const rec=(state='RESERVED'):EvidenceRecord=>({id:'evidence-1',version:1,targetType:'Vessel',targetId:'v1',metadata:{filename:'sample.pdf',declaredMime:'application/pdf',expectedBytes:file().size},state,scanState:'UNAVAILABLE',integrityStatus:'NOT_CHECKED',sha256:null,actualBytes:null,canDownload:false,blockReason:'QUARANTINED',evidenceType:'ATTACHMENT',verificationAssessment:'NOT_PERFORMED',operationalAssessment:'NOT_PERFORMED'});
const op=():EvidenceOperation=>({file:file(),targetType:'Vessel',targetId:'v1',reserveKey:'reserve-key',uploadKey:'upload-key'});
const reply=(value:unknown,status=200)=>new Response(JSON.stringify(value),{status,headers:{'Content-Type':'application/json'}});
const deferred=<T,>()=>{let resolve!:(v:T)=>void;const promise=new Promise<T>(r=>{resolve=r;});return {promise,resolve};};
function mount(api:Api,extra={}){if(!vi.isMockFunction(api.evidenceList))vi.spyOn(api,'evidenceList').mockResolvedValue({items:[],nextCursor:null,hasMore:false});return render(<EvidenceIntake api={api} targetType="Vessel" targetId="v1" lang="en" disabled={false} expired={false} onFailure={vi.fn()} onDirty={vi.fn()} onBusy={vi.fn()} onUncertain={vi.fn()} {...extra}/>);}
describe('quarantined evidence lifecycle',()=>{
 it('holds logout barrier before deferred CSRF and through final status',async()=>{
  const csrf=deferred<Response>(),status=deferred<Response>();let posts=0;
  const transport=vi.fn(async(url:RequestInfo|URL,init?:RequestInit)=>{const path=String(url);if(path==='/bff/session')return reply({subject:'a',tenantId:'t',companyId:'c'});if(path==='/bff/antiforgery')return (await csrf.promise).clone();if(path.endsWith('/content')){posts++;expect(init?.body).toBe(operation.file);return reply(rec('STORED_QUARANTINED'));}if(init?.method==='POST'){posts++;return reply(rec());}return status.promise;});
  const api=new BffApi(transport as typeof fetch);await api.session();const operation=op();const pending=api.evidenceIntake(operation,true);
  await expect(api.logout()).rejects.toMatchObject({code:'OPERATION_IN_PROGRESS'});expect(posts).toBe(0);csrf.resolve(reply({requestToken:'csrf'}));await waitFor(()=>expect(posts).toBe(2));await expect(api.logout()).rejects.toMatchObject({code:'OPERATION_IN_PROGRESS'});status.resolve(reply(rec('STORED_QUARANTINED')));await pending;
 });
 it('recovers lost binary response via GET and never repeats stored bytes',async()=>{
  let uploads=0;const transport=vi.fn(async(_url:RequestInfo|URL,init?:RequestInit)=>{if(init?.body instanceof File){uploads++;throw Error('lost response');}return reply(init?.method==='POST'?rec():rec('STORED_QUARANTINED'));});
  const api=new Api('token',transport as typeof fetch),operation=op();await expect(api.evidenceIntake(operation,true)).rejects.toBeInstanceOf(ApiError);expect(operation.record?.id).toBe('evidence-1');await api.evidenceIntake(operation,false);await api.evidenceIntake(operation,true);expect(uploads).toBe(1);
 });
 it('never retries a RECEIVING attempt and denies new capture when logout is pending',async()=>{
  const logout=deferred<Response>();const transport=vi.fn(async(url:RequestInfo|URL,init?:RequestInit)=>{
   if(String(url)==='/bff/session')return reply({subject:'a',tenantId:'t',companyId:'c'});
   if(String(url)==='/bff/antiforgery')return reply({requestToken:'csrf'});
   if(String(url)==='/bff/logout')return logout.promise;
   expect(init?.method).not.toBe('POST');return reply(rec('RECEIVING'));
  });
  const api=new BffApi(transport as typeof fetch);await api.session();const operation=op();operation.record=rec();await api.evidenceIntake(operation,true);expect(transport.mock.calls.some(([,init])=>init?.body instanceof File)).toBe(false);
  const leaving=api.logout();await expect(api.evidenceIntake(op(),true)).rejects.toMatchObject({code:'SESSION_ENDING'});logout.resolve(reply({}));await leaving;
 });
 it('supersede reserves a new child with reason/version and only uploads to that child',async()=>{
  const child={...rec(),id:'child-1',previousId:'old-1'};const transport=vi.fn(async(url:RequestInfo|URL,init?:RequestInit)=>{
   if(String(url).endsWith('/supersede')){expect(init?.headers).toMatchObject({'If-Match':'"4"','Idempotency-Key':'reserve-key'});expect(JSON.parse(init?.body as string)).toEqual({metadata:{targetType:'Vessel',targetId:'v1',filename:'sample.pdf',declaredMime:'application/pdf',expectedBytes:file().size},reason:'Correct attachment'});return reply(child);}
   if(init?.body instanceof File){expect(String(url)).toBe('/api/v1/evidence-intake/child-1/content');return reply({...child,state:'STORED_QUARANTINED'});}
   return reply({...child,state:'STORED_QUARANTINED'});
  });
  const operation={...op(),previous:{...rec('STORED_QUARANTINED'),id:'old-1',version:4},reason:'Correct attachment'};await new Api('token',transport as typeof fetch).evidenceIntake(operation,true);expect(transport).toHaveBeenCalledTimes(3);
 });
 it('loads bounded receipts/history and requires a reason for correction',async()=>{
  const api=new Api('');vi.spyOn(api,'evidenceCapabilities').mockResolvedValue({enabled:true,canCapture:true});const list=vi.spyOn(api,'evidenceList').mockResolvedValueOnce({items:[rec('STORED_QUARANTINED')],nextCursor:'next',hasMore:true}).mockResolvedValue({items:[{...rec(),id:'second'}],nextCursor:null,hasMore:false});
  vi.spyOn(api,'evidenceHistory').mockResolvedValue({items:[{eventId:'event',version:1,occurredAt:'2026-10-27T00:00:00Z',action:'UNKNOWN_PRIVATE_ACTION',actorRelation:'OTHER',changedFields:['private_filename']}],nextCursor:null,hasMore:false});
  const intake=vi.spyOn(api,'evidenceIntake').mockResolvedValue({...rec('STORED_QUARANTINED'),id:'new',previousId:'evidence-1'});mount(api);
  await screen.findByText('Correct with a new file');fireEvent.click(screen.getByText('More intakes'));await waitFor(()=>expect(list).toHaveBeenLastCalledWith('Vessel','v1','next'));
  fireEvent.click(screen.getAllByText('Intake history')[0]);await screen.findByText(/Historical metadata change/);expect(screen.queryByText(/private_filename|UNKNOWN_PRIVATE_ACTION/)).toBeNull();
  fireEvent.click(screen.getByText('Correct with a new file'));fireEvent.change(screen.getByLabelText('Attachment (PDF, JPEG or PNG)'),{target:{files:[file()]}});expect(screen.getByText('Send to quarantine')).toBeDisabled();fireEvent.change(screen.getByLabelText('Correction reason (restricted audit record)'),{target:{value:'Correct file'}});fireEvent.click(screen.getByText('Send to quarantine'));await waitFor(()=>expect(intake).toHaveBeenCalled());expect(intake.mock.calls[0][0].previous?.id).toBe('evidence-1');expect(intake.mock.calls[0][0].reason).toBe('Correct file');
 });
 it('clears receipts and ignores deferred history after scope expiry',async()=>{
  const api=new Api('');vi.spyOn(api,'evidenceCapabilities').mockResolvedValue({enabled:true,canCapture:true});vi.spyOn(api,'evidenceList').mockResolvedValue({items:[rec('STORED_QUARANTINED')],nextCursor:null,hasMore:false});const history=deferred<{items:never[];nextCursor:null;hasMore:false}>();vi.spyOn(api,'evidenceHistory').mockReturnValue(history.promise);
  const props={api,targetType:'Vessel' as const,targetId:'v1',lang:'en' as const,disabled:false,expired:false,onFailure:vi.fn(),onDirty:vi.fn(),onBusy:vi.fn(),onUncertain:vi.fn()};const view=render(<EvidenceIntake {...props}/>);fireEvent.click(await screen.findByText('Intake history'));view.rerender(<EvidenceIntake {...props} expired/>);history.resolve({items:[],nextCursor:null,hasMore:false});await waitFor(()=>expect(screen.queryByText(/sample.pdf/)).toBeNull());expect(screen.getByLabelText('Attachment (PDF, JPEG or PNG)')).toBeDisabled();
 });
 it('scope expiry while awaiting CSRF sends no file or reservation',async()=>{
  const csrf=deferred<Response>();const transport=vi.fn(async(url:RequestInfo|URL)=>String(url)==='/bff/session'?reply({subject:'a',tenantId:'t',companyId:'c'}):csrf.promise);
  const api=new BffApi(transport as typeof fetch);await api.session();const pending=api.evidenceIntake(op(),true);api.invalidate();csrf.resolve(reply({requestToken:'csrf'}));await expect(pending).rejects.toMatchObject({status:401});expect(transport).toHaveBeenCalledTimes(2);
 });
 it('preserves operation/file through rejected recovery and uses status-only recovery',async()=>{
  const api=new Api('');vi.spyOn(api,'evidenceCapabilities').mockResolvedValue({enabled:true,canCapture:true});const calls:EvidenceOperation[]=[];
  const intake=vi.spyOn(api,'evidenceIntake').mockImplementation(async(operation)=>{calls.push(operation);if(calls.length===1)throw new ApiError(0,'NETWORK_ERROR','lost');if(calls.length===2)throw new ApiError(403,'forbidden','rejected');return rec('STORED_QUARANTINED');});mount(api);const input=await screen.findByLabelText('Attachment (PDF, JPEG or PNG)');const selected=file();fireEvent.change(input,{target:{files:[selected]}});fireEvent.click(screen.getByText('Send to quarantine'));await screen.findByText('Check intake status only');await waitFor(()=>expect(screen.getByText('Check intake status only')).toBeEnabled());fireEvent.click(screen.getByText('Check intake status only'));await waitFor(()=>expect(intake).toHaveBeenCalledTimes(2));await waitFor(()=>expect(screen.getByText('Check intake status only')).toBeEnabled());expect(input).toBeDisabled();fireEvent.click(screen.getByText('Check intake status only'));await screen.findByRole('status');expect(calls[0]).toBe(calls[1]);expect(calls[1]).toBe(calls[2]);expect(calls[0].file).toBe(selected);expect(intake.mock.calls.map(c=>c[1])).toEqual([true,false,false]);
 });
 it('renders filename as text without preview and gates parent logout',async()=>{
  const api=new Api('');vi.spyOn(api,'evidenceCapabilities').mockResolvedValue({enabled:true,canCapture:true});const write=vi.spyOn(api,'evidenceIntake');mount(api,{disabled:true});const input=await screen.findByLabelText('Attachment (PDF, JPEG or PNG)');expect(input).toBeDisabled();fireEvent.click(screen.getByText('Send to quarantine'));expect(write).not.toHaveBeenCalled();expect(document.querySelector('img,iframe,object,a[download]')).toBeNull();
 });
 it('safe text filenames never create elements',async()=>{const api=new Api('');vi.spyOn(api,'evidenceCapabilities').mockResolvedValue({enabled:true,canCapture:true});mount(api);const input=await screen.findByLabelText('Attachment (PDF, JPEG or PNG)');fireEvent.change(input,{target:{files:[new File(['benign'],'<img src=x onerror=alert(1)>.pdf',{type:'application/pdf'})]}});expect(screen.getByText(/<img src=x/)).toBeInTheDocument();expect(document.querySelector('img')).toBeNull();});
 it('confirmed abandonment clears native selection and reselecting the same file does not resend',async()=>{
  const api=new Api('');vi.spyOn(api,'evidenceCapabilities').mockResolvedValue({enabled:true,canCapture:true});const intake=vi.spyOn(api,'evidenceIntake').mockRejectedValue(new ApiError(0,'NETWORK_ERROR','Response lost'));const confirm=vi.spyOn(window,'confirm').mockReturnValue(true);
  try {
   const user=userEvent.setup();mount(api);const selected=file();const original=await screen.findByLabelText('Attachment (PDF, JPEG or PNG)') as HTMLInputElement;await user.upload(original,selected);await user.click(screen.getByText('Send to quarantine'));await waitFor(()=>expect(screen.getByText('Abandon local intake')).toBeEnabled());
   await user.click(screen.getByText('Abandon local intake'));expect(confirm).toHaveBeenCalledWith(expect.stringContaining('does not delete server data'));
   const fresh=screen.getByLabelText('Attachment (PDF, JPEG or PNG)') as HTMLInputElement;expect(fresh).not.toBe(original);expect(fresh.value).toBe('');expect(fresh.files).toHaveLength(0);expect(screen.getByText('Send to quarantine')).toBeDisabled();
   await user.upload(fresh,selected);expect(fresh.files?.[0]).toBe(selected);expect(screen.getByText('Send to quarantine')).toBeEnabled();expect(intake).toHaveBeenCalledTimes(1);
  } finally {confirm.mockRestore();}
 });

});
