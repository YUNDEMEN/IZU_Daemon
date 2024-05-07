@echo off
dotnet publish ./src/izu/izu_daemon  -f net7.0 -c Release -r win-x64 -p:PublishSingleFile=false --self-contained false --output ./build/release.izud.net7.win_x64