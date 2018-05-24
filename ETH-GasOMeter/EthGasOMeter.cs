using Nethereum.Web3;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;
using System;
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
        private const string InfuraDevWeb3 = "https://mainnet.infura.io/ANueYSYQTstCr2mFJjPE";

        public event Program.MessageHandler OnMessage;
        public event Program.RequestUserInputHandler OnRequestUserInput;
        public event EthGasStation.EthGasStationHandler OnEthGasStation;

        public Transaction Transaction { get; }
        public EthGasStation EthGasStation { get; private set; }

        private bool _EnableEthGasStation;
        private string _UserWeb3;
        private decimal _DelayLoopSec;
        private Task _Task;
        private CancellationTokenSource _CancellationTokenSource;

        public EthGasOMeter(string userWeb3, string recentBlocks, string delayLoopMS, bool enableEthGasStation)
        {
            _UserWeb3 = userWeb3;
            _DelayLoopSec = decimal.Parse(delayLoopMS);
            _EnableEthGasStation = enableEthGasStation;
            Transaction = new Transaction(int.Parse(recentBlocks));
        }

        public void Start(string toAddress, bool showCancel = false)
        {
            var addressUtil = new AddressUtil();
            
            if (!string.IsNullOrWhiteSpace(toAddress))
            {
                if (toAddress.StartsWith("0x") && addressUtil.IsValidAddressLength(toAddress) && addressUtil.IsChecksumAddress(toAddress))
                {
                    _CancellationTokenSource = new CancellationTokenSource();
                    _Task = Task.Run(() => RunMeter(toAddress, _CancellationTokenSource.Token), _CancellationTokenSource.Token);
                    return;
                }
                else
                {
                    toAddress = null;
                    OnMessage?.Invoke(this, new MessageArgs("Invalid user defined address."));
                }
            }
            
            var query = new StringBuilder();
            query.AppendLine("Select Token Contract:");
            query.AppendLine("1: 0xBitcoin");
            query.AppendLine("2: 0xCatEther");
            query.AppendLine("3: KIWI Token");
            query.AppendLine("4: 0xZibit");
            query.AppendLine("or enter Contract Address (including '0x' prefix)");
            if (showCancel) { query.AppendLine("Press Ctrl-C again to quit."); }

            var request = new RequestUserInputArgs(query.ToString());

            while (string.IsNullOrWhiteSpace(toAddress))
            {
                OnRequestUserInput?.Invoke(this, request);

                switch (request.UserInput)
                {
                    case "1":
                    case "0xBitcoin":
                        toAddress = Contracts._0xBitcoin;
                        break;

                    case "2":
                    case "0xCatEther":
                        toAddress = Contracts._0xCatether;
                        break;

                    case "3":
                    case "KIWI Token":
                        toAddress = Contracts.KIWI_Token;
                        break;

                    case "4":
                    case "0xZibit":
                        toAddress = Contracts._0xZibit;
                        break;

                    default:
                        if (request.UserInput == null) { break; }

                        if (request.UserInput.StartsWith("0x") && addressUtil.IsValidAddressLength(request.UserInput) && addressUtil.IsChecksumAddress(request.UserInput))
                        {
                            toAddress = request.UserInput;
                        }
                        else
                        {
                            OnMessage?.Invoke(this, new MessageArgs("Invalid address."));
                            request.UserInput = null;
                        }
                        break;
                }
            }
            _CancellationTokenSource = new CancellationTokenSource();
            _Task = Task.Run(() => RunMeter(toAddress, _CancellationTokenSource.Token), _CancellationTokenSource.Token);
        }

        private void RunMeter(string toAddress, CancellationToken token)
        {
            Transaction.MonitorAddress = toAddress;
            var lastBlockNo = new BigInteger(0);
            var web3 = new Web3(string.IsNullOrWhiteSpace(_UserWeb3) ? InfuraDevWeb3 : _UserWeb3);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var blockNo = new HexBigInteger(web3.Eth.Blocks.GetBlockNumber.SendRequestAsync().Result.Value);

                    if (blockNo.Value > lastBlockNo)
                    {
                        lastBlockNo = blockNo;
                        Transaction.AddBlockByNumber(blockNo, web3);

                        if (_EnableEthGasStation)
                        {
                            Task.Run(() => EthGasStation.GetLatestGasStation()).
                                 ContinueWith(task =>
                                 {
                                     if (task.Result != null && (EthGasStation == null || task.Result.BlockNumber > EthGasStation.BlockNumber))
                                     {
                                         EthGasStation = task.Result;
                                         OnEthGasStation?.Invoke(this, new EthGasStation.EthGasStationArgs(EthGasStation));
                                     }
                                 });
                        }
                    }
                }
                catch (AggregateException aEx)
                {
                    var errMessage = new StringBuilder();
                    errMessage.AppendLine(aEx.Message);

                    if (!aEx.InnerExceptions.Any()) { if (aEx.InnerException != null) { errMessage.AppendLine(" " + aEx.InnerException.Message); } }
                    else
                    {
                        foreach (var ex in aEx.InnerExceptions)
                        {
                            errMessage.AppendLine(" " + ex.Message);
                            if (ex.InnerException != null) { errMessage.AppendLine("  " + ex.InnerException.Message); }
                        }
                    }
                    OnMessage?.Invoke(this, new MessageArgs(errMessage.ToString()));
                }
                catch (Exception ex)
                {
                    var errMessage = new StringBuilder();
                    errMessage.AppendLine(ex.Message);
                    if (ex.InnerException != null) { errMessage.AppendLine(" " + ex.InnerException.Message); }
                    OnMessage?.Invoke(this, new MessageArgs(errMessage.ToString()));
                }
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
                Task.Delay((int)(_DelayLoopSec * 1000));
            }
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

                        if (Transaction.AllBlocks != null && Transaction.AllBlocks.Any()) { Transaction.AllBlocks.Clear(); }

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
