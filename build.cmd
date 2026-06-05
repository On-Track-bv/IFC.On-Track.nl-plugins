@REM Purpose: Windows CMD entry point for NUKE build system
@echo off
powershell -ExecutionPolicy ByPass -NoProfile -File "%~dp0build.ps1" %*
