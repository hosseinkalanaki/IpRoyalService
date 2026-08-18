# IPRoyal Automatic Proxy Enforcement Service

This package installs a Windows Service that routes ordinary system-wide IPv4 and IPv6 traffic through a user-selected HTTP, SOCKS4, or SOCKS5 proxy, plus a compact Windows control application for configuration, connection status, service controls, and logs. Browsers and other applications do not need individual proxy settings.

> **Safety warning:** first test proxy failure, recovery, DNS, reboot, and both existing and new RDP connections on a disposable Windows machine with console access.

## Install on Windows

### 1. Download the installer

Open the project's GitHub **Releases** page and download:

```text
IpRoyalService-vX.Y.Z-win-x64-Setup.exe
```

This is the primary end-user download for 64-bit Intel/AMD Windows. Do not download GitHub's automatically generated “Source code” archives. The installer contains the service, Windows control application, private .NET runtimes, packet engine, and configuration wizard; Visual Studio, the .NET SDK, and programming knowledge are not required.

Supported systems are x64-compatible Windows 10 version 1809 or newer and Windows 11. Unsupported Windows versions or processor architectures are rejected by setup with a clear message. The installer uses normal Windows UAC and standard built-in service/security commands; it does not require Microsoft Store applications, file associations, PowerShell modules, or an interactive login after installation.

An adjacent `.sha256` file is provided for optional download verification.

### 2. Run the installer

1. Double-click the downloaded installer.
2. Approve the Windows administrator prompt.
3. Keep the default installation folder unless there is a specific reason to change it.
4. Enter the proxy settings described below.
5. Select **Install**.

After a successful clean installation, the installer registers the `IpRoyalProxyEnforcement` Windows Service for automatic startup, starts it immediately, and offers to open **IPRoyal Proxy Control**. Installation failures are reported and a newly created, broken service registration is removed.

## Use IPRoyal Proxy Control

Open **Start → IPRoyal Automatic Proxy Enforcement → IPRoyal Proxy Control**, or use the optional desktop shortcut selected during setup. Approve the administrator prompt: Windows requires elevation to control the service and update its protected configuration.

The top panel shows the actual service and validated connection state, including stopped, connecting, connected, authentication failed, proxy unreachable, invalid configuration, connection lost, reconnecting, fail-closed proxy unavailable, and service error. A running service is not shown as connected until usable outbound proxy traffic succeeds. The selected HTTP, SOCKS4, or SOCKS5 protocol is displayed.

To change settings, select HTTP, SOCKS4, or SOCKS5; edit the server, ports, username, and password; then select **Save configuration and restart**. The service uses only that protocol and never silently falls back. Values are preserved when switching protocols. SOCKS4 uses the username as its user ID and does not use the password; HTTP and SOCKS5 pass their supported authentication fields.

Use **Start / Connect**, **Stop / Disconnect**, or **Restart / Reconnect** to control the real Windows Service. Stopping the service intentionally removes the application-owned TUN state and returns networking to the existing direct-network behavior described below; it does not silently disable enforcement while the service is running.

The built-in log viewer efficiently displays the latest service log entries and refreshes automatically. **Clear displayed view** clears only the window, not the on-disk service log. Password and common authentication representations are redacted before display.

## Proxy settings requested by the installer

| Setting | Required value |
|---|---|
| Protocol | Exactly one of HTTP, SOCKS4, or SOCKS5 |
| Proxy server | Provider hostname or IP address; required |
| Proxy server port | Provider port from 1 to 65535; default `1080` |
| Reserved/local port | Base of three unused loopback ports; default `11200` |
| Username | Optional authentication username; SOCKS4 user ID |
| Password | Optional and masked; not used by SOCKS4 |

The installer asks for the protocol and writes it to the same `config.json` used by the controller and service. The reserved/local port is a loopback-only validation endpoint and must differ from the proxy server port. Passwords are not passed through command-line arguments or deliberately written to logs.

## Configuration after installation

The service always reads:

```text
C:\Program Files\IpRoyalService\config.json
```

If a different installation directory was selected, `config.json` is beside `IpRoyalService.exe` in that directory. Its format is:

```json
{
  "protocol": "SOCKS5",
  "server": "proxy.example.com",
  "server_port": 1080,
  "reserve_port": 11200,
  "username": "your-username",
  "password": "your-password"
}
```

The installer restricts this file to Administrators and SYSTEM. The control application is the preferred way to change these values. Manual editing remains supported: open the file with an elevated editor, save it, then restart the service using the control application. The service does not live-reload this file.

