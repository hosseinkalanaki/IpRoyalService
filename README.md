# IPRoyal SOCKS5 Enforcement Service

This Windows Service routes ordinary system-wide IPv4 and IPv6 traffic through a SOCKS5 proxy. Browsers and other applications do not need individual proxy settings.

> **Safety warning:** first test proxy failure, recovery, DNS, reboot, and both existing and new RDP connections on a disposable Windows machine with console access.

## Install on Windows

### 1. Download the installer

Open the project's GitHub **Releases** page and download:

```text
IpRoyalService-vX.Y.Z-win-x64-Setup.exe
```

This is the primary end-user download for 64-bit Intel/AMD Windows. Do not download GitHub's automatically generated “Source code” archives. The installer contains the service, private .NET runtime, packet engine, configuration wizard, and service-management shortcut; Visual Studio, the .NET SDK, and programming knowledge are not required.

An adjacent `.sha256` file is provided for optional download verification.

### 2. Run the installer

1. Double-click the downloaded installer.
2. Approve the Windows administrator prompt.
3. Keep the default installation folder unless there is a specific reason to change it.
4. Enter the proxy settings described below.
5. Select **Install**.

After a successful clean installation, the installer registers the `IpRoyalProxyEnforcement` Windows Service for automatic startup and starts it immediately. Installation failures are reported and a newly created, broken service registration is removed.

## Proxy settings requested by the installer

| Setting | Normal value |
|---|---|
| Proxy type | `socks` |
| Proxy version | `5` |
| Proxy server | Provider hostname or IP address; required |
| Proxy server port | Provider port from 1 to 65535; default `1080` |
| Reserved/local port | Unused local port; default `11200` |
| Username | Optional; leave empty for no authentication |
| Password | Optional; leave empty for no authentication |

Username and password must either both be filled or both be empty. The reserved/local port must differ from the proxy server port. The password is held only long enough to create the configuration and is not passed through installer command-line arguments or deliberately written to installer/service logs.

## Configuration after installation

The service always reads:

```text
C:\Program Files\IpRoyalService\config.json
```

If a different installation directory was selected, `config.json` is beside `IpRoyalService.exe` in that directory. Its format is:

```json
{
  "type": "socks",
  "version": "5",
  "server": "proxy.example.com",
  "server_port": 1080,
  "reserve_port": 11200,
  "username": "",
  "password": ""
}
```

The installer restricts this file to Administrators and SYSTEM. To change the proxy later:

1. Open **Start → IPRoyal SOCKS5 Enforcement → Edit Proxy Configuration**.
2. Approve administrator access if Windows requests it.
3. Save the edited file.
4. Restart the service using **Manage Service**.

The service does not live-reload this file; changes take effect after restart.

## Start, stop, restart, status, and logs

Open **Start → IPRoyal SOCKS5 Enforcement → Manage Service**. The menu can start, stop, restart, display status, edit configuration, or open the service log. Windows requests administrator permission for service changes.

Administrators can alternatively use PowerShell:

```powershell
Start-Service IpRoyalProxyEnforcement
Stop-Service IpRoyalProxyEnforcement
Restart-Service IpRoyalProxyEnforcement
Get-Service IpRoyalProxyEnforcement
```

Operational logs are stored at:

```text
C:\ProgramData\IpRoyalService\service.log
```

The password is never intentionally logged, and packet-engine output is redacted as an additional precaution.

## Upgrade or reinstall

Download and run the newer installer. It safely stops the existing service before replacing application-owned binaries and starts it again afterward.

The existing installed `config.json` is preserved by default. The installer displays an explicit **Replace my existing config.json** option during an upgrade; leave it unselected to retain the current proxy settings. If selected, the installer asks for new values and replaces the file. A failed replacement attempts to restore the previous configuration.

## Uninstall

Use either:

- **Windows Settings → Apps → Installed apps → IPRoyal SOCKS5 Enforcement → Uninstall**, or
- **Control Panel → Programs and Features**.

The standard uninstaller stops and unregisters only `IpRoyalProxyEnforcement`, removes installed application files and application-owned runtime logs, and leaves `config.json` in the installation directory for safe reuse. Delete that remaining file manually only when its stored credentials are no longer needed.

## Networking behavior

When the proxy is healthy, ordinary IPv4/IPv6 application traffic and DNS use the enforced proxy path. Local loopback and private-network communication remain direct.

When the proxy is unavailable or authentication fails, captured Internet traffic stays blocked instead of silently falling back to a direct connection. The service checks recovery every 10 seconds and restores proxied traffic automatically.

TCP/UDP RDP traffic on port 3389 and private/local destinations bypass proxy enforcement. This is intended to preserve existing and new RDP administration sessions. A custom RDP port is not automatically exempted and requires a routing-rule change before deployment.

An intentional service stop or uninstall removes transient networking state owned by this application and restores ordinary direct networking. The installer does not alter unrelated services, Windows Firewall rules, system proxy settings, or permanent routes.

## Troubleshooting

### Installer rejects the proxy settings

Confirm that the type is `socks`, version is `5`, the server is not empty, both ports are from 1 to 65535 and differ, and username/password are either both filled or both empty.

### Installation fails

Read the understandable error shown by the installer. Confirm administrator approval, sufficient disk space, and that security software did not block service registration. Reboot only if Windows reports files are locked, then run the installer again. Inno Setup's installer log can be supplied to an administrator, but inspect it before sharing; the custom configuration code does not log the entered password.

### Proxy is unreachable or authentication fails

Internet access is intentionally unavailable because enforcement is fail-closed. Check the hostname, port, optional credentials, firewall access to the proxy endpoint, and provider status. Correct `config.json`, then restart the service or wait for automatic recovery.

### Service does not start

Use **Manage Service → Show service status** and inspect `C:\ProgramData\IpRoyalService\service.log`. Configuration problems are reported without printing the password. Re-run the installer if `IpRoyalService.exe` or `engine\sing-box.exe` is missing.

### Emergency recovery

From local or console access, open **Manage Service** and choose **Stop service**. This intentionally stops the packet engine and removes its temporary routing state.

## Technical limitation

The project uses a user-mode TUN engine. It maintains strict routing while the engine is active, but cannot promise a mathematically zero-length bypass window if both the service and engine crash simultaneously. A signed Windows Filtering Platform callout driver is required for that stronger guarantee.

---

## Developer and release-maintainer instructions

End users do not need anything in this section.

### Build and test

Requirements: Windows and the .NET 8 SDK.

```powershell
dotnet restore
dotnet build --no-restore -c Release
dotnet test --no-build -c Release
```

Tests do not modify live networking.

### Build an installer locally

Install Inno Setup 6, obtain the pinned/trusted sing-box Windows AMD64 ZIP, then run:

```powershell
.\build\Build-Installer.ps1 `
  -Version 1.0.0 `
  -SingBoxZip C:\staging\sing-box-windows-amd64.zip
```

Output:

```text
artifacts\installer\IpRoyalService-v1.0.0-win-x64-Setup.exe
artifacts\installer\IpRoyalService-v1.0.0-win-x64-Setup.exe.sha256
```

The script publishes a self-contained single-file x64 service, stages only runtime/user files, compiles the installer, and generates a checksum. `config.json` is not embedded as a fixed file; the installer creates it from validated wizard input and preserves an installed copy during upgrades unless replacement is explicitly selected.

### Publish a GitHub Release

Push a semantic version tag such as `v1.0.0`. `.github/workflows/release.yml` restores, builds, tests, verifies the pinned packet-engine checksum, publishes the service, builds the Inno Setup installer, and attaches only the versioned installer and checksum to the GitHub Release.
