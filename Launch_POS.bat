@echo off
title Launching Saraa Boutique POS...
cd /d "%~dp0"
echo Starting Saraa Boutique POS Desktop application...
powershell -Command "Unblock-File -Path '%~dp0HyperPOS.exe' -ErrorAction SilentlyContinue"
start "" "%~dp0HyperPOS.exe"
exit