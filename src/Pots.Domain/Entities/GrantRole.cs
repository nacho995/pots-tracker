namespace Pots.Domain.Entities;

// The string forms ("Viewer", "Editor") are persisted via
// PatientGrantConfiguration.HasConversion<string>() AND referenced by name
// in the RLS policy `patients_owner_or_editor_update` in migration
// EnableRowLevelSecurity. RENAMING THESE VALUES IS A SCHEMA CHANGE — update
// the migration policy AND any existing rows together. See task #10 for a
// regression test pinning the two together.
public enum GrantRole
{
    Viewer = 1,
    Editor = 2,
}
