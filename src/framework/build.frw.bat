@ECHO OFF
dotnet publish ./serviceframework  -f net6.0 -c Release -r win-x64 --sc false --output ./build/release.net6.win_x64

copy ".\build\release.net6.win_x64\ServiceFramework.dll" "..\..\references\framework" 

PAUSE