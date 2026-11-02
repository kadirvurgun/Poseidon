namespace Poseidon.Domain;

public enum CrewIdentityField { FullName, EmployeeNo }

// Engineering capture bounds, not an approved natural-key/uniqueness catalog.
public static class CrewIdentityData
{
    public static bool Valid(string? name, string? employee) =>
        (name == null || name.Length <= 200 && !name.Any(char.IsControl)) &&
        (employee == null || employee.Length <= 64 && !employee.Any(char.IsControl));

    public static bool EmployeeChangeAllowed(string mode, string? current, string? proposed) => mode switch {
        "MutableDeclaration" => true,
        "ImmutableAfterCapture" => current == null || string.Equals(current, proposed, StringComparison.Ordinal),
        _ => false
    };
}
