@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0dev.ps1" play
if errorlevel 1 pause
