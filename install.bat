@echo off
sc create izu-daemon binPath=%~dp0IZU-Service.exe
pause