using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pots.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase6_BackfillDisplayNameAndAuditFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Two unrelated-but-coincident schema changes packed in one
            // migration because both surfaced in the same incident:
            //
            //   (1) Formalise audit_log.patient_id FK as ON DELETE SET NULL.
            //       The constraint was already altered out-of-band on
            //       production Neon during the May-20 caregiver cleanup
            //       (see Pots.Api.Patients incident), but the change never
            //       lived in source control. This migration makes the EF
            //       model snapshot match production. Audit rows are
            //       history: they must survive their patient being deleted,
            //       just without the FK link.
            //
            //   (2) Backfill users.display_name from patients.name where a
            //       user is the owner of a patient. Phase 6 separates the
            //       USER's name (Ignacio, María — every user has one) from
            //       the POTS PROFILE name (the patient's display name).
            //       Pre-Phase-6 the two were conflated: typing your name
            //       in /profile created a Patient. For existing patient-
            //       owners we want their display_name to inherit the
            //       Patient name so they don't see a blank "Tu nombre"
            //       field after the deploy. Caregivers (no patient) keep
            //       NULL and will be prompted to set it on next visit.
            migrationBuilder.Sql(@"
-- (1) audit_log → patients FK: RESTRICT → SET NULL
ALTER TABLE audit_log
  DROP CONSTRAINT IF EXISTS fk_audit_log_patients_patient_id;
ALTER TABLE audit_log
  ADD CONSTRAINT fk_audit_log_patients_patient_id
  FOREIGN KEY (patient_id) REFERENCES patients(id) ON DELETE SET NULL;

-- (2) Backfill users.display_name from patients.name for patient-owners.
-- The audit_log_no_update trigger does NOT fire on users (it's audit_log
-- only), so this UPDATE runs cleanly. Only rows where users.display_name
-- IS NULL are touched — never overwrite anything a user has already set.
UPDATE users u
   SET display_name = p.name
  FROM patients p
 WHERE p.owner_user_id = u.id
   AND u.display_name IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- (2 first — drop display_name backfill is meaningless; we leave the values
--  in place because clearing them would invent data loss on rollback.)

-- (1) Restore RESTRICT semantics
ALTER TABLE audit_log
  DROP CONSTRAINT IF EXISTS fk_audit_log_patients_patient_id;
ALTER TABLE audit_log
  ADD CONSTRAINT fk_audit_log_patients_patient_id
  FOREIGN KEY (patient_id) REFERENCES patients(id) ON DELETE RESTRICT;
");
        }
    }
}
