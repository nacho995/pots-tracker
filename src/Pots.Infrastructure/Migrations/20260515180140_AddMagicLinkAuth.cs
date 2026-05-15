using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pots.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMagicLinkAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "magic_link_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "citext", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_magic_link_tokens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_magic_link_tokens_expires_at",
                table: "magic_link_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_magic_link_tokens_token_hash",
                table: "magic_link_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.Sql(@"
-- magic_link_tokens: auth bootstrap. Intentionally NOT under RLS — the
-- sign-in flow has no authenticated user yet. The raw token (not stored;
-- only its SHA-256 hash) is the access credential, so leakage of the table
-- contents alone cannot forge a session. The unique index on token_hash
-- prevents replay across users.
GRANT SELECT, INSERT, UPDATE ON magic_link_tokens TO pots_app;

-- auth_provision_user: SECURITY DEFINER bypass that returns the user_id
-- for a given email, creating the row if it doesn't exist. Used by the
-- magic-link verify endpoint AFTER the token has been validated, so it
-- only runs on confirmed email ownership.
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
REVOKE EXECUTE ON FUNCTION auth_provision_user(citext) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION auth_provision_user(citext) TO pots_app;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS auth_provision_user(citext);
REVOKE SELECT, INSERT, UPDATE ON magic_link_tokens FROM pots_app;
");
            migrationBuilder.DropTable(
                name: "magic_link_tokens");
        }
    }
}
