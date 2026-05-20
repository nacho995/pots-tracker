using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pots.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase7_AddRecordedByToLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase 7.2 — extend the recorded_by_user_id pattern from Phase 5
            // (daily_status_entries, episodes) to the remaining patient-data
            // tables so an Editor on-behalf record carries attribution and
            // RLS pins the self-stamp.
            //
            // Same shape as 20260520102238_AddRecordedByOnLogs:
            //   1. ADD COLUMN nullable
            //   2. UPDATE ... FROM patients (backfill from owner)
            //   3. ALTER COLUMN SET NOT NULL
            //   4. ADD FOREIGN KEY users(id) ON DELETE RESTRICT
            //   5. CREATE INDEX
            // The backfill is correct because pre-Phase-7 only the owner
            // ever wrote to these tables (Phase 5 only enabled Editor for
            // status; Phase 7 extends to the rest).
            //
            // Then tighten INSERT RLS to require self-stamp, and column-
            // level UPDATE GRANT so recorded_by/patient_id/status remain
            // immutable at the Postgres layer (mirror of Phase 5 fix).
            migrationBuilder.Sql(@"
-- symptom_logs --------------------------------------------------------------
ALTER TABLE symptom_logs ADD COLUMN recorded_by_user_id uuid NULL;
UPDATE symptom_logs sl
   SET recorded_by_user_id = p.owner_user_id
  FROM patients p WHERE p.id = sl.patient_id;
ALTER TABLE symptom_logs ALTER COLUMN recorded_by_user_id SET NOT NULL;
ALTER TABLE symptom_logs
  ADD CONSTRAINT fk_symptom_logs_users_recorded_by_user_id
  FOREIGN KEY (recorded_by_user_id) REFERENCES users(id) ON DELETE RESTRICT;
CREATE INDEX ix_symptom_logs_recorded_by_user_id
  ON symptom_logs (recorded_by_user_id);

-- vital_sign_logs -----------------------------------------------------------
ALTER TABLE vital_sign_logs ADD COLUMN recorded_by_user_id uuid NULL;
UPDATE vital_sign_logs vl
   SET recorded_by_user_id = p.owner_user_id
  FROM patients p WHERE p.id = vl.patient_id;
ALTER TABLE vital_sign_logs ALTER COLUMN recorded_by_user_id SET NOT NULL;
ALTER TABLE vital_sign_logs
  ADD CONSTRAINT fk_vital_sign_logs_users_recorded_by_user_id
  FOREIGN KEY (recorded_by_user_id) REFERENCES users(id) ON DELETE RESTRICT;
CREATE INDEX ix_vital_sign_logs_recorded_by_user_id
  ON vital_sign_logs (recorded_by_user_id);

-- preventive_action_logs ----------------------------------------------------
ALTER TABLE preventive_action_logs ADD COLUMN recorded_by_user_id uuid NULL;
UPDATE preventive_action_logs pa
   SET recorded_by_user_id = p.owner_user_id
  FROM patients p WHERE p.id = pa.patient_id;
ALTER TABLE preventive_action_logs ALTER COLUMN recorded_by_user_id SET NOT NULL;
ALTER TABLE preventive_action_logs
  ADD CONSTRAINT fk_preventive_action_logs_users_recorded_by_user_id
  FOREIGN KEY (recorded_by_user_id) REFERENCES users(id) ON DELETE RESTRICT;
CREATE INDEX ix_preventive_action_logs_recorded_by_user_id
  ON preventive_action_logs (recorded_by_user_id);

-- Tighten INSERT policies: callers MUST self-stamp recorded_by_user_id.
DROP POLICY IF EXISTS symptom_logs_insert ON symptom_logs;
CREATE POLICY symptom_logs_insert ON symptom_logs FOR INSERT
  WITH CHECK (
    has_patient_edit_access(patient_id, app_current_user_id())
    AND recorded_by_user_id = app_current_user_id()
  );

DROP POLICY IF EXISTS vital_sign_logs_insert ON vital_sign_logs;
CREATE POLICY vital_sign_logs_insert ON vital_sign_logs FOR INSERT
  WITH CHECK (
    has_patient_edit_access(patient_id, app_current_user_id())
    AND recorded_by_user_id = app_current_user_id()
  );

DROP POLICY IF EXISTS preventive_action_logs_insert ON preventive_action_logs;
CREATE POLICY preventive_action_logs_insert ON preventive_action_logs FOR INSERT
  WITH CHECK (
    has_patient_edit_access(patient_id, app_current_user_id())
    AND recorded_by_user_id = app_current_user_id()
  );

-- Column-level UPDATE grants. Mirror of the Phase 5 / caregiver_notes fix:
-- the C# domain treats recorded_by/patient_id/created_at as immutable, but
-- without column-level grants an Editor with has_patient_edit_access could
-- in theory rewrite history via direct UPDATE. We REVOKE table-level UPDATE
-- and grant only the columns that the existing Update methods touch.
--
-- symptom_logs has NO domain Update method (each entry is a separate
-- moment-in-time record), so REVOKE UPDATE entirely. Same for vital_sign_logs.
-- preventive_action_logs HAS an Update for the daily upsert flow — grant
-- only the mutable fields, excluding recorded_by_user_id, patient_id, day,
-- created_at (all immutable after creation).
REVOKE UPDATE ON symptom_logs FROM pots_app;
REVOKE UPDATE ON vital_sign_logs FROM pots_app;
REVOKE UPDATE ON preventive_action_logs FROM pots_app;
GRANT UPDATE (
  fluid_ml, electrolyte_taken, morning_water_before_standing, urine_color,
  salt_target_reached, regular_meals, no_skipped_breakfast, skipped_breakfast,
  small_frequent_meals, avoided_large_high_carb_meal, adequate_protein,
  alcohol_avoided, caffeine_level,
  compression_socks, waist_high_compression, abdominal_compression, compression_hours,
  recumbent_exercise, walking, strength, stretching, pt_exercises,
  exercise_duration_min, exercise_intensity, post_exercise_symptoms,
  planned_rest_breaks, avoided_overexertion, used_activity_pacing,
  avoided_long_standing, sat_during_shower_or_cooking, used_mobility_aid,
  mobility_aid,
  avoided_heat, used_cooling_vest_fan, cold_shower, avoided_hot_bath_sauna, stayed_in_shade_ac,
  slept_enough, sleep_quality, consistent_bedtimes, nap_taken, woke_refreshed,
  medication_taken_as_prescribed, missed_dose, side_effects,
  new_medication_or_supplement, rescue_medication_used,
  updated_at
) ON preventive_action_logs TO pots_app;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
GRANT UPDATE ON symptom_logs TO pots_app;
GRANT UPDATE ON vital_sign_logs TO pots_app;
REVOKE UPDATE (
  fluid_ml, electrolyte_taken, morning_water_before_standing, urine_color,
  salt_target_reached, regular_meals, no_skipped_breakfast, skipped_breakfast,
  small_frequent_meals, avoided_large_high_carb_meal, adequate_protein,
  alcohol_avoided, caffeine_level,
  compression_socks, waist_high_compression, abdominal_compression, compression_hours,
  recumbent_exercise, walking, strength, stretching, pt_exercises,
  exercise_duration_min, exercise_intensity, post_exercise_symptoms,
  planned_rest_breaks, avoided_overexertion, used_activity_pacing,
  avoided_long_standing, sat_during_shower_or_cooking, used_mobility_aid,
  mobility_aid,
  avoided_heat, used_cooling_vest_fan, cold_shower, avoided_hot_bath_sauna, stayed_in_shade_ac,
  slept_enough, sleep_quality, consistent_bedtimes, nap_taken, woke_refreshed,
  medication_taken_as_prescribed, missed_dose, side_effects,
  new_medication_or_supplement, rescue_medication_used,
  updated_at
) ON preventive_action_logs FROM pots_app;
GRANT UPDATE ON preventive_action_logs TO pots_app;

DROP POLICY IF EXISTS preventive_action_logs_insert ON preventive_action_logs;
CREATE POLICY preventive_action_logs_insert ON preventive_action_logs FOR INSERT
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));

