import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { App } from "./App";
import { Api } from "./api";
const reply = (body: unknown) => new Response(JSON.stringify(body));
const record = (n: number) => ({draftId:`00000000-0000-0000-0000-${String(n).padStart(12,"0")}`,version:1,persistenceState:"DRAFT",submissionAllowed:false,data:{name:`Vessel ${String(n).padStart(3,"0")}`,flag:"TR",vessel_type:"tug"}});
const all = Array.from({length: 625},(_,i)=>record(i+1));
let fetcher: ReturnType<typeof vi.fn<typeof fetch>>;
beforeEach(()=>{
 vi.unstubAllEnvs(); history.replaceState(null,"","/"); Element.prototype.scrollIntoView=vi.fn();
 vi.spyOn(window,"confirm").mockReturnValue(true);
 fetcher=vi.fn(async(input)=>{
  const url=new URL(String(input),"http://localhost");
  if(url.pathname.endsWith("/session"))return reply({subject:"test",tenantId:"tenant",companyId:"company",vesselIds:[],synthetic:true});
  if(url.pathname.endsWith("/certificates"))return reply({items:[]});
  const q=url.searchParams.get("q")||"", start=Number(url.searchParams.get("cursor")||0);
  const matches=all.filter(v=>v.data.name.toLowerCase().includes(q.toLowerCase()));const items=matches.slice(start,start+50); const more=start+50<matches.length;
  return reply({items:url.pathname.includes("/inventory/")?items.map(v=>({id:v.draftId,version:v.version,adminState:"DRAFT",data:v.data,completeness:{missingRequiredFields:[],invalidFields:[]},createdAt:null,modifiedAt:null})):items,nextCursor:more?String(start+50):null,hasMore:more});
 });vi.stubGlobal("fetch",fetcher);
});
async function login(){const user=userEvent.setup();render(<App/>);await user.type(screen.getByLabelText("Erişim belirteci"),"token");await user.click(screen.getByRole("button",{name:"Oturumu doğrula"}));await screen.findByText("Vessel 001");return user;}
describe("inventory browsing",()=>{
 it("pages server results and resets cursor on filter and refresh",async()=>{
  const user=await login();await user.click(screen.getByRole("button",{name:"Sonraki sayfa"}));await screen.findByText("Vessel 051");expect(screen.queryByText("Vessel 001")).not.toBeInTheDocument();
  await user.type(screen.getByLabelText("Taslaklarda ara"),"Vessel 550");await user.type(screen.getByLabelText("Bayrak filtresi"),"TR");
  await user.click(screen.getByRole("button",{name:"Ara / filtrele"}));await screen.findByText("Vessel 550");
  const queryCalls=fetcher.mock.calls.filter(([url])=>String(url).includes("q=Vessel+550"));expect(queryCalls.length).toBeGreaterThan(0);expect(String(queryCalls.at(-1)![0])).not.toContain("cursor=");
  expect(screen.queryByRole("button",{name:"Sonraki sayfa"})).not.toBeInTheDocument();
  await user.click(screen.getByRole("button",{name:"Filtreleri temizle"}));await screen.findByText("Vessel 001");
  expect(screen.getByLabelText("Bayrak filtresi")).toHaveValue("");
 });
 it("allows certificate vessel selection beyond 500 without discarding edits or selecting defaults",async()=>{
  const user=await login();await user.click(screen.getByRole("button",{name:"Sertifika taslakları"}));await screen.findByText("Henüz taslak yok");await user.click(screen.getByRole("button",{name:"Yeni taslak"}));
  await user.type(screen.getByLabelText("Sertifika tipi"),"Keep unsaved");expect(screen.getByLabelText("Gemi *")).toHaveValue("");
  await user.click(screen.getByRole("button",{name:"Daha fazla gemi"}));await screen.findByRole("option",{name:/Vessel 051/});
  await user.type(screen.getByLabelText("Seçilecek gemiyi ara"),"Vessel 550");await user.click(screen.getByRole("button",{name:"Gemileri ara"}));await screen.findByRole("option",{name:/Vessel 550/});
  await user.selectOptions(screen.getByLabelText("Gemi *"),record(550).draftId);expect(screen.getByLabelText("Sertifika tipi")).toHaveValue("Keep unsaved");
  await user.clear(screen.getByLabelText("Seçilecek gemiyi ara"));await user.type(screen.getByLabelText("Seçilecek gemiyi ara"),"no matches");await user.click(screen.getByRole("button",{name:"Gemileri ara"}));
  await waitFor(()=>expect(screen.queryByRole("option",{name:/Vessel 550/})).not.toBeInTheDocument());expect(screen.getByLabelText("Gemi *")).toHaveValue(record(550).draftId);
  vi.mocked(window.confirm).mockReturnValue(false);await user.click(screen.getByRole("button",{name:"Gemi taslakları"}));expect(screen.getByLabelText("Gemi *")).toHaveValue(record(550).draftId);
  expect(fetcher.mock.calls.some(([,init])=>init?.method==="POST")).toBe(false);
 });
 it("ignores a late old search even when transport ignores abort",async()=>{
  const user=await login();let release!:(r:Response)=>void;const base=fetcher.getMockImplementation()!;
  fetcher.mockImplementation(async(input,init)=>String(input).includes("q=old")?new Promise(resolve=>{release=resolve;}):base(input,init));
  await user.type(screen.getByLabelText("Taslaklarda ara"),"old");await user.click(screen.getByRole("button",{name:"Ara / filtrele"}));await waitFor(()=>expect(release).toBeTypeOf("function"));
  await user.clear(screen.getByLabelText("Taslaklarda ara"));await user.type(screen.getByLabelText("Taslaklarda ara"),"Vessel 550");await user.click(screen.getByRole("button",{name:"Ara / filtrele"}));await screen.findByText("Vessel 550");
  release(reply({items:[{...record(2),data:{name:"STALE"}}],nextCursor:null,hasMore:false}));await waitFor(()=>expect(screen.queryByText("STALE")).not.toBeInTheDocument());expect(screen.getByText("Vessel 550")).toBeInTheDocument();
 });
 it("ignores a stale vessel-picker response and preserves the chosen vessel",async()=>{
  const user=await login();await user.click(screen.getByRole("button",{name:"Sertifika taslakları"}));await screen.findByText("Henüz taslak yok");await user.click(screen.getByRole("button",{name:"Yeni taslak"}));
  let release!:(r:Response)=>void;const base=fetcher.getMockImplementation()!;fetcher.mockImplementation(async(input,init)=>String(input).includes("q=old")?new Promise(resolve=>{release=resolve;}):base(input,init));
  await user.type(screen.getByLabelText("Seçilecek gemiyi ara"),"old");await user.click(screen.getByRole("button",{name:"Gemileri ara"}));await waitFor(()=>expect(release).toBeTypeOf("function"));
  await user.clear(screen.getByLabelText("Seçilecek gemiyi ara"));await user.type(screen.getByLabelText("Seçilecek gemiyi ara"),"Vessel 625");await user.click(screen.getByRole("button",{name:"Gemileri ara"}));await screen.findByRole("option",{name:/Vessel 625/});await user.selectOptions(screen.getByLabelText("Gemi *"),record(625).draftId);
  release(reply({items:[record(2)],nextCursor:null,hasMore:false}));await waitFor(()=>expect(screen.queryByRole("option",{name:/Vessel 002/})).not.toBeInTheDocument());expect(screen.getByLabelText("Gemi *")).toHaveValue(record(625).draftId);
 });
 it("does not return late authenticated rows after client invalidation",async()=>{
  let release!:(r:Response)=>void;const transport=vi.fn(()=>new Promise<Response>(resolve=>{release=resolve;}));const client=new Api("test",transport);const pending=client.list("vessels");client.invalidate();release(reply({items:[record(1)]}));await expect(pending).rejects.toMatchObject({status:401});
 });
 it("encodes query values and rejects inconsistent pagination responses",async()=>{
  const transport=vi.fn().mockResolvedValue(reply({items:[],nextCursor:null,hasMore:true}));
  await expect(new Api("test",transport).list("vessels",{q:"%_\\",flag:"TR",cursor:"bounded"})).rejects.toMatchObject({code:"INVALID_RESPONSE"});
  const url=new URL(transport.mock.calls[0][0],"http://localhost");expect(url.searchParams.get("q")).toBe("%_\\");expect(url.searchParams.get("cursor")).toBe("bounded");
 });
});
