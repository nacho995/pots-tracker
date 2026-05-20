using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pots.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixUpgradeRequestsFunctionCast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bug fix: the SECURITY DEFINER fn declared `message text` but
            // returned `grant_upgrade_requests.message` which is
            // varchar(500). Postgres is strict about RETURNS TABLE column
            // types and aborts with SQLSTATE 42804 the moment a real row is
            // streamed — so callers got a 500 only when the inbox was
            // non-empty (the empty case never streamed a value, so the
            // mismatch went undetected during initial testing).
            //
            // Fix: explicit `::text` cast on r.message, matching how
            // u.email::text is already cast.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION list_pending_upgrade_requests_for_my_patient()
RETURNS TABLE(
  request_id uuid,
  grant_id uuid,
  requester_email text,
  message text,
  requested_at timestamptz
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
    SELECT r.id, r.grant_id, u.email::text, r.message::text, r.requested_at
    FROM grant_upgrade_requests r
    JOIN patients p ON p.id = r.patient_id
    JOIN users    u ON u.id = r.requester_user_id
    WHERE p.owner_user_id = v_caller
      AND r.status = 'Pending'
    ORDER BY r.requested_at ASC;
END;
$$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION list_pending_upgrade_requests_for_my_patient()
RETURNS TABLE(
  request_id uuid,
  grant_id uuid,
  requester_email text,
  message text,
  requested_at timestamptz
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
    SELECT r.id, r.grant_id, u.email::text, r.message, r.requested_at
    FROM grant_upgrade_requests r
    JOIN patients p ON p.id = r.patient_id
    JOIN users    u ON u.id = r.requester_user_id
    WHERE p.owner_user_id = v_caller
      AND r.status = 'Pending'
    ORDER BY r.requested_at ASC;
END;
$$;
");
        }
    }
}
