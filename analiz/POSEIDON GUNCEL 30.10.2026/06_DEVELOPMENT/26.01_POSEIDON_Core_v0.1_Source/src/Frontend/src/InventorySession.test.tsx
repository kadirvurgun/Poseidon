import {beforeEach,it,expect,vi} from 'vitest';
import {fireEvent,render,screen,waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {App} from './App';
import {BffApi,type InventoryVessel} from './api';
const reply=(body:unknown,status=200)=>new Response(JSON.stringify(body),{status});
const session={subject:'synthetic-manager',tenantId:'tenant',companyId:'company',vesselIds:[],synthetic:false};
const record:InventoryVessel={id:'00000000-0000-0000-0000-000000000001',version:1,adminState:'DRAFT',data:{name:'Scoped vessel',flag:'TR',vessel_type:'Tug',asset_mode:'TUG'},completeness:{missingRequiredFields:[],invalidFields:[]},createdAt:null,modifiedAt:null};
beforeEach(()=>{vi.restoreAllMocks();vi.unstubAllEnvs();vi.stubEnv('VITE_ENABLE_BFF','true');history.replaceState(null,'','/');Element.prototype.scrollIntoView=vi.fn();vi.spyOn(window,'confirm').mockReturnValue(true);});
for(const phase of ['csrf','logout'])for(const action of ['metadata','record','reopen'])it(`blocks ${action} while confirmed sign-out awaits ${phase}`,async()=>{
 const item={...record,adminState:action==='reopen'?'RECORDED':'DRAFT'};let defer=false,release!:(value:Response)=>void;let writes=0;
 const transport=vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>{
  const url=String(input);
  if(url==='/bff/session')return reply(session);
  if(url==='/bff/antiforgery')return defer&&phase==='csrf'?new Promise<Response>(r=>release=r):reply({requestToken:'csrf-fixture'});
  if(url==='/bff/logout')return phase==='logout'?new Promise<Response>(r=>release=r):new Response(null,{status:204});
  if(url.endsWith('/capabilities'))return reply({canWriteDraft:true,canManage:true});
  if(url.endsWith('/history'))return reply({items:[],nextCursor:null,hasMore:false});
  if(url.includes('/inventory/')&&init?.method==='POST'){writes++;return reply(item);}
  if(url.endsWith(record.id))return reply(item);
  return reply({items:url.includes('/inventory/')?[item]:[],nextCursor:null,hasMore:false});
 });vi.stubGlobal('fetch',transport);const user=userEvent.setup();render(<App/>);await screen.findByText('Henüz taslak yok');await user.click(screen.getByRole('button',{name:'İdari envanter'}));await user.click(await screen.findByRole('button',{name:'Aç'}));await screen.findByLabelText('Gemi adı');
 if(action==='metadata')await user.type(screen.getByLabelText('Notlar'),'Keep edit');
 if(action==='reopen')await user.type(screen.getByLabelText('Yeniden açma gerekçesi (kısıtlı denetim kaydı)'),'Correction');
 const name=action==='metadata'?'Metadata kaydet':action==='record'?'Envantere al':'Düzeltme için yeniden aç';
 await waitFor(()=>expect(screen.getByRole('button',{name})).toBeEnabled());defer=true;await user.click(screen.getByRole('button',{name:'Çıkış'}));await waitFor(()=>expect(release).toBeTypeOf('function'));
 expect(screen.getByRole('button',{name})).toBeDisabled();expect(screen.getByRole('button',{name:'Metadata kaydet'})).toBeDisabled();
 await user.click(screen.getByRole('button',{name}));fireEvent.submit(screen.getByLabelText('Gemi adı').closest('form')!);expect(writes).toBe(0);
 release(phase==='csrf'?reply({requestToken:'csrf-fixture'}):new Response(null,{status:204}));await screen.findByRole('button',{name:'Giriş yap'});expect(writes).toBe(0);
});
it('reserves a write before CSRF and rejects logout without canceling the eventual write',async()=>{
 let release!:(value:Response)=>void;let requests=0;
 const transport=vi.fn(async(input:RequestInfo|URL)=>{const url=String(input);if(url==='/bff/session')return reply(session);if(url==='/bff/antiforgery')return new Promise<Response>(r=>release=r);if(url==='/bff/inventory/vessels'){requests++;return reply(record);}throw new Error('Unexpected logout request');});
 const api=new BffApi(transport);await api.session();const save=api.inventoryWrite('create',{data:{}},'key');await waitFor(()=>expect(release).toBeTypeOf('function'));
 await expect(api.logout()).rejects.toMatchObject({code:'OPERATION_IN_PROGRESS'});expect(transport.mock.calls.some(([u])=>u==='/bff/logout')).toBe(false);
 release(reply({requestToken:'csrf-fixture'}));await expect(save).resolves.toMatchObject({id:record.id});expect(requests).toBe(1);
});
it('reserves logout before CSRF and blocks imperative write callers too',async()=>{
 let release!:(value:Response)=>void;
 const transport=vi.fn(async(input:RequestInfo|URL)=>{if(input==='/bff/session')return reply(session);if(input==='/bff/antiforgery')return new Promise<Response>(r=>release=r);if(input==='/bff/logout')return new Response(null,{status:204});throw new Error('No inventory write may start');});
 const api=new BffApi(transport);await api.session();const logout=api.logout();await expect(api.inventoryWrite('create',{data:{}},'key')).rejects.toMatchObject({code:'SESSION_ENDING'});expect(transport.mock.calls.filter(([u])=>String(u).includes('/inventory/'))).toHaveLength(0);release(reply({requestToken:'csrf-fixture'}));await logout;
});
it('warns about a possibly committed first create before relogin after concurrent session expiry',async()=>{
 let checkSession=false,sessionReply!:(value:Response)=>void,writeReply!:(value:Response)=>void;
 const transport=vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>{
  const url=String(input);
  if(url==='/bff/session')return checkSession?new Promise<Response>(resolve=>sessionReply=resolve):reply(session);
  if(url==='/bff/antiforgery')return reply({requestToken:'csrf-fixture'});
  if(url.endsWith('/capabilities'))return reply({canWriteDraft:true,canManage:true});
  if(url==='/bff/inventory/vessels'&&init?.method==='POST')return new Promise<Response>(resolve=>writeReply=resolve);
  return reply({items:[],hasMore:false,nextCursor:null});
 });vi.stubGlobal('fetch',transport);const user=userEvent.setup();render(<App/>);await screen.findByText('Henüz taslak yok');await user.click(screen.getByRole('button',{name:'İdari envanter'}));await user.click(await screen.findByRole('button',{name:'Yeni envanter taslağı'}));
 checkSession=true;fireEvent.focus(window);await waitFor(()=>expect(sessionReply).toBeTypeOf('function'));
 await user.click(screen.getByRole('button',{name:'Metadata kaydet'}));await waitFor(()=>expect(writeReply).toBeTypeOf('function'));sessionReply(reply({},401));await waitFor(()=>expect(transport.mock.calls.filter(([url])=>url==='/bff/session').length).toBeGreaterThan(1));await new Promise(resolve=>setTimeout(resolve,0));writeReply(reply(record));await screen.findByText('Oturum sona erdi.');
 await waitFor(()=>expect(screen.getByRole('button',{name:'Yeniden giriş yap'})).toBeEnabled());vi.mocked(window.confirm).mockReturnValue(false);await user.click(screen.getByRole('button',{name:'Yeniden giriş yap'}));
 expect(window.confirm).toHaveBeenLastCalledWith(expect.stringContaining('kayıt sunucuda oluşmuş olabilir'));
 expect(screen.getByRole('button',{name:'Aynı işlemi tekrar dene'})).toBeDisabled();expect(screen.queryByText('Sunucudaki güncel kayıt doğrulandı.')).not.toBeInTheDocument();
 expect(transport.mock.calls.filter(([url,init])=>url==='/bff/inventory/vessels'&&init?.method==='POST')).toHaveLength(1);
});
