# DruOPC user manual

DruOPC is a web-based OPC UA client for browsing, inspecting and testing OPC UA
servers. This manual covers every feature. If you have not connected to anything
yet, start with the **[getting started guide](getting-started.md)** — it gets you
from zero to a live connection in 15 minutes.

New to OPC UA terminology? Jump to the [glossary](#glossary) whenever a term is
unfamiliar.

**Contents:**
[The screen at a glance](#the-screen-at-a-glance) ·
[Connecting](#connecting) ·
[Certificates and trust](#certificates-and-trust) ·
[The address-space tree](#the-address-space-tree) ·
[Attributes and references](#attributes-and-references) ·
[The watch list](#the-watch-list) ·
[Writing values](#writing-values) ·
[Events](#events) ·
[Alarms & Conditions](#alarms--conditions) ·
[History](#history) ·
[Methods](#methods) ·
[The value inspector](#the-value-inspector) ·
[Session details](#session-details-status-bar) ·
[Sharing and multiple servers](#sharing-and-multiple-servers) ·
[Troubleshooting](#troubleshooting) ·
[Glossary](#glossary)

## The screen at a glance

```text
┌───────────────────────────────────────────────────────────────────────────────┐
│ DruOPC  [ opc.tcp://localhost:50000 ] [Connect] [Endpoints…] 👤  ⧉new window │ ← top bar
├───────────────┬────────────────────────────────────┬──────────────────────────┤
│ ADDRESS SPACE │ WATCH LIST                         │ Selected node            │
│ [search…   🔍]│ live values, trends, write, CSV    │ name, NodeId, breadcrumb │
│ ▸ Objects     │                                    ├──────────────────────────┤
│   ▸ OpcPlc    ├────────────────────────────────────┤ ATTRIBUTES               │
│   ▸ Server    │ [Events] [Alarms & Conditions]     │ data type, value, access │
│ ▸ Types       │ live events / alarm summary        ├──────────────────────────┤
│ ▸ Views       │                                    │ REFERENCES               │
│               │                                    │ links to related nodes   │
├───────────────┴────────────────────────────────────┴──────────────────────────┤
│ endpoint | security | identity | namespaces | subscriptions | cert            │ ← status bar
└───────────────────────────────────────────────────────────────────────────────┘
```

- **Left** — the server's *address space*: every node the server exposes, as a tree.
- **Center** — your working area: the live **watch list** on top; **events** and
  **alarms** tabs below.
- **Right** — details of whichever node is selected: its attributes and references.
- **Bottom** — session facts: where you are connected, how, and with what identity.

## Connecting

### Quick connect

Type the server's endpoint URL (for example `opc.tcp://192.168.1.50:4840`) into the
address box and click **Connect**. DruOPC discovers the server's endpoints and
picks one automatically — an unencrypted endpoint by default, or the most secure
one if you tick **Prefer secure endpoint** in the **👤** menu.

Recently used servers appear as suggestions when you click into the address box.

### Choosing a specific endpoint

Click **Endpoints…** instead of Connect. DruOPC lists every endpoint the server
offers with its security policy (the encryption algorithm suite), security mode
(**None** / **Sign** / **SignAndEncrypt**), security level, and which login types
it accepts. Click **Connect** on the row you want.

Use this when a server offers several security configurations and you need a
particular one — for example, plant policy requires SignAndEncrypt, or you are
debugging and want None so you can see traffic in Wireshark.

### Logging in (user identity)

Open the **👤** menu *before* connecting and pick one of:

- **Anonymous** — no login. Many servers allow this read-only or not at all.
- **Username / password** — as configured on the server. (The simulator ships with
  `sysadmin` / `demo` and `user1` / `password`.)
- **X509 certificate** — upload a `.pfx`/`.p12` user certificate file and enter its
  password. The server must have been configured to trust that user certificate.

If you connect anonymously and later get `BadUserAccessDenied` errors when writing,
reconnect with a proper login — the server allowed you in, but read-only.

### Certificates and trust

OPC UA connections are mutually authenticated with certificates, like HTTPS in both
directions. Two things must happen before a secure connection works:

1. **You trust the server.** The first time DruOPC sees a server's certificate it
   shows a dialog with the subject, issuer, thumbprint and validity dates.
   - **Trust once** — accept for this browser session only.
   - **Trust permanently** — save the certificate to DruOPC's trusted store so you
     are never asked again for this server.
   - **Cancel** — abort the connection.

   If you prefer to skip the prompt entirely (for example on a test bench), tick
   **Auto-accept server certificate** in the **👤** menu. Leave it off for
   production networks.

2. **The server trusts you.** DruOPC identifies itself with its own certificate
   (created automatically on first run, stored under
   `~/.local/share/DruOPC/pki` on Linux, `%LOCALAPPDATA%\DruOPC\pki` on Windows).
   Most real servers *reject unknown clients* by default: your first secure
   connection attempt fails, and the server puts DruOPC's certificate in its
   "rejected" list. Go to the server's own certificate management (its web page,
   engineering tool, or `pki/rejected` folder), mark the DruOPC certificate as
   trusted, and connect again.

   The simulator in this repository auto-accepts client certificates, so you will
   not hit this locally — but you will on a real PLC, and it is the single most
   common "why won't it connect" cause in OPC UA.

### Connection status and reconnecting

The dot in the top-right shows the connection state: green **Connected**, yellow
**Connecting/Reconnecting**, grey **Disconnected**. If the network drops or the
server restarts, DruOPC reconnects automatically and re-establishes its
subscriptions — watch items keep updating and the alarm list refreshes itself.

## The address-space tree

The tree shows the server's nodes, loaded on demand as you expand folders.

- **Icons**: 📁 object/folder · 📊 variable (a value you can read) · ƒ method ·
  🧩/🧬/🔢/🔗 type-definition nodes.
- **Click** a node to inspect it (right panel).
- **Hover** over a node to reveal its quick actions:
  - **👁** on a variable — add it to the watch list.
  - **▶** on a method — open the call dialog.
  - **⚡** on an object — subscribe to its events (see [Events](#events)).
- Everything interesting on most servers lives under **Objects**. **Types** and
  **Views** describe the server's type system — useful reference, rarely needed
  day to day.

### Search and "go to node"

The box above the tree does two things:

- **Text search**: type part of a name (`pallet`, `temperature`) and press Enter.
  DruOPC sweeps the address space breadth-first and lists up to 50 matches; click
  one to jump there. Large servers are searched up to a few thousand nodes deep —
  if the header says the search stopped early, refine your term.
- **NodeId jump**: paste a NodeId string (`ns=3;s=FastUInt1`, `i=2253`) and press
  Enter to go straight to that node.

Clicking a search result, a reference, or the 🎯 button anywhere in the app
*reveals* the node: the tree expands along its path, scrolls to it and selects it.

### The selected-node bar

Above the attributes panel you see the selected node's name, class, and NodeId,
plus its **breadcrumb** — the path from the top of the address space, each part
clickable. Buttons:

- **🔍** — open the [value inspector](#the-value-inspector) (variables only).
- **🕘** — read the node's [history](#history) (variables only).
- **🎯** — reveal the node in the tree.
- **⧉ id** — copy the NodeId to the clipboard (paste it into PLC configs, OPC
  Publisher files, tickets…).
- **⧉ link** — copy a shareable URL that opens DruOPC, connects to this server
  and jumps to this node. Send it to a colleague: "look at this node".

## Attributes and references

**Attributes** (right, top) are the node's metadata and current state, decoded into
plain language: node class, display name, description, **DataType** (resolved to a
readable name like `Float` instead of a raw id), **ValueRank** (scalar or array),
**AccessLevel** (`Read | Write` — can you write to it? is history available?),
and for variables the current **Value** with its status code and source/server
timestamps. Every row has a **⧉** copy button. **⟳** re-reads from the server.

**References** (right, bottom) are the node's links to other nodes — its parent
(`Organizes`), its components (`HasComponent`), its type (`HasTypeDefinition`) and
so on. **→** marks forward references, **←** inverse ones. Click any target name to
navigate to it.

## The watch list

The watch list is your live-data workspace — the equivalent of a "watch table" in a
PLC programming tool.

- **Add** variables with the **👁** button in the tree (or anywhere you see it).
- Each row shows the display name and NodeId, the **live value** (cells flash on
  change), a **sparkline** trend of the last 60 numeric samples (hover for
  min/max), the data type, the source timestamp, and the status code
  (green = Good, red = Bad).
- **Update speed**: the `pub` dropdown sets how often the server sends batched
  updates (publishing interval); `sample` sets how fast the server checks each
  value (sampling interval). Defaults (500 ms / 250 ms) suit most cases.
- Row buttons: **✎** write · **🔍** inspect full value · **🕘** history ·
  **⧉** copy value · **🎯** show in tree · **–** remove.
- **⭳ csv** exports the current list snapshot; **🗑** clears the list.

### Writing values

Click **✎** on a writable row (the pencil only appears when the server grants
write access), type the new value, press **Enter** (or **Esc** to cancel).

- Numbers, booleans (`true`/`false`), strings and timestamps are parsed according
  to the node's data type.
- **Arrays**: enter comma-separated values — `1, 2, 3`.
- A rejected write shows the server's reason as a toast — for example
  `BadUserAccessDenied` (log in with more rights), `BadTypeMismatch` (the text
  could not be converted), or `BadNotWritable`.

Writes go straight to the live server — treat them with the same respect as
forcing a tag from an HMI.

## Events

OPC UA servers raise **events** — notifications with a source, severity, time and
message (alarms are a special kind of event).

1. In the tree, hover over an event-capable object — the **Server** object at the
   top catches everything server-wide — and click **⚡**.
2. The **Events** tab (center, bottom) lists events as they arrive, newest first,
   severity color-coded.
3. Filter server-side with the header dropdowns: **sev** (minimum severity) and
   **type** (all / conditions only / system events only).
4. **⭳ csv** exports the log, **🗑** clears it, **⏹** stops the subscription.

Only one event notifier is monitored at a time; clicking ⚡ elsewhere moves the
subscription.

## Alarms & Conditions

The **Alarms & Conditions** tab (center, bottom) is a live alarm summary like the
one in your HMI, built on OPC UA Alarms & Conditions (companion spec Part 9).

- Click **Subscribe**. DruOPC subscribes to condition events on the Server object
  and asks the server to *replay* every currently retained condition
  (a `ConditionRefresh`), so the list is complete from the first second.
- Each row shows the state dot (**red ● = active**, grey = inactive, **! =
  unacknowledged**), severity, source, condition name, type, message and time.
- **✔ ack** acknowledges a condition. The comment sent with it is editable in the
  header field (default: "Acknowledged via DruOPC").
- **⟳ refresh** re-requests the retained conditions; **⏹** unsubscribes.

Rows disappear when the server clears the condition (its *Retain* flag goes
false). If an acknowledge fails with `BadEventIdUnknown`, the condition changed
state in the same instant — the row refreshes and clicking ack again works.

## History

If a server historizes a variable (its AccessLevel includes `HistoryRead`), the
**🕘** button opens the history dialog:

1. Pick a time range — times are entered and displayed in *your browser's*
   timezone — and a maximum point count.
2. **Read** fetches the raw history: a chart (numeric values), a table of every
   point with status codes, and **⭳ csv** for export.

Servers without history for that node answer
`BadHistoryOperationUnsupported` — the dialog says so plainly. The bundled
simulator does not historize, so try this against a real historian-backed server.

## Methods

Methods are commands a server exposes ("reset counter", "switch heater on").

1. Click **▶** on a method node.
2. The dialog shows each input argument with its name, data type and description.
   Fill them in (comma-separated for arrays) and click **Call**.
3. Output arguments and the result appear in the dialog. Errors come back as
   OPC UA status codes — `BadUserAccessDenied` again means your login lacks
   permission.

## The value inspector

The watch list truncates long values to keep rows readable. The **🔍** button
opens the full value:

- **Structures / UDTs** (a motor's parameter block, a recipe record…) render as an
  expandable JSON tree — every field readable, nested structures included.
- **Long strings and byte arrays** appear in full (byte arrays as hex).
- **Large arrays** show element by element.
- **⟳** re-reads; **⧉** copies the raw text.

This is where DruOPC shows you what is *inside* an `ExtensionObject` that other
tools display as an opaque blob.

## Session details (status bar)

The bar along the bottom shows the endpoint URL, negotiated security mode and
policy, your identity, and the server certificate thumbprint. Two entries are
clickable:

- **N namespaces** — the server's namespace table: which index (`ns=…`) maps to
  which URI. Useful when NodeIds from documentation use a different index than the
  live server.
- **N subscriptions** — every subscription in your session with its server id,
  actual (revised) publishing interval and monitored-item count. Handy to confirm
  what load you are putting on the server.

## Sharing and multiple servers

- **Deep links**: `http://<druopc-host>:5000/?endpoint=opc.tcp://…&node=ns=3;s=…`
  opens DruOPC, connects and reveals the node. The **⧉ link** button builds these
  for you. Anyone with network reach to the DruOPC host and the server can follow
  the link — treat links like remote controls, not like screenshots.
- **Multiple servers**: every browser tab is its own independent session. **⧉ new
  window** opens another tab pre-filled with the current server; change the URL to
  compare two servers side by side. Certificate trust decisions are per-tab unless
  you chose **Trust permanently**.

## Troubleshooting

| Symptom | Likely cause and fix |
|---|---|
| `Connect failed: BadNotConnected` / `No such host` / timeout | Wrong URL, server not running, or a firewall blocks the port. Verify the endpoint URL, ping the host, check the port is open (`Test-NetConnection <ip> -Port 4840` on Windows). |
| Trust dialog appears again after "Trust once" | Expected — *once* means this session. Use **Trust permanently**. |
| Certificate trusted, connection still fails with a security error | The *server* has not trusted DruOPC's certificate yet. Approve the "DruOPC" client certificate in the server's certificate manager (often a `pki/rejected` folder or a web UI), then reconnect. |
| `BadUserAccessDenied` on write or method call | Your identity lacks rights. Reconnect with a username/password or certificate that has them. |
| `BadNodeIdUnknown` when jumping to a NodeId | The node does not exist on *this* server, or the namespace index differs. Check the namespace table (status bar) and adjust `ns=`. |
| `BadTypeMismatch` on write | The text could not be converted to the node's data type. Check the DataType attribute; use `true`/`false` for booleans, plain numbers for numerics, comma-separated values for arrays. |
| Watch values never update | The node may simply not be changing. Check the status column; try a smaller sampling interval; confirm the server allows subscriptions (some cap them). |
| `BadHistoryOperationUnsupported` in the history dialog | The server does not historize this node. Nothing is wrong with the connection. |
| Acknowledge fails with `BadEventIdUnknown` | The condition changed state between display and click. The row updates within a second — acknowledge again. |
| Simulator: `Failed to establish tcp listener` / port in use | Another program (or a second simulator) already uses port 50000. Stop it, or change `OpcPlc:OpcUa:ServerPort` in `src/appsettings.json`. |
| Simulator: startup error about the web server / port 5080 | Something else owns port 5080. Change `OpcPlc:WebServerPort` in `src/appsettings.json` (it only serves the optional `pn.json` file). |
| Works on `localhost`, but a colleague's PC cannot reach the simulator or DruOPC | Windows Firewall blocked the ports when you dismissed its first-run dialog. Allow the apps in *Windows Security → Firewall → Allow an app*, or re-run and click **Allow access**. |
| DruOPC page does not load | The DruOPC host process is not running, or another program owns port 5000. Restart with `dotnet run --project browser`; set `ASPNETCORE_URLS=http://localhost:<other-port>` to move it. |
| Clipboard buttons do nothing | Browsers restrict clipboard access on non-HTTPS pages served from other machines. Use DruOPC on `localhost`, or serve it over HTTPS. |

## Glossary

| Term | Meaning |
|---|---|
| **OPC UA** | Open Platform Communications *Unified Architecture* — the vendor-neutral industrial data protocol. Successor to classic OPC/OPC DA. |
| **Server / client** | The server exposes data (PLC, gateway, historian); the client consumes it (DruOPC, HMI, SCADA, MES). |
| **Endpoint** | A server's connectable address: `opc.tcp://host:port`, plus a security configuration. |
| **Address space** | Everything a server exposes, organized as a graph of nodes. What you browse in the left tree. |
| **Node** | One item in the address space: a folder, a value, a method, a type. The OPC UA word for "tag" (and more). |
| **NodeId** | A node's unique address: namespace index + identifier, e.g. `ns=3;s=FastUInt1`. |
| **Namespace** | A naming scope that keeps vendors' identifiers from colliding. The `ns=3` index maps to a URI in the server's namespace table. |
| **Attribute** | A property every node carries: DisplayName, DataType, Value, AccessLevel… |
| **Reference** | A typed link between nodes ("organizes", "has component", "has type definition"). |
| **Subscription** | A standing request: "server, push me changes". Powers the watch list, events and alarms. |
| **Publishing / sampling interval** | How often the server sends update batches / how often it checks each value for change. |
| **Monitored item** | One entry inside a subscription — a watched value or event source. |
| **Method** | A callable command on the server, with typed input/output arguments. |
| **Event / Condition / Alarm** | An event is a notification. A condition is an event with state (active, acknowledged). An alarm is a condition tied to something abnormal. |
| **ConditionRefresh** | Asking the server to replay all currently retained conditions so a client can rebuild its alarm list. |
| **Acknowledge** | Confirming an alarm has been seen — same meaning as in any HMI. |
| **Security policy / mode** | The crypto suite, and whether messages are signed and/or encrypted (`None`, `Sign`, `SignAndEncrypt`). |
| **Application certificate** | The X509 certificate a client or server uses to prove its identity to the other side. |
| **User identity** | Who is logged in *within* the connection: anonymous, username/password, or a user certificate. |
| **Status code** | OPC UA's result for every operation — `Good`, or a specific `Bad…`/`Uncertain…` reason. |
| **ExtensionObject / UDT** | A structured value (like a PLC user-defined type). DruOPC decodes these into readable trees. |
| **Historizing** | The server records a variable's past values, readable via HistoryRead. |
