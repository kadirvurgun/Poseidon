using System.Security.Cryptography;
using System.Text;

namespace Poseidon.Domain;

public enum TugAssignmentCommand { Propose, Respond, Withdraw }
public sealed record TugAssignmentTransition(string? ToState, string? ToAck, string? Failure);
public sealed record TugAssignmentScope(Guid JobId, long JobRevision, Guid TugId, DateTimeOffset PlannedStart, DateTimeOffset PlannedEnd, string Role);
public sealed record TugAssignmentWindow(Guid Id, Guid TugId, DateTimeOffset PlannedStart, DateTimeOffset PlannedEnd, string State, string MasterAck);

// DEV-083 / T-TUG-004 (18.01 Backlog row 84: "Assignment checks time overlap + crew/equipment/PMS/cert;
// shore proposes; Master/Tug Master acknowledgement preserved"). 60.02 Rev2 paragraphs 108 (closed
// contract: assignment_role LEAD|SUPPORT|ESCORT, master_ack PENDING|ACCEPTED|REJECTED, decision_reason
// text2000), 116 (interval intersection max(a.from,b.from) < min(a.to,b.to); boundary touch is no
// overlap), 119-128 (POST / proposes; POST /{id}/master-ack {decision, scopeHash, reason}; ACCEPT
// never makes CONFIRMED automatically; cancel before start), 49 and 61 (only the verified Tug Master of
// that tug for that period decides; acknowledgement tokens are invalid once the scope changes).
// CONFIRMED/RELEASED/ACTIVE are later work and deliberately unreachable here. Nothing in this policy
// is a dispatch, release, permit or safety decision.
public static class TugAssignmentWorkflow
{
    public const string SourceLocator = "18.01/sheet:Backlog/row:84;60.02/paragraph:108,116,119-128,49,61";
    public const string SourceVersion = "18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:84;60.02:Rev2:108-128";
    public const string Proposed = "Proposed", Cancelled = "Cancelled";
    public static readonly string[] States = [Proposed, Cancelled];
    public const string Pending = "Pending", Accepted = "Accepted", Rejected = "Rejected";
    public static readonly string[] Acks = [Pending, Accepted, Rejected];
    public const string Accept = "ACCEPT", Reject = "REJECT";
    public static readonly string[] Decisions = [Accept, Reject];
    public static readonly string[] Roles = ["Lead", "Support", "Escort"];
    public const int ReasonMax = 2000;
    public static readonly TimeSpan MaxWindow = TimeSpan.FromDays(31);

    public static bool ValidReason(string? reason) => reason != null && reason.Length is > 0 and <= ReasonMax &&
        !string.IsNullOrWhiteSpace(reason) && reason.Trim() == reason && !reason.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));

    // A proposal needs an explicit, ordered, bounded window: no requested-time fallback and no zero buffer.
    public static string? InvalidProposal(DateTimeOffset? start, DateTimeOffset? end, string? role) =>
        start == null || end == null ? "planned_window_required" : end <= start ? "planned_window_invalid" :
        end.Value - start.Value > MaxWindow ? "planned_window_invalid" : role == null || !Roles.Contains(role, StringComparer.Ordinal) ? "assignment_role_invalid" : null;

    public static TugAssignmentTransition Evaluate(string state, string ack, TugAssignmentCommand command, string? decision = null, string? reason = null)
    {
        if (!States.Contains(state) || !Acks.Contains(ack)) return new(null, null, "invalid_state");
        switch (command) {
            case TugAssignmentCommand.Respond:
                // One human acknowledgement per proposed scope; a changed scope needs a new proposal.
                if (state != Proposed || ack != Pending) return new(null, null, "invalid_state");
                if (decision is not (Accept or Reject)) return new(null, null, "validation_failed");
                // A rejection always carries its reason; an acceptance reason is optional but bounded.
                if (decision == Reject ? !ValidReason(reason) : reason != null && !ValidReason(reason)) return new(null, null, "validation_failed");
                return new(Proposed, decision == Accept ? Accepted : Rejected, null);
            case TugAssignmentCommand.Withdraw:
                if (state != Proposed) return new(null, null, "invalid_state");
                return ValidReason(reason) ? new(Cancelled, ack, null) : new(null, null, "validation_failed");
            default: return new(null, null, "invalid_state");
        }
    }

    // Exact scope the Tug Master acknowledges: job revision, tug, planned window and role.
    public static string ScopeHash(TugAssignmentScope s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|',
        "poseidon.tug-assignment-scope.v1", s.JobId.ToString("D"), s.JobRevision.ToString(System.Globalization.CultureInfo.InvariantCulture), s.TugId.ToString("D"),
        s.PlannedStart.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        s.PlannedEnd.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture), s.Role))));

    // 60.02 paragraph 116: strict intersection; equal end/start is no overlap.
    public static bool Overlaps(DateTimeOffset aStart, DateTimeOffset aEnd, DateTimeOffset bStart, DateTimeOffset bEnd) =>
        (aStart > bStart ? aStart : bStart) < (aEnd < bEnd ? aEnd : bEnd);

    // Another active proposal for the same tug overlapping this window is a POTENTIAL conflict (both
    // are tentative); it is surfaced for human review, never resolved or hidden by the server.
    public static TugAssignmentWindow[] TimeOverlaps(TugAssignmentWindow subject, IEnumerable<TugAssignmentWindow> others) =>
        others.Where(o => o.Id != subject.Id && o.TugId == subject.TugId && o.State == Proposed &&
            Overlaps(subject.PlannedStart, subject.PlannedEnd, o.PlannedStart, o.PlannedEnd)).OrderBy(o => o.Id).ToArray();
}
