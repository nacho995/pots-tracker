using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pots.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedPatientsListing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Owners listing their granted users hit list_my_patient_grants();
            // grantees listing the patients shared with THEM hit this function.
            // RLS on users would otherwise hide the owner's email row; this
            // function bypasses via SECURITY DEFINER and filters internally to
            // rows where the caller is the active grantee.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION list_my_shared_patients()
RETURNS TABLE(
    patient_id   uuid,
    patient_name text,
    owner_email  text,
    role_name    text,
    granted_at   timestamptz
)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_caller uuid := app_current_user_id();
BEGIN
  IF v_caller IS NULL THEN RETURN; END IF;
  RETURN QUERY
    SELECT
      p.id              AS patient_id,
      p.name::text      AS patient_name,
      u.email::text     AS owner_email,
      g.role::text      AS role_name,
      g.granted_at
    FROM patient_grants g
    JOIN patients p ON p.id = g.patient_id
    JOIN users    u ON u.id = p.owner_user_id
    WHERE g.grantee_user_id = v_caller
      AND g.revoked_at IS NULL
      AND p.deleted_at IS NULL
    ORDER BY g.granted_at DESC;
END;
$$;
REVOKE EXECUTE ON FUNCTION list_my_shared_patients() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION list_my_shared_patients() TO pots_app;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS list_my_shared_patients();");
        }
    }
}
