using Nethereum.Web3;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ETH_GasOMeter
{
    class EthGasOMeter : IDisposable
    {
        private static DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public event Transaction.EthGasStationEventHandler OnEthGasStationLog;
        public event Transaction.TransactionEventHandler OnTransactionLog;

        public delegate void MessageHandler(object sender, MessageArgs e);
        public event MessageHandler OnMessage;

        public delegate void RequestUserInputHandler(object sender, RequestUserInputArgs e);
        public event RequestUserInputHandler OnRequestUserInput;

        public string MonitorAddress { get; private set; }

        private const string InfuraDevWeb3 = "https://mainnet.infura.io/ANueYSYQTstCr2mFJjPE";

        private string _UserWeb3;
        private int _DelayLoopMS;
        private Task _Task;
        private CancellationTokenSource _CancellationTokenSource;

        public EthGasOMeter(int delayLoopMS, string userWeb3)
        {
            _DelayLoopMS = delayLoopMS;
            _UserWeb3 = userWeb3;
        }

        public void Start(bool showCancel = false)
        {
            var query = new StringBuilder();
            query.AppendLine("Select Token Contract:");
            query.AppendLine("1: 0xBitcoin");
            query.AppendLine("2: 0xCatEther");
            query.AppendLine("3: KIWI Token");
            query.AppendLine("or enter Contract Address (including '0x' prefix)");
            if (showCancel) { query.AppendLine("Press Ctrl-C again to quit."); }

            var selectedAddress = string.Empty;
            var request = new RequestUserInputArgs(query.ToString());

            while (string.IsNullOrWhiteSpace(selectedAddress))
            {
                OnRequestUserInput?.Invoke(this, request);

                switch (request.UserInput)
                {
                    case "1":
                    case "0xBitcoin":
                        selectedAddress = Contracts._0xBitcoin;
                        break;

                    case "2":
                    case "0xCatEther":
                        selectedAddress = Contracts._0xCatether;
                        break;

                    case "3":
                    case "KIWI Token":
                        selectedAddress = Contracts.KIWI_Token;
                        break;

                    default:
                        if (request.UserInput == null) { break; }

                        var addressUtil = new AddressUtil();
                        if (request.UserInput.StartsWith("0x") && addressUtil.IsValidAddressLength(request.UserInput) && addressUtil.IsChecksumAddress(request.UserInput))
                        {
                            selectedAddress = request.UserInput;
                        }
                        else { OnMessage?.Invoke(this, new MessageArgs("Invalid address.")); }
                        break;
                }
            }

            _CancellationTokenSource = new CancellationTokenSource();
            _Task = Task.Factory.StartNew(() => RunMeter(selectedAddress, _CancellationTokenSource.Token), _CancellationTokenSource.Token);
        }

        private void RunMeter(string selectedAddress, CancellationToken token)
        {
            MonitorAddress = selectedAddress;

            var lastBlockNo = new BigInteger(0);
            var web3 = new Web3(string.IsNullOrWhiteSpace(_UserWeb3) ? InfuraDevWeb3 : _UserWeb3);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var tempLastBlockNo = new HexBigInteger(web3.Eth.Blocks.GetBlockNumber.SendRequestAsync().Result.Value - 1);
                    if (tempLastBlockNo.Value <= lastBlockNo)
                    {
                        Task.Delay(_DelayLoopMS);
                        continue;
                    }

                    var ethGasStation = Task.Factory.StartNew(() =>
                    {
                        var tempGasStation = EthGasStation.GetLatestGasStation();

                        if (tempGasStation == null || tempGasStation.BlockNumber.Equals(0)) // failed query
                        {
                            var currentGas = UnitConversion.Convert.FromWei(web3.Eth.GasPrice.SendRequestAsync().Result.Value, UnitConversion.EthUnit.Gwei);
                            tempGasStation = new EthGasStation(Convert.ToDecimal(currentGas));
                        }
                        return tempGasStation;
                    }).ContinueWith((gasStationTask) => {
                        if (gasStationTask.Result != null) { OnEthGasStationLog?.Invoke(this, new Transaction.EthGasStationEventArgs(gasStationTask.Result)); }
                        return gasStationTask.Result;
                    });

                    var filterInput = new NewFilterInput()
                    {
                        Address = new string[] { MonitorAddress },
                        FromBlock = new BlockParameter(tempLastBlockNo),
                        ToBlock = new BlockParameter(web3.Eth.Blocks.GetBlockNumber.SendRequestAsync().Result)
                    };

                    var logs = Task.Factory.StartNew(() =>
                    {
                        var tempLogs = web3.Eth.Filters.GetLogs.SendRequestAsync(filterInput).Result;
                        while (tempLogs == null) { tempLogs = web3.Eth.Filters.GetLogs.SendRequestAsync(filterInput).Result; }
                        return tempLogs;
                    });

                    var lastBlock = Task.Factory.StartNew(() =>
                    {
                        var tempBlock = web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(tempLastBlockNo).Result;
                        while (tempBlock == null) { tempBlock = web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(tempLastBlockNo).Result; }
                        return tempBlock;
                    });

                    var transactionEventList = Task.Factory.StartNew(() =>
                    {
                        return logs.Result.AsParallel().
                                           Select(log => new Transaction.TransactionEvent(log, web3)).
                                           AsEnumerable().
                                           OrderBy(transaction => transaction.Log.LogIndex.Value).
                                           ToList();
                    }).ContinueWith((txEventList) =>
                    {
                        OnTransactionLog?.Invoke(this, new Transaction.TransactionEventArgs(MonitorAddress, 
                                                                                            txEventList.Result, lastBlock.Result, 
                                                                                            ConvertUNIXTimestampToLocalDateTime));
                        return txEventList.Result;
                    });

                    lastBlockNo = tempLastBlockNo;
                }
                catch (Exception ex) { OnMessage?.Invoke(this, new MessageArgs(ex.ToString())); ; }

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
                Task.Delay(_DelayLoopMS);
            }
        }

        private DateTime ConvertUNIXTimestampToLocalDateTime(string timestamp)
        {
            if (string.IsNullOrWhiteSpace(timestamp)) { return Epoch; }

            if (timestamp.StartsWith("0x")) { timestamp = timestamp.Substring(2); }

            try { return Epoch.AddSeconds(ulong.Parse(timestamp, NumberStyles.HexNumber)).ToLocalTime(); }
            catch { return Epoch; }
        }

        public class RequestUserInputArgs : EventArgs
        {
            public RequestUserInputArgs(string message) => Message = message;

            public string Message { get; }

            public string UserInput { get; set; }
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        try { _CancellationTokenSource.Cancel(); }
                        catch { }

                        try { _Task.Wait(); }
                        catch { }

                        if (_CancellationTokenSource != null) { _CancellationTokenSource.Dispose(); }
                        if (_Task != null) { _Task.Dispose(); }

                        OnMessage?.Invoke(this, new MessageArgs("Process cancelled."));
                    }
                    catch (Exception ex)
                    {
                        using (var errorStream = new StreamWriter(Console.OpenStandardError()))
                        {
                            errorStream.Write(ex.ToString());
                            errorStream.Flush();
                        }
                        Console.Write(ex.ToString());
                        Environment.Exit(1);
                    }
                }
                _Task = null;
                _CancellationTokenSource = null;

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~EthGasOMeter() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion
    }
}
