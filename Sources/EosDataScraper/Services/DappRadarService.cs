using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ditch.EOS.Models;
using EosDataScraper.Common;
using EosDataScraper.Common.Services;
using EosDataScraper.DataAccess;
using EosDataScraper.Extensions;
using EosDataScraper.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace EosDataScraper.Services
{
    public partial class DappRadarService : BaseDbService
    {
        protected override bool SingleRun => true;

        public DappRadarService(ILogger<DappRadarService> logger, IConfiguration configuration)
            : base(logger, configuration) { }

        protected override async Task DoSomethingAsync(NpgsqlConnection connection, CancellationToken token)
        {
            var isAny = await connection.IsDappRadarExistAsync(token);
            if (isAny)
                return;

            connection.Close();

            var api = new AppSettings();
            Configuration.GetSection("DappradarApi")
                .Bind(api);

            var client = new HttpClient();

            var dapps = new List<int>();
            await GetAppIdAsync(client, api.EosListUrl, dapps, token);
            await GetAppIdAsync(client, api.EosTheRestListUrl, dapps, token);
            dapps = dapps.Distinct().OrderBy(i => i).ToList();

            var set = new List<RootObject>(dapps.Count);

            for (var i = 0; i < dapps.Count; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);

                var id = dapps[i];
                Logger.LogInformation($"Progress: {i} | {dapps.Count}");

                var info = await GetDappInfoOrDefaultAsync(client, $"{api.EosUrl}{id}", token);
                if (info != null)
                    set.Add(info);
            }

            NpgsqlTransaction transaction = null;
            try
            {
                connection.Open();
                transaction = connection.BeginTransaction();

                foreach (var item in set)
                {
                    await InsertOrUpdateDappInfoAsync(connection, item, token);
                }

                transaction.CommitAndDispose();
                connection.Close();
            }
            catch (HttpRequestException e)
            {
                transaction.RollbackAndDispose();
                Logger.LogWarning(e, nameof(DappRadarService));
            }
            catch (Exception e)
            {
                transaction.RollbackAndDispose();
                Logger.LogError(e, nameof(DappRadarService));
            }
        }

        private async Task<RootObject> GetDappInfoOrDefaultAsync(HttpClient client, string url, CancellationToken token)
        {
            try
            {
                var msg = await client.GetAsync(url, token);

                if (!msg.IsSuccessStatusCode)
                    return null;

                var json = await msg.Content.ReadAsStringAsync();
                var root = JsonConvert.DeserializeObject<RootObject>(json);

                if (root.Data?.Contracts != null && root.Data.Contracts.Any())
                {
                    var contracts = new ulong[root.Data.Contracts.Count];
                    for (var i = 0; i < root.Data.Contracts.Count; i++)
                    {
                        var contract = root.Data.Contracts[i];
                        var aId = BaseName.StringToName(contract.Address);
                        var a = BaseName.UlongToString(aId);
                        if (a.Equals(contract.Address, StringComparison.Ordinal))
                        {
                            contracts[i] = aId;
                        }
                        else
                        {
                            return null;
                        }
                    }

                    return root;
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning(e, e.Message);
            }

            return null;
        }

        private async Task InsertOrUpdateDappInfoAsync(NpgsqlConnection connection, RootObject root, CancellationToken token)
        {
            var info = root.Data.Info;
            var dApp = new Dapp
            {
                Id = info.Id,
                Author = info.Author,
                Slug = info.Slug,
                Description = info.Description,
                Title = info.Title,
                Url = info.Url,
                Category = info.Category
            };

            var contracts = new ulong[root.Data.Contracts.Count];
            for (var i = 0; i < root.Data.Contracts.Count; i++)
            {
                var contract = root.Data.Contracts[i];
                var aId = BaseName.StringToName(contract.Address);
                var a = BaseName.UlongToString(aId);
                if (a.Equals(contract.Address, StringComparison.Ordinal))
                {
                    contracts[i] = aId;
                }
                else
                {
                    return;
                }
            }

            await connection.InsertOrUpdateAsync(dApp, contracts, token);
        }

        private async Task GetAppIdAsync(HttpClient client, string url, List<int> dapps, CancellationToken token)
        {
            var msg = await client.GetAsync(url, token);
            msg.EnsureSuccessStatusCode();
            var json = await msg.Content.ReadAsStringAsync();

            var resp = JsonConvert.DeserializeObject<JObject>(json);

            var list = resp.SelectToken("data.list");
            foreach (var itm in list)
            {
                var id = itm.Value<int>("id");
                dapps.Add(id);
            }
        }

        #region appSettings

        internal class AppSettings
        {
            public string EosUrl { get; set; }

            public string EosListUrl { get; set; }

            public string EosTheRestListUrl { get; set; }
        }

        #endregion
    }
}
