using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pots.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackingData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_status_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    posture = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    activity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    location_note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    episode_occurred = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_status_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_daily_status_entries_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "episodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    main_symptom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    posture_before = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    trigger_suspected = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    hr_during_bpm = table.Column<int>(type: "integer", nullable: true),
                    bp_during_systolic = table.Column<int>(type: "integer", nullable: true),
                    bp_during_diastolic = table.Column<int>(type: "integer", nullable: true),
                    action_taken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recovery_time_minutes = table.Column<int>(type: "integer", nullable: true),
                    prevented_fainting = table.Column<bool>(type: "boolean", nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_episodes", x => x.id);
                    table.ForeignKey(
                        name: "fk_episodes_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "patient_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hydration_target_ml = table.Column<int>(type: "integer", nullable: false),
                    salt_target_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    salt_target_mg = table.Column<int>(type: "integer", nullable: true),
                    salt_clinician_attestation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    compression_goal_hours_per_day = table.Column<int>(type: "integer", nullable: true),
                    exercise_plan_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    sleep_target_hours = table.Column<decimal>(type: "numeric(4,1)", nullable: true),
                    language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patient_targets", x => x.id);
                    table.ForeignKey(
                        name: "fk_patient_targets_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "preventive_action_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fluid_ml = table.Column<int>(type: "integer", nullable: true),
                    electrolyte_taken = table.Column<bool>(type: "boolean", nullable: false),
                    morning_water_before_standing = table.Column<bool>(type: "boolean", nullable: false),
                    urine_color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    salt_target_reached = table.Column<bool>(type: "boolean", nullable: true),
                    regular_meals = table.Column<bool>(type: "boolean", nullable: false),
                    skipped_breakfast = table.Column<bool>(type: "boolean", nullable: false),
                    small_frequent_meals = table.Column<bool>(type: "boolean", nullable: false),
                    avoided_large_high_carb_meal = table.Column<bool>(type: "boolean", nullable: false),
                    adequate_protein = table.Column<bool>(type: "boolean", nullable: false),
                    alcohol_avoided = table.Column<bool>(type: "boolean", nullable: false),
                    caffeine_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    compression_socks = table.Column<bool>(type: "boolean", nullable: false),
                    waist_high_compression = table.Column<bool>(type: "boolean", nullable: false),
                    abdominal_compression = table.Column<bool>(type: "boolean", nullable: false),
                    compression_hours_worn = table.Column<int>(type: "integer", nullable: true),
                    recumbent_exercise = table.Column<bool>(type: "boolean", nullable: false),
                    walking = table.Column<bool>(type: "boolean", nullable: false),
                    strength = table.Column<bool>(type: "boolean", nullable: false),
                    stretching = table.Column<bool>(type: "boolean", nullable: false),
                    pt_exercises = table.Column<bool>(type: "boolean", nullable: false),
                    exercise_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    exercise_intensity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    post_exercise_symptoms = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    planned_rest_breaks = table.Column<bool>(type: "boolean", nullable: false),
                    avoided_overexertion = table.Column<bool>(type: "boolean", nullable: false),
                    used_activity_pacing = table.Column<bool>(type: "boolean", nullable: false),
                    avoided_long_standing = table.Column<bool>(type: "boolean", nullable: false),
                    sat_during_shower_cooking = table.Column<bool>(type: "boolean", nullable: false),
                    mobility_aid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    avoided_heat = table.Column<bool>(type: "boolean", nullable: false),
                    used_cooling_vest_fan = table.Column<bool>(type: "boolean", nullable: false),
                    cold_shower = table.Column<bool>(type: "boolean", nullable: false),
                    avoided_hot_bath_sauna = table.Column<bool>(type: "boolean", nullable: false),
                    stayed_in_shade_ac = table.Column<bool>(type: "boolean", nullable: false),
                    slept_enough = table.Column<bool>(type: "boolean", nullable: false),
                    sleep_quality = table.Column<int>(type: "integer", nullable: true),
                    consistent_bedtimes = table.Column<bool>(type: "boolean", nullable: false),
                    nap_taken = table.Column<bool>(type: "boolean", nullable: false),
                    woke_refreshed = table.Column<bool>(type: "boolean", nullable: true),
                    medication_taken_as_prescribed = table.Column<bool>(type: "boolean", nullable: false),
                    missed_dose = table.Column<bool>(type: "boolean", nullable: false),
                    side_effects = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    new_medication_or_supplement = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    rescue_medication_used = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_preventive_action_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_preventive_action_logs_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "symptom_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dizziness = table.Column<int>(type: "integer", nullable: true),
                    palpitations = table.Column<int>(type: "integer", nullable: true),
                    tachycardia_sensation = table.Column<int>(type: "integer", nullable: true),
                    chest_discomfort = table.Column<int>(type: "integer", nullable: true),
                    shortness_of_breath = table.Column<int>(type: "integer", nullable: true),
                    near_fainting = table.Column<int>(type: "integer", nullable: true),
                    fainting_episode = table.Column<bool>(type: "boolean", nullable: false),
                    blood_pooling = table.Column<int>(type: "integer", nullable: true),
                    brain_fog = table.Column<int>(type: "integer", nullable: true),
                    headache = table.Column<int>(type: "integer", nullable: true),
                    visual_disturbance = table.Column<int>(type: "integer", nullable: true),
                    tremor = table.Column<int>(type: "integer", nullable: true),
                    weakness = table.Column<int>(type: "integer", nullable: true),
                    fatigue = table.Column<int>(type: "integer", nullable: true),
                    sleepiness = table.Column<int>(type: "integer", nullable: true),
                    nausea = table.Column<int>(type: "integer", nullable: true),
                    abdominal_pain = table.Column<int>(type: "integer", nullable: true),
                    bloating = table.Column<int>(type: "integer", nullable: true),
                    bowel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    appetite_level = table.Column<int>(type: "integer", nullable: true),
                    heat_intolerance = table.Column<int>(type: "integer", nullable: true),
                    sweating = table.Column<int>(type: "integer", nullable: true),
                    chills = table.Column<int>(type: "integer", nullable: true),
                    flushing = table.Column<int>(type: "integer", nullable: true),
                    cold_extremities = table.Column<int>(type: "integer", nullable: true),
                    anxiety = table.Column<int>(type: "integer", nullable: true),
                    mood = table.Column<int>(type: "integer", nullable: true),
                    ability_to_work = table.Column<int>(type: "integer", nullable: true),
                    ability_to_walk = table.Column<int>(type: "integer", nullable: true),
                    social_tolerance = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_symptom_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_symptom_logs_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vital_sign_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resting_hr_bpm = table.Column<int>(type: "integer", nullable: true),
                    standing_hr_bpm2min = table.Column<int>(type: "integer", nullable: true),
                    standing_hr_bpm5min = table.Column<int>(type: "integer", nullable: true),
                    standing_hr_bpm10min = table.Column<int>(type: "integer", nullable: true),
                    bp_lying_systolic = table.Column<int>(type: "integer", nullable: true),
                    bp_lying_diastolic = table.Column<int>(type: "integer", nullable: true),
                    bp_sitting_systolic = table.Column<int>(type: "integer", nullable: true),
                    bp_sitting_diastolic = table.Column<int>(type: "integer", nullable: true),
                    bp_standing_systolic = table.Column<int>(type: "integer", nullable: true),
                    bp_standing_diastolic = table.Column<int>(type: "integer", nullable: true),
                    spo2percent = table.Column<int>(type: "integer", nullable: true),
                    weight_kg = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    menstrual_cycle_day = table.Column<int>(type: "integer", nullable: true),
                    sleep_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    sleep_quality = table.Column<int>(type: "integer", nullable: true),
                    steps = table.Column<int>(type: "integer", nullable: true),
                    exercise_minutes = table.Column<int>(type: "integer", nullable: true),
                    time_upright_minutes = table.Column<int>(type: "integer", nullable: true),
                    time_lying_minutes = table.Column<int>(type: "integer", nullable: true),
                    ambient_temp_c = table.Column<decimal>(type: "numeric(4,1)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vital_sign_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_vital_sign_logs_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_daily_status_entries_patient_id_created_at",
                table: "daily_status_entries",
                columns: new[] { "patient_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_episodes_patient_id_start_time",
                table: "episodes",
                columns: new[] { "patient_id", "start_time" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_patient_targets_patient_id",
                table: "patient_targets",
                column: "patient_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_preventive_action_logs_patient_id_day",
                table: "preventive_action_logs",
                columns: new[] { "patient_id", "day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_symptom_logs_patient_id_recorded_at",
                table: "symptom_logs",
                columns: new[] { "patient_id", "recorded_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_vital_sign_logs_patient_id_recorded_at",
                table: "vital_sign_logs",
                columns: new[] { "patient_id", "recorded_at" },
                descending: new[] { false, true });

            migrationBuilder.Sql(@"
-- Extend the auth + RLS model to the new tracking tables. The owner-vs-
-- grantee policies reuse has_patient_access / has_patient_edit_access /
-- is_patient_owner from migration EnableRowLevelSecurity.

-- Grants for the runtime app role.
GRANT SELECT, INSERT, UPDATE, DELETE ON
  daily_status_entries, symptom_logs, vital_sign_logs,
  preventive_action_logs, episodes, patient_targets TO pots_app;

-- Force RLS so the non-superuser table owner is also policy-bound in prod.
ALTER TABLE daily_status_entries     ENABLE ROW LEVEL SECURITY;
ALTER TABLE symptom_logs             ENABLE ROW LEVEL SECURITY;
ALTER TABLE vital_sign_logs          ENABLE ROW LEVEL SECURITY;
ALTER TABLE preventive_action_logs   ENABLE ROW LEVEL SECURITY;
ALTER TABLE episodes                 ENABLE ROW LEVEL SECURITY;
ALTER TABLE patient_targets          ENABLE ROW LEVEL SECURITY;

ALTER TABLE daily_status_entries     FORCE ROW LEVEL SECURITY;
ALTER TABLE symptom_logs             FORCE ROW LEVEL SECURITY;
ALTER TABLE vital_sign_logs          FORCE ROW LEVEL SECURITY;
ALTER TABLE preventive_action_logs   FORCE ROW LEVEL SECURITY;
ALTER TABLE episodes                 FORCE ROW LEVEL SECURITY;
ALTER TABLE patient_targets          FORCE ROW LEVEL SECURITY;

-- daily_status_entries: read by owner+grantees; write by owner+editors.
CREATE POLICY daily_status_select ON daily_status_entries FOR SELECT
  USING (has_patient_access(patient_id, app_current_user_id()));
CREATE POLICY daily_status_insert ON daily_status_entries FOR INSERT
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));
CREATE POLICY daily_status_update ON daily_status_entries FOR UPDATE
  USING (has_patient_edit_access(patient_id, app_current_user_id()))
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));
CREATE POLICY daily_status_delete ON daily_status_entries FOR DELETE
  USING (is_patient_owner(patient_id, app_current_user_id()));

-- symptom_logs: same pattern.
CREATE POLICY symptom_logs_select ON symptom_logs FOR SELECT
  USING (has_patient_access(patient_id, app_current_user_id()));
CREATE POLICY symptom_logs_insert ON symptom_logs FOR INSERT
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));
CREATE POLICY symptom_logs_update ON symptom_logs FOR UPDATE
  USING (has_patient_edit_access(patient_id, app_current_user_id()))
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));
CREATE POLICY symptom_logs_delete ON symptom_logs FOR DELETE
  USING (is_patient_owner(patient_id, app_current_user_id()));