DROP POLICY IF EXISTS vital_sign_logs_insert ON vital_sign_logs;
CREATE POLICY vital_sign_logs_insert ON vital_sign_logs FOR INSERT
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));

DROP POLICY IF EXISTS symptom_logs_insert ON symptom_logs;
CREATE POLICY symptom_logs_insert ON symptom_logs FOR INSERT
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));

ALTER TABLE preventive_action_logs
  DROP CONSTRAINT IF EXISTS fk_preventive_action_logs_users_recorded_by_user_id;
DROP INDEX IF EXISTS ix_preventive_action_logs_recorded_by_user_id;
ALTER TABLE preventive_action_logs DROP COLUMN IF EXISTS recorded_by_user_id;

ALTER TABLE vital_sign_logs
  DROP CONSTRAINT IF EXISTS fk_vital_sign_logs_users_recorded_by_user_id;
DROP INDEX IF EXISTS ix_vital_sign_logs_recorded_by_user_id;
ALTER TABLE vital_sign_logs DROP COLUMN IF EXISTS recorded_by_user_id;

ALTER TABLE symptom_logs
  DROP CONSTRAINT IF EXISTS fk_symptom_logs_users_recorded_by_user_id;
DROP INDEX IF EXISTS ix_symptom_logs_recorded_by_user_id;
ALTER TABLE symptom_logs DROP COLUMN IF EXISTS recorded_by_user_id;
");
        }
    }
}
