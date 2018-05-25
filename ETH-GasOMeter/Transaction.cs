using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ETH_GasOMeter
{
    class Transaction
    {
        public string MonitorAddress { get; set; }

        private int _RecentBlocks;
        private List<Block> _Blocks;

        [JsonIgnore]
        public List<Block> AllBlocks
        {
            get { lock (_Blocks) { return _Blocks; } }
        }

        public List<Block> Blocks
        {
            get
            {
                lock (_Blocks)
                {
                    try
                    {
                        var filterBlocks = _Blocks.Select(b => Json.CloneObject(b)).ToList();
                        filterBlocks.ForEach(b => b.Results.
                                                    RemoveAll(result => !(result.To ?? string.Empty).Equals(MonitorAddress, StringComparison.OrdinalIgnoreCase)));
                        return filterBlocks;
                    }
                    catch { return _Blocks; }
                }
            }
        }

        public delegate void BlockEventHandler(object sender, Block.BlockEventArgs e);
        public event BlockEventHandler OnBlockEvent;

        public Transaction(int recentBlocks)
        {
            _Blocks = new List<Block>();
            _RecentBlocks = recentBlocks;
        }

        public async void AddBlockByNumber(HexBigInteger blockNumber, Web3 web3)
        {
            await Task.Run(() =>
            {
                Block newBlock = null;
                while (newBlock == null) { try { newBlock = new Block(blockNumber, web3); } catch { } }
                lock (_Blocks)
                {
                    _Blocks.Insert(0, newBlock);
                    _Blocks.Sort((x, y) => y.Number.CompareTo(x.Number));
                    var lastBlockNum = _Blocks.Max(b => b.Number) - _RecentBlocks;
                    _Blocks.RemoveAll(b => b.Number <= lastBlockNum);
                }
                OnBlockEvent?.Invoke(this, new Block.BlockEventArgs(newBlock, MonitorAddress));
            });
        }

        public class Block
        {
            private static DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            public BigInteger Number { get; set; }

            public DateTime Timestamp { get; set; }

            public decimal GasUsedPercent { get; set; }

            public List<Result> Results { get; set; }

            public Block(HexBigInteger blockNumber, Web3 web3)
            {
                BlockWithTransactions blockWithTransactions = web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(blockNumber).Result;
                while (blockWithTransactions == null) { blockWithTransactions = web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(blockNumber).Result; }

                var resultTask = Task.Run(() =>
                {
                    return blockWithTransactions.Transactions.
                                                 AsParallel().
                                                 Select(txHash => new Result(web3, txHash)).
                                                 AsEnumerable().
                                                 OrderBy(tx => tx.TransactionIndex).
                                                 ToList();
                });

                Number = blockWithTransactions.Number;

                Timestamp = ConvertUNIXTimestampToLocalDateTime(blockWithTransactions.Timestamp.HexValue.ToString());

                GasUsedPercent = Math.Round((decimal)blockWithTransactions.GasUsed.Value / (decimal)blockWithTransactions.GasLimit.Value * 100, 3);

                Results = resultTask.Result;
            }

            public Block() { }

            private DateTime ConvertUNIXTimestampToLocalDateTime(string timestamp)
            {
                if (string.IsNullOrWhiteSpace(timestamp)) { return Epoch; }

                if (timestamp.StartsWith("0x")) { timestamp = timestamp.Substring(2); }

                try { return Epoch.AddSeconds(ulong.Parse(timestamp, NumberStyles.HexNumber)).ToLocalTime(); }
                catch { return Epoch; }
            }

            public class Result
            {
                public string TransactionHash { get; set; }

                public BigInteger BlockNumber { get; set; }

                public BigInteger TransactionIndex { get; set; }

                public string From { get; set; }

                public string To { get; set; }

                public BigInteger GasPrice { get; set; }

                public decimal GasPriceGwei => Math.Round(UnitConversion.Convert.FromWei(GasPrice, toUnit: UnitConversion.EthUnit.Gwei), 9);

                public BigInteger? GasUsed { get; set; }

                public decimal? FeeETH => (GasUsed == null) ? null :
                    (decimal?)Math.Round(UnitConversion.Convert.FromWei(GasPrice * GasUsed.Value, toUnit: UnitConversion.EthUnit.Ether), 8);

                public string Status { get; set; }

                public Result(Web3 web3, Nethereum.RPC.Eth.DTOs.Transaction transaction)
                {
                    Task<TransactionReceipt> resultTask = null;
                    if (transaction.BlockNumber == null || transaction.BlockNumber.Value == null) { Status = "pending"; }
                    else
                    {
                        resultTask = Task.Run(() =>
                        {
                            TransactionReceipt tempReciept = null;
                            while (tempReciept == null) { tempReciept = web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction.TransactionHash).Result; }
                            return tempReciept;
                        }).
                        ContinueWith(task =>
                        {
                            var reciept = task.Result;
                            GasUsed = reciept.GasUsed.Value;
                            Status = (reciept.Status.Value == 1) ? "success" : "fail";
                            return reciept;
                        });
                    }

                    TransactionHash = transaction.TransactionHash;
                    BlockNumber = transaction.BlockNumber;
                    TransactionIndex = transaction.TransactionIndex.Value;
                    From = transaction.From;
                    To = transaction.To;
                    GasPrice = transaction.GasPrice.Value;

                    if (resultTask != null) { resultTask.Wait(); }
                }

                public Result() { }
            }

            public class BlockEventArgs : EventArgs
            {
                public Block Block { get; }

                public string BlockSummary
                {
                    get
                    {
                        var summary = new StringBuilder();
                        summary.AppendLine();
                        summary.AppendLine(string.Format("Block number: {0} - Gas Used {1}% - Timestamp: {2}", Block.Number, Block.GasUsedPercent, Block.Timestamp));
                        
                        var allGasPrices = Block.Results.Select(r => r.GasPrice).OrderBy(p => p).ToArray();
                        if (allGasPrices.Any())
                        {
                            var highestGasPrice = Math.Round(UnitConversion.Convert.FromWei(allGasPrices.Max(), toUnit: UnitConversion.EthUnit.Gwei), 2);
                            var lowestGasPrice = Math.Round(UnitConversion.Convert.FromWei(allGasPrices.Min(), toUnit: UnitConversion.EthUnit.Gwei), 2);

                            var percentile80GasPrice = Math.Round(UnitConversion.Convert.FromWei(allGasPrices[(int)(allGasPrices.Count() * 0.8)], toUnit: UnitConversion.EthUnit.Gwei), 2);
                            var percentile60GasPrice = Math.Round(UnitConversion.Convert.FromWei(allGasPrices[(int)(allGasPrices.Count() * 0.6)], toUnit: UnitConversion.EthUnit.Gwei), 2);
                            var percentile40GasPrice = Math.Round(UnitConversion.Convert.FromWei(allGasPrices[(int)(allGasPrices.Count() * 0.4)], toUnit: UnitConversion.EthUnit.Gwei), 2);
                            var percentile20GasPrice = Math.Round(UnitConversion.Convert.FromWei(allGasPrices[(int)(allGasPrices.Count() * 0.2)], toUnit: UnitConversion.EthUnit.Gwei), 2);
                            var percentile10GasPrice = Math.Round(UnitConversion.Convert.FromWei(allGasPrices[(int)(allGasPrices.Count() * 0.1)], toUnit: UnitConversion.EthUnit.Gwei), 2);

                            summary.AppendLine(string.Format("Highest/Lowest Gas price: {0}/{1} GWei", highestGasPrice, lowestGasPrice));
                            summary.AppendLine(string.Format("80/60/40/20/10 percentile Gas price: {0}/{1}/{2}/{3}/{4} GWei", 
                                                             percentile80GasPrice, percentile60GasPrice, percentile40GasPrice, percentile20GasPrice, percentile10GasPrice));
                        }

                        var monitorResults = Block.Results.
                                                   Where(r => (r.To ?? string.Empty).Equals(_MonitorAddress, StringComparison.OrdinalIgnoreCase)).
                                                   ToArray();

                        if (monitorResults.Any())
                        {
                            summary.AppendLine();
                            summary.AppendLine(string.Format("To Address: {0}", monitorResults[0].To));
                        }

                        foreach (var result in monitorResults)
                        {
                            summary.AppendLine(string.Format("Transaction Hash: {0}", result.TransactionHash));
                            summary.AppendLine(string.Format("Status: {0}", result.Status));
                            summary.AppendLine(string.Format("From Address: {0}", result.From));
                            summary.AppendLine(string.Format("Gas Price: {0} GWei", result.GasPriceGwei));
                            if (result.GasUsed.HasValue) { summary.AppendLine(string.Format("Gas Used: {0}", result.GasUsed)); }
                            if (result.FeeETH.HasValue) { summary.AppendLine(string.Format("Fee: {0} Ether", result.FeeETH)); }
                        }
                        return summary.ToString();
                    }
                }

                private string _MonitorAddress;

                public BlockEventArgs(Block block, string monitorAddress)
                {
                    Block = block;
                    _MonitorAddress = monitorAddress;
                }
            }
        }
    }
}
