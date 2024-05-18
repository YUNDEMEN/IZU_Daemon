@echo off
dotnet publish ./src/izu/izu_emulation  -f net7.0 -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false --output ./build/release.izue.s.net7.win_x64