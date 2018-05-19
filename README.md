# ETH-GasOMeter

This application grabs Gas prices of ERC-20 contract token mining activity from the Ethereum network, and also from ethgasstation.info

Built with Visual Studio 2017 (requires .NET Core 2.0, compatible with Windows / Linux / macOS)

.Net Core runtime can be downloaded from the following links:

- Windows [https://www.microsoft.com/net/download/windows/run]

- Linux [https://www.microsoft.com/net/download/linux/run]

- macOS [https://www.microsoft.com/net/download/macos/run]

--------------------------------------------------------------------

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

--------------------------------------------------------------------

The following files can be used to run the software:

	- Linux 
	sudo chmod u+x ETH-GasOMeter.sh
	./ETH-GasOMeter.sh
	
	- Windows
	ETH-GasOMeter.bat

To directly run the software, enter as below:

	dotnet ETH-GasOMeter.dll [arg1=param1 arg2=param2]

If required, edit the above batch script file(s) with the command line arguments below:

	api-bind	Bind Json API via IP:port, default 127.0.0.1:1888 (note: application will need to run as administator/sudo to work out of 127.0.0.1)
	
	loop-delay	Delay loop query into the Ethereum network in milliseconds, default 5000 (avoid going too low or it will render this application unresponsive)

	recent-blocks	API display a history of recent blocks, default 120 (approx. 30 minutes)
	
	api-summary	API display summarised fields, default true (false to display original fields)
	
	silent		To improve performance for API monitoring systems only, default false
	
	web3-url	User-defined web3 provider URL, default developer's mainnet Infura provider URL
	
	address		User-defined address to monitor so to start the process immediately, default null (will pause at the address selection menu)	

### Releases can be found [here](https://github.com/lwYeo/ETH-GasOMeter/releases).

--------------------------------------------------------------------

If you find this tool useful, and would like to support the developer, then consider a donation.

BTC						:	3GS5J5hcG6Qcu9xHWGmJaV5ftWLmZuR255

ETH (or any ERC-20 token)	:	0x9172ff7884cefed19327adace9c470ef1796105c

LTC						:	LbFkAto1qYt8RdTFHL871H4djendcHyCyB
