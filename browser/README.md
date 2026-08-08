# UaScope — web-based OPC UA browser

UaScope is a Blazor Server application for browsing and inspecting any OPC UA server
from a web browser. It was built alongside the OPC PLC simulator in this repository,
but works against any reachable OPC UA endpoint.

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
- **Authentication** — anonymous or username/password; optional secure endpoint
  selection; auto-accept toggle for untrusted server certificates.
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
  watch events live with severity coloring.
- **Session panel** — endpoint, security, identity, server certificate
  thumbprint and the namespace table.
- **Automatic reconnect** — keep-alive monitoring with reconnect handling and
  visible connection state.

## Client certificate

On first run UaScope creates a self-signed client application certificate in
`~/.local/share/UaScope/pki` (or the platform equivalent). Servers that enforce
client-certificate trust must trust that certificate before secure connections
succeed.
