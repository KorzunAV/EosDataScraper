using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EosDataScraper.Api.Models;
using Newtonsoft.Json;

namespace EosDataScraper.Api.Entities
{
    [Table("api_user")]
    [JsonObject(MemberSerialization.OptIn)]
    public class UserEntity
    {
        [Key]
        [Column("id")]
        [JsonProperty("id")]
        public int Id { get; set; }

        [Required]
        [Column("login")]
        [JsonProperty("login")]
        public string Login { get; set; }
        [Required]
        [Column("password")]
        public byte[] Password { get; set; }

        [Required]
        [Column("role")]
        public Roles Role { get; set; }
    }
}