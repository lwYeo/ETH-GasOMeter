﻿using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.IO;
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

                Task.Factory.StartNew(() => Process(_Listener));
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
                OnMessage?.Invoke(this, new MessageArgs("API service stopping..."));
                _IsOngoing = false;
                _Listener.Stop();
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

                Stream output = response.OutputStream;
                if (buffer != null) { output.Write(buffer, 0, buffer.Length); }
                output.Close();
            }
        }

        public class APIResponseArgs : EventArgs
        {
            private static readonly Newtonsoft.Json.JsonSerializerSettings settings = new Newtonsoft.Json.JsonSerializerSettings()
            {
                ContractResolver = new Json.ClassNameContractResolver()
            };

            public APIResponseArgs(bool isSummary, string address, EthGasStation ethGasStation, List<Transaction.TransactionEventArgs> transactionEventList)
            {
                Response = isSummary ?
                    Json.SerializeFromObject(new TransactionsSummary(address, ethGasStation, transactionEventList), settings) :
                    Json.SerializeFromObject(new Transactions(address, ethGasStation, transactionEventList), settings);
            }

            public string Response { get; }
        }

        public class Transactions
        {
            public Transactions(string address, EthGasStation ethGasStation, List<Transaction.TransactionEventArgs> transactionEventList)
            {
                MonitorAddress = address;
                EthGasStation = ethGasStation;
                Blocks = transactionEventList;
            }

            public string MonitorAddress { get; }

            public EthGasStation EthGasStation { get; }

            public List<Transaction.TransactionEventArgs> Blocks { get; }
        }

        public class TransactionsSummary
        {
            public TransactionsSummary(string address, EthGasStation ethGasStation, List<Transaction.TransactionEventArgs> transactionEventList)
            {
                MonitorAddress = address;
                EthGasStation = ethGasStation;

                Blocks = new List<Block>(transactionEventList.Count);
                transactionEventList.ForEach(txEvent => Blocks.Add(new Block(txEvent.BlockNumber, txEvent.BlockTimestamp, txEvent.Events)));
            }

            public string MonitorAddress { get; }

            public decimal HighestGweiOrGasStationSafeLow
            {
                get
                {
                    var highestGwei = Blocks.Max(b => (b.Transactions.Any()) ? b.Transactions.Max(tx => tx.GasPriceGwei) : 0);
                    return new decimal[] { highestGwei, EthGasStation.SafeLowGwei }.Max();
                }
            }

            public decimal HighestGweiOrGasStationAverage
            {
                get
                {
                    var highestGwei = Blocks.Max(b => (b.Transactions.Any()) ? b.Transactions.Max(tx => tx.GasPriceGwei) : 0);
                    return new decimal[] { highestGwei, EthGasStation.AverageGwei }.Max();
                }
            }

            public decimal HighestGweiOrGasStationFast
            {
                get
                {
                    var highestGwei = Blocks.Max(b => (b.Transactions.Any()) ? b.Transactions.Max(tx => tx.GasPriceGwei) : 0);
                    return new decimal[] { highestGwei, EthGasStation.FastGwei }.Max();
                }
            }

            public decimal HighestGweiOrGasStationFastest
            {
                get
                {
                    var highestGwei = Blocks.Max(b => (b.Transactions.Any()) ? b.Transactions.Max(tx => tx.GasPriceGwei) : 0);
                    return new decimal[] { highestGwei, EthGasStation.FastestGwei }.Max();
                }
            }

            public EthGasStation EthGasStation { get; }

            public List<Block> Blocks { get; }

            public class Block
            {
                public Block(BigInteger blockNumber, DateTime timestamp, ETH_GasOMeter.Transaction.TransactionEvent[] transactionEvents)
                {
                    BlockNumber = blockNumber;
                    Timestamp = timestamp;

                    Transactions = new List<Transaction>();
                    transactionEvents.ToList().ForEach(tx => Transactions.Add(new Transaction(tx)));
                }

                public BigInteger BlockNumber { get; }

                public DateTime Timestamp { get; }

                public List<Transaction> Transactions { get; }

                public class Transaction
                {
                    public Transaction(ETH_GasOMeter.Transaction.TransactionEvent transactionEvent)
                    {
                        Hash = transactionEvent.Log.TransactionHash;
                        Status = (transactionEvent.Reciept.Status.Value.Equals(1)) ? "success" : "failed";
                        From = transactionEvent.Transaction.From;
                        GasPriceGwei = UnitConversion.Convert.FromWei(transactionEvent.Transaction.GasPrice.Value, UnitConversion.EthUnit.Gwei);
                        GasUsed = transactionEvent.Reciept.GasUsed.Value;
                        FeeETH = UnitConversion.Convert.FromWei(transactionEvent.Transaction.GasPrice.Value * GasUsed, toUnit: UnitConversion.EthUnit.Ether);
                    }

                    public string Hash { get; }

                    public string Status { get; }

                    public string From { get; }

                    public decimal GasPriceGwei { get; }

                    public BigInteger GasUsed { get; }

                    public decimal FeeETH { get; }
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
