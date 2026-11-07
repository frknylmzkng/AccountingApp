@echo off
setlocal
set "DOTNET_GENERATE_ASPNET_CERTIFICATE=false"
set "DOTNET_NOLOGO=true"
if exist "%~dp0artifacts\tools\dotnet-10.0.403\sdk\10.0.403\dotnet.dll" (
    set "DOTNET_ROOT=%~dp0artifacts\tools\dotnet-10.0.403"
    set "DOTNET_ROOT_X64=%~dp0artifacts\tools\dotnet-10.0.403"
    "%~dp0artifacts\tools\dotnet-10.0.403\dotnet.exe" %*
) else (
    dotnet.exe %*
)
exit /b %ERRORLEVEL%
