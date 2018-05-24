using Nethereum.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ETH_GasOMeter
{
    class ApiService : IDisposable
    {
        public const string DefaultAPIPath = "127.0.0.1:1888";

        public delegate void MessageHandler(object sender, MessageArgs e);
        public event MessageHandler OnMessage;

        public delegate void APIResponseHandler(object sender, ref APIResponseArgs e);
        public event APIResponseHandler OnAPIResponse;

        private bool _IsOngoing;
        private HttpListener _Listener;

        public ApiService()
        {
            if (!HttpListener.IsSupported) { throw new NotSupportedException("Obsolete OS detected, API will not start."); }
        }

        public void Start(string apiBind)
        {
            var httpBind = apiBind.ToString();
            if (string.IsNullOrWhiteSpace(httpBind))
            {
                OnMessage.Invoke(this, new MessageArgs(string.Format("API-bind is null or empty, using default {0}", DefaultAPIPath)));
                httpBind = DefaultAPIPath;
            }

            if (!httpBind.StartsWith("http://") || httpBind.StartsWith("https://")) { httpBind = "http://" + httpBind; }
            if (!httpBind.EndsWith("/")) { httpBind += "/"; }

            if (!int.TryParse(httpBind.Split(':')[2].TrimEnd('/'), out int port)) { throw new ArgumentException("Invalid port provided."); }

            var tempIPAddress = httpBind.Split("//")[1].Split(':')[0];
            if (!IPAddress.TryParse(tempIPAddress, out IPAddress ipAddress)) { throw new ArgumentException("Invalid IP address provided."); }

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                try { socket.Bind(new IPEndPoint(ipAddress, port)); }
                catch (Exception) { throw new ArgumentException(string.Format("API failed to bind to {0}", apiBind)); }
            };

            try
            {
                _Listener = new HttpListener();
                _Listener.Prefixes.Add(httpBind);
                _IsOngoing = true;

                Task.Run(() => Process(_Listener));
            }
            catch (Exception)
            {
                _IsOngoing = false;
                throw new ArgumentException("An error has occured while starting API.");
            }
        }

        public void Stop()
        {
            if (_IsOngoing)
            {
                _IsOngoing = false;
                OnMessage?.Invoke(this, new MessageArgs("API service stopping..."));
                try { _Listener.Stop(); }
                catch (Exception ex) { OnMessage?.Invoke(this, new MessageArgs(ex.ToString())); }
            }
        }

        private void Process(HttpListener listener)
        {
            listener.Start();
            OnMessage?.Invoke(this, new MessageArgs(string.Format("API service started at {0}...", listener.Prefixes.ElementAt(0))));
            while (_IsOngoing)
            {
                HttpListenerResponse response = listener.GetContext().Response;

                APIResponseArgs responseArgs = null;
                byte[] buffer = null;
                try
                {
                    while (responseArgs == null)
                    {
                        OnAPIResponse?.Invoke(this, ref responseArgs);
                        if (responseArgs == null) { Task.Delay(1000); }
                    }
                    buffer = Encoding.UTF8.GetBytes(responseArgs.Response);
                    response.ContentLength64 = buffer.Length;
                }
                catch (Exception ex) { OnMessage?.Invoke(this, new MessageArgs(ex.ToString())); }

                using (var output = response.OutputStream) { if (buffer != null) { output.Write(buffer, 0, buffer.Length); } }

                buffer = null;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
            }
        }

        public class APIResponseArgs : EventArgs
        {
            private static readonly JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                ContractResolver = new Json.ClassNameContractResolver()
            };

            public string Response { get; }

            public APIResponseArgs(string address, string[] excludeAddresses, Transaction transaction, EthGasStation ethGasStation, bool enableEthGasStation) => 
                Response = Json.SerializeFromObject(new TransactionSummary(address, excludeAddresses.ToList(), transaction, ethGasStation, enableEthGasStation), settings);
        }

        public class TransactionSummary
        {
            public EthGasStation EthGasStation { get; }

            public BigInteger BlockNumber { get; }

            public DateTime BlockTimestamp { get; }

            public int BasedOnNumberOfBlocks { get; }

            public decimal GasUsedPercent { get; }

            public decimal HighestGasPriceGwei { get; }

            public decimal LowestGasPriceGwei { get; }

            public decimal Percentile80GasPriceGwei { get; }

            public decimal Percentile60GasPriceGwei { get; }

            public decimal Percentile40GasPriceGwei { get; }

            public decimal Percentile20GasPriceGwei { get; }

            public decimal Percentile10GasPriceGwei { get; }

            public string ToAddress { get; }

            public List<string> ExcludeFromAddresses { get; }

            public decimal HighestTxPriceOrPercentile80GasPriceGwei { get; }

            public decimal HighestTxPriceOrPercentile60GasPriceGwei { get; }

            public decimal HighestTxPriceOrPercentile40GasPriceGwei { get; }

            public decimal HighestTxPriceOrPercentile20GasPriceGwei { get; }

            public decimal HighestTxPriceOrPercentile10GasPriceGwei { get; }

            public List<Block> Blocks { get; }

            public TransactionSummary(string toAddress, List<string> excludeFromAddresses, Transaction transaction, EthGasStation ethGasStation, bool enableEthGasStation)
            {
                EthGasStation = ethGasStation;
                _EnableEthGasStation = enableEthGasStation;

                ToAddress = toAddress;
                ExcludeFromAddresses = excludeFromAddresses;
                Blocks = transaction.Blocks.Select(b => new Block(b)).ToList();

                if (!Blocks.Any()) { return; }

                BlockNumber = Blocks[0].BlockNumber;
                BlockTimestamp = Blocks[0].Timestamp;
                BasedOnNumberOfBlocks = Blocks.Count();
                GasUsedPercent = Math.Round(Blocks.Average(b => b.GasUsedPercent), 3);

                var allSuccessTransactions = transaction.AllBlocks.SelectMany(b => b.Results.Where(r => r.BlockNumber != null && r.Status == "success")).ToArray();
                var allSuccessGasPrice = allSuccessTransactions.Select(result => result.GasPrice).OrderBy(p => p).ToArray();
                
                if (allSuccessGasPrice.Any())
                {
                    HighestGasPriceGwei = UnitConversion.Convert.FromWei(allSuccessGasPrice.Max(), toUnit: UnitConversion.EthUnit.Gwei);

                    LowestGasPriceGwei = UnitConversion.Convert.FromWei(allSuccessGasPrice.Min(), toUnit: UnitConversion.EthUnit.Gwei);

                    Percentile80GasPriceGwei = UnitConversion.Convert.FromWei(allSuccessGasPrice[(int)(allSuccessGasPrice.Count() * 0.8)], toUnit: UnitConversion.EthUnit.Gwei);
                    Percentile60GasPriceGwei = UnitConversion.Convert.FromWei(allSuccessGasPrice[(int)(allSuccessGasPrice.Count() * 0.6)], toUnit: UnitConversion.EthUnit.Gwei);
                    Percentile40GasPriceGwei = UnitConversion.Convert.FromWei(allSuccessGasPrice[(int)(allSuccessGasPrice.Count() * 0.4)], toUnit: UnitConversion.EthUnit.Gwei);
                    Percentile20GasPriceGwei = UnitConversion.Convert.FromWei(allSuccessGasPrice[(int)(allSuccessGasPrice.Count() * 0.2)], toUnit: UnitConversion.EthUnit.Gwei);
                    Percentile10GasPriceGwei = UnitConversion.Convert.FromWei(allSuccessGasPrice[(int)(allSuccessGasPrice.Count() * 0.1)], toUnit: UnitConversion.EthUnit.Gwei);

                    var monitorBlocks = transaction.Blocks.
                                                    SelectMany(b => b.Results.
                                                                      Where(r => ExcludeFromAddresses.All(a => !a.Equals(r.From, StringComparison.OrdinalIgnoreCase))).
                                                                      Select(r => r.GasPrice)).
                                                    OrderBy(p => p).
                                                    ToArray();

                    var maxMonitorGwei = UnitConversion.Convert.FromWei((monitorBlocks.Any() ? monitorBlocks.Max() : 0), 
                                                                        toUnit: UnitConversion.EthUnit.Gwei);

                    HighestTxPriceOrPercentile80GasPriceGwei = new decimal[] { maxMonitorGwei, Percentile80GasPriceGwei }.Max();
                    HighestTxPriceOrPercentile60GasPriceGwei = new decimal[] { maxMonitorGwei, Percentile60GasPriceGwei }.Max();
                    HighestTxPriceOrPercentile40GasPriceGwei = new decimal[] { maxMonitorGwei, Percentile40GasPriceGwei }.Max();
                    HighestTxPriceOrPercentile20GasPriceGwei = new decimal[] { maxMonitorGwei, Percentile20GasPriceGwei }.Max();
                    HighestTxPriceOrPercentile10GasPriceGwei = new decimal[] { maxMonitorGwei, Percentile10GasPriceGwei }.Max();
                }
            }

            private bool _EnableEthGasStation;

            public bool ShouldSerializeEthGasStation() => _EnableEthGasStation;

            public class Block
            {
                public BigInteger BlockNumber { get; }

                public DateTime Timestamp { get; }

                public decimal GasUsedPercent { get; }

                public List<Transaction> Transactions { get; }

                public Block(ETH_GasOMeter.Transaction.Block transactionBlock)
                {
                    BlockNumber = transactionBlock.Number;
                    Timestamp = transactionBlock.Timestamp;
                    GasUsedPercent = transactionBlock.GasUsedPercent;
                    Transactions = transactionBlock.Results.Select(r => new Transaction(r)).ToList();
                }

                public class Transaction
                {
                    public string Hash { get; }

                    public string Status { get; }

                    public string From { get; }

                    public decimal GasPriceGwei { get; }

                    public BigInteger? GasUsed { get; }

                    public decimal? FeeETH { get; }

                    public Transaction(ETH_GasOMeter.Transaction.Block.Result result)
                    {
                        Hash = result.TransactionHash;
                        Status = result.Status;
                        From = result.From;
                        GasPriceGwei = result.GasPriceGwei;
                        GasUsed = result.GasUsed;
                        FeeETH = result.FeeETH;
                    }
                }
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
                    if (_Listener != null) { _Listener.Close(); }
                }

                _Listener = null;
                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ApiService() {
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
