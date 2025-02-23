﻿// ReSharper disable InconsistentNaming
// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable UnusedAutoPropertyAccessor.Local
namespace EosDataScraper.Services
{
    public partial class TokenCostService
    {
        #region generated by http://json2csharp.com/

        #region https://pro-api.coinmarketcap.com/v1/cryptocurrency/quotes/latest?symbol=EOS

        private class Coinmarketcap
        {
            //private class Status
            //{
            //    public DateTime timestamp { get; set; }
            //    public int error_code { get; set; }
            //    public object error_message { get; set; }
            //    public int elapsed { get; set; }
            //    public int credit_count { get; set; }
            //}

            public class USD
            {
                public decimal price { get; set; }
                //public double volume_24h { get; set; }
                //public double percent_change_1h { get; set; }
                //public double percent_change_24h { get; set; }
                //public double percent_change_7d { get; set; }
                //public double market_cap { get; set; }
                //public DateTime last_updated { get; set; }
            }

            public class Quote
            {
                public USD USD { get; set; }
            }

            public class EOS
            {
                //public int id { get; set; }
                //public string name { get; set; }
                //public string symbol { get; set; }
                //public string slug { get; set; }
                //public double circulating_supply { get; set; }
                //public double total_supply { get; set; }
                //public object max_supply { get; set; }
                //public DateTime date_added { get; set; }
                //public int num_market_pairs { get; set; }
                //public List<object> tags { get; set; }
                //public object platform { get; set; }
                //public int cmc_rank { get; set; }
                //public DateTime last_updated { get; set; }
                public Quote quote { get; set; }
            }

            public class Data
            {
                public EOS EOS { get; set; }
            }

            public class RootObject
            {
                //public Status status { get; set; }
                public Data data { get; set; }
            }
        }


        #endregion

        #region https://api.dexeos.io/v2/token

        private class DexEosToken
        {
            public class Summary
            {
                //public double high { get; set; }
                //public double low { get; set; }
                //public double volume { get; set; }
                //public double volume_eos { get; set; }
                //public double percent { get; set; }
                public decimal last_price { get; set; }
                //public string last_tx_type { get; set; }
            }

            public class RootObject
            {
                //public int pk { get; set; }
                public string code { get; set; }
                public string symbol { get; set; }
                //public int decimals { get; set; }
                //public string name { get; set; }
                public Summary summary { get; set; }
            }
        }


        #endregion

        #region https://api.newdex.io/v1/tickers

        private class NewDexToken
        {
            public class Datum
            {
                public string symbol { get; set; }
                public string contract { get; set; }
                public string currency { get; set; }
                public decimal last { get; set; }
                //public double change { get; set; }
                //public double high { get; set; }
                //public double low { get; set; }
                //public double amount { get; set; }
                //public double volume { get; set; }
            }

            public class RootObject
            {
                //public int code { get; set; }
                public Datum[] data { get; set; }
            }
        }

        #endregion



        #endregion
    }
}