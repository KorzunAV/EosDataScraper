using System;

namespace EosDataScraper.Api.Models
{
    public class TokenResponse
    {
        public string Token { get; set; }

        public DateTimeOffset Expires { get; set; }
    }
}