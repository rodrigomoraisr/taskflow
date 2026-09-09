-- Run as neondb_owner after the EF migrations, in neondb.
-- Explicit grants keep migration history and future tables private by default.
BEGIN;
GRANT CONNECT ON DATABASE neondb TO taskflow_app;
GRANT USAGE ON SCHEMA public TO taskflow_app;
GRANT SELECT, INSERT, UPDATE ON TABLE
    public."Users", public."Workspaces", public."WorkspaceUsers",
    public."Projects", public.tasks, public.comments,
    public.refresh_sessions, public.refresh_tokens
TO taskflow_app;
GRANT SELECT, INSERT ON TABLE public.task_activities TO taskflow_app;
-- All identifiers are application-generated UUIDs; no sequence grants are needed.
COMMIT;
