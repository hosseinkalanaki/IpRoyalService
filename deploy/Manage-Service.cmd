@echo off
setlocal
:menu
cls
echo IPRoyal Proxy Service
echo =====================
echo 1. Start service
echo 2. Stop service
echo 3. Restart service
echo 4. Show service status
echo 5. Edit config.json
echo 6. Open service log
echo 7. Exit
choice /c 1234567 /n /m "Choose an option: "
if errorlevel 7 goto end
if errorlevel 6 goto logs
if errorlevel 5 goto config
if errorlevel 4 goto status
if errorlevel 3 goto restart
if errorlevel 2 goto stop
if errorlevel 1 goto start
:logs
start "" notepad.exe "%ProgramData%\IpRoyalService\service.log"
goto wait
:config
powershell.exe -NoProfile -Command "Start-Process notepad.exe -Verb RunAs -ArgumentList '%~dp0config.json'"
goto menu
:status
powershell.exe -NoProfile -Command "Get-Service IpRoyalProxyEnforcement | Format-Table -AutoSize"
goto wait
:restart
powershell.exe -NoProfile -Command "Start-Process powershell.exe -Verb RunAs -Wait -ArgumentList '-NoProfile -Command Restart-Service IpRoyalProxyEnforcement'"
goto wait
:stop
powershell.exe -NoProfile -Command "Start-Process powershell.exe -Verb RunAs -Wait -ArgumentList '-NoProfile -Command Stop-Service IpRoyalProxyEnforcement'"
goto wait
:start
powershell.exe -NoProfile -Command "Start-Process powershell.exe -Verb RunAs -Wait -ArgumentList '-NoProfile -Command Start-Service IpRoyalProxyEnforcement'"
goto wait
:wait
echo.
pause
goto menu
:end
exit /b
