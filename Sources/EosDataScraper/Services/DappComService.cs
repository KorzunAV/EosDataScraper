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
using Newtonsoft.Json.Linq;
using Npgsql;

namespace EosDataScraper.Services
{
    public partial class DappComService : BaseDbService
    {
        protected override bool SingleRun => true;

        public DappComService(ILogger<DappComService> logger, IConfiguration configuration)
            : base(logger, configuration) { }

        protected override async Task DoSomethingAsync(NpgsqlConnection connection, CancellationToken token)
        {
            var isAny = await connection.IsDappComExistAsync(token);
            if (isAny)
                return;

            connection.Close();

            var api = new AppSettings();
            Configuration.GetSection("DappComApi")
                .Bind(api);

            var client = new HttpClient();

            var dapps = await GetAppIdAsync(client, api.Url, token);

            var set = new List<RootObject>(dapps.Count);

            for (var i = 0; i < dapps.Count; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);

                Logger.LogInformation($"Progress: {i} | {dapps.Count}");

                var dapp = dapps[i];
                var info = await GetDappInfoOrDefaultAsync(client, $"{api.Url}app/details/{dapp}", token);
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
                Logger.LogWarning(e, nameof(DappComService));
            }
            catch (Exception e)
            {
                transaction.RollbackAndDispose();
                Logger.LogError(e, nameof(DappComService));
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

                if (root.app.contracts != null && root.app.contracts.Any())
                {
                    var contracts = new ulong[root.app.contracts.Length];
                    for (var i = 0; i < root.app.contracts.Length; i++)
                    {
                        var contract = root.app.contracts[i];
                        var name = contract.Type == JTokenType.String
                            ? contract.Value<string>()
                            : contract.Value<string>("address");

                        if (string.IsNullOrEmpty(name))
                            return null;

                        var aId = BaseName.StringToName(name);
                        var a = BaseName.UlongToString(aId);
                        if (a.Equals(name, StringComparison.Ordinal))
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
            const int minId = 1000000;
            const int maxId = 3000000;

            var dapp = new Dapp
            {
                Author = root.app.developer ?? string.Empty,
                Slug = root.app.identifier,
                Description = root.app.desc_en ?? string.Empty,
                Title = root.app.@abstract ?? string.Empty,
                Url = root.app.url ?? string.Empty,
                Category = root.app.category?.name ?? string.Empty
            };

            var contracts = new ulong[root.app.contracts.Length];
            for (var i = 0; i < root.app.contracts.Length; i++)
            {
                var contract = root.app.contracts[i];
                var name = contract.Type == JTokenType.String
                    ? contract.Value<string>()
                    : contract.Value<string>("address");

                var aId = BaseName.StringToName(name);
                var a = BaseName.UlongToString(aId);
                if (a.Equals(name, StringComparison.Ordinal))
                {
                    contracts[i] = aId;
                }
                else
                {
                    return;
                }
            }

            await connection.FindAndInsertOrUpdateAsync(minId, maxId, dapp, contracts, token);
        }

        private async Task<List<string>> GetAppIdAsync(HttpClient client, string url, CancellationToken token)
        {
            var msg = await client.GetAsync($"{url}app", token);
            msg.EnsureSuccessStatusCode();
            var json = await msg.Content.ReadAsStringAsync();

            var apps = JsonConvert.DeserializeObject<JArray>(json);
            var dapps = new List<string>();

            foreach (var app in apps)
            {
                var name = app.SelectToken("chain.name").Value<string>();
                if (name.Equals("EOS", StringComparison.OrdinalIgnoreCase))
                {
                    var id = app.Value<string>("identifier");
                    dapps.Add(id);
                }
            }

            return dapps;
        }

        #region appSettings

        private class AppSettings
        {
            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            public string Url { get; set; }
        }

        #endregion
    }
}
