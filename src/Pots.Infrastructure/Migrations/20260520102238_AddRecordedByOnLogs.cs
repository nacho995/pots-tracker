using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pots.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordedByOnLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add recorded_by_user_id to daily_status_entries and episodes.
            //
            // Why hand-rolled SQL instead of the EF defaults: the EF scaffold
            // used Guid.Empty as the default value to make the column NOT
            // NULL on add, which would have violated the FK to users on
            // every existing row (Guid.Empty isn't a real user). Correct
            // shape:
            //   1. ADD COLUMN ... NULL          (transient nullable state)
            //   2. UPDATE ... FROM patients     (backfill from owner)
            //   3. ALTER COLUMN ... SET NOT NULL
            //   4. ADD FOREIGN KEY              (now safe)
            //   5. CREATE INDEX
            //
            // The backfill is correct: every existing row was logged by the
            // owner because Phase 5 is the first feature that lets anyone
            // else write. New rows are stamped at INSERT time by the API.
            migrationBuilder.Sql(@"
-- daily_status_entries
ALTER TABLE daily_status_entries
  ADD COLUMN recorded_by_user_id uuid NULL;
UPDATE daily_status_entries dse
   SET recorded_by_user_id = p.owner_user_id
  FROM patients p
 WHERE p.id = dse.patient_id;
ALTER TABLE daily_status_entries
  ALTER COLUMN recorded_by_user_id SET NOT NULL;
ALTER TABLE daily_status_entries
  ADD CONSTRAINT fk_daily_status_entries_users_recorded_by_user_id
  FOREIGN KEY (recorded_by_user_id) REFERENCES users(id) ON DELETE RESTRICT;
CREATE INDEX ix_daily_status_entries_recorded_by_user_id
  ON daily_status_entries (recorded_by_user_id);

-- episodes
ALTER TABLE episodes
  ADD COLUMN recorded_by_user_id uuid NULL;
UPDATE episodes e
   SET recorded_by_user_id = p.owner_user_id
  FROM patients p
 WHERE p.id = e.patient_id;
ALTER TABLE episodes
  ALTER COLUMN recorded_by_user_id SET NOT NULL;
ALTER TABLE episodes
  ADD CONSTRAINT fk_episodes_users_recorded_by_user_id
  FOREIGN KEY (recorded_by_user_id) REFERENCES users(id) ON DELETE RESTRICT;
CREATE INDEX ix_episodes_recorded_by_user_id
  ON episodes (recorded_by_user_id);

-- Tighten the existing INSERT policies so the DB floor enforces self-stamp:
-- a caller cannot spoof recorded_by_user_id to look like someone else
-- registered an entry. has_patient_edit_access still gates WHO can write;
-- the new CHECK pins WHO they claim to be.
DROP POLICY IF EXISTS daily_status_insert ON daily_status_entries;
CREATE POLICY daily_status_insert ON daily_status_entries FOR INSERT
  WITH CHECK (
    has_patient_edit_access(patient_id, app_current_user_id())
    AND recorded_by_user_id = app_current_user_id()
  );

DROP POLICY IF EXISTS episodes_insert ON episodes;
CREATE POLICY episodes_insert ON episodes FOR INSERT
  WITH CHECK (
    has_patient_edit_access(patient_id, app_current_user_id())
    AND recorded_by_user_id = app_current_user_id()
  );

-- Column-level UPDATE grants. Mirror of the caregiver_notes Phase 4 fix.
-- Without this, RLS UPDATE permitted by has_patient_edit_access would let
-- an Editor rewrite Amelia's historical status entries (status, recorded_by)
-- and forge attribution. We instead REVOKE table-level UPDATE and grant
-- only the columns that the domain's UpdateDetail path actually touches.
-- Status, recorded_by_user_id, patient_id, created_at all stay immutable
-- at the Postgres column-grant layer — below RLS.
REVOKE UPDATE ON daily_status_entries FROM pots_app;
GRANT UPDATE (posture, activity, location_note, note, episode_occurred)
  ON daily_status_entries TO pots_app;

-- Episodes have no domain UPDATE path at all — Episode.Create is the only
-- writer. Revoke UPDATE entirely; if a future feature needs to edit an
-- episode, a follow-up migration must opt back in column-by-column with the
-- same reasoning recorded here.
REVOKE UPDATE ON episodes FROM pots_app;

-- NOTE on backfill safety: the column add + UPDATE FROM patients + SET NOT
-- NULL above runs inside the migration's single transaction. PostgreSQL's
-- ALTER TABLE ADD COLUMN acquires ACCESS EXCLUSIVE; concurrent INSERTs
-- block until commit, so no row can land with NULL recorded_by between the
-- ADD and the SET NOT NULL.
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Restore previous table-level UPDATE grants.
REVOKE UPDATE (posture, activity, location_note, note, episode_occurred)
  ON daily_status_entries FROM pots_app;
GRANT UPDATE ON daily_status_entries TO pots_app;
GRANT UPDATE ON episodes TO pots_app;

-- Restore the previous INSERT policies (no recorded_by self-stamp).
DROP POLICY IF EXISTS daily_status_insert ON daily_status_entries;
CREATE POLICY daily_status_insert ON daily_status_entries FOR INSERT
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));

DROP POLICY IF EXISTS episodes_insert ON episodes;
CREATE POLICY episodes_insert ON episodes FOR INSERT
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));

ALTER TABLE episodes
  DROP CONSTRAINT IF EXISTS fk_episodes_users_recorded_by_user_id;
DROP INDEX IF EXISTS ix_episodes_recorded_by_user_id;
ALTER TABLE episodes DROP COLUMN IF EXISTS recorded_by_user_id;

ALTER TABLE daily_status_entries
  DROP CONSTRAINT IF EXISTS fk_daily_status_entries_users_recorded_by_user_id;
DROP INDEX IF EXISTS ix_daily_status_entries_recorded_by_user_id;
ALTER TABLE daily_status_entries DROP COLUMN IF EXISTS recorded_by_user_id;
");
        }
    }
}
