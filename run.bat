@echo off
title Email Summarizer (Win32)
cd /d "%~dp0"

if exist "publish\EmailSummarizer.exe" (
    start "" "publish\EmailSummarizer.exe" %*
) else if exist "bin\Release\net8.0-windows\EmailSummarizer.exe" (
    start "" "bin\Release\net8.0-windows\EmailSummarizer.exe" %*
) else (
    dotnet run -c Release -- %*
)
