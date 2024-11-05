@echo off
cd "%~dp0"
set WINtpEXE=%~dp0
sc create WINtp binPath= "\"%WINtpEXE%WINtp.exe\" -k" start= auto depend= "Tcpip"
sc failure WINtp reset= 60 actions= restart/60000
sc config WINtp obj= "LocalSystem"
sc description WINtp "WINtp is a simple NTP client."

pause