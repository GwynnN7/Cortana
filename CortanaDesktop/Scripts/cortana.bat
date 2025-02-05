@echo off
dotnet build -o ../out --artifacts-path ../out/lib ../
start "../out/CortanaDesktop.exe"