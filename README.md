# ETH-GasOMeter

This application grabs Gas prices of ERC-20 contract token mining activity from the Ethereum network, and also from ethgasstation.info

Built for with Visual Studio 2015 (requires .NET Core 2.0, compatible with Windows / Linux / macOS)

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

To run the software, enter the following in the command line (excluding square brackets):

	dotnet ETH-GasOMeter.dll [arg1=param1 arg2=param2]

Command line arguments:

	bind-api	Bind Json API via IP:port, default 127.0.0.1:1888
	
	loop-delay	Delay loop query into the Ethereum network in milliseconds, default 5000 (avoid going too low or it will render this app unresponsive)

Releases can be found [here](https://github.com/lwYeo/ETH-GasOMeter/releases).

--------------------------------------------------------------------

If you find this tool useful, and would like to support the developer, then consider a donation.

BTC						:	3GS5J5hcG6Qcu9xHWGmJaV5ftWLmZuR255

ETH (or any ERC-20 token)	:	0x9172ff7884cefed19327adace9c470ef1796105c

LTC						:	LbFkAto1qYt8RdTFHL871H4djendcHyCyB