-- vital_sign_logs: same pattern.
CREATE POLICY vital_sign_logs_select ON vital_sign_logs FOR SELECT
  USING (has_patient_access(patient_id, app_current_user_id()));
CREATE POLICY vital_sign_logs_insert ON vital_sign_logs FOR INSERT
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));
CREATE POLICY vital_sign_logs_update ON vital_sign_logs FOR UPDATE
  USING (has_patient_edit_access(patient_id, app_current_user_id()))
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));
CREATE POLICY vital_sign_logs_delete ON vital_sign_logs FOR DELETE
  USING (is_patient_owner(patient_id, app_current_user_id()));

-- preventive_action_logs: same pattern.
CREATE POLICY preventive_action_logs_select ON preventive_action_logs FOR SELECT
  USING (has_patient_access(patient_id, app_current_user_id()));
CREATE POLICY preventive_action_logs_insert ON preventive_action_logs FOR INSERT
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));
CREATE POLICY preventive_action_logs_update ON preventive_action_logs FOR UPDATE
  USING (has_patient_edit_access(patient_id, app_current_user_id()))
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));
CREATE POLICY preventive_action_logs_delete ON preventive_action_logs FOR DELETE
  USING (is_patient_owner(patient_id, app_current_user_id()));

-- episodes: same pattern. Editors can register an episode on the owner's behalf.
CREATE POLICY episodes_select ON episodes FOR SELECT
  USING (has_patient_access(patient_id, app_current_user_id()));
CREATE POLICY episodes_insert ON episodes FOR INSERT
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));
CREATE POLICY episodes_update ON episodes FOR UPDATE
  USING (has_patient_edit_access(patient_id, app_current_user_id()))
  WITH CHECK (has_patient_edit_access(patient_id, app_current_user_id()));
CREATE POLICY episodes_delete ON episodes FOR DELETE
  USING (is_patient_owner(patient_id, app_current_user_id()));

-- patient_targets: STRICTER — only the owner reads or writes their personal
-- targets. The salt-gate (salt_target_enabled) is patient-only by design
-- (CLAUDE.md §2). Grantees do not get a settings UI in v1.
CREATE POLICY patient_targets_owner_select ON patient_targets FOR SELECT
  USING (is_patient_owner(patient_id, app_current_user_id()));
CREATE POLICY patient_targets_owner_insert ON patient_targets FOR INSERT
  WITH CHECK (is_patient_owner(patient_id, app_current_user_id()));
CREATE POLICY patient_targets_owner_update ON patient_targets FOR UPDATE
  USING (is_patient_owner(patient_id, app_current_user_id()))
  WITH CHECK (is_patient_owner(patient_id, app_current_user_id()));
CREATE POLICY patient_targets_owner_delete ON patient_targets FOR DELETE
  USING (is_patient_owner(patient_id, app_current_user_id()));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP POLICY IF EXISTS patient_targets_owner_delete           ON patient_targets;
DROP POLICY IF EXISTS patient_targets_owner_update           ON patient_targets;
DROP POLICY IF EXISTS patient_targets_owner_insert           ON patient_targets;
DROP POLICY IF EXISTS patient_targets_owner_select           ON patient_targets;
DROP POLICY IF EXISTS episodes_delete                        ON episodes;
DROP POLICY IF EXISTS episodes_update                        ON episodes;
DROP POLICY IF EXISTS episodes_insert                        ON episodes;
DROP POLICY IF EXISTS episodes_select                        ON episodes;
DROP POLICY IF EXISTS preventive_action_logs_delete          ON preventive_action_logs;
DROP POLICY IF EXISTS preventive_action_logs_update          ON preventive_action_logs;
DROP POLICY IF EXISTS preventive_action_logs_insert          ON preventive_action_logs;
DROP POLICY IF EXISTS preventive_action_logs_select          ON preventive_action_logs;
DROP POLICY IF EXISTS vital_sign_logs_delete                 ON vital_sign_logs;
DROP POLICY IF EXISTS vital_sign_logs_update                 ON vital_sign_logs;
DROP POLICY IF EXISTS vital_sign_logs_insert                 ON vital_sign_logs;
DROP POLICY IF EXISTS vital_sign_logs_select                 ON vital_sign_logs;
DROP POLICY IF EXISTS symptom_logs_delete                    ON symptom_logs;
DROP POLICY IF EXISTS symptom_logs_update                    ON symptom_logs;
DROP POLICY IF EXISTS symptom_logs_insert                    ON symptom_logs;
DROP POLICY IF EXISTS symptom_logs_select                    ON symptom_logs;
DROP POLICY IF EXISTS daily_status_delete                    ON daily_status_entries;
DROP POLICY IF EXISTS daily_status_update                    ON daily_status_entries;
DROP POLICY IF EXISTS daily_status_insert                    ON daily_status_entries;
DROP POLICY IF EXISTS daily_status_select                    ON daily_status_entries;

ALTER TABLE patient_targets        NO FORCE ROW LEVEL SECURITY;
ALTER TABLE episodes               NO FORCE ROW LEVEL SECURITY;
ALTER TABLE preventive_action_logs NO FORCE ROW LEVEL SECURITY;
ALTER TABLE vital_sign_logs        NO FORCE ROW LEVEL SECURITY;
ALTER TABLE symptom_logs           NO FORCE ROW LEVEL SECURITY;
ALTER TABLE daily_status_entries   NO FORCE ROW LEVEL SECURITY;

ALTER TABLE patient_targets        DISABLE ROW LEVEL SECURITY;
ALTER TABLE episodes               DISABLE ROW LEVEL SECURITY;
ALTER TABLE preventive_action_logs DISABLE ROW LEVEL SECURITY;
ALTER TABLE vital_sign_logs        DISABLE ROW LEVEL SECURITY;
ALTER TABLE symptom_logs           DISABLE ROW LEVEL SECURITY;
ALTER TABLE daily_status_entries   DISABLE ROW LEVEL SECURITY;

REVOKE SELECT, INSERT, UPDATE, DELETE ON
  daily_status_entries, symptom_logs, vital_sign_logs,
  preventive_action_logs, episodes, patient_targets FROM pots_app;
");

            migrationBuilder.DropTable(
                name: "daily_status_entries");

            migrationBuilder.DropTable(
                name: "episodes");

            migrationBuilder.DropTable(
                name: "patient_targets");

            migrationBuilder.DropTable(
                name: "preventive_action_logs");

            migrationBuilder.DropTable(
                name: "symptom_logs");

            migrationBuilder.DropTable(
                name: "vital_sign_logs");
        }
    }
}
