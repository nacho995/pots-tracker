using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pots.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientUserEmailsFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase 6 attribution support — resolve "who recorded this entry"
            // to a display name (with email fallback) for rendering in the
            // daily history, shared dashboard, and history page.
            //
            // RLS on `users` (users_self_select) only exposes the caller's
            // own row. Any cross-user join from a status/episode row to
            // users.email/display_name would return NULL. SECURITY DEFINER
            // bypass returning ONLY users who are the owner or an active
            // grantee of the target patient. The function verifies the
            // caller has access first, so callers cannot probe arbitrary
            // user IDs.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION list_patient_user_emails(p_patient_id uuid)
RETURNS TABLE(user_id uuid, email text, display_name text)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_caller uuid := app_current_user_id();
BEGIN
  IF v_caller IS NULL THEN RETURN; END IF;
  IF NOT has_patient_access(p_patient_id, v_caller) THEN
    RETURN;
  END IF;
  RETURN QUERY
    SELECT p.owner_user_id, u.email::text, u.display_name::text
    FROM patients p
    JOIN users u ON u.id = p.owner_user_id
    WHERE p.id = p_patient_id
    UNION
    SELECT g.grantee_user_id, u.email::text, u.display_name::text
    FROM patient_grants g
    JOIN users u ON u.id = g.grantee_user_id
    WHERE g.patient_id = p_patient_id
      AND g.revoked_at IS NULL;
END;
$$;
REVOKE EXECUTE ON FUNCTION list_patient_user_emails(uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION list_patient_user_emails(uuid) TO pots_app;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS list_patient_user_emails(uuid);");
        }
    }
}
