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
            _Instance.OnTransactionLog += TransactionLogHandler;
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

        private static void _APIService_OnMessage(object sender, ApiService.MessageArgs e) { Console.WriteLine(e.Message); }

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
                _APIService.Dispose();
            }
            _Instance.OnTransactionLog -= TransactionLogHandler;
            _Instance.Dispose();
            _Instance = null;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            
            _Instance = new EthGasOMeter(_Args.ContainsKey("loop-delay") ? Convert.ToInt32(_Args["loop-delay"]) : 5000);
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
                    _LastEventLog = e;
                    var transactionEvents = e.Events;
                    var ethGasStation = e.EthGasStation;

                    Console.WriteLine();
                    Console.WriteLine(string.Format("Latest Block number from ethgasstation.info: {0}", ethGasStation.BlockNumber));
                    Console.WriteLine(string.Format("Gas Use: {0}%", ethGasStation.GasUsePercent));
                    Console.WriteLine(string.Format("SafeLow: {0} GWei, estimate confirmation: {1} mins", ethGasStation.SafeLowGwei, ethGasStation.SafeLowWaitMinutes));
                    Console.WriteLine(string.Format("Average: {0} GWei, estimate confirmation: {1} mins", ethGasStation.AverageGwei, ethGasStation.AverageWaitMinutes));
                    Console.WriteLine(string.Format("Fast:    {0} GWei, estimate confirmation: {1} mins", ethGasStation.FastGwei, ethGasStation.FastWaitMinutes));
                    Console.WriteLine(string.Format("Fastest: {0} GWei, estimate confirmation: {1} mins", ethGasStation.FastestGwei, ethGasStation.FastestWaitMinutes));

                    Console.WriteLine();
                    Console.WriteLine(string.Format("Last Block number: {0} - Timestamp: {1}", e.BlockNumber, e.BlockTimestamp));

                    foreach (var transactionEvent in transactionEvents)
                    {
                        try
                        {
                            var gasBurnt = new HexBigInteger((transactionEvent.Transaction.GasPrice.Value * transactionEvent.Reciept.GasUsed.Value).ToHex(false));

                            Console.WriteLine(string.Format("Transaction Hash: {0}", transactionEvent.Log.TransactionHash));
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
    }
}
