# DruOpc — web-based OPC UA browser

DruOpc is a Blazor Server application for browsing and inspecting any OPC UA server
from a web browser. It was built alongside the OPC PLC simulator in this repository,
but works against any reachable OPC UA endpoint.

**New here?** The [getting started guide](../docs/getting-started.md) gets you to a
live connection in 15 minutes; the [user manual](../docs/user-manual.md) covers
every feature, troubleshooting and an OPC UA glossary.

## Run

```bash
dotnet run --project browser
# then open http://localhost:5080
```

By default it offers to connect to `opc.tcp://localhost:50000` (the local OPC PLC
simulator). Enter any other endpoint URL to browse a different server.

## Features

- **Endpoint discovery** — list a server's endpoints with security policy, mode,
  security level and supported identity tokens; connect to a specific endpoint.
- **Authentication** — anonymous, username/password or X509 certificate
  (.pfx/.p12 upload); optional secure endpoint selection; per-certificate
  trust dialog (trust once / permanently) with auto-accept off by default.
- **Address-space tree** — lazy-loading hierarchy with node-class icons,
  continuation-point handling and per-node actions.
- **Go to node** — jump directly to a node by NodeId string (`ns=3;s=MyNode`).
- **Attributes panel** — all attributes of the selected node, with decoded
  access levels, value ranks, resolved data-type names and value timestamps.
- **References panel** — forward and inverse references with reference-type
  names and clickable targets.
- **Watch list (data access view)** — live subscription-driven values with
  status, timestamps, configurable publishing/sampling intervals, and inline
  writes to writable variables (typed parsing, arrays as comma-separated values).
- **Method calls** — resolve input/output arguments of a method, enter values,
  call, and inspect results.
- **Event view** — subscribe to any event notifier (e.g. the Server object) and
  watch events live with severity coloring; server-side filters by minimum
  severity and event type.
- **Alarms & conditions** — live view of the server's retained conditions with
  active/acknowledged state, ConditionRefresh replay, and one-click
  Acknowledge.
- **History read** — raw history over a time range with a chart, table and CSV
  export (for servers that historize).
- **Value inspector** — full, untruncated values; server-defined structures
  (UDTs) render as an expandable JSON tree.
- **Subscription inspector** — the session's subscriptions with ids, revised
  intervals and item counts.
- **Session panel** — endpoint, security, identity, server certificate
  thumbprint and the namespace table.
- **Automatic reconnect** — keep-alive monitoring with reconnect handling and
  visible connection state.

## Client certificate

On first run DruOpc creates a self-signed client application certificate in
`~/.local/share/DruOpc/pki` (or the platform equivalent). Servers that enforce
client-certificate trust must trust that certificate before secure connections
succeed.
