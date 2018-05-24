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
        public delegate void MessageHandler(object sender, MessageArgs e);
        public delegate void RequestUserInputHandler(object sender, RequestUserInputArgs e);

        private static bool _IsCancelKeyPressed;
        private static ManualResetEvent _ManualResetEvent;
        private static EthGasOMeter _Instance;
        private static ApiService _APIService;
        private static Dictionary<string, string> _Args;

        static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.Title = "ETH-GasOMeter by lwYeo (2018)";
            Console.CancelKeyPress += Console_CancelKeyPress;

            _ManualResetEvent = new ManualResetEvent(false);
            _Args = GetArguments(args);

            while (_Instance == null)
            {
                try
                {
                    _Instance = new EthGasOMeter(_Args["web3-url"], _Args["recent-blocks"], _Args["loop-delay"], bool.Parse(_Args["enable-ethgasstation"]));
                    _Instance.OnMessage += _OnMessage;
                    _Instance.OnEthGasStation += _Instance_OnEthGasStation;
                    _Instance.Transaction.OnBlockEvent += _Transaction_OnBlockEvent;
                    _Instance.OnRequestUserInput += _Instance_OnRequestUserInput;
                    _Instance.Start(_Args["to-address"]);
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
                _APIService.OnMessage += _OnMessage;
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

        private static Dictionary<string, string> GetArguments(string[] args)
        {
            var argDictionary = args.ToDictionary(k => k.Split('=').First().ToLower(), v => v.Split('=').Last());

            if (!argDictionary.ContainsKey("api-bind")) { argDictionary.Add("api-bind", ApiService.DefaultAPIPath); }

            if (!argDictionary.ContainsKey("loop-delay") || !Int32.TryParse(argDictionary["loop-delay"], NumberStyles.None, CultureInfo.InvariantCulture,
                out int loopDelay))
            {
                loopDelay = 10; // seconds
                if (argDictionary.ContainsKey("loop-delay")) { argDictionary["loop-delay"] = loopDelay.ToString(); }
                else { argDictionary.Add("loop-delay", loopDelay.ToString()); }
            }

            if (!argDictionary.ContainsKey("recent-blocks") || !Int32.TryParse(argDictionary["recent-blocks"], NumberStyles.None, CultureInfo.InvariantCulture,
                out int recentBlocks))
            {
                recentBlocks = 40; // approximately 10 minutes (15s per block)
                if (argDictionary.ContainsKey("recent-blocks")) { argDictionary["recent-blocks"] = recentBlocks.ToString(); }
                else { argDictionary.Add("recent-blocks", recentBlocks.ToString()); }
            }

            if (!argDictionary.ContainsKey("silent")) { argDictionary.Add("silent", "false"); }

            if (!argDictionary.ContainsKey("web3-url")) { argDictionary.Add("web3-url", null); }

            if (!argDictionary.ContainsKey("to-address")) { argDictionary.Add("to-address", null); }

            if (!argDictionary.ContainsKey("exclude-from-address")) { argDictionary.Add("exclude-from-address", null); }

            if (!argDictionary.ContainsKey("enable-ethgasstation")) { argDictionary.Add("enable-ethgasstation", "false"); }

            return argDictionary;
        }

        private static void _OnMessage(object sender, MessageArgs e) { Console.WriteLine(e.Message); }

        private static void _Instance_OnRequestUserInput(object sender, RequestUserInputArgs e)
        {
            Console.Write(e.Message);
            while (e.UserInput == null) { e.UserInput = Console.ReadLine(); }
        }

        private static void _APIService_OnAPIResponse(object sender, ref ApiService.APIResponseArgs e)
        {
            e = new ApiService.APIResponseArgs(_Instance.Transaction.MonitorAddress, 
                                               (_Args["exclude-from-address"] ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries),
                                               _Instance.Transaction,
                                               _Instance.EthGasStation,
                                               bool.Parse(_Args["enable-ethgasstation"]));
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
                _APIService.OnMessage -= _OnMessage;
                _APIService.OnAPIResponse -= _APIService_OnAPIResponse;
                _APIService.Dispose();
            }
            _Instance.OnMessage -= _OnMessage;
            _Instance.OnEthGasStation -= _Instance_OnEthGasStation;
            _Instance.Transaction.OnBlockEvent -= _Transaction_OnBlockEvent;
            _Instance.OnRequestUserInput -= _Instance_OnRequestUserInput;
            _Instance.Dispose();
            _Instance = null;

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);

            while (_Instance == null)
            {
                try
                {
                    _Instance = new EthGasOMeter(_Args["web3-url"], _Args["recent-blocks"], _Args["loop-delay"], bool.Parse(_Args["enable-ethgasstation"]));
                    _Instance.OnMessage += _OnMessage;
                    _Instance.OnEthGasStation += _Instance_OnEthGasStation;
                    _Instance.Transaction.OnBlockEvent += _Transaction_OnBlockEvent;
                    _Instance.OnRequestUserInput += _Instance_OnRequestUserInput;

                    Task.Run(() => { _Instance.Start(null, showCancel: true); }).
                         ContinueWith((task) =>
                         {
                             try
                             {
                                 _APIService = new ApiService();
                                 _APIService.OnMessage += _OnMessage;
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

        private static void _Transaction_OnBlockEvent(object sender, Transaction.Block.BlockEventArgs e)
        {
            lock (sender)
            {
                if (Convert.ToBoolean(_Args["silent"])) { return; }
                try { Console.WriteLine(e.BlockSummary); }
                catch (Exception ex) { Console.WriteLine(ex.ToString()); }
            }
        }

        private static void _Instance_OnEthGasStation(object sender, EthGasStation.EthGasStationArgs e)
        {
            lock (sender)
            {
                if (Convert.ToBoolean(_Args["silent"])) { return; }
                try { Console.WriteLine(e.Message); }
                catch (Exception ex) { Console.WriteLine(ex.ToString()); }
            }
        }
    }

    public class MessageArgs : EventArgs
    {
        public MessageArgs(string message) { Message = message; }

        public string Message { get; }
    }

    public class RequestUserInputArgs : EventArgs
    {
        public string Message { get; }

        public string UserInput { get; set; }

        public RequestUserInputArgs(string message) => Message = message;
    }
}
