@echo off
echo.

for %%X in (dotnet.exe) do (set FOUND=%%~$PATH:X)
if defined FOUND (goto dotNetFound) else (goto dotNetNotFound)

:dotNetNotFound
echo .NET Core is not found or not installed, install and download from https://www.microsoft.com/net/download/windows/run
goto end

:dotNetFound
echo .NET Core is available, starting ETH-GasOMeter...
call dotnet ETH-GasOMeter.dll

:end
echo.
pause