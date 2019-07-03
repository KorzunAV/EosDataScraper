using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace EosDataScraper.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    [Table("dapp")]
    public class Dapp
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonProperty("id")]
        [Column("id")]
        public int Id { get; set; }

        [JsonProperty("author")]
        [Column("author")]
        public string Author { get; set; }
        
        [Required]
        [JsonProperty("slug")]
        [Column("slug")]
        public string Slug { get; set; }
        
        [JsonProperty("description")]
        [Column("description")]
        public string Description { get; set; }
        
        [JsonProperty("title")]
        [Column("title")]
        public string Title { get; set; }
        
        [JsonProperty("url")]
        [Column("url")]
        public string Url { get; set; }

        [JsonProperty("category")]
        [Column("category")]
        public string Category { get; set; }
    }
}
