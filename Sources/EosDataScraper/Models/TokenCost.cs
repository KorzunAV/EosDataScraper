namespace EosDataScraper.Models
{
    public class TokenCost
    {
        public ulong Contract { get; set; }

        public string TokenName { get; set; }

        public decimal EosRate { get; set; }

        public decimal UsdRate { get; set; }
        
        public override string ToString()
        {
            return $"{TokenName}-{EosRate}";
        }
    }
}
