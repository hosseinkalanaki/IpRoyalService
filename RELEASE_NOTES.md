## IPRoyal Automatic Proxy Enforcement v2

Download **`IpRoyalService-v2.0.0-win-x64-Setup.exe`**, run it, approve the administrator prompt, and enter the proxy server, port, username, password, and reserved/local port. The installer no longer asks for a protocol.

Version 2 validates SOCKS5 first and automatically falls back to HTTP with the same credentials. When neither protocol is usable, strict fail-closed enforcement keeps ordinary outbound Internet traffic blocked while the existing RDP exemption remains active.

The installer supports x64-compatible Windows 10 version 1809 or newer and Windows 11 VPS installations. It contains the .NET runtime and packet engine; no development tools or Microsoft Store applications are required.

Upgrading from Version 1 preserves the existing `C:\Program Files\IpRoyalService\config.json` and credentials unless **Replace my existing config.json** is explicitly selected. Legacy `type` and `version` fields are accepted but ignored because selection is automatic.

Uninstall through **Settings → Apps → Installed apps** or **Control Panel → Programs and Features**. The service is stopped and removed automatically; `config.json` is preserved for a future reinstall.
