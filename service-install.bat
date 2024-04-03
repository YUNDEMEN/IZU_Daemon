@echo off
sc create izu-daemon binPath="%~dp0IZU-Service.exe --urls http://127.0.0.1:8031" start= auto
pause