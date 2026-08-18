## IPRoyal Automatic Proxy Enforcement v2.4.0

For Windows 10/11 x64 VPS systems, download **`IpRoyalService-v2.4.0-win-x64-Setup.exe`**. This is the installer; the `.sha256` file is only for checksum verification.

Version 2.4.0 adds explicit HTTP, SOCKS4, and SOCKS5 selection in both setup and IPRoyal Proxy Control. The service configures and validates only the selected protocol—there is no automatic fallback. SOCKS4 uses its protocol-specific user ID behavior; SOCKS5 and HTTP use their supported authentication fields.

Connection status now distinguishes invalid configuration, authentication failure, unreachable or timed-out proxies, connection loss, reconnecting, fail-closed proxy unavailability, and service errors. Normal TUN, DNS, and RDP-direct packet activity is removed from the user log, ANSI control sequences are stripped, and detailed redacted engine output remains in `C:\ProgramData\IpRoyalService\engine-debug.log` for diagnosis.

Strict fail-closed IPv4/IPv6 and DNS enforcement remains active whenever the selected proxy is unavailable, while the existing RDP exemption remains active. The self-contained installer includes the Windows Service, control application, .NET runtime, and packet engine.

Upgrades preserve the installed `config.json` by default. Legacy configurations with a valid HTTP or SOCKS version are migrated; configurations without a determinable protocol show an actionable error and can be corrected in IPRoyal Proxy Control without losing endpoint or credential values.
