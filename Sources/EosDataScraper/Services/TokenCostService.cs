using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ditch.EOS.Models;
using EosDataScraper.DataAccess;
using EosDataScraper.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Npgsql;

namespace EosDataScraper.Services
{
    public partial class TokenCostService : BaseDbService
    {
        protected override bool OpenConnect => false;

        public TokenCostService(ILogger<TokenCostService> logger, IConfiguration configuration)
            : base(logger, configuration)
        {
        }

        protected override async Task DoSomethingAsync(NpgsqlConnection connection, CancellationToken token)
        {
            var settings = new TokenCostServiceSettings();
            Configuration.GetSection("TokenCostServiceSettings").Bind(settings);

            var eosToUsd = await GetEosUsdAsync(settings, token);

            var tokenCost = new List<TokenCost>
            {
                new TokenCost
                {
                    Contract = 6138663591592764928, //eosio.token
                    TokenName = "EOS",
                    EosRate = 1,
                    UsdRate = eosToUsd
                }
            };

            await GetNewDexContractsCostAsync(settings, eosToUsd, tokenCost, token);
            await GetDexEosContractsCostAsync(settings, eosToUsd, tokenCost, token);

            connection.Open();
            NpgsqlTransaction transaction = null;
            try
            {
                transaction = connection.BeginTransaction();
                await connection.UpdateTokenCost(tokenCost, token);
                transaction.CommitAndDispose();
            }
            catch (Exception)
            {
                transaction.RollbackAndDispose();
                throw;
            }
        }

        protected override async Task BeforeStartDelay(CancellationToken token)
        {
            if (PreventDos)
                await Task.Delay(TimeSpan.FromMinutes(5), token);
            else
                await Task.Delay(TimeSpan.FromMinutes(15), token)
                    .ConfigureAwait(false);
        }

        protected override void OnException(Exception ex)
        {
            if (ex is HttpRequestException)
            {
                if (PreventDos)
                    Logger.LogWarning(ex, ex.Message);
                else
                    PreventDos = true;
            }
            else
            {
                base.OnException(ex);
            }
        }

        private async Task<decimal> GetEosUsdAsync(TokenCostServiceSettings settings, CancellationToken token)
        {
            var currency = "EOS";

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", settings.CoinmarketcapApiKey);

            var message = await client.GetAsync($"{settings.CoinmarketcapApi}/cryptocurrency/quotes/latest?symbol={currency}", token)
                .ConfigureAwait(false);

            message.EnsureSuccessStatusCode();

            var json = await message.Content.ReadAsStringAsync()
                .ConfigureAwait(false);

            var root = JsonConvert.DeserializeObject<Coinmarketcap.RootObject>(json);
            var price = root.data.EOS.quote.USD.price;
            return price;
        }

        private async Task GetDexEosContractsCostAsync(TokenCostServiceSettings settings, decimal eosToUsd, List<TokenCost> tokenCosts, CancellationToken token)
        {
            var client = new HttpClient();
            var message = await client.GetAsync($"{settings.DexEosApi}/token", token)
                 .ConfigureAwait(false);

            message.EnsureSuccessStatusCode();

            var json = await message.Content.ReadAsStringAsync()
                .ConfigureAwait(false);

            var typedResult = JsonConvert.DeserializeObject<DexEosToken.RootObject[]>(json);

            foreach (var item in typedResult)
            {
                var tokenCost = new TokenCost
                {
                    Contract = BaseName.StringToName(item.code),
                    TokenName = item.symbol.ToUpper(),
                    EosRate = item.summary.last_price,
                    UsdRate = item.summary.last_price * eosToUsd
                };

                if (tokenCosts.Any(t => t.Contract == tokenCost.Contract && t.TokenName.Equals(tokenCost.TokenName)))
                    continue;

                tokenCosts.Add(tokenCost);
            }
        }

        private async Task GetNewDexContractsCostAsync(TokenCostServiceSettings settings, decimal eosToUsd, List<TokenCost> tokenCosts, CancellationToken token)
        {
            var client = new HttpClient();
            var message = await client.GetAsync($"{settings.NewDexApi}/tickers", token)
                .ConfigureAwait(false);

            message.EnsureSuccessStatusCode();

            var json = await message.Content.ReadAsStringAsync()
                .ConfigureAwait(false);

            var typedResult = JsonConvert.DeserializeObject<NewDexToken.RootObject>(json);

            foreach (var item in typedResult.data)
            {
                if (!item.symbol.EndsWith("-eos"))
                    continue;

                var tokenCost = new TokenCost
                {
                    Contract = BaseName.StringToName(item.contract),
                    TokenName = item.currency.ToUpper(),
                    EosRate = item.last,
                    UsdRate = item.last * eosToUsd
                };

                if (tokenCosts.Any(t => t.Contract == tokenCost.Contract && t.TokenName.Equals(tokenCost.TokenName)))
                    continue;

                tokenCosts.Add(tokenCost);
            }
        }

        #region Section

        private class TokenCostServiceSettings
        {
            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            public string CoinmarketcapApiKey { get; set; }
            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            public string CoinmarketcapApi { get; set; }
            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            public string DexEosApi { get; set; }
            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            public string NewDexApi { get; set; }
        }

        #endregion
    }
}
