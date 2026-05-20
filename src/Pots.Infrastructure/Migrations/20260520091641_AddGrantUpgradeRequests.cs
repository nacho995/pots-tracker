using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pots.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGrantUpgradeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grant_upgrade_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grant_upgrade_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_grant_upgrade_requests_patient_grants_grant_id",
                        column: x => x.grant_id,
                        principalTable: "patient_grants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_grant_upgrade_requests_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_grant_upgrade_requests_users_requester_user_id",
                        column: x => x.requester_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_grant_upgrade_requests_users_resolved_by_user_id",
                        column: x => x.resolved_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_grant_upgrade_requests_grant_one_pending",
                table: "grant_upgrade_requests",
                column: "grant_id",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_grant_upgrade_requests_patient_id",
                table: "grant_upgrade_requests",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_grant_upgrade_requests_requester_user_id",
                table: "grant_upgrade_requests",
                column: "requester_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_grant_upgrade_requests_resolved_by_user_id",
                table: "grant_upgrade_requests",
                column: "resolved_by_user_id");

            // =================================================================
            // RLS + helper functions for the new table.
            //
            // Access rules:
            //   SELECT — requester sees own rows; patient owner sees all rows
            //            on patients they own (regardless of status).
            //   INSERT — must self-stamp requester_user_id; the row's
            //            patient_id must be one the caller has access to.
            //   UPDATE — both requester (cancel) and owner (approve/deny)
            //            need USING access, but their allowed *terminal*
            //            statuses differ. The WITH CHECK is a CASE on
            //            `status`:
            //              Approved/Denied → must be patient owner
            //              Cancelled       → must be original requester
            //              Pending         → either (defensive; no path
            //                                writes Pending→Pending today)
            //            Plus `resolved_by_user_id` is pinned to the caller
            //            so a row can't be "resolved by" someone else.
            //            Defense-in-depth against an app-layer bypass: a
            //            requester cannot self-approve at the DB floor.
            //   DELETE — denied at app layer; this table is history. Not
            //            REVOKEd here because pots_app needs DELETE elsewhere
            //            (Restrict FK is the practical floor).
            // =================================================================
            migrationBuilder.Sql(@"
GRANT SELECT, INSERT, UPDATE, DELETE ON grant_upgrade_requests TO pots_app;
ALTER TABLE grant_upgrade_requests ENABLE ROW LEVEL SECURITY;
ALTER TABLE grant_upgrade_requests FORCE ROW LEVEL SECURITY;

CREATE POLICY grant_upgrade_requests_select ON grant_upgrade_requests FOR SELECT
  USING (
    requester_user_id = app_current_user_id()
    OR is_patient_owner(patient_id, app_current_user_id())
  );

CREATE POLICY grant_upgrade_requests_insert ON grant_upgrade_requests FOR INSERT
  WITH CHECK (
    requester_user_id = app_current_user_id()
    AND has_patient_access(patient_id, app_current_user_id())
  );

-- Update policy. Both requester (to cancel) and patient owner (to
-- approve/deny) need UPDATE access, but their allowed terminal statuses
-- differ. The CHECK enforces that:
--   * resolved_by_user_id is self-stamped (no impersonation)
--   * a Cancelled row was resolved by the original requester
--   * an Approved or Denied row was resolved by the patient owner — a
--     requester cannot self-approve, even if the app layer were bypassed.
--   * a Pending row can only stay Pending if it's the requester touching it
--     (defensive — no path currently writes Pending→Pending, but RLS-as-
--     floor should not allow a non-owner to nudge owner-state).
CREATE POLICY grant_upgrade_requests_update ON grant_upgrade_requests FOR UPDATE
  USING (
    requester_user_id = app_current_user_id()
    OR is_patient_owner(patient_id, app_current_user_id())
  )
  WITH CHECK (
    (resolved_by_user_id IS NULL OR resolved_by_user_id = app_current_user_id())
    AND CASE status
      WHEN 'Approved'  THEN is_patient_owner(patient_id, app_current_user_id())
      WHEN 'Denied'    THEN is_patient_owner(patient_id, app_current_user_id())
      WHEN 'Cancelled' THEN requester_user_id = app_current_user_id()
      WHEN 'Pending'   THEN requester_user_id = app_current_user_id()
                            OR is_patient_owner(patient_id, app_current_user_id())
      ELSE FALSE
    END
  );

-- ---------------------------------------------------------------------------
-- SECURITY DEFINER helpers for listing requests joined with user emails.
-- RLS on `users` only exposes the caller's own email; the joins below need
-- to surface the *other* party's email to the legitimate participant. The
-- function gates this internally by checking that the caller is either the
-- requester (for the requester-side view) or the patient owner (for the
-- owner-side view).
-- ---------------------------------------------------------------------------

-- Owner view: the patient owner's inbox of PENDING requests on her patient.
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
REVOKE EXECUTE ON FUNCTION list_pending_upgrade_requests_for_my_patient() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION list_pending_upgrade_requests_for_my_patient() TO pots_app;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS list_pending_upgrade_requests_for_my_patient();
DROP POLICY IF EXISTS grant_upgrade_requests_update ON grant_upgrade_requests;
DROP POLICY IF EXISTS grant_upgrade_requests_insert ON grant_upgrade_requests;
DROP POLICY IF EXISTS grant_upgrade_requests_select ON grant_upgrade_requests;
ALTER TABLE IF EXISTS grant_upgrade_requests NO FORCE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS grant_upgrade_requests DISABLE ROW LEVEL SECURITY;
REVOKE SELECT, INSERT, UPDATE, DELETE ON grant_upgrade_requests FROM pots_app;
");
            migrationBuilder.DropTable(
                name: "grant_upgrade_requests");
        }
    }
}
