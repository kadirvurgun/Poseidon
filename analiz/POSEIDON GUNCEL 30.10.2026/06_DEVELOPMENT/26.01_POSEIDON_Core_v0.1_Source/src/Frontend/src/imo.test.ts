import {describe,it,expect} from "vitest";
import {imoIssue,imoMessage} from "./imo";
import {validate,payload} from "./forms";
describe("optional ship IMO arithmetic, not registry verification",()=>{
 it.each(["1234567","9319466","8814275"])("accepts check-digit example %s without asserting assignment",value=>expect(imoIssue(value)).toBeNull());
 it.each(["1234568","8814274"])("rejects incorrect check digit %s",value=>{
  expect(imoIssue(value)).toBe("checksum");
  for(const lang of ["tr","en"] as const)expect(validate("vessels",{imo_no:value},lang).imo_no).toEqual([imoMessage(value,lang)]);
 });
 it.each(["123","１２３４５６７","١٢٣٤٥٦٧","1234567\n"," 1234567","IMO1234567"])("rejects noncanonical format %s",value=>expect(imoIssue(value)).toBe("format"));
 it("keeps optional blank absent, without fabricating a number",()=>{expect(validate("vessels",{imo_no:""},"tr")).toEqual({});expect(payload({imo_no:""})).toEqual({});});
});
