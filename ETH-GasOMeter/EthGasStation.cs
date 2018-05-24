using Newtonsoft.Json;
using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ETH_GasOMeter
{
    class EthGasStation
    {
        public static async Task<EthGasStation> GetLatestGasStation()
        {
            return await Task.Run(() =>
            {
                lock (_Object) { return Json.DeserializeFromURL<EthGasStation>(GasStationURL); }
            });
        }

        [JsonProperty("blockNum")]
        public BigInteger BlockNumber { get; private set; }

        private decimal _GasUsed;
        [JsonProperty("speed")]
        public decimal GasUsedPercent
        {
            get { return Math.Round(_GasUsed * 100, 3); }
            private set { _GasUsed = value; }
        }

        private decimal _SafeLow;
        [JsonProperty("safeLow")]
        public decimal SafeLowGwei
        {
            get { return _SafeLow / 10; }
            private set { _SafeLow = value; }
        }

        [JsonProperty("safeLowWait")]
        public decimal SafeLowWaitMinutes { get; private set; }

        private decimal _Average;
        [JsonProperty("average")]
        public decimal AverageGwei
        {
            get { return _Average / 10; }
            private set { _Average = value; }
        }

        [JsonProperty("avgWait")]
        public decimal AverageWaitMinutes { get; private set; }

        private decimal _Fast;
        [JsonProperty("fast")]
        public decimal FastGwei
        {
            get { return _Fast / 10; }
            private set { _Fast = value; }
        }

        [JsonProperty("fastWait")]
        public decimal FastWaitMinutes { get; private set; }

        //private decimal _Fastest;
        //[JsonProperty("fastest")]
        //public decimal FastestGwei
        //{
        //    get { return _Fastest / 10; }
        //    private set { _Fastest = value; }
        //}

        //[JsonProperty("fastestWait")]
        //public decimal FastestWaitMinutes { get; private set; }

        public delegate void EthGasStationHandler(object sender, EthGasStationArgs e);

        public EthGasStation() { }

        public EthGasStation(decimal fixedGasPrice)
        {
            SafeLowGwei = fixedGasPrice * 10;
            AverageGwei = fixedGasPrice * 10;
            FastGwei = fixedGasPrice * 10;
            //FastestGwei = fixedGasPrice * 10;
        }

        private const string GasStationURL = "https://ethgasstation.info/json/ethgasAPI.json";
        private static object _Object = new object();

        public class EthGasStationArgs : EventArgs
        {
            public EthGasStation EthGasStation { get; }

            public string Message
            {
                get
                {
                    var message = new StringBuilder();
                    message.AppendLine();
                    message.AppendLine(string.Format("Latest Block number from ethgasstation.info: {0}", EthGasStation.BlockNumber));
                    message.AppendLine(string.Format("Gas Used: {0}%", EthGasStation.GasUsedPercent));
                    message.AppendLine(string.Format("SafeLow : {0} GWei, estimate confirmation: {1} mins", EthGasStation.SafeLowGwei, EthGasStation.SafeLowWaitMinutes));
                    message.AppendLine(string.Format("Average : {0} GWei, estimate confirmation: {1} mins", EthGasStation.AverageGwei, EthGasStation.AverageWaitMinutes));
                    message.AppendLine(string.Format("Fast    : {0} GWei, estimate confirmation: {1} mins", EthGasStation.FastGwei, EthGasStation.FastWaitMinutes));
                    //message.AppendLine(string.Format("Fastest : {0} GWei, estimate confirmation: {1} mins", EthGasStation.FastestGwei, EthGasStation.FastestWaitMinutes));
                    return message.ToString();
                }
            }

            public EthGasStationArgs(EthGasStation ethGasStation) => EthGasStation = ethGasStation;
        }
    }
}
