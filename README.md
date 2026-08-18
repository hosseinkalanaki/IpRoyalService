# IPRoyal SOCKS5 Enforcement Service

A Windows Service that sends ordinary IPv4/IPv6 traffic through the SOCKS5 proxy in `config.json`. It manages a **sing-box TUN** packet engine with strict routing, proxied DNS, health monitoring, automatic recovery, and explicit RDP/private-network exclusions.

> **Safety warning:** test installation, proxy failure, service crash, reboot, IPv4/IPv6, DNS, and RDP access on a disposable Windows VM with console access before deploying to any production or remote machine.

## Design and guarantees

The TUN engine installs temporary owned routes while it runs. `strict_route` prevents ordinary direct fallback and DNS leakage; the SOCKS server is reached through the automatically detected physical interface. TCP/UDP port 3389 and private/local destinations route directly, so inbound RDP responses, outbound RDP clients, loopback, and ordinary LAN administration are not proxied. The service only writes `%ProgramData%\IpRoyalService\engine.json` and TUN-owned routes/adapters; it does not change system proxy settings, unrelated firewall rules, or permanent routes.

If the proxy becomes unusable, traffic captured by the strict TUN remains unavailable and health checks continue. When healthy again, traffic recovers automatically. Service recovery is configured for crashes. An intentional stop removes the engine's TUN routes, restoring the pre-service networking state. Therefore, **intentional stop/uninstall is deliberately fail-open**, as required for clean restoration. A process crash has a short service-recovery interval; this user-mode design cannot make a mathematically gap-free crash kill-switch. Environments requiring a zero-gap guarantee must add an organization-managed, signed WFP callout driver and should not claim this service alone provides that stronger property.

## Prerequisites

- Windows 10/11 or Windows Server 2019+ x64
- Administrator rights (LocalSystem is used because TUN/route operations require it)
- .NET 8 SDK to build; self-contained publishing avoids a runtime prerequisite
- A trusted x64 Windows `sing-box` release ZIP, supplied explicitly to the installer. Pin and verify its checksum/signature through your deployment system.

## Configuration

The supplied schema is preserved exactly:

```json
{
  "type": "socks",
  "version": "5",
  "server": "geo.iproyal.com",
  "server_port": 12321,
  "reserve_port": 11200,
  "username": "...",
  "password": "..."
}
```

`reserve_port` is a loopback-only SOCKS health listener. The password is never deliberately logged; engine output is redacted as defense in depth. The installer restricts `config.json` to Administrators and SYSTEM. For production, inject the file from a secrets system rather than committing credentials.

## Build and test

```powershell
dotnet restore
dotnet build --no-restore -c Release
dotnet test --no-build -c Release
dotnet publish src/IpRoyalService -c Release -r win-x64 --self-contained true -o artifacts/publish
```

Tests validate configuration and secret redaction only and do not alter live networking.

## Install and operate

From an elevated PowerShell prompt:

```powershell
.\artifacts\publish\deploy\Install.ps1 -PublishDirectory .\artifacts\publish -SingBoxZip C:\staging\sing-box-windows-amd64.zip
Start-Service IpRoyalProxyEnforcement
Restart-Service IpRoyalProxyEnforcement
Stop-Service IpRoyalProxyEnforcement
Get-Service IpRoyalProxyEnforcement
```

Edit `C:\Program Files\IpRoyalService\config.json` only while stopped, then restart. Installation is idempotent. To uninstall while retaining configuration:

```powershell
& 'C:\Program Files\IpRoyalService\deploy\Uninstall.ps1'
```

Add `-RemoveConfiguration` to remove credentials too. Durable newline-delimited JSON logs are written to `%ProgramData%\IpRoyalService\service.log` (and to standard output when run interactively). Startup/configuration, engine state, health loss/recovery, and shutdown are logged without the password.

## Troubleshooting

- **Service immediately stops:** inspect service-host logs; malformed config and a missing `engine\sing-box.exe` are fatal before routes change.
- **No Internet:** verify proxy DNS reachability, credentials, port egress, and that the local `reserve_port` is unused.
- **RDP concern:** validate both an already-open and a new RDP session from a separate console-controlled VM. RDP uses direct port 3389 and private destinations bypass the TUN.
- **Engine exits:** use a sing-box version supporting `tun`, `mixed`, `strict_route`, DNS HTTPS, and Windows route management. Service recovery retries automatically.
- **Emergency recovery:** from local/console access run `Stop-Service IpRoyalProxyEnforcement`; intentional stop removes owned transient networking state.

## Security notes

Run as LocalSystem only because transparent adapter and route management require administrative networking privileges. Keep the install directory ACL restricted, checksum-pin the packet engine, rotate the supplied sample credential, and never submit real `config.json` contents in diagnostic reports.
