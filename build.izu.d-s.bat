@echo off
dotnet publish ./src/izu/izu_daemon  -f net7.0 -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false --output ./build/release.izud.s.net7.win_x64