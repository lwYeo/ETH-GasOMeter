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
        private static Transaction.TransactionEventArgs _LastEventLog;

        static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.Title = "ETH-GasOMeter by lwyeo (2018)";
            Console.CancelKeyPress += Console_CancelKeyPress;

            _ManualResetEvent = new ManualResetEvent(false);

            _Args = new Dictionary<string, string>();
            args.ToList().ForEach(a => _Args.Add(a.Split('=').First().ToLowerInvariant(), a.Split('=').Last()));

            _Instance = new EthGasOMeter(_Args.ContainsKey("loop-delay") ? Convert.ToInt32(_Args["loop-delay"]) : 5000);
            _Instance.OnMessage += _Instance_OnMessage;
            _Instance.OnTransactionLog += TransactionLogHandler;
            _Instance.OnRequestUserInput += _Instance_OnRequestUserInput;
            _Instance.Start();

            try
            {
                _APIService = new ApiService();
                _APIService.OnMessage += _APIService_OnMessage;
                _APIService.OnAPIResponse += _APIService_OnAPIResponse;

                _APIService.Start(_Args.ContainsKey("api-bind") ? _Args["api-bind"] : string.Empty);
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

        private static void _Instance_OnRequestUserInput(object sender, EthGasOMeter.RequestUserInputArgs e)
        {
            Console.Write(e.Message);
            e.UserInput = Console.ReadLine();
        }

        private static void _APIService_OnMessage(object sender, MessageArgs e) { Console.WriteLine(e.Message); }

        private static void _Instance_OnMessage(object sender, MessageArgs e) { Console.WriteLine(e.Message); }

        private static void _APIService_OnAPIResponse(object sender, ApiService.APIResponseArgs e)
        {
            e.Response = Json.SerializeFromObject(_LastEventLog);
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            if (_IsCancelKeyPressed)
            {
                _ManualResetEvent.Set();
                return;
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
            _Instance.OnTransactionLog -= TransactionLogHandler;
            _Instance.OnRequestUserInput -= _Instance_OnRequestUserInput;
            _Instance.Dispose();
            _Instance = null;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            
            _Instance = new EthGasOMeter(_Args.ContainsKey("loop-delay") ? Convert.ToInt32(_Args["loop-delay"]) : 5000);
            _Instance.OnMessage += _Instance_OnMessage;
            _Instance.OnTransactionLog += TransactionLogHandler;
            _Instance.OnRequestUserInput += _Instance_OnRequestUserInput;
            Task.Factory.StartNew(() => { _Instance.Start(showCancel: true); }).
                         ContinueWith((t) =>
                         {
                             try
                             {
                                 _APIService = new ApiService();
                                 _APIService.OnMessage += _APIService_OnMessage;
                                 _APIService.OnAPIResponse += _APIService_OnAPIResponse;

                                 _APIService.Start(_Args.ContainsKey("api-bind") ? _Args["api-bind"] : string.Empty);
                             }
                             catch (ArgumentException ex) { Console.WriteLine(ex.Message); }
                             catch (NotSupportedException ex) { Console.WriteLine(ex.Message); }
                             catch (Exception ex) { Console.WriteLine(ex.ToString()); }

                             Task.Delay(0);
                             _IsCancelKeyPressed = false;
                         });
        }

        private static void TransactionLogHandler(object sender, Transaction.TransactionEventArgs e)
        {
            lock (sender)
            {
                try
                {
                    UpdateLastEventLog(e);
                    if (e.Events == null) { return; }

                    var transactionEvents = _LastEventLog.Events;
                    var ethGasStation = _LastEventLog.EthGasStation;

                    if (ethGasStation != null)
                    {
                        Console.WriteLine();
                        Console.WriteLine(string.Format("Latest Block number from ethgasstation.info: {0}", ethGasStation.BlockNumber));
                        Console.WriteLine(string.Format("Gas Use: {0}%", ethGasStation.GasUsePercent));
                        Console.WriteLine(string.Format("SafeLow: {0} GWei, estimate confirmation: {1} mins", ethGasStation.SafeLowGwei, ethGasStation.SafeLowWaitMinutes));
                        Console.WriteLine(string.Format("Average: {0} GWei, estimate confirmation: {1} mins", ethGasStation.AverageGwei, ethGasStation.AverageWaitMinutes));
                        Console.WriteLine(string.Format("Fast:    {0} GWei, estimate confirmation: {1} mins", ethGasStation.FastGwei, ethGasStation.FastWaitMinutes));
                        Console.WriteLine(string.Format("Fastest: {0} GWei, estimate confirmation: {1} mins", ethGasStation.FastestGwei, ethGasStation.FastestWaitMinutes));
                    }

                    Console.WriteLine();
                    Console.WriteLine(string.Format("Last Block number: {0} - Timestamp: {1}", e.BlockNumber, e.BlockTimestamp));

                    foreach (var transactionEvent in transactionEvents)
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

        private static void UpdateLastEventLog(Transaction.TransactionEventArgs e)
        {
            try
            {
                if (_LastEventLog != null && e.EthGasStation != null)
                {
                    if (_LastEventLog.EthGasStation == null) { _LastEventLog.EthGasStation = e.EthGasStation; }
                    else if (e.EthGasStation.BlockNumber > _LastEventLog.EthGasStation.BlockNumber) { _LastEventLog.EthGasStation = e.EthGasStation; }
                }

                if (e.Events != null)
                {
                    var tempLastEthGasStation = _LastEventLog?.EthGasStation;
                    _LastEventLog = e;
                    e.EthGasStation = tempLastEthGasStation;
                }
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
