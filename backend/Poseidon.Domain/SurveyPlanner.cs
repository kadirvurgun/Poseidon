namespace Poseidon.Domain;

// DEV-112 / T-CERT2-002 (18.01 sheet:Backlog/row:113): fleet calendar, survey window,
// voyage/port fit, pre-survey checklist, class/flag coordination, CONDITIONAL/NOT_FIT states.
// 60.04 Rev3: requirement window is fixed and never filled from planning dates (p126, p222);
// numeric window ranges only come from an authorised source policy (p92) - this planner reads
// the window the certificate declares and discloses that it is not a source-policy window.
// Class/flag/authority coordination is external and never simulated here.
public sealed record SurveyWindowDto(string State,DateOnly? Start,DateOnly? End,int? DaysToOpen,int? DaysToClose,int? DaysOverdue,string[] Qualifiers);
public sealed record SurveyPortCallFact(string Reference,DateOnly? From,DateOnly? To);
public sealed record SurveyFitDto(string State,string[] Reasons,string[] Basis);

public static class SurveyPlannerPolicy
{
    public const string SourceVersion="18.01:row113+60.04Rev3:p92,p126,p128,p140,p150,p221-222:v46.3";
    public static readonly string[] WindowStates=["UNKNOWN","UPCOMING","OPEN","OVERDUE"];
    // FIT is deliberately absent: no input set available to this product can establish it.
    public static readonly string[] FitStates=["CONDITIONAL","NOT_FIT","UNKNOWN"];
    public static readonly string[] PortReferenceStates=["CURRENT","SUPERSEDED","NOT_FOUND","NOT_VISIBLE"];

    public static SurveyWindowDto Window(DateOnly today,bool declared,DateOnly? start,DateOnly? end) {
        string[] always=["DECLARED_WINDOW_NOT_SOURCE_POLICY","SURVEY_SATISFACTION_NOT_RECORDED"];
        if(!declared && start==null && end==null)return new("UNKNOWN",null,null,null,null,null,["WINDOW_NOT_DECLARED",..always]);
        if(start==null || end==null || end<start)return new("UNKNOWN",start,end,null,null,null,["WINDOW_INVALID",..always]);
        if(today<start)return new("UPCOMING",start,end,start.Value.DayNumber-today.DayNumber,end.Value.DayNumber-today.DayNumber,null,always);
        if(today<=end)return new("OPEN",start,end,0,end.Value.DayNumber-today.DayNumber,null,always);
        // No survey execution/satisfaction record exists in this product, so a passed window is
        // overdue unless an authority record (external) shows otherwise.
        return new("OVERDUE",start,end,null,null,today.DayNumber-end.Value.DayNumber,always);
    }

    // Voyage/port fit. Known conflicts give NOT_FIT; any missing input gives UNKNOWN; a complete
    // consistent input set only reaches CONDITIONAL because the voyage reference is unavailable,
    // the port call is an unverified declaration and authority/class coordination is external.
    public static SurveyFitDto Fit(DateOnly today,SurveyWindowDto window,DateOnly? expiry,PreSurveyPlan? plan,SurveyPortCallFact? port) {
        var notFit=new List<string>();var unknown=new List<string>();
        string[] basis=["VOYAGE_REFERENCE_UNAVAILABLE","DECLARED_WINDOW_NOT_SOURCE_POLICY","AUTHORITY_COORDINATION_EXTERNAL"];
        if(window.State=="UNKNOWN")unknown.Add("WINDOW_UNKNOWN");
        if(window.State=="OVERDUE")notFit.Add("WINDOW_CLOSED");
        var start=plan?.PlannedStart;var end=plan?.PlannedEnd;
        if(plan==null)unknown.Add("NO_PREPARATION_PLAN");
        else if(start==null || end==null)unknown.Add("PLANNED_DATES_MISSING");
        if(start!=null && end!=null) {
            if(end<start)notFit.Add("PLANNED_DATES_INVALID");
            if(start<today)notFit.Add("PLANNED_DATES_IN_PAST");
            if(window.Start!=null && window.End!=null && window.State!="UNKNOWN" && (start<window.Start || end>window.End))notFit.Add("PLANNED_OUTSIDE_SURVEY_WINDOW");
            if(expiry!=null && end>expiry)notFit.Add("PLANNED_AFTER_DECLARED_EXPIRY");
        }
        if(expiry==null)unknown.Add("EXPIRY_UNKNOWN");
        if(plan!=null) {
            if(port==null)unknown.Add("PORT_CALL_NOT_LINKED");
            else if(port.Reference!="CURRENT")unknown.Add("PORT_CALL_REFERENCE_NOT_CURRENT");
            else if(port.From==null || port.To==null || port.To<port.From)unknown.Add("PORT_CALL_DATES_UNKNOWN");
            else if(start!=null && end!=null && (port.To<start || port.From>end))notFit.Add("PORT_CALL_OUTSIDE_PLANNED_DATES");
        }
        if(notFit.Count>0)return new("NOT_FIT",[..notFit,..unknown],basis);
        if(unknown.Count>0)return new("UNKNOWN",unknown.ToArray(),basis);
        return new("CONDITIONAL",["VOYAGE_UNKNOWN","PORT_CALL_DECLARED_UNVERIFIED","AUTHORITY_COORDINATION_EXTERNAL","SURVEYOR_BOOKING_NOT_RECORDED"],basis);
    }

    public static bool Intersects(SurveyWindowDto window,DateOnly? expiry,DateOnly from,DateOnly to)=>window.State=="UNKNOWN" ||
        window.Start<=to && window.End>=from || expiry>=from && expiry<=to;
}
