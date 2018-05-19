using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ETH_GasOMeter
{
    class Program
    {
        private static bool _IsCancelKeyPressed;
        private static ManualResetEvent _ManualResetEvent;
        private static EthGasOMeter _Instance;
        private static ApiService _APIService;
        private static Dictionary<string, string> _Args;
        private static EthGasStation _EthGasStation;
        private static List<Transaction.TransactionEventArgs> _EventLogList;

        static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.Title = "ETH-GasOMeter by lwYeo (2018)";
            Console.CancelKeyPress += Console_CancelKeyPress;

            _ManualResetEvent = new ManualResetEvent(false);

            _Args = args.ToDictionary(k => k.Split('=').First().ToLower(), v => v.Split('=').Last());
            CheckArguments(ref _Args);

            while (_Instance == null)
            {
                try
                {
                    _Instance = new EthGasOMeter(Convert.ToInt32(_Args["loop-delay"]), _Args["web3-url"], _Args["address"]);
                    _Instance.OnMessage += _Instance_OnMessage;
                    _Instance.OnEthGasStationLog += _Instance_OnEthGasStationLog;
                    _Instance.OnTransactionLog += TransactionLogHandler;
                    _Instance.OnRequestUserInput += _Instance_OnRequestUserInput;
                    _Instance.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine();
                    try { _Instance.Dispose(); } catch { }
                    _Instance = null;
                }
            }

            try
            {
                _APIService = new ApiService();
                _APIService.OnMessage += _APIService_OnMessage;
                _APIService.OnAPIResponse += _APIService_OnAPIResponse;

                _APIService.Start(_Args["api-bind"]);
            }
            catch (ArgumentException ex) { Console.WriteLine(ex.Message); }
            catch (NotSupportedException ex) { Console.WriteLine(ex.Message); }
            catch (Exception ex) { Console.WriteLine(ex.ToString()); }

            _ManualResetEvent.WaitOne();

            if (_APIService != null)
            {
                _APIService.Stop();
                _APIService.Dispose();
            }
            Environment.Exit(0);
        }

        private static void CheckArguments(ref Dictionary<string,string> args)
        {
            if (!args.ContainsKey("api-bind")) { args.Add("api-bind", ApiService.DefaultAPIPath); }

            if (!args.ContainsKey("loop-delay") || !Int32.TryParse(args["loop-delay"], NumberStyles.None, CultureInfo.InvariantCulture,
                out int loopDelay))
            {
                loopDelay = 5000; // 5 seconds
                if (args.ContainsKey("loop-delay")) { args["loop-delay"] = loopDelay.ToString(); }
                else { args.Add("loop-delay", loopDelay.ToString()); }
            }

            if (!args.ContainsKey("recent-blocks") || !Int32.TryParse(args["recent-blocks"], NumberStyles.None, CultureInfo.InvariantCulture,
                out int recentBlocks))
            {
                recentBlocks = 120; // approximately 30 minutes (15s per block)
                if (args.ContainsKey("recent-blocks")) { args["recent-blocks"] = recentBlocks.ToString(); }
                else { args.Add("recent-blocks", recentBlocks.ToString()); }
            }

            if (!args.ContainsKey("api-summary")) { args.Add("api-summary", "true"); }

            if (!args.ContainsKey("silent")) { args.Add("silent", "false"); }

            if (!args.ContainsKey("web3-url")) { args.Add("web3-url", null); }

            if (!args.ContainsKey("address")) { args.Add("address", null); }
        }

        private static void _Instance_OnRequestUserInput(object sender, EthGasOMeter.RequestUserInputArgs e)
        {
            Console.Write(e.Message);
            while (e.UserInput == null) { e.UserInput = Console.ReadLine(); }
        }

        private static void _APIService_OnMessage(object sender, MessageArgs e) { Console.WriteLine(e.Message); }

        private static void _Instance_OnMessage(object sender, MessageArgs e) { Console.WriteLine(e.Message); }

        private static void _APIService_OnAPIResponse(object sender, ref ApiService.APIResponseArgs e)
        {
            if (_EthGasStation == null || _EventLogList == null) { return; }
            e = new ApiService.APIResponseArgs(Convert.ToBoolean(_Args["api-summary"]), _Instance.MonitorAddress, _EthGasStation, _EventLogList);
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Ctrl-C was pressed.");
            e.Cancel = true;
            if (_IsCancelKeyPressed)
            {
                _ManualResetEvent.Set();
                Environment.Exit(0);
            }
            _IsCancelKeyPressed = true;

            if (_APIService != null)
            {
                _APIService.Stop();
                _APIService.OnMessage -= _APIService_OnMessage;
                _APIService.OnAPIResponse -= _APIService_OnAPIResponse;
                _APIService.Dispose();
            }
            _Instance.OnMessage -= _Instance_OnMessage;
            _Instance.OnEthGasStationLog -= _Instance_OnEthGasStationLog;
            _Instance.OnTransactionLog -= TransactionLogHandler;
            _Instance.OnRequestUserInput -= _Instance_OnRequestUserInput;
            _Instance.Dispose();
            _Instance = null;

            if (_EventLogList != null) { _EventLogList.Clear(); }

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);

            while (_Instance == null)
            {
                try
                {
                    _Instance = new EthGasOMeter(Convert.ToInt32(_Args["loop-delay"]), _Args["web3-url"], null);
                    _Instance.OnMessage += _Instance_OnMessage;
                    _Instance.OnEthGasStationLog += _Instance_OnEthGasStationLog;
                    _Instance.OnTransactionLog += TransactionLogHandler;
                    _Instance.OnRequestUserInput += _Instance_OnRequestUserInput;

                    Task.Factory.StartNew(() => { _Instance.Start(showCancel: true); }).
                                 ContinueWith((task) =>
                                 {
                                     try
                                     {
                                         _APIService = new ApiService();
                                         _APIService.OnMessage += _APIService_OnMessage;
                                         _APIService.OnAPIResponse += _APIService_OnAPIResponse;

                                         _APIService.Start(_Args["api-bind"]);
                                     }
                                     catch (ArgumentException ex) { Console.WriteLine(ex.Message); }
                                     catch (NotSupportedException ex) { Console.WriteLine(ex.Message); }
                                     catch (Exception ex) { Console.WriteLine(ex.ToString()); }

                                     _IsCancelKeyPressed = false;
                                 });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine();
                    try { _Instance.Dispose(); } catch { }
                    _Instance = null;
                }
            }
        }

        private static void _Instance_OnEthGasStationLog(object sender, Transaction.EthGasStationEventArgs e)
        {
            lock (sender)
            {
                if (Convert.ToBoolean(_Args["silent"])) { return; }

                if (_EthGasStation == null || e.EthGasStation.BlockNumber > _EthGasStation.BlockNumber)
                {
                    _EthGasStation = e.EthGasStation;
                    Console.WriteLine();
                    Console.WriteLine(string.Format("Latest Block number from ethgasstation.info: {0}", _EthGasStation.BlockNumber));
                    Console.WriteLine(string.Format("Gas Use: {0}%", _EthGasStation.GasUsePercent));
                    Console.WriteLine(string.Format("SafeLow: {0} GWei, estimate confirmation: {1} mins", _EthGasStation.SafeLowGwei, _EthGasStation.SafeLowWaitMinutes));
                    Console.WriteLine(string.Format("Average: {0} GWei, estimate confirmation: {1} mins", _EthGasStation.AverageGwei, _EthGasStation.AverageWaitMinutes));
                    Console.WriteLine(string.Format("Fast:    {0} GWei, estimate confirmation: {1} mins", _EthGasStation.FastGwei, _EthGasStation.FastWaitMinutes));
                    Console.WriteLine(string.Format("Fastest: {0} GWei, estimate confirmation: {1} mins", _EthGasStation.FastestGwei, _EthGasStation.FastestWaitMinutes));
                }
            }
        }

        private static void TransactionLogHandler(object sender, Transaction.TransactionEventArgs e)
        {
            lock (sender)
            {
                try
                {
                    UpdateEventLogs(e);

                    if (Convert.ToBoolean(_Args["silent"])) { return; }

                    Console.WriteLine();
                    Console.WriteLine(string.Format("Address: {0}", e.Address));
                    Console.WriteLine(string.Format("Last Block number: {0} - Timestamp: {1}", e.BlockNumber, e.BlockTimestamp));

                    foreach (var transactionEvent in e.Events)
                    {
                        try
                        {
                            var gasBurnt = new HexBigInteger((transactionEvent.Transaction.GasPrice.Value * transactionEvent.Reciept.GasUsed.Value).ToHex(false));

                            Console.WriteLine(string.Format("Transaction Hash: {0}", transactionEvent.Log.TransactionHash));
                            Console.WriteLine(string.Format("Transaction Status: {0}", transactionEvent.Reciept.Status.Value.Equals(1) ? "success" : "failed"));
                            Console.WriteLine(string.Format("From: {0}", transactionEvent.Transaction.From));
                            Console.WriteLine(string.Format("Gas Price: {0} GWei", UnitConversion.Convert.FromWei(transactionEvent.Transaction.GasPrice.Value, toUnit: UnitConversion.EthUnit.Gwei)));
                            Console.WriteLine(string.Format("Gas Used: {0}", transactionEvent.Reciept.GasUsed.Value));
                            Console.WriteLine(string.Format("Transaction Fee: {0} Ether", UnitConversion.Convert.FromWei(gasBurnt.Value, toUnit: UnitConversion.EthUnit.Ether)));
                        }
                        catch (Exception ex) { Console.WriteLine(ex.ToString()); }
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex.ToString()); }
            }
        }

        private static void UpdateEventLogs(Transaction.TransactionEventArgs e)
        {
            try
            {
                if (_EventLogList == null) { _EventLogList = new List<Transaction.TransactionEventArgs>(Int32.Parse(_Args["recent-blocks"])); }

                _EventLogList.Insert(0, e);
                _EventLogList.RemoveAll(log => log.BlockNumber < e.BlockNumber - Int32.Parse(_Args["recent-blocks"]));
            }
            catch (Exception ex) { Console.WriteLine(ex.ToString()); }
        }
    }

    public class MessageArgs : EventArgs
    {
        public MessageArgs(string message) { Message = message; }

        public string Message { get; }
    }
}
