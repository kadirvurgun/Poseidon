namespace Poseidon.Domain;

public enum TugJobCommand { Submit, Cancel }
public sealed record TugJobTransition(string? ToState, string? Failure, string[] Missing);

// DEV-081 / T-TUG-002 (18.01 Backlog row 82) with the 60.02 §5 command contract
// (paragraphs 81..92): POST / and PUT /{id} keep DRAFT; POST /{id}/submit moves
// DRAFT→SUBMITTED only when every §4 required field and the owner are present;
// POST /{id}/cancel moves DRAFT/SUBMITTED→CANCELLED with a reason. Planning,
// assignment, readiness, release and execution states (60.02 paragraph 72) belong
// to later work (DEV-083/084+) and are deliberately not reachable here.
// POL-DEFAULT-01: incomplete drafts are allowed; submission with missing required
// data is refused and the missing keys are returned, never defaulted.
public static class TugJobLifecycle
{
    public const string Draft = "Draft", Submitted = "Submitted", Cancelled = "Cancelled";
    public static readonly string[] States = [Draft, Submitted, Cancelled];
    public const int ReasonMax = 500;
    public static bool Editable(string state) => state == Draft;
    public static bool ValidReason(string? reason) => reason != null && reason.Length is > 0 and <= ReasonMax &&
        !string.IsNullOrWhiteSpace(reason) && reason.Trim() == reason && !reason.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));
    public static TugJobTransition Evaluate(string state, TugJobCommand command, TugRequestInputs inputs, string? reason = null)
    {
        if (!States.Contains(state)) return new(null, "invalid_state", []);
        switch (command) {
            case TugJobCommand.Submit:
                if (state != Draft) return new(null, "invalid_state", []);
                if (TugRequestPolicy.Invalid(inputs).Length != 0) return new(null, "validation_failed", []);
                var missing = TugRequestPolicy.Missing(inputs);
                return missing.Length != 0 ? new(null, "tug_request_incomplete", missing) : new(Submitted, null, []);
            case TugJobCommand.Cancel:
                if (state is not (Draft or Submitted)) return new(null, "invalid_state", []);
                return ValidReason(reason) ? new(Cancelled, null, []) : new(null, "validation_failed", []);
            default: return new(null, "invalid_state", []);
        }
    }
}
