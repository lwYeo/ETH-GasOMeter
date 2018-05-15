﻿using Nethereum.Geth;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace ETH_GasOMeter
{
    class Transaction
    {
        public delegate void TransactionEventHandler(object sender, TransactionEventArgs e);
        
        public class TransactionEvent
        {
            public TransactionEvent(FilterLog log, Web3Geth web3)
            {
                Log = log;

                var transactionTask = Task.Factory.StartNew(() =>
                {
                    var tempTransaction = web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(log.TransactionHash).Result;
                    while (tempTransaction == null) { tempTransaction = web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(log.TransactionHash).Result; }
                    return tempTransaction;
                });

                var recieptTask = Task.Factory.StartNew(() =>
                {
                    var tempReciept = web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(log.TransactionHash).Result;
                    while (tempReciept == null) { tempReciept = web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(log.TransactionHash).Result; }
                    return tempReciept;
                });

                Task.WaitAll(transactionTask, recieptTask);

                Transaction = transactionTask.Result;
                Reciept = recieptTask.Result;
            }
            public FilterLog Log { get; }

            public Nethereum.RPC.Eth.DTOs.Transaction Transaction { get; }

            public TransactionReceipt Reciept { get; }
        }

        public class TransactionEventArgs : EventArgs
        {
            public TransactionEventArgs(List<TransactionEvent> data, BlockWithTransactions block, EthGasStation ethGasStation, Func<string, DateTime> unixTimestampConverter)
            {
                UnixTimestampConverter = unixTimestampConverter;
                EthGasStation = ethGasStation;
                Block = block;
                Events = data.ToArray();
            }

            public EthGasStation EthGasStation { get; }

            public BigInteger BlockNumber
            {
                get { return Block.Number.Value; }
            }

            public DateTime BlockTimestamp
            {
                get { return UnixTimestampConverter(Block.Timestamp.HexValue.ToString()); }
            }

            public TransactionEvent[] Events { get; }

            private Func<string, DateTime> UnixTimestampConverter { get; }

            private BlockWithTransactions Block { get; }
        }
    }
}