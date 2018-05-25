@echo off
echo.

for %%X in (dotnet.exe) do (set FOUND=%%~$PATH:X)
if defined FOUND (goto dotNetFound) else (goto dotNetNotFound)

:dotNetNotFound
echo .NET Core is not found or not installed,
echo download and install from https://www.microsoft.com/net/download/windows/run
goto end

:dotNetFound
echo .NET Core is available, starting ETH-GasOMeter...
pushd %~dp0
dotnet ETH-GasOMeter.dll to-address=0xB6eD7644C69416d67B522e20bC294A9a9B405B31 enable-ethgasstation=true

:end
echo.
pause