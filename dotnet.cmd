@echo off
setlocal
set "DOTNET_GENERATE_ASPNET_CERTIFICATE=false"
set "DOTNET_NOLOGO=true"
if exist "%~dp0artifacts\tools\dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%~dp0artifacts\tools\dotnet"
    set "DOTNET_ROOT_X64=%~dp0artifacts\tools\dotnet"
    "%~dp0artifacts\tools\dotnet\dotnet.exe" %*
) else (
    dotnet.exe %*
)
exit /b %ERRORLEVEL%
