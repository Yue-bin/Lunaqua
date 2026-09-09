@echo off
rem 用 Git Bash 跑指纹向导：避免 WSL 因内存不足启动失败（HCS/0x800705aa）
setlocal
set "GITBASH=%ProgramFiles%\Git\bin\bash.exe"
if not exist "%GITBASH%" set "GITBASH=%ProgramFiles(x86)%\Git\bin\bash.exe"
if not exist "%GITBASH%" set "GITBASH=%LocalAppData%\Programs\Git\bin\bash.exe"
if not exist "%GITBASH%" (
  echo 找不到 Git Bash，请装 Git for Windows，或手动指定 bash.exe
  exit /b 1
)
"%GITBASH%" "%~dp0wizard.sh" %*
