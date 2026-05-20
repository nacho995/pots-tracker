using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pots.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaregiverNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "caregiver_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caregiver_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_caregiver_notes_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_caregiver_notes_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_caregiver_notes_users_deleted_by_user_id",
                        column: x => x.deleted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_caregiver_notes_author_user_id",
                table: "caregiver_notes",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_caregiver_notes_deleted_by_user_id",
                table: "caregiver_notes",
                column: "deleted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_caregiver_notes_patient_created",
                table: "caregiver_notes",
                columns: new[] { "patient_id", "created_at" });

            // =================================================================
            // RLS + listing helper.
            //
            // Access rules:
            //   SELECT — anyone with has_patient_access (owner + active grantees)
            //            can read. Soft-deleted rows stay visible at the DB
            //            layer; the listing function filters them.
            //   INSERT — must self-stamp author_user_id and have access to the
            //            patient.
            //   UPDATE — author may soft-delete their own note; the patient
            //            owner may soft-delete ANY note (her data, her call).
            //            No edit path is exposed — body is immutable.
            //   DELETE — never via RLS; the table is append-only at the row
            //            level. Soft-delete is the supported affordance.
            // =================================================================
            migrationBuilder.Sql(@"
-- Column-level UPDATE grant so the DB floor enforces note immutability.
-- The C# domain marks Body/AuthorUserId/CreatedAt/PatientId as
-- immutable, but RLS WITH CHECK alone cannot pin column-level no-change
-- — only Postgres column-level GRANT can. Soft-delete needs to write
-- deleted_at + deleted_by_user_id, and nothing else.
GRANT SELECT, INSERT ON caregiver_notes TO pots_app;
GRANT UPDATE (deleted_at, deleted_by_user_id) ON caregiver_notes TO pots_app;
ALTER TABLE caregiver_notes ENABLE ROW LEVEL SECURITY;
ALTER TABLE caregiver_notes FORCE ROW LEVEL SECURITY;

CREATE POLICY caregiver_notes_select ON caregiver_notes FOR SELECT
  USING (has_patient_access(patient_id, app_current_user_id()));

CREATE POLICY caregiver_notes_insert ON caregiver_notes FOR INSERT
  WITH CHECK (
    author_user_id = app_current_user_id()
    AND has_patient_access(patient_id, app_current_user_id())
  );

-- Soft-delete is the only UPDATE allowed. The CHECK ties the deleter to
-- the author OR the patient owner; nothing else can mutate the row.
CREATE POLICY caregiver_notes_update ON caregiver_notes FOR UPDATE
  USING (
    author_user_id = app_current_user_id()
    OR is_patient_owner(patient_id, app_current_user_id())
  )
  WITH CHECK (
    deleted_by_user_id = app_current_user_id()
    AND (
      author_user_id = app_current_user_id()
      OR is_patient_owner(patient_id, app_current_user_id())
    )
  );

-- ---------------------------------------------------------------------------
-- Listing helper. Needs to join users.email which RLS would hide for
-- caregivers viewing OTHER caregivers' notes. Mirrors the
-- list_my_patient_grants / list_pending_upgrade_requests_for_my_patient
-- pattern: SECURITY DEFINER bypasses RLS internally, but verifies the
-- caller has access to the patient FIRST.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION list_caregiver_notes(
  p_patient_id uuid,
  p_take int DEFAULT 100
)
RETURNS TABLE(
  note_id uuid,
  author_user_id uuid,
  author_email text,
  body text,
  created_at timestamptz,
  is_deleted boolean,
  caller_is_owner boolean
)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_caller uuid := app_current_user_id();
  v_owner  boolean;
  v_take   int := GREATEST(LEAST(p_take, 500), 1);
BEGIN
  IF v_caller IS NULL THEN RETURN; END IF;
  -- Defense-in-depth: function inherits no caller RLS; verify explicitly.
  IF NOT has_patient_access(p_patient_id, v_caller) THEN
    RETURN;
  END IF;
  v_owner := is_patient_owner(p_patient_id, v_caller);
  RETURN QUERY
    SELECT n.id, n.author_user_id, u.email::text, n.body::text, n.created_at,
           (n.deleted_at IS NOT NULL) AS is_deleted,
           v_owner AS caller_is_owner
    FROM caregiver_notes n
    JOIN users u ON u.id = n.author_user_id
    WHERE n.patient_id = p_patient_id
      AND n.deleted_at IS NULL
    ORDER BY n.created_at DESC
    LIMIT v_take;
END;
$$;
REVOKE EXECUTE ON FUNCTION list_caregiver_notes(uuid, int) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION list_caregiver_notes(uuid, int) TO pots_app;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS list_caregiver_notes(uuid, int);
DROP POLICY IF EXISTS caregiver_notes_update ON caregiver_notes;
DROP POLICY IF EXISTS caregiver_notes_insert ON caregiver_notes;
DROP POLICY IF EXISTS caregiver_notes_select ON caregiver_notes;
ALTER TABLE IF EXISTS caregiver_notes NO FORCE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS caregiver_notes DISABLE ROW LEVEL SECURITY;
REVOKE UPDATE (deleted_at, deleted_by_user_id) ON caregiver_notes FROM pots_app;
REVOKE SELECT, INSERT ON caregiver_notes FROM pots_app;
");
            migrationBuilder.DropTable(
                name: "caregiver_notes");
        }
    }
}
