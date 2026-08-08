# Getting started

This guide takes you from nothing to browsing live PLC data in your web browser in
about 15 minutes. No OPC UA experience is needed — new terms are explained as they
come up, and there is a [glossary](user-manual.md#glossary) in the user manual.

You will set up two programs:

| Program | What it is | Where it runs |
|---|---|---|
| **DruOPC Simulator** | A pretend PLC that speaks OPC UA and generates changing values, alarms and events — so you have something realistic to connect to | A console window |
| **DruOPC** | A web-based OPC UA client for browsing and inspecting any OPC UA server | Your web browser |

Already have a real PLC or OPC UA server on your network? You can skip the simulator
and point DruOPC straight at it — see [Connecting to a real PLC](#connecting-to-a-real-plc).

## New to OPC UA? Read this first (2 minutes)

If you know PLCs but not OPC UA, here is the mental model:

- **OPC UA** is the standard way industrial equipment shares data over a network.
  Think of it as the successor to OPC DA / classic OPC, without the Windows DCOM pain.
- An OPC UA **server** runs on (or next to) the equipment and exposes its data.
  A **client** (like DruOPC, or an HMI/SCADA package) connects to the server to
  read, write and subscribe to that data.
- What you call a **tag** in the PLC world is called a **node** in OPC UA. Every node
  has a unique address called a **NodeId** — for example `ns=3;s=FastUInt1`
  (`ns=3` is the namespace, `s=FastUInt1` is the identifier inside it).
- The server's address is an **endpoint URL** that looks like
  `opc.tcp://192.168.1.50:4840` — a hostname or IP plus a port, with the `opc.tcp://`
  prefix instead of `http://`.
- Instead of polling, OPC UA clients usually create a **subscription**: the server
  pushes value changes to the client. That is what powers DruOPC's live watch list.

That is enough to follow everything below.

## Step 1 — Install the .NET SDK

Both programs run on the free, cross-platform .NET SDK (version 10 or later).
Check whether you already have it:

```console
dotnet --version
```

If that prints `10.x` or higher, skip ahead. Otherwise install it:

- **Windows**: open a terminal (PowerShell) and run
  `winget install Microsoft.DotNet.SDK.10`, or download the installer from
  <https://dotnet.microsoft.com/download>.
- **Ubuntu/Debian Linux**: `sudo apt install dotnet-sdk-10.0`
- **macOS**: `brew install dotnet-sdk`, or use the download page above.

Close and reopen your terminal afterwards so `dotnet` is on your PATH.

> **Locked-down work laptop?** The installer needs administrator rights, and
> `winget` may be disabled by IT policy. Either ask IT to install the ".NET SDK",
> or use the no-admin option: on the download page pick **Binaries → x64 (zip)**,
> extract it anywhere you have write access, and use the full path to
> `dotnet.exe` in the commands below.

## Step 2 — Get the code

If you have git:

```console
git clone https://github.com/drusteeby/DruOPC.git
cd DruOPC
```

**No git? Use the ZIP** (nothing wrong with that):

1. On the GitHub page, click the green **Code** button → **Download ZIP**.
2. Right-click the downloaded file → **Extract All**. Watch out: Windows often
   extracts to a *nested* folder — `DruOPC-main\DruOPC-main`.
3. In PowerShell, change into the **inner** folder — the one that directly
   contains `src` and `browser`:

   ```powershell
   cd $env:USERPROFILE\Downloads\DruOPC-main\DruOPC-main
   dir   # you should see: src, browser, docs, tests, ...
   ```

   If `dir` does not show `src` and `browser`, you are one folder too high or
   too low — every command below is run from this folder.

## Step 3 — Start the simulator

From the repository folder:

```console
dotnet run --project src
```

The first run downloads packages and compiles, which takes a minute or two. You know
it is ready when the log shows lines containing:

```text
OPC UA Server started
PLC simulation started, press Ctrl+C to exit ...
```

Your simulated PLC is now listening at **`opc.tcp://localhost:50000`**. Leave this
window open.

> **Windows may pop a firewall dialog** ("Windows Defender Firewall has blocked
> some features…"). For this guide everything runs on your own machine, so it
> works either way — but click **Allow access** if other computers should be able
> to reach the simulator or DruOPC later.

> **Want alarms too?** Stop the server (Ctrl+C) and restart it with the alarm
> simulation switched on:
>
> - Windows (PowerShell): `$env:OpcPlc__Simulation__AddAlarmSimulation="true"; dotnet run --project src`
> - Linux/macOS: `OpcPlc__Simulation__AddAlarmSimulation=true dotnet run --project src`
>
> Or edit `src/appsettings.json` and set `"AddAlarmSimulation": true`. The
> [configuration reference](../README.md#configuration) lists every setting.
>
> Note for PowerShell: the `$env:` variable sticks for that terminal window. To
> turn alarms back off later in the same window, run
> `$env:OpcPlc__Simulation__AddAlarmSimulation=$null` before restarting.

## Step 4 — Start DruOPC

Open a **second** terminal in the same folder:

```console
dotnet run --project browser
```

When it prints `Now listening on: http://localhost:5080`, open
**<http://localhost:5080>** in your web browser (Chrome, Edge or Firefox).

## Step 5 — Connect

1. The address box at the top already contains `opc.tcp://localhost:50000` —
   the simulator you just started.
2. Click **Connect**.
3. A dialog appears: **Untrusted server certificate**. This is normal — OPC UA
   servers identify themselves with a certificate, and DruOPC has never seen this
   one before (the same way your browser warns about a self-signed website). Since
   you started this server yourself, click **Trust permanently**.
4. The status dot turns green (**Connected**), and the address-space tree fills in
   on the left.

## Step 6 — Look around

Try these, in order — together they touch everything a first session needs:

1. **Browse**: in the left tree, expand **Objects → OpcPlc → Telemetry → Fast**.
   Click **FastUInt1**. The right panel shows all of the node's *attributes* —
   its data type, access level, current value, timestamps.
2. **Watch it live**: hover over **FastUInt1** in the tree and click the **👁 (eye)**
   button. The node appears in the center **Watch List** and its value ticks up
   once per second, with a small live trend line.
3. **Search**: type `pallet` into the search box above the tree and press Enter.
   Click a result — the tree expands to the node and selects it.
4. **Write a value**: in the tree, find **Objects → OpcPlc → DemoLine →
   Station20 → St20_Recipe.UnitId** (or search for `UnitId`), watch it with
   **👁**, then click the **✎ (pencil)** in its watch row, type `PART-1234`,
   press Enter. The value changes — you just wrote to a tag over OPC UA.

   (Stick to Station20 for this test: the simulator includes a "tag writer"
   demo that periodically rewrites the Station10 handshake tags to mimic a
   running station. If you write to a Station10 tag and it later changes by
   itself, that is the simulation writing — not your write failing.)
5. **Call a method**: expand **Objects → OpcPlc → Methods**, hover over
   **ResetStepUp** and click **▶**. Click **Call** in the dialog. Methods are how
   OPC UA servers expose commands ("reset counter", "start pump").
6. **Events**: in the tree, hover over the **Server** object (directly under
   Objects) and click **⚡**. The **Events** tab in the center starts listing the
   events the server raises.
7. **Alarms** (if you enabled the alarm simulation in step 3): switch the center
   bottom tab to **Alarms & Conditions** and click **Subscribe**. Active alarms
   appear with red dots; click **✔ ack** to acknowledge one, like you would in an
   HMI alarm summary.

**Done for now?** Press **Ctrl+C** in each terminal window (or simply close the
windows). That stops both programs completely — nothing keeps running in the
background, and starting them again later is the same two `dotnet run` commands.

## Connecting to a real PLC

DruOPC works with any OPC UA server — Siemens S7-1500, Beckhoff TwinCAT,
Rockwell, Kepware, Ignition, and so on.

Browsing, watching and subscribing are **read-only** — nothing on the PLC changes
unless you explicitly write a value (✎) or call a method (▶). Treat those two
actions with the same care as forcing a tag from an HMI on a running line.

1. Find the server's endpoint URL. It is in the device/server configuration and
   looks like `opc.tcp://<ip-address>:<port>`. Common ports: 4840 (default),
   48010, 49320 (Kepware), 62541 (Ignition).
2. Make sure the OPC UA server is *enabled* on the device (on many PLCs it is off
   until you enable it in the engineering tool) and that your PC can reach that
   IP and port (no firewall in the way).
3. Type the URL into DruOPC's address box and click **Endpoints…** to see what
   security the server offers, or just **Connect** to take the best match.
4. If the server requires a login, open the **👤** menu first and enter the
   username/password (or an X509 user certificate) configured on the server.
5. Trust the server's certificate when prompted — and note that most servers also
   need to trust *DruOPC's* certificate before they let it in. If the connection
   fails with a certificate error even after you clicked trust, go to the server's
   own certificate management and mark the "DruOPC" client certificate as trusted,
   then connect again. This two-way trust step trips up everyone once; details in
   the [user manual](user-manual.md#certificates-and-trust).

## Something not working?

The [troubleshooting section](user-manual.md#troubleshooting) in the user manual
covers the common failures — port already in use, `BadUserAccessDenied`,
certificate rejections, firewalls, and more.

## Where to next

- The **[user manual](user-manual.md)** walks through every DruOPC feature:
  history, alarms, event filters, the value inspector, CSV exports, deep links.
- The **[configuration reference](../README.md#configuration)** documents every
  simulator setting (node counts, rates, boilers, alarms, security).
