## IPRoyal SOCKS5 Enforcement for Windows

Download **`IpRoyalService-vX.Y.Z-win-x64-Setup.exe`**, run it, approve the administrator prompt, and enter the SOCKS5 proxy settings in the installer. Username and password are optional.

The installer registers and starts the Windows Service automatically. Upgrades preserve the existing `C:\Program Files\IpRoyalService\config.json` unless **Replace my existing config.json** is explicitly selected.

Uninstall through **Settings → Apps → Installed apps** or **Control Panel → Programs and Features**. The service is stopped and removed automatically; `config.json` is preserved for a future reinstall.

Read the project README before production deployment. Test fail-closed behavior and RDP access on a disposable Windows machine with console access first.