## Start, stop, restart, status, and logs

Open **IPRoyal Proxy Control** from the Start menu. The legacy **Advanced Service Menu** shortcut remains available as a command-line fallback. Windows requests administrator permission for protected changes.

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

- **Windows Settings → Apps → Installed apps → IPRoyal Automatic Proxy Enforcement → Uninstall**, or
- **Control Panel → Programs and Features**.

The standard uninstaller stops and unregisters only `IpRoyalProxyEnforcement`, removes installed application files and application-owned runtime logs, and leaves `config.json` in the installation directory for safe reuse. Delete that remaining file manually only when its stored credentials are no longer needed.

## Networking behavior

When the proxy is healthy, ordinary IPv4/IPv6 application traffic and DNS use the selected enforced proxy path. SOCKS5 is always validated first on initial connection or re-evaluation; HTTP is attempted only after SOCKS5 fails. A working selection is retained until its health check fails.

When the selected protocol is unusable or authentication fails, captured Internet traffic stays blocked instead of falling back to another protocol or a direct connection. Validation occurs through the selected proxy inside active strict enforcement. The service retries every 10 seconds and restores traffic only after usable outbound traffic validates.

TCP/UDP RDP traffic on port 3389 and private/local destinations bypass proxy enforcement. This is intended to preserve existing and new RDP administration sessions. A custom RDP port is not automatically exempted and requires a routing-rule change before deployment.

An intentional service stop or uninstall removes transient networking state owned by this application and restores ordinary direct networking. The installer does not alter unrelated services, Windows Firewall rules, system proxy settings, or permanent routes.

## Troubleshooting

### Installer rejects the proxy settings

Confirm that a protocol is selected, the server is not empty, both ports are from 1 to 65535, and the ports differ. Older HTTP/SOCKS version fields are migrated when unambiguous; otherwise select a protocol in the controller and save without losing the other values.

### Installation fails

Read the understandable error shown by the installer. Confirm administrator approval, sufficient disk space, and that security software did not block service registration. Reboot only if Windows reports files are locked, then run the installer again. Inno Setup's installer log can be supplied to an administrator, but inspect it before sharing; the custom configuration code does not log the entered password.

### Proxy is unreachable or authentication fails

Internet access is intentionally unavailable because enforcement is fail-closed. The status distinguishes known authentication, reachability, timeout, configuration, and service failures. Check the selected protocol against the provider, then verify hostname, port, credentials, firewall access, and provider status.

The GUI log contains concise operational events and filters repetitive TUN/DNS/RDP packet messages. Detailed redacted engine diagnostics are stored separately at `C:\ProgramData\IpRoyalService\engine-debug.log`; inspect that file before sharing it.

### Service does not start

Open **IPRoyal Proxy Control** and inspect its connection state and built-in log viewer, or read `C:\ProgramData\IpRoyalService\service.log`. Configuration problems are reported without printing the password. Re-run the installer if `IpRoyalService.exe`, `IpRoyalControl.exe`, or `engine\sing-box.exe` is missing.

### Emergency recovery

From local or console access, open **IPRoyal Proxy Control** and select **Stop / Disconnect**. This intentionally stops the packet engine and removes its temporary routing state.

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

Install Inno Setup 6.7.1 (the version pinned by CI), obtain the pinned/trusted sing-box Windows AMD64 ZIP, then run:

```powershell
.\build\Build-Installer.ps1 `
  -Version 2.4.0 `
  -SingBoxZip C:\staging\sing-box-windows-amd64.zip
```

Output:

```text
artifacts\installer\IpRoyalService-v2.4.0-win-x64-Setup.exe
artifacts\installer\IpRoyalService-v2.4.0-win-x64-Setup.exe.sha256
```

The script publishes self-contained single-file x64 service and control executables, stages only runtime/user files, compiles the installer, and generates a checksum. `config.json` is not embedded as a fixed file; the installer creates it from validated wizard input and preserves an installed copy during upgrades unless replacement is explicitly selected.

`installer\IpRoyalService.iss` is source code used only by maintainers and GitHub Actions. Normal users must never download, open, or try to execute the `.iss` file; only the compiled `*-Setup.exe` from GitHub Releases is installable.

### Publish a GitHub Release

Push a semantic version tag such as `v1.0.0`. `.github/workflows/release.yml` restores, builds, tests, verifies the pinned packet-engine checksum, publishes the service, builds the Inno Setup installer, and attaches only the versioned installer and checksum to the GitHub Release.
