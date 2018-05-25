# ETH-GasOMeter

This application gets gas price activities from transactions in the Ethereum network, and computes dynamic gas price via JSON API.

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

	api-bind		Bind Json API via IP:port, default 127.0.0.1:1888 (note: application will need to run as administator/sudo to bind IP other than 127.0.0.1)
	
	loop-delay		Delay loop query into the web3 provider in seconds, default 10

	recent-blocks		API display a history of recent blocks, default 40 (approx. 10 minutes)
	
	silent			Disables printing into console, may improve performance (for API monitoring systems only), default false
	
	web3-url		User-defined web3 provider URL, default developer's mainnet Infura provider URL
	
	to-address		User-defined address to monitor so to start the process immediately, default null (will pause at the address selection menu)
	
	exclude-from-address	User-defined addresses(seperated by a comma ',') to exclude from HighestTxPrice calculation, default null
	
	enable-ethgasstation	Enables reading of EthGasStation.info into API & console, default false

### Releases can be found [here](https://github.com/lwYeo/ETH-GasOMeter/releases).

--------------------------------------------------------------------

If you find this tool useful, and would like to support the developer, then consider a donation.

BTC						:	3GS5J5hcG6Qcu9xHWGmJaV5ftWLmZuR255

ETH (or any token)	:	0x9172ff7884cefed19327adace9c470ef1796105c

LTC						:	LbFkAto1qYt8RdTFHL871H4djendcHyCyB


--------------------------------------------------------------------

Below is the example of the API output:
```
{
  "EthGasStation": {
    "BlockNumber": 5667524,
    "GasUsedPercent": 99.837,
    "SafeLowGwei": 11.0,
    "SafeLowWaitMinutes": 3.0,
    "AverageGwei": 11.0,
    "AverageWaitMinutes": 3.0,
    "FastGwei": 16.0,
    "FastWaitMinutes": 0.5
  },
  "LastBlockNumber": 5667528,
  "LastBlockTimestamp": "2018-05-24T15:28:13+08:00",
  "BasedOnNumberOfBlocks": 39,
  "GasUsedPercent": 99.142,
  "HighestGasPriceGwei": 1220.958843346,
  "LowestGasPriceGwei": 1.0,
  "Percentile80GasPriceGwei": 24.0,
  "Percentile60GasPriceGwei": 15.3,
  "Percentile40GasPriceGwei": 13.0,
  "Percentile20GasPriceGwei": 12.0,
  "Percentile10GasPriceGwei": 10.0,
  "ToAddress": "0xB6eD7644C69416d67B522e20bC294A9a9B405B31",
  "ExcludeFromAddresses": [],
  "HighestTxPriceOrPercentile80GasPriceGwei": 24.0,
  "HighestTxPriceOrPercentile60GasPriceGwei": 18.0,
  "HighestTxPriceOrPercentile40GasPriceGwei": 18.0,
  "HighestTxPriceOrPercentile20GasPriceGwei": 18.0,
  "HighestTxPriceOrPercentile10GasPriceGwei": 18.0,
  "Blocks": [
    {
      "BlockNumber": 5667528,
      "Timestamp": "2018-05-24T15:28:13+08:00",
      "GasUsedPercent": 99.931,
      "Transactions": [
        {
          "Hash": "0xbfd535eda04408c4680cce48efd2ff9a339361227994f32de68e8d5401094274",
          "Status": "success",
          "From": "0x53ce57325c126145de454719b4931600a0bd6fc4",
          "GasPriceGwei": 15.0,
          "GasUsed": 36729,
          "FeeETH": 0.00055094
        }
      ]
    },
    {
      "BlockNumber": 5667527,
      "Timestamp": "2018-05-24T15:27:51+08:00",
      "GasUsedPercent": 71.898,
      "Transactions": []
    },
	.
	.
	.
  ]
}
```
