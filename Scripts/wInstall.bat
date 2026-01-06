@echo off
cd "%~dp0"
if not exist "WINtp.exe" (
    cd ..
)
set WINtpEXE=%cd%
sc create WINtp binPath= "\"%WINtpEXE%\WINtp.exe\" -k" start= auto depend= "tcpip"/"nsi"
sc failure WINtp reset= 60 actions= restart/60000
sc config WINtp obj= "LocalSystem"
sc description WINtp "A Simple NTP Client."
sc start WINtp

pause