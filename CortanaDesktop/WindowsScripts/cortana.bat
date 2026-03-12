@echo off
dotnet build -o ../out --artifacts-path ../out/lib ../
..\out\CortanaDesktop.exe