using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EosDataScraper.Common.Services;
using EosDataScraper.DataAccess;
using EosDataScraper.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Npgsql;

namespace EosDataScraper.Services
{
    public class NodeInfoService : BaseDbService
    {
        protected override bool SingleRun => true;


        public NodeInfoService(ILogger<NodeInfoService> logger, IConfiguration configuration)
            : base(logger, configuration)
        {
        }

        protected override async Task DoSomethingAsync(NpgsqlConnection connection, CancellationToken token)
        {
            var nodes = await connection.GetAllNodeInfo(token);
            connection.Close();

            var client = new HttpClient();

            var serviceUrl = Configuration.GetConnectionString("AddressesUrl");
            var msg = await client
                .GetAsync(serviceUrl, token)
                .ConfigureAwait(false);

            msg.EnsureSuccessStatusCode();

            var json = await msg.Content
                .ReadAsStringAsync()
                .ConfigureAwait(false);

            client.Dispose();
            client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };


            var urls = new List<string>();
            var nodeAddresses = JsonConvert.DeserializeObject<NodeAddress[]>(json);
            foreach (var item in nodeAddresses)
            {
                if (!item.IsNode)
                    continue;

                foreach (var node in item.Nodes)
                {
                    var http = node.Value<string>("http_server_address");
                    if (!string.IsNullOrEmpty(http))
                    {
                        urls.Add($"http://{http}");
                    }

                    var https = node.Value<string>("https_server_address");
                    if (!string.IsNullOrEmpty(https))
                    {
                        urls.Add($"https://{https}");
                    }
                }
            }

            urls = urls.Distinct().ToList();
            var currentNodes = new List<NodeInfo>();
            Parallel.ForEach(urls, url =>
            {
                var node = TestUrl(client, url, token).Result;
                if (node != null)
                {
                    lock (currentNodes)
                    {
                        currentNodes.Add(node);
                    }
                }
            });

            var newNods = new List<NodeInfo>();
            var delNodes = new List<NodeInfo>();
            foreach (var node in currentNodes)
            {
                var oldNode = nodes.SingleOrDefault(n => n.Url.Equals(node.Url));

                if (oldNode == null)
                {
                    if (node.IsMainNet)
                        newNods.Add(node);
                }
                else if (!node.IsMainNet)
                {
                    delNodes.Add(node);
                }
            }

            if (newNods.Any() || delNodes.Any())
            {
                connection.Open();

                if (delNodes.Any())
                    await connection.DeleteNodeInfos(delNodes, token);

                if (newNods.Any())
                    await connection.InsertNodeInfos(newNods, token);
            }
        }


        private async Task<NodeInfo> TestUrl(HttpClient client, string url, CancellationToken token)
        {
            try
            {
                var start = DateTime.Now;
                var msg = await client
                    .GetAsync($"{url}/v1/chain/get_info", token)
                    .ConfigureAwait(false);

                var end = DateTime.Now;

                if (msg.IsSuccessStatusCode)
                {
                    var text = await msg.Content.ReadAsStringAsync()
                        .ConfigureAwait(false);

                    var isSuccessStatusCode = text.Contains("last_irreversible_block_num");

                    var node = new NodeInfo(url)
                    {
                        IsMainNet = text.Contains($"\"chain_id\":\"{NodeInfo.MainNet}\"")
                    };
                    node.Update(end - start, isSuccessStatusCode);
                    return node;
                }
            }
            catch (OperationCanceledException)
            {
                //todo nothing
            }
            catch
            {
                //todo nothing
            }

            return null;
        }
    }
}
