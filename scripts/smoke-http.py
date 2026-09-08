"""HTTP assertions for the disposable container smoke test; uses only Python's standard library."""
import json
import pathlib
import sys
import urllib.error
import urllib.request

base_url, mode, state_path = sys.argv[1:]


def request(method, path, expected=200, body=None, token=None):
    headers = {"X-Correlation-ID": "container-smoke"}
    if body is not None:
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = f"Bearer {token}"
    req = urllib.request.Request(base_url + path, method=method, headers=headers,
                                 data=json.dumps(body).encode() if body is not None else None)
    try:
        response = urllib.request.urlopen(req, timeout=10)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        assert response.status == expected, f"{method} {path}: expected {expected}, got {response.status}"
        assert response.headers["X-Correlation-ID"] == "container-smoke"
        data = response.read()
        if "json" in response.headers.get("Content-Type", ""):
            return json.loads(data)
        return data.decode()


if mode == "unavailable":
    assert request("GET", "/health/live") == "Healthy"
    assert request("GET", "/health/ready", expected=503) == "Unhealthy"
elif mode == "create":
    assert request("GET", "/health/ready") == "Healthy"
    # Production does not expose the development OpenAPI document.
    problem = request("GET", "/openapi/v1.json", expected=404)
    assert problem["correlationId"] == "container-smoke"
    credentials = {"email": "container-smoke@example.test", "password": "Container-smoke-password-2026"}
    user = request("POST", "/auth/register", expected=201, body=credentials)
    session = request("POST", "/auth/login", body=credentials)
    token = session["token"]
    workspace = user["workspaceId"]
    project = request("POST", f"/api/workspaces/{workspace}/projects", expected=201,
                      token=token, body={"name": "Container project", "description": "Smoke test"})
    task = request("POST", f"/api/workspaces/{workspace}/tasks", expected=201, token=token,
                   body={"projectId": project["id"], "title": "Survives restart", "description": "Smoke test"})
    pathlib.Path(state_path).write_text(json.dumps({"credentials": credentials,
                                                  "workspace": workspace, "task": task["id"]}))
elif mode == "persisted":
    state = json.loads(pathlib.Path(state_path).read_text())
    session = request("POST", "/auth/login", body=state["credentials"])
    task = request("GET", f"/api/workspaces/{state['workspace']}/tasks/{state['task']}",
                   token=session["token"])
    assert task["title"] == "Survives restart"
else:
    raise ValueError(f"Unknown smoke-test mode: {mode}")
