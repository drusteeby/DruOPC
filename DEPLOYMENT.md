# OPC PLC Server - Deployment Guide for Linux

This guide covers the complete process of configuring custom nodes, publishing the application for Linux, and deploying it as either a standalone binary or systemd service.

## Table of Contents
1. [Configuring Custom Nodes (nodesfile.json)](#configuring-custom-nodes)
2. [Publishing for Linux](#publishing-for-linux)
3. [Running as a Binary](#running-as-a-binary)
4. [Running as a systemd Service](#running-as-a-systemd-service)
5. [Firewall Configuration](#firewall-configuration)
6. [Verification and Testing](#verification-and-testing)

---

## Configuring Custom Nodes

The `nodesfile.json` file allows you to define custom OPC UA nodes that will be created in the server's address space. These nodes are NOT part of the simulation but remain visible and can be modified by OPC UA clients.

### File Structure

```json
{
  "Folder": "RootFolderName",
  "FolderList": [
    {
      "Folder": "SubFolderName",
      "NodeList": [ /* nodes here */ ]
    }
  ],
  "NodeList": [ /* nodes at root level */ ]
}
```

### Node Properties

| Property | Required | Description | Default |
|----------|----------|-------------|---------|
| **NodeId** | ✅ Yes | Unique identifier (string or number) | - |
| **Name** | ⬜ No | Display name of the node | NodeId value |
| **DataType** | ⬜ No | OPC UA data type (Boolean, String, Int32, Float, Double, etc.) | Int32 |
| **ValueRank** | ⬜ No | -1 for Scalar, >=0 for arrays | -1 |
| **AccessLevel** | ⬜ No | CurrentRead, CurrentReadOrWrite | CurrentReadOrWrite |
| **Description** | ⬜ No | Node description | NodeId value |
| **NamespaceIndex** | ⬜ No | Namespace index (typically 3 for custom nodes) | 2 |
| **Value** | ⬜ No | Initial value for the node | Type default |

### Supported Data Types

- **Boolean** - True/False values
- **String** - Text values
- **Int32** - 32-bit signed integers
- **UInt32** - 32-bit unsigned integers
- **Float** - Single-precision floating point
- **Double** - Double-precision floating point
- **DateTime** - Date and time values
- **ByteString** - Binary data

### Example Configuration

```json
{
  "Folder": "DemoLine",
  "FolderList": [
    {
      "Folder": "Station10",
      "NodeList": [
        {
          "NodeId": "St10_Data.status.ReadComplete",
          "Name": "St10_Data.status.ReadComplete",
          "DataType": "Boolean",
          "ValueRank": -1,
          "AccessLevel": "CurrentReadOrWrite",
          "Description": "RF Read Complete status",
          "NamespaceIndex": 3
        },
        {
          "NodeId": "St10_Data.Header.UnitId.Data",
          "Name": "St10_Data.Header.UnitId.Data",
          "DataType": "String",
          "ValueRank": -1,
          "AccessLevel": "CurrentReadOrWrite",
          "Description": "Unit ID data",
          "NamespaceIndex": 3,
          "Value": ""
        },
        {
          "NodeId": "St10_Data.Header.PalletNumber",
          "Name": "St10_Data.Header.PalletNumber",
          "DataType": "Int32",
          "ValueRank": -1,
          "AccessLevel": "CurrentReadOrWrite",
          "Description": "Pallet number",
          "NamespaceIndex": 3
        }
      ]
    }
  ]
}
```

### Editing Guidelines

1. **Maintain Valid JSON**: Use a JSON validator or editor with syntax checking
2. **Unique NodeIds**: Ensure each NodeId is unique within the namespace
3. **Consistent Naming**: Use clear, descriptive names for nodes
4. **NamespaceIndex**: Use index 3 for user-defined nodes (standard practice)
5. **Data Types**: Match data types to your application's requirements
6. **Access Levels**: Use `CurrentRead` for read-only, `CurrentReadOrWrite` for read/write nodes

### Loading the Configuration

The server automatically loads `nodesfile.json` from the application directory. You can specify a different file in `appsettings.json`:

```json
{
  "OpcPlc": {
    "NodesFileName": "nodesfile.json"
  }
}
```

---

## Publishing for Linux

### Publishing 

#### Self-Contained Deployment (Recommended)


Includes the .NET Runtime - no dependencies on target system.

```bash
# Navigate to the project directory
cd src

# Publish for Linux x64
dotnet publish -c Release -r linux-x64 --self-contained true -o ./publish/linux-x64

# Publish for Linux ARM64 (Raspberry Pi, etc.)
dotnet publish -c Release -r linux-arm64 --self-contained true -o ./publish/linux-arm64
```


### Publish Output

After publishing, you'll find these files in the output directory:

```
publish/linux-x64/
├── opcplc                          # Main executable
├── appsettings.json                # Configuration file
├── nodesfile.json                  # Node definitions
├── opcplc.service                  # systemd service file
├── *.dll                           # Dependencies (if not single-file)
├── *.so                            # Native libraries
└── Boilers/                        # Model files
    CompanionSpecs/
    SimpleEvent/
```

**Note:** The `pki/` directory for certificates is created at runtime.

### Transferring to Linux System

#### Method 1: Using SCP

```bash
# Copy entire publish directory
scp -r ./publish/linux-x64/* user@linux-host:/opt/opc-plc/

# Copy single file (if using single-file publish)
scp ./publish/linux-x64-single/opcplc user@linux-host:/opt/opc-plc/
```

#### Method 2: Using RSYNC

```bash
# Sync with compression
rsync -avz ./publish/linux-x64/ user@linux-host:/opt/opc-plc/

# Sync and show progress
rsync -avz --progress ./publish/linux-x64/ user@linux-host:/opt/opc-plc/
```

#### Method 3: Archive and Transfer

```bash
# Create compressed archive
tar -czf opc-plc-linux.tar.gz -C ./publish/linux-x64 .

# Transfer archive
scp opc-plc-linux.tar.gz user@linux-host:/tmp/

# On Linux host: Extract
ssh user@linux-host
sudo mkdir -p /opt/opc-plc
sudo tar -xzf /tmp/opc-plc-linux.tar.gz -C /opt/opc-plc
sudo chmod +x /opt/opc-plc/opcplc
```

---

## Running as a Binary

### Initial Setup

```bash
# SSH into Linux system
ssh user@linux-host

# Navigate to installation directory
cd /opt/opc-plc

# Set execute permissions
sudo chmod +x opcplc

# Verify required files exist
ls -la appsettings.json nodesfile.json opcplc
```

### Configuration

Edit `appsettings.json` to configure the server:

```bash
sudo nano /opt/opc-plc/appsettings.json
```

Key configuration options:

```json
{
  "OpcPlc": {
    "PortNum": 50000,
    "AutoAcceptCerts": true,
    "NodesFileName": "nodesfile.json",
    "UnsecureTransport": false,
    "ShowPublisherConfigJsonIp": true,
    "WebServerPort": 8080,
    "Hostname": "localhost"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

**Important Settings:**

- `PortNum`: OPC UA server port (default: 50000)
- `AutoAcceptCerts`: Accept all client certificates automatically (use only for testing)
- `NodesFileName`: Path to custom nodes configuration
- `UnsecureTransport`: Allow unencrypted connections (not recommended for production)
- `WebServerPort`: Port for web interface serving pn.json

### Running the Application

#### Foreground Execution (for testing)

```bash
# Run directly (self-contained or single-file)
./opcplc

# Or using dotnet (if framework-dependent)
dotnet opcplc.dll

# Run with environment variables
ASPNETCORE_URLS="http://*:8080" ./opcplc

# Run with specific log level
ASPNETCORE_ENVIRONMENT=Development ./opcplc
```

**To stop:** Press `Ctrl+C`

#### Background Execution with nohup

```bash
# Run in background
nohup ./opcplc > /var/log/opc-plc.log 2>&1 &

# Save process ID
echo $! > /var/run/opc-plc.pid

# Check if running
ps aux | grep opcplc
# or
cat /var/run/opc-plc.pid | xargs ps -p

# View logs in real-time
tail -f /var/log/opc-plc.log

# Stop the process
kill $(cat /var/run/opc-plc.pid)
# or
pkill opcplc
```

#### Using Screen

```bash
# Install screen (if not available)
sudo apt-get install screen  # Debian/Ubuntu
sudo yum install screen      # CentOS/RHEL

# Start a screen session
screen -S opc-plc

# Run the application
cd /opt/opc-plc
./opcplc

# Detach from screen: Press Ctrl+A, then D

# List screen sessions
screen -ls

# Reattach to screen
screen -r opc-plc

# Kill screen session
screen -X -S opc-plc quit
```

#### Using Tmux

```bash
# Install tmux (if not available)
sudo apt-get install tmux    # Debian/Ubuntu
sudo yum install tmux        # CentOS/RHEL

# Start a tmux session
tmux new -s opc-plc

# Run the application
cd /opt/opc-plc
./opcplc

# Detach from tmux: Press Ctrl+B, then D

# List tmux sessions
tmux ls

# Reattach to tmux
tmux attach -t opc-plc

# Kill tmux session
tmux kill-session -t opc-plc
```

### Testing the Server

```bash
# Check if the server is listening on OPC UA port
netstat -tulpn | grep 50000
# or
ss -tulpn | grep 50000

# Check web server port
netstat -tulpn | grep 8080

# Test web interface (if enabled)
curl http://localhost:8080/pn.json

# Test from another machine
curl http://<linux-host-ip>:8080/pn.json

# Check process status
ps aux | grep opcplc

# Check resource usage
top -p $(pgrep opcplc)
```

### Stopping the Application

```bash
# If running in foreground: Ctrl+C

# If running in background with PID file
kill $(cat /var/run/opc-plc.pid)

# Find and kill by process name
pkill opcplc

# Force kill if needed
pkill -9 opcplc

# Or find PID and kill
ps aux | grep opcplc
kill <PID>
kill -9 <PID>  # Force kill
```

---

## Running as a systemd Service

Running OPC PLC as a systemd service ensures automatic startup on boot, automatic restart on failure, and provides better process management and logging.

### Service Configuration File

The included `opcplc.service` file defines the service configuration:

```ini
[Unit]
Description=OPC PLC Server
After=network.target

[Service]
Type=simple
WorkingDirectory=/opt/opc-plc
ExecStart=/opt/opc-plc/opcplc
User=root
Restart=on-failure
RestartSec=10
KillSignal=SIGINT

# Security settings
NoNewPrivileges=true
PrivateTmp=true

# Logging
StandardOutput=journal
StandardError=journal
SyslogIdentifier=opcplc

[Install]
WantedBy=multi-user.target
```

### Installation Steps

#### Step 1: Create Service User (Recommended for Security)

```bash
# Create a dedicated user for the service (no login shell)
sudo useradd -r -s /bin/false opcplc

# Set ownership of installation directory
sudo chown -R opcplc:opcplc /opt/opc-plc

# Ensure executable permissions
sudo chmod +x /opt/opc-plc/opcplc

# Verify permissions
ls -la /opt/opc-plc/opcplc
```

#### Step 2: Install the Service File

```bash
# Copy service file to systemd directory
sudo cp /opt/opc-plc/opcplc.service /etc/systemd/system/

# Or download/create directly
sudo nano /etc/systemd/system/opcplc.service
```

#### Step 3: Customize Service Configuration

Edit the service file to match your installation:

```bash
sudo nano /etc/systemd/system/opcplc.service
```

**Recommended configuration with non-root user:**

```ini
[Unit]
Description=OPC PLC Server
After=network.target
Documentation=https://github.com/Azure-Samples/iot-edge-opc-plc

[Service]
Type=simple
WorkingDirectory=/opt/opc-plc
ExecStart=/opt/opc-plc/opcplc
User=opcplc
Group=opcplc

# Restart policy
Restart=on-failure
RestartSec=10
StartLimitBurst=5
StartLimitIntervalSec=300

# Signals
KillSignal=SIGINT
TimeoutStopSec=30

# Security settings
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/opt/opc-plc/pki
ReadWritePaths=/opt/opc-plc/logs

# Resource limits
LimitNOFILE=65536
MemoryMax=512M
CPUQuota=100%

# Logging
StandardOutput=journal
StandardError=journal
SyslogIdentifier=opcplc

# Environment variables (optional)
#Environment="ASPNETCORE_URLS=http://*:8080"
#Environment="ASPNETCORE_ENVIRONMENT=Production"

[Install]
WantedBy=multi-user.target
```

**Configuration Options Explained:**

- **WorkingDirectory**: Must match installation path
- **ExecStart**: Full path to executable
- **User/Group**: Service runs as this user (use `opcplc` or appropriate user, not `root`)
- **Restart**: Restart policy (`no`, `on-success`, `on-failure`, `on-abnormal`, `on-abort`, `always`)
- **RestartSec**: Wait time before restart
- **StartLimitBurst**: Max restarts within StartLimitIntervalSec
- **KillSignal**: Signal to send for graceful shutdown (SIGINT or SIGTERM)
- **TimeoutStopSec**: Max time to wait for graceful shutdown
- **NoNewPrivileges**: Prevent privilege escalation
- **PrivateTmp**: Use private /tmp directory
- **ProtectSystem**: Protect system directories
- **ProtectHome**: Protect home directories
- **ReadWritePaths**: Allow write access to specific paths
- **LimitNOFILE**: Max open file descriptors
- **MemoryMax**: Maximum memory usage
- **CPUQuota**: CPU time limit (100% = 1 core)

#### Step 4: Reload systemd and Enable Service

```bash
# Reload systemd to recognize new/changed service
sudo systemctl daemon-reload

# Enable service to start at boot
sudo systemctl enable opcplc

# Start the service
sudo systemctl start opcplc

# Check service status
sudo systemctl status opcplc
```

Expected output:
```
● opcplc.service - OPC PLC Server
     Loaded: loaded (/etc/systemd/system/opcplc.service; enabled; vendor preset: enabled)
     Active: active (running) since Thu 2024-01-09 10:00:00 UTC; 5s ago
   Main PID: 1234 (opcplc)
      Tasks: 15 (limit: 4915)
     Memory: 45.2M
        CPU: 1.234s
     CGroup: /system.slice/opcplc.service
             └─1234 /opt/opc-plc/opcplc
```

### Service Management Commands

```bash
# Start the service
sudo systemctl start opcplc

# Stop the service
sudo systemctl stop opcplc

# Restart the service
sudo systemctl restart opcplc

# Reload configuration without restart (if supported)
sudo systemctl reload opcplc

# Check service status
sudo systemctl status opcplc

# Check if service is running
sudo systemctl is-active opcplc

# Check if service is enabled
sudo systemctl is-enabled opcplc

# Enable service (auto-start on boot)
sudo systemctl enable opcplc

# Disable service (no auto-start)
sudo systemctl disable opcplc

# Enable and start in one command
sudo systemctl enable --now opcplc

# Disable and stop in one command
sudo systemctl disable --now opcplc
```

### Viewing Logs

systemd captures all service output in the journal:

```bash
# View all logs for the service
sudo journalctl -u opcplc

# Follow logs in real-time
sudo journalctl -u opcplc -f

# View logs with timestamps
sudo journalctl -u opcplc -o short-precise

# View logs since boot
sudo journalctl -u opcplc -b

# View logs for specific time range
sudo journalctl -u opcplc --since "2024-01-09 10:00:00"
sudo journalctl -u opcplc --since "1 hour ago"
sudo journalctl -u opcplc --since today
sudo journalctl -u opcplc --since yesterday

# View last N lines
sudo journalctl -u opcplc -n 100

# View logs in reverse order (newest first)
sudo journalctl -u opcplc -r

# View logs with priority level
sudo journalctl -u opcplc -p err       # Errors only
sudo journalctl -u opcplc -p warning   # Warnings and above

# Export logs to file
sudo journalctl -u opcplc > opcplc.log

# View logs with full output (no truncation)
sudo journalctl -u opcplc --no-pager

# Clear old logs (optional)
sudo journalctl --vacuum-time=7d       # Keep last 7 days
sudo journalctl --vacuum-size=100M     # Keep max 100MB
```

### Troubleshooting

#### Service Won't Start

```bash
# Check detailed status
sudo systemctl status opcplc -l --no-pager

# View recent error logs
sudo journalctl -u opcplc -n 50 --no-pager

# Check for syntax errors in service file
sudo systemd-analyze verify opcplc.service

# Test executable manually as service user
sudo -u opcplc /opt/opc-plc/opcplc

# Check if port is already in use
sudo netstat -tulpn | grep 50000
sudo ss -tulpn | grep 50000

# Check SELinux denials (if applicable)
sudo ausearch -m avc -ts recent
```

#### Permission Errors

```bash
# Check ownership
ls -la /opt/opc-plc/

# Fix ownership
sudo chown -R opcplc:opcplc /opt/opc-plc

# Fix permissions
sudo chmod +x /opt/opc-plc/opcplc
sudo chmod 644 /opt/opc-plc/appsettings.json
sudo chmod 644 /opt/opc-plc/nodesfile.json

# Create necessary directories
sudo mkdir -p /opt/opc-plc/pki/own
sudo mkdir -p /opt/opc-plc/pki/trusted
sudo mkdir -p /opt/opc-plc/pki/rejected
sudo mkdir -p /opt/opc-plc/logs
sudo chown -R opcplc:opcplc /opt/opc-plc/pki
sudo chown -R opcplc:opcplc /opt/opc-plc/logs
```

#### Port Already in Use

```bash
# Find what's using the port
sudo netstat -tulpn | grep 50000
sudo ss -tulpn | grep 50000
sudo lsof -i :50000

# Kill conflicting process
sudo kill <PID>
sudo systemctl stop <conflicting-service>

# Or change port in appsettings.json
sudo nano /opt/opc-plc/appsettings.json
# Change "PortNum": 50000 to another port

# Restart service after configuration change
sudo systemctl restart opcplc
```

#### Certificate Issues

```bash
# Remove existing certificates
sudo rm -rf /opt/opc-plc/pki

# Recreate directory structure
sudo mkdir -p /opt/opc-plc/pki/{own,trusted,rejected,issuer}
sudo chown -R opcplc:opcplc /opt/opc-plc/pki

# Restart service to regenerate certificates
sudo systemctl restart opcplc

# Monitor certificate generation
sudo journalctl -u opcplc -f
```

#### Service Crashes or Restarts Frequently

```bash
# Check restart history
sudo systemctl status opcplc

# View crash logs
sudo journalctl -u opcplc -p err

# Check system resources
free -h
df -h
top

# Increase memory limit if needed
sudo nano /etc/systemd/system/opcplc.service
# Add or modify: MemoryMax=1G

# Reload and restart
sudo systemctl daemon-reload
sudo systemctl restart opcplc

# Monitor resource usage
watch -n 1 'systemctl status opcplc | grep -E "(Memory|CPU)"'
```

#### Configuration Changes Not Taking Effect

```bash
# After editing appsettings.json or nodesfile.json
sudo systemctl restart opcplc

# After editing service file
sudo systemctl daemon-reload
sudo systemctl restart opcplc

# Verify configuration is loaded
sudo journalctl -u opcplc -n 50 | grep -i "configuration"
```

### Service Configuration Best Practices

1. **Run as Non-Root User**
```ini
   # Never run as root unless absolutely necessary
   User=opcplc
   Group=opcplc
```

2. **Set Resource Limits**
```ini
   LimitNOFILE=65536
   MemoryMax=512M
   CPUQuota=100%
   TasksMax=256
```

3. **Enable Security Features**
```ini
   NoNewPrivileges=true
   PrivateTmp=true
   ProtectSystem=strict
   ProtectHome=true
   ReadOnlyPaths=/
   ReadWritePaths=/opt/opc-plc/pki
   ReadWritePaths=/opt/opc-plc/logs
```

4. **Configure Restart Policy**
```ini
   Restart=on-failure
   RestartSec=10
   StartLimitBurst=5
   StartLimitIntervalSec=300
```

5. **Set Appropriate Timeouts**
```ini
   TimeoutStartSec=60
   TimeoutStopSec=30
```

6. **Use Systemd Journal for Logging**
```ini
   StandardOutput=journal
   StandardError=journal
   SyslogIdentifier=opcplc
```

### Monitoring the Service

```bash
# Monitor service status continuously
watch -n 2 'systemctl status opcplc'

# Monitor logs in real-time
sudo journalctl -u opcplc -f --since "5 minutes ago"

# Check system resource usage
systemctl show opcplc --property=MainPID
top -p $(systemctl show opcplc --property=MainPID --value)

# View performance metrics
systemctl show opcplc | grep -E "(CPU|Memory|Tasks)"

# Check open files
sudo lsof -p $(systemctl show opcplc --property=MainPID --value)

# Check network connections
sudo netstat -anp | grep $(systemctl show opcplc --property=MainPID --value)
```

### Updating the Application

```bash
# Stop the service
sudo systemctl stop opcplc

# Backup current version
sudo cp -r /opt/opc-plc /opt/opc-plc.backup

# Transfer and extract new version
# (use methods from Publishing section)

# Verify permissions
sudo chown -R opcplc:opcplc /opt/opc-plc
sudo chmod +x /opt/opc-plc/opcplc

# Start the service
sudo systemctl start opcplc

# Check status
sudo systemctl status opcplc

# If problems occur, rollback
# sudo systemctl stop opcplc
# sudo rm -rf /opt/opc-plc
# sudo mv /opt/opc-plc.backup /opt/opc-plc
# sudo systemctl start opcplc
```

---

## Firewall Configuration

If the OPC PLC server is not accessible from other machines, you may need to configure the firewall.

### UFW (Ubuntu/Debian)

```bash
# Check UFW status
sudo ufw status

# Allow OPC UA port
sudo ufw allow 50000/tcp comment 'OPC UA Server'

# Allow web server port (if enabled)
sudo ufw allow 8080/tcp comment 'OPC PLC Web Interface'

# Enable UFW (if disabled)
sudo ufw enable

# Verify rules
sudo ufw status numbered

# Remove a rule (by number)
sudo ufw delete <number>
```

### firewalld (CentOS/RHEL/Fedora)

```bash
# Check firewalld status
sudo firewall-cmd --state

# Allow OPC UA port permanently
sudo firewall-cmd --permanent --add-port=50000/tcp

# Allow web server port permanently
sudo firewall-cmd --permanent --add-port=8080/tcp

# Reload firewall to apply changes
sudo firewall-cmd --reload

# Verify rules
sudo firewall-cmd --list-all

# Or create a service definition
sudo nano /etc/firewalld/services/opcplc.xml
```

```xml
<?xml version="1.0" encoding="utf-8"?>
<service>
  <short>OPC PLC</short>
  <description>OPC UA PLC Server</description>
  <port protocol="tcp" port="50000"/>
  <port protocol="tcp" port="8080"/>
</service>
```

```bash
# Add the service
sudo firewall-cmd --permanent --add-service=opcplc
sudo firewall-cmd --reload
```

### iptables (Legacy)

```bash
# Allow OPC UA port
sudo iptables -A INPUT -p tcp --dport 50000 -j ACCEPT

# Allow web server port
sudo iptables -A INPUT -p tcp --dport 8080 -j ACCEPT

# Save rules (Ubuntu/Debian)
sudo apt-get install iptables-persistent
sudo netfilter-persistent save

# Save rules (CentOS/RHEL)
sudo service iptables save

# View rules
sudo iptables -L -n -v

# Delete a rule
sudo iptables -D INPUT -p tcp --dport 50000 -j ACCEPT
```

### Testing Firewall Configuration

```bash
# From the server itself
netstat -tulpn | grep -E '(50000|8080)'

# From another machine
telnet <server-ip> 50000
nc -zv <server-ip> 50000

# Test web interface
curl http://<server-ip>:8080/pn.json
```

---

## Verification and Testing

### Verify Server is Running

```bash
# Check if process is running
ps aux | grep opcplc
pgrep -fl opcplc

# Check listening ports
sudo netstat -tulpn | grep opcplc
sudo ss -tulpn | grep opcplc

# Check specific ports
sudo netstat -tulpn | grep -E '(50000|8080)'

# Check service status (if running as service)
sudo systemctl status opcplc
sudo systemctl is-active opcplc
```

### Test OPC UA Endpoint

#### Using an OPC UA Client (UaExpert)

1. **Download UaExpert**: https://www.unified-automation.com/products/development-tools/uaexpert.html

2. **Connection Settings**:
   - **Endpoint URL**: `opc.tcp://<linux-host-ip>:50000`
   - **Security Mode**: `Sign & Encrypt` (recommended) or `None` (if UnsecureTransport enabled)
   - **Security Policy**: `Basic256Sha256` (recommended) or `None`
   - **Authentication**: `Anonymous` (or configured username/password)

3. **Browse Nodes**:
   - Navigate to: `Root → Objects → <YourRootFolder>`
   - Verify your custom nodes appear
   - Try reading and writing values

#### Using opcua-client CLI (Node.js)

```bash
# Install node-opcua-client
npm install -g opcua-commander

# Connect to server
opcua-commander -e opc.tcp://<linux-host-ip>:50000
```

#### Using Python (opcua library)

```python
from opcua import Client

client = Client("opc.tcp://<linux-host-ip>:50000")
try:
    client.connect()
    print("Connected to OPC UA Server")
    
    # Browse root node
    root = client.get_root_node()
    print("Root node:", root)
    
    # Get Objects node
    objects = client.get_objects_node()
    print("Objects node:", objects)
    
    # List children
    children = objects.get_children()
    for child in children:
        print(f"Child: {child.get_browse_name()}")
    
finally:
    client.disconnect()
```

### Test Web Interface

```bash
# Test locally on server
curl http://localhost:8080/pn.json

# Test from another machine
curl http://<linux-host-ip>:8080/pn.json

# Pretty print JSON
curl http://<linux-host-ip>:8080/pn.json | jq .

# Test in browser
# Navigate to: http://<linux-host-ip>:8080/pn.json
```

### Verify Custom Nodes

```bash
# Check logs for node creation
sudo journalctl -u opcplc | grep -i "node"
sudo journalctl -u opcplc | grep -i "nodesfile"

# Expected output should show nodes being created:
# "Loading user defined nodes from nodesfile.json"
# "Created node: St10_Data.status.ReadComplete"
```

Using OPC UA client:
1. Browse to `Objects` → `<YourRootFolder>` (e.g., "DemoLine")
2. Expand folders (e.g., "Station10", "Station20")
3. Verify all nodes from `nodesfile.json` are visible
4. Test reading node values
5. Test writing to nodes (if AccessLevel is CurrentReadOrWrite)

### Performance Testing

```bash
# Monitor CPU and memory
top -p $(pgrep opcplc)
htop -p $(pgrep opcplc)

# Monitor system resources
watch -n 1 'systemctl status opcplc | grep -E "(Memory|CPU|Tasks)"'

# Check open connections
sudo netstat -anp | grep opcplc | wc -l

# Monitor logs for errors
sudo journalctl -u opcplc -f -p err

# Load testing (using OPC UA client with multiple connections)
# - Connect multiple clients simultaneously
# - Subscribe to multiple nodes
# - Perform rapid read/write operations
```

### Network Testing

```bash
# Test connectivity from client machine
ping <linux-host-ip>
telnet <linux-host-ip> 50000
nc -zv <linux-host-ip> 50000

# Test DNS resolution
nslookup <linux-hostname>
dig <linux-hostname>

# Trace route
traceroute <linux-host-ip>

# Check firewall rules
sudo iptables -L -n -v | grep 50000
sudo ufw status | grep 50000
sudo firewall-cmd --list-all | grep 50000
```

---

## Common Deployment Scenarios

### Scenario 1: Development/Testing Environment

**Configuration:**
- Self-contained deployment
- Run as binary in screen/tmux
- Auto-accept certificates enabled
- Unsecure transport allowed

```bash
# Publish
dotnet publish -c Release -r linux-x64 --self-contained true -o ./publish/linux-x64

# Transfer
scp -r ./publish/linux-x64/* user@dev-server:/opt/opc-plc/

# Configure
nano /opt/opc-plc/appsettings.json
# Set: AutoAcceptCerts: true, UnsecureTransport: true

# Run
screen -S opc-plc
cd /opt/opc-plc
./opcplc
```

### Scenario 2: Production Environment

**Configuration:**
- Self-contained single-file deployment
- Run as systemd service with non-root user
- Security hardening enabled
- Secure transport only
- Resource limits configured

```bash
# Publish
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o ./publish/linux-x64-single

# Transfer
scp ./publish/linux-x64-single/* user@prod-server:/tmp/
ssh user@prod-server
sudo mkdir -p /opt/opc-plc
sudo mv /tmp/opcplc /opt/opc-plc/
sudo cp /tmp/{appsettings.json,nodesfile.json,opcplc.service} /opt/opc-plc/

# Setup service user
sudo useradd -r -s /bin/false opcplc
sudo chown -R opcplc:opcplc /opt/opc-plc
sudo chmod +x /opt/opc-plc/opcplc

# Configure
sudo nano /opt/opc-plc/appsettings.json
# Set: AutoAcceptCerts: false, UnsecureTransport: false

# Install and start service
sudo cp /opt/opc-plc/opcplc.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now opcplc
sudo systemctl status opcplc
```

### Scenario 3: Edge Device (Raspberry Pi, Industrial Gateway)

**Configuration:**
- ARM64 build
- Minimal resource footprint
- Framework-dependent (if .NET runtime pre-installed)
- Service with resource limits

```bash
# Publish for ARM64
dotnet publish -c Release -r linux-arm64 --self-contained true -o ./publish/linux-arm64

# Transfer (may take longer on slow networks)
rsync -avz --progress ./publish/linux-arm64/ pi@raspberry:/opt/opc-plc/

# Configure with resource limits
sudo nano /etc/systemd/system/opcplc.service
# Add: MemoryMax=256M, CPUQuota=50%

# Install and monitor
sudo systemctl enable --now opcplc
watch -n 2 'systemctl status opcplc'
```

---

## Summary

### Deployment Methods Comparison

| Method | Use Case | Pros | Cons | Auto-Restart | Boot Startup |
|--------|----------|------|------|--------------|--------------|
| **Binary (Foreground)** | Development, Testing | Easy debug, immediate output | No auto-restart, manual start | ❌ No | ❌ No |
| **Binary (Background)** | Temporary deployment | Quick setup, simple | No auto-restart, manual management | ❌ No | ❌ No |
| **Screen/Tmux** | Development, Remote work | Persistent session, reattachable | Manual start, not production-ready | ❌ No | ❌ No |
| **systemd Service** | Production | Auto-start, monitoring, logging, restart | More complex setup | ✅ Yes | ✅ Yes |

### Quick Reference Commands

```bash
# ============================================================
# PUBLISHING
# ============================================================

# Standard self-contained
dotnet publish -c Release -r linux-x64 --self-contained true -o ./publish/linux-x64

# Single-file
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o ./publish/linux-x64-single

# ARM64 (Raspberry Pi)
dotnet publish -c Release -r linux-arm64 --self-contained true -o ./publish/linux-arm64

# ============================================================
# TRANSFER
# ============================================================

# SCP
scp -r ./publish/linux-x64/* user@host:/opt/opc-plc/

# RSYNC
rsync -avz ./publish/linux-x64/ user@host:/opt/opc-plc/

# Archive
tar -czf opc-plc.tar.gz -C ./publish/linux-x64 .
scp opc-plc.tar.gz user@host:/tmp/

# ============================================================
# BINARY EXECUTION
# ============================================================

# Run foreground
cd /opt/opc-plc && ./opcplc

# Run background
nohup ./opcplc > /var/log/opc-plc.log 2>&1 &

# Run in screen
screen -S opc-plc
./opcplc
# Ctrl+A, D to detach

# Stop
pkill opcplc

# ============================================================
# SYSTEMD SERVICE
# ============================================================

# Install service
sudo cp opcplc.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable opcplc
sudo systemctl start opcplc

# Manage service
sudo systemctl start opcplc      # Start
sudo systemctl stop opcplc       # Stop
sudo systemctl restart opcplc    # Restart
sudo systemctl status opcplc     # Status

# View logs
sudo journalctl -u opcplc -f     # Follow logs
sudo journalctl -u opcplc -n 100 # Last 100 lines

# ============================================================
# TESTING
# ============================================================

# Check running
ps aux | grep opcplc
sudo netstat -tulpn | grep 50000

# Test endpoint
telnet <ip> 50000
curl http://<ip>:8080/pn.json

# View logs
sudo journalctl -u opcplc -f
tail -f /var/log/opc-plc.log
```

### Configuration Files Quick Reference

**appsettings.json** - Main configuration:
```json
{
  "OpcPlc": {
    "PortNum": 50000,
    "AutoAcceptCerts": true,
    "NodesFileName": "nodesfile.json",
    "UnsecureTransport": false,
    "WebServerPort": 8080
  }
}
```

**nodesfile.json** - Custom nodes:
```json
{
  "Folder": "MyFolder",
  "NodeList": [
    {
      "NodeId": "MyNode",
      "DataType": "String",
      "AccessLevel": "CurrentReadOrWrite"
    }
  ]
}
```

**opcplc.service** - systemd service:
```ini
[Unit]
Description=OPC PLC Server
After=network.target

[Service]
WorkingDirectory=/opt/opc-plc
ExecStart=/opt/opc-plc/opcplc
User=opcplc
Group=opcplc

# Restart policy
Restart=on-failure
RestartSec=10
StartLimitBurst=5
StartLimitIntervalSec=300

# Signals
KillSignal=SIGINT
TimeoutStopSec=30

# Security settings
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/opt/opc-plc/pki
ReadWritePaths=/opt/opc-plc/logs

# Resource limits
LimitNOFILE=65536
MemoryMax=512M
CPUQuota=100%

# Logging
StandardOutput=journal
StandardError=journal
SyslogIdentifier=opcplc

# Environment variables (optional)
#Environment="ASPNETCORE_URLS=http://*:8080"
#Environment="ASPNETCORE_ENVIRONMENT=Production"

[Install]
WantedBy=multi-user.target
```

---

## Additional Resources

- **OPC Foundation**: https://opcfoundation.org/
- **OPC UA .NET Standard Stack**: https://github.com/OPCFoundation/UA-.NETStandard
- **.NET Publishing Documentation**: https://learn.microsoft.com/en-us/dotnet/core/deploying/
- **systemd Service Documentation**: https://www.freedesktop.org/software/systemd/man/systemd.service.html
- **OPC PLC GitHub Repository**: https://github.com/Azure-Samples/iot-edge-opc-plc

---

## Troubleshooting Checklist

- [ ] Executable has execute permissions (`chmod +x opcplc`)
- [ ] Service user has ownership of installation directory
- [ ] Firewall allows ports 50000 (OPC UA) and 8080 (web interface)
- [ ] `appsettings.json` and `nodesfile.json` are present
- [ ] No other process is using port 50000
- [ ] Service file is in `/etc/systemd/system/`
- [ ] Service is enabled (`systemctl is-enabled opcplc`)
- [ ] Logs show no errors (`journalctl -u opcplc`)
- [ ] Certificates are generated in `pki/` directory
- [ ] Network connectivity between client and server

---

For more information about OPC PLC features, simulated nodes, and advanced configuration options, see the main [README.md](README.md).
