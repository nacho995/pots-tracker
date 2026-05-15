using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pots.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- =========================================================================
-- IMPORTANT for future entity migrations:
--   Every new public-schema table that holds user data MUST in its migration:
--     1. GRANT SELECT, INSERT, UPDATE, DELETE ... TO pots_app;
--     2. ALTER TABLE ... ENABLE ROW LEVEL SECURITY;
--     3. ALTER TABLE ... FORCE ROW LEVEL SECURITY;   -- bind the owner too
--     4. CREATE POLICY ... using app_current_user_id() / has_patient_access(...)
--   We intentionally do NOT set ALTER DEFAULT PRIVILEGES for tables, because
--   that would give pots_app silent DML access to new tables that may have
--   forgotten to ENABLE RLS — exactly the kind of footgun that leaks data
--   on the next 'add new entity' migration.
-- =========================================================================

-- Explicit table-level grants for the four tables that exist today.
GRANT SELECT, INSERT, UPDATE, DELETE ON users, patients, patient_grants, audit_log TO pots_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO pots_app;
-- Sequences are safe to default-grant: a new sequence on its own doesn't
-- expose user data; the parent table is the gate.
ALTER DEFAULT PRIVILEGES FOR ROLE pots_dev IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO pots_app;

-- Helper: read the current user_id pinned by RlsCommandInterceptor.
-- Safe against malformed GUC values: NULLIF on '' avoids cast errors when
-- no user context has been set.
CREATE OR REPLACE FUNCTION app_current_user_id() RETURNS uuid AS $$
  SELECT NULLIF(current_setting('app.current_user_id', true), '')::uuid
$$ LANGUAGE SQL STABLE;

-- SECURITY DEFINER helpers run with the function owner's (pots_dev) privileges,
-- bypassing RLS internally. This breaks the cross-table policy recursion that
-- occurs when patients.SELECT references patient_grants and grants.SELECT
-- references patients.
CREATE OR REPLACE FUNCTION is_patient_owner(p_patient_id uuid, p_user_id uuid)
RETURNS boolean
LANGUAGE SQL SECURITY DEFINER STABLE
SET search_path = public
AS $$
  SELECT EXISTS (
    SELECT 1 FROM patients p
    WHERE p.id = p_patient_id AND p.owner_user_id = p_user_id
  );
$$;

CREATE OR REPLACE FUNCTION has_patient_access(p_patient_id uuid, p_user_id uuid)
RETURNS boolean
LANGUAGE SQL SECURITY DEFINER STABLE
SET search_path = public
AS $$
  SELECT EXISTS (
    SELECT 1 FROM patients p
    WHERE p.id = p_patient_id AND p.owner_user_id = p_user_id
  ) OR EXISTS (
    SELECT 1 FROM patient_grants g
    WHERE g.patient_id = p_patient_id
      AND g.grantee_user_id = p_user_id
      AND g.revoked_at IS NULL
  );
$$;

CREATE OR REPLACE FUNCTION has_patient_edit_access(p_patient_id uuid, p_user_id uuid)
RETURNS boolean
LANGUAGE SQL SECURITY DEFINER STABLE
SET search_path = public
AS $$
  SELECT EXISTS (
    SELECT 1 FROM patients p
    WHERE p.id = p_patient_id AND p.owner_user_id = p_user_id
  ) OR EXISTS (
    SELECT 1 FROM patient_grants g
    WHERE g.patient_id = p_patient_id
      AND g.grantee_user_id = p_user_id
      AND g.role = 'Editor'
      AND g.revoked_at IS NULL
  );
$$;

REVOKE EXECUTE ON FUNCTION is_patient_owner(uuid, uuid) FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION has_patient_access(uuid, uuid) FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION has_patient_edit_access(uuid, uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION is_patient_owner(uuid, uuid) TO pots_app;
GRANT EXECUTE ON FUNCTION has_patient_access(uuid, uuid) TO pots_app;
GRANT EXECUTE ON FUNCTION has_patient_edit_access(uuid, uuid) TO pots_app;

-- audit_log mutation defense: REVOKE on the app role + DB-level trigger that
-- raises for BOTH pots_app and pots_dev. The trigger is the floor when the
-- table owner would otherwise bypass RLS.
REVOKE UPDATE, DELETE ON audit_log FROM pots_app;

CREATE OR REPLACE FUNCTION audit_log_block_mutation() RETURNS trigger AS $$
BEGIN
  RAISE EXCEPTION 'audit_log is append-only; % is not permitted', TG_OP;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER audit_log_no_update
  BEFORE UPDATE ON audit_log
  FOR EACH ROW EXECUTE FUNCTION audit_log_block_mutation();

CREATE TRIGGER audit_log_no_delete
  BEFORE DELETE ON audit_log
  FOR EACH ROW EXECUTE FUNCTION audit_log_block_mutation();

-- users: app role has no DELETE. GDPR Art. 17 erasure flows through an
-- explicit, privileged ops workflow (separate role/process), not through the
-- pots_app session. This makes accidental erasure impossible from the API.
REVOKE DELETE ON users FROM pots_app;

-- Enable AND force RLS so the table owner (pots_dev) is also policy-bound.
-- Without FORCE, any code path that ever connects as pots_dev silently
-- bypasses every policy. Migrations still work because they run DDL, not DML.
ALTER TABLE users           ENABLE ROW LEVEL SECURITY;
ALTER TABLE patients        ENABLE ROW LEVEL SECURITY;
ALTER TABLE patient_grants  ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit_log       ENABLE ROW LEVEL SECURITY;

ALTER TABLE users           FORCE ROW LEVEL SECURITY;
ALTER TABLE patients        FORCE ROW LEVEL SECURITY;
ALTER TABLE patient_grants  FORCE ROW LEVEL SECURITY;
ALTER TABLE audit_log       FORCE ROW LEVEL SECURITY;

-- USERS: a user can only see/insert/update their own row.
CREATE POLICY users_self_select ON users FOR SELECT
  USING (id = app_current_user_id());
CREATE POLICY users_self_insert ON users FOR INSERT
  WITH CHECK (id = app_current_user_id());
CREATE POLICY users_self_update ON users FOR UPDATE
  USING (id = app_current_user_id())
  WITH CHECK (id = app_current_user_id());

-- PATIENTS: owner has full access; viewers/editors per active grant.
CREATE POLICY patients_owner_or_grantee_select ON patients FOR SELECT
  USING (has_patient_access(id, app_current_user_id()));

CREATE POLICY patients_owner_insert ON patients FOR INSERT
  WITH CHECK (owner_user_id = app_current_user_id());

CREATE POLICY patients_owner_or_editor_update ON patients FOR UPDATE
  USING (has_patient_edit_access(id, app_current_user_id()))
  WITH CHECK (has_patient_edit_access(id, app_current_user_id()));

CREATE POLICY patients_owner_delete ON patients FOR DELETE
  USING (owner_user_id = app_current_user_id());

-- PATIENT_GRANTS: visible to grantee or patient owner; mutations only by
-- patient owner AND the audit fields (granted_by, revoked_by) must reflect
-- the current acting user — a session compromise cannot forge audit attribution.
CREATE POLICY grants_grantee_or_owner_select ON patient_grants FOR SELECT
  USING (
    grantee_user_id = app_current_user_id()
    OR is_patient_owner(patient_id, app_current_user_id())
  );

CREATE POLICY grants_owner_modify ON patient_grants FOR ALL
  USING (is_patient_owner(patient_id, app_current_user_id()))
  WITH CHECK (
    is_patient_owner(patient_id, app_current_user_id())
    AND granted_by_user_id = app_current_user_id()
    AND (revoked_by_user_id IS NULL OR revoked_by_user_id = app_current_user_id())
  );

-- AUDIT_LOG: actor sees own actions; patient-access holders see entries for
-- their patient. Insert must self-stamp actor AND, when patient-scoped, the
-- actor must have access to that patient.
CREATE POLICY audit_actor_or_patient_access_select ON audit_log FOR SELECT
  USING (
    actor_user_id = app_current_user_id()
    OR (
      patient_id IS NOT NULL
      AND has_patient_access(patient_id, app_current_user_id())
    )
  );

CREATE POLICY audit_self_insert ON audit_log FOR INSERT
  WITH CHECK (
    actor_user_id = app_current_user_id()
    AND (patient_id IS NULL OR has_patient_access(patient_id, app_current_user_id()))
  );

-- ==========================================================================
-- PRIVILEGED BYPASS — SECURITY-SENSITIVE
-- auth_find_user_by_email runs as SECURITY DEFINER and bypasses RLS. It is
-- the one path that lets pots_app translate an email into a user_id WITHOUT
-- an authenticated context (sign-in bootstrap).
--
-- SAFETY REQUIREMENTS for callers:
--   - API endpoint MUST enforce rate limiting (e.g., 5 requests / IP / 5min)
--     to prevent user enumeration via timing or response presence.
--   - Response to the client MUST be identical for user-found and
--     user-not-found cases; never reveal account existence to the requestor.
--   - This function returns ONLY the user_id, never the email back, never
--     any other column. Do not extend the result set without security review.
-- ==========================================================================
CREATE OR REPLACE FUNCTION auth_find_user_by_email(p_email citext)
RETURNS TABLE(id uuid)
LANGUAGE SQL
SECURITY DEFINER
STABLE
SET search_path = public
AS $$
  SELECT u.id FROM users u WHERE u.email = p_email LIMIT 1;
$$;
REVOKE EXECUTE ON FUNCTION auth_find_user_by_email(citext) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION auth_find_user_by_email(citext) TO pots_app;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS auth_find_user_by_email(citext);

DROP POLICY IF EXISTS audit_self_insert                       ON audit_log;
DROP POLICY IF EXISTS audit_actor_or_patient_access_select    ON audit_log;
DROP POLICY IF EXISTS grants_owner_modify                     ON patient_grants;
DROP POLICY IF EXISTS grants_grantee_or_owner_select          ON patient_grants;
DROP POLICY IF EXISTS patients_owner_delete                   ON patients;
DROP POLICY IF EXISTS patients_owner_or_editor_update         ON patients;
DROP POLICY IF EXISTS patients_owner_insert                   ON patients;
DROP POLICY IF EXISTS patients_owner_or_grantee_select        ON patients;
DROP POLICY IF EXISTS users_self_update                       ON users;
DROP POLICY IF EXISTS users_self_insert                       ON users;
DROP POLICY IF EXISTS users_self_select                       ON users;

ALTER TABLE audit_log       NO FORCE ROW LEVEL SECURITY;
ALTER TABLE patient_grants  NO FORCE ROW LEVEL SECURITY;
ALTER TABLE patients        NO FORCE ROW LEVEL SECURITY;
ALTER TABLE users           NO FORCE ROW LEVEL SECURITY;

ALTER TABLE audit_log       DISABLE ROW LEVEL SECURITY;
ALTER TABLE patient_grants  DISABLE ROW LEVEL SECURITY;
ALTER TABLE patients        DISABLE ROW LEVEL SECURITY;
ALTER TABLE users           DISABLE ROW LEVEL SECURITY;

GRANT DELETE ON users TO pots_app;

DROP TRIGGER IF EXISTS audit_log_no_delete ON audit_log;
DROP TRIGGER IF EXISTS audit_log_no_update ON audit_log;
DROP FUNCTION IF EXISTS audit_log_block_mutation();

GRANT UPDATE, DELETE ON audit_log TO pots_app;

DROP FUNCTION IF EXISTS has_patient_edit_access(uuid, uuid);
DROP FUNCTION IF EXISTS has_patient_access(uuid, uuid);
DROP FUNCTION IF EXISTS is_patient_owner(uuid, uuid);
DROP FUNCTION IF EXISTS app_current_user_id();

ALTER DEFAULT PRIVILEGES FOR ROLE pots_dev IN SCHEMA public
  REVOKE USAGE, SELECT ON SEQUENCES FROM pots_app;
REVOKE USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public FROM pots_app;
REVOKE SELECT, INSERT, UPDATE, DELETE ON users, patients, patient_grants, audit_log FROM pots_app;
");
        }
    }
}
