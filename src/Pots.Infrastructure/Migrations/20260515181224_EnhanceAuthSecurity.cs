using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pots.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceAuthSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Lowercase-hex CHECK on magic_link_tokens.token_hash so the unique index
-- cannot silently miss after any case-normalising contributor change.
ALTER TABLE magic_link_tokens
  ADD CONSTRAINT ck_magic_link_tokens_token_hash_lowercase_hex
  CHECK (token_hash ~ '^[0-9a-f]{64}$');

-- Replace auth_provision_user to also audit the sign-in / provisioning event.
-- SET row_security = off (function-scoped) lets the audit_log INSERT bypass
-- the audit_self_insert WITH CHECK (which expects app.current_user_id to
-- match actor_user_id — impossible here because the user is being established
-- by this very call). Defence-in-depth retained: SECURITY DEFINER + REVOKE
-- FROM PUBLIC + GRANT only to pots_app + the function still validates email
-- through the citext parameter type.
CREATE OR REPLACE FUNCTION auth_provision_user(p_email citext)
RETURNS uuid
LANGUAGE plpgsql SECURITY DEFINER
SET search_path = public
SET row_security = off
AS $$
DECLARE
  v_id uuid;
  v_action text;
BEGIN
  SELECT u.id INTO v_id FROM users u WHERE u.email = p_email;
  IF v_id IS NULL THEN
    v_id := gen_random_uuid();
    INSERT INTO users (id, email, created_at) VALUES (v_id, p_email, NOW());
    v_action := 'user.provisioned';
  ELSE
    v_action := 'user.signin';
  END IF;
  INSERT INTO audit_log (id, actor_user_id, action, entity_type, entity_id, created_at)
    VALUES (gen_random_uuid(), v_id, v_action, 'User', v_id, NOW());
  RETURN v_id;
END;
$$;
REVOKE EXECUTE ON FUNCTION auth_provision_user(citext) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION auth_provision_user(citext) TO pots_app;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION auth_provision_user(p_email citext)
RETURNS uuid
LANGUAGE plpgsql SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_id uuid;
BEGIN
  SELECT u.id INTO v_id FROM users u WHERE u.email = p_email;
  IF v_id IS NULL THEN
    v_id := gen_random_uuid();
    INSERT INTO users (id, email, created_at) VALUES (v_id, p_email, NOW());
  END IF;
  RETURN v_id;
END;
$$;

ALTER TABLE magic_link_tokens
  DROP CONSTRAINT IF EXISTS ck_magic_link_tokens_token_hash_lowercase_hex;
");
        }
    }
}
