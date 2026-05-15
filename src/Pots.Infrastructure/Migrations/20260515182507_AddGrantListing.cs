using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pots.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGrantListing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SECURITY DEFINER bypass: the owner needs to see the grantee's
            // email when listing grants, but users.email RLS only lets a user
            // see their own row (users_self_select). Rather than weaken that
            // policy or denormalize grantee_email onto patient_grants, expose a
            // narrow function that returns ONLY the join result for the caller's
            // own patient (verified inside via app_current_user_id() = owner).
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION list_my_patient_grants()
RETURNS TABLE(grant_id uuid, grantee_email text, role_name text, granted_at timestamptz)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_caller uuid := app_current_user_id();
BEGIN
  IF v_caller IS NULL THEN RETURN; END IF;
  RETURN QUERY
    SELECT g.id AS grant_id, u.email::text AS grantee_email, g.role::text AS role_name, g.granted_at
    FROM patient_grants g
    JOIN users    u ON u.id = g.grantee_user_id
    JOIN patients p ON p.id = g.patient_id
    WHERE p.owner_user_id = v_caller
      AND g.revoked_at IS NULL
    ORDER BY g.granted_at DESC;
END;
$$;
REVOKE EXECUTE ON FUNCTION list_my_patient_grants() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION list_my_patient_grants() TO pots_app;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS list_my_patient_grants();");
        }
    }
}
