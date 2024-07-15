@echo off
dotnet publish ./src/tools/IZU  -f net7.0-windows -c Release -r win-x64 -p:PublishSingleFile=false --self-contained false --output ./build/release.izu-tools.win_x64

pause