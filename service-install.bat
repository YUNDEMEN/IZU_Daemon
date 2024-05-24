@echo off

sc create izu-daemon binPath="%~dp0IZUE.exe --urls http://127.0.0.1:8031"
sc failure izu-daemon reset= 0 actions= restart/0
TIMEOUT /t 2
sc start izu-daemon
TIMEOUT /t 3
sc config izu-daemon start= auto

pause