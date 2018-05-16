using Newtonsoft.Json;
using System;
using System.Numerics;

namespace ETH_GasOMeter
{
    class EthGasStation
    {
        public static EthGasStation GetLatestGasStation()
        {
            lock (_Object)
            {
                return Json.DeserializeFromURL<EthGasStation>(GasStationURL);
            }
        }

        public EthGasStation() { }

        [JsonProperty("blockNum")]
        public BigInteger BlockNumber { get; private set; }

        private decimal _GasUse;
        [JsonProperty("speed")]
        public decimal GasUsePercent
        {
            get { return Math.Round(_GasUse * 100, 3); }
            private set { _GasUse = value; }
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

        private decimal _Fastest;
        [JsonProperty("fastest")]
        public decimal FastestGwei
        {
            get { return _Fastest / 10; }
            private set { _Fastest = value; }
        }

        [JsonProperty("fastestWait")]
        public decimal FastestWaitMinutes { get; private set; }

        private const string GasStationURL = "https://ethgasstation.info/json/ethgasAPI.json";
        private static object _Object = new object();
    }
}
