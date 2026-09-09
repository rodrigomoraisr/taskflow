#!/usr/bin/env bash
set -euo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="taskflow-smoke-$(date +%s)-$$"
scratch="$(mktemp -d)"
# Explicit overrides isolate this run from the developer's .env and host ports.
export API_PORT=0 POSTGRES_PORT=0
export POSTGRES_PASSWORD="$(python3 -c 'import secrets; print(secrets.token_hex(24))')"
export TASKFLOW_JWT_KEY="$(python3 -c 'import secrets; print(secrets.token_hex(48))')"
compose=(docker compose --project-name "$project" --file "$repo_dir/docker-compose.yml")
cleanup() {
    result=$?
    trap - EXIT
    if [ "$result" -ne 0 ]; then
        "${compose[@]}" logs --no-color --tail 80 || true
    fi
    if [ -n "${invalid_container:-}" ]; then
        if [ "$result" -ne 0 ]; then docker logs "$invalid_container" || true; fi
        docker rm -f "$invalid_container" >/dev/null || true
    fi
    # Only resources created under this unique smoke-test project are removed.
    "${compose[@]}" down --volumes --remove-orphans >/dev/null || true
    rm -rf "$scratch"
    exit "$result"
}
trap cleanup EXIT

"${compose[@]}" build

# An actual migration failure must prevent the API from starting.
cat > "$scratch/failed-migration.yml" <<'YAML'
services:
  migrate:
    environment:
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=taskflow;Username=postgres;Password=deliberately-wrong;GSS Encryption Mode=Disable"
YAML
if "${compose[@]}" --file "$scratch/failed-migration.yml" up -d api; then
    echo "Expected deployment to stop after a migration failure." >&2
    exit 1
fi
migration_id="$("${compose[@]}" ps --all -q migrate)"
test "$(docker inspect --format '{{.State.Status}}' "$migration_id")" = exited
test "$(docker inspect --format '{{.State.ExitCode}}' "$migration_id")" -eq 1
if [ -n "$("${compose[@]}" ps --status running -q api)" ]; then
    echo "API started despite failed migrations." >&2
    exit 1
fi

"${compose[@]}" up -d --wait --wait-timeout 120
base_url="http://$("${compose[@]}" port api 8080)"
test "$("${compose[@]}" exec -T api id -u)" -ne 0
test "$("${compose[@]}" run --rm --no-deps -T --entrypoint id migrate -u)" -ne 0
test -z "$("${compose[@]}" exec -T api dotnet --list-sdks)"

"${compose[@]}" run --rm --no-deps -T migrate > "$scratch/migration.log"
python3 -c 'import pathlib,sys; assert "Applying 0 pending migration(s)." in pathlib.Path(sys.argv[1]).read_text()' "$scratch/migration.log"

# Exercise the production grant script in an isolated database in this disposable cluster.
"${compose[@]}" exec -T postgres psql -U postgres -d taskflow -v ON_ERROR_STOP=1 \
    -c 'CREATE DATABASE neondb;' >/dev/null
grant_connection="Host=postgres;Port=5432;Database=neondb;Username=postgres;Password=$POSTGRES_PASSWORD;GSS Encryption Mode=Disable"
if "${compose[@]}" run --rm --no-deps -T \
    -e "ConnectionStrings__DefaultConnection=$grant_connection" \
    -e TASKFLOW_APPLY_RUNTIME_GRANTS=true migrate; then
    echo "Expected migration command to fail when runtime grants cannot be applied." >&2
    exit 1
fi
"${compose[@]}" exec -T postgres psql -U postgres -d neondb -v ON_ERROR_STOP=1 \
    -c 'CREATE ROLE taskflow_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;' >/dev/null
for attempt in 1 2; do
    "${compose[@]}" run --rm --no-deps -T \
        -e "ConnectionStrings__DefaultConnection=$grant_connection" \
        -e TASKFLOW_APPLY_RUNTIME_GRANTS=true migrate
done
grants_valid="$("${compose[@]}" exec -T postgres psql -U postgres -d neondb -At -v ON_ERROR_STOP=1 -c \
    "SELECT has_table_privilege('taskflow_app', 'public.tasks', 'SELECT')
        AND has_table_privilege('taskflow_app', 'public.tasks', 'UPDATE')
        AND has_table_privilege('taskflow_app', 'public.task_activities', 'INSERT')
        AND NOT has_table_privilege('taskflow_app', 'public.tasks', 'DELETE')
        AND NOT has_table_privilege('taskflow_app', 'public.task_activities', 'UPDATE')
        AND NOT has_table_privilege('taskflow_app', 'public.\"__EFMigrationsHistory\"', 'SELECT');")"
test "$grants_valid" = t
cat > "$scratch/invalid-api.yml" <<'YAML'
services:
  api:
    restart: "no"
    environment:
      Jwt__Key: ""
YAML
invalid_container="$("${compose[@]}" --file "$scratch/invalid-api.yml" run -d --no-deps api)"
for ((attempt=0; attempt<20; attempt++)); do
    if [ "$(docker inspect --format '{{.State.Status}}' "$invalid_container")" = exited ]; then break; fi
    sleep 1
done
test "$(docker inspect --format '{{.State.Status}}' "$invalid_container")" = exited
test "$(docker inspect --format '{{.State.ExitCode}}' "$invalid_container")" -ne 0
docker logs "$invalid_container" > "$scratch/missing-key.log" 2>&1
python3 -c 'import pathlib,sys; assert "Jwt:Key is not configured" in pathlib.Path(sys.argv[1]).read_text()' "$scratch/missing-key.log"
docker rm "$invalid_container" > /dev/null
invalid_container=

python3 "$repo_dir/scripts/smoke-http.py" "$base_url" create "$scratch/state.json"
"${compose[@]}" stop postgres
python3 "$repo_dir/scripts/smoke-http.py" "$base_url" unavailable "$scratch/state.json"
"${compose[@]}" up -d --wait --wait-timeout 60 postgres
"${compose[@]}" restart api
# Docker may allocate a different ephemeral host port when restarting a container.
base_url="http://$("${compose[@]}" port api 8080)"
curl --fail --silent --show-error --retry 15 --retry-all-errors --retry-delay 1 \
    "$base_url/health/ready" > /dev/null
python3 "$repo_dir/scripts/smoke-http.py" "$base_url" persisted "$scratch/state.json"
echo "Container smoke checks passed: migration gate, non-root runtime, health, auth and persisted task."
