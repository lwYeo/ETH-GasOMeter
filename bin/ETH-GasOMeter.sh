command -v dotnet >/dev/null 2>&1 ||
{
 echo >&2 ".NET Core is not found or not installed,"
 echo >&2 "download and install from https://www.microsoft.com/net/download/linux/run";
 read -p "Press any key to continue...";
 exit 1;
}
dotnet ETH-GasOMeter.dll to-address=0xB6eD7644C69416d67B522e20bC294A9a9B405B31 enable-ethgasstation=true