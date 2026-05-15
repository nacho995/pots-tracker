-- Creates the application role that is subject to Row-Level Security.
-- The migrations role (POSTGRES_USER from docker-compose, currently pots_dev)
-- is the table owner and bypasses RLS by default — that's what runs migrations.
-- The runtime application connects as pots_app and is fully subject to RLS.
--
-- This script runs ONCE on first volume init via Postgres's docker entrypoint.
-- Per-table GRANTs and policy setup live in the EnableRowLevelSecurity EF
-- migration so they survive `dotnet ef database drop` + re-apply cycles.
--
-- Production deploys MUST replace the password via env/secret manager.

CREATE ROLE pots_app LOGIN PASSWORD 'pots_app_dev_only_change_me';
GRANT CONNECT ON DATABASE pots TO pots_app;
GRANT USAGE ON SCHEMA public TO pots_app;
