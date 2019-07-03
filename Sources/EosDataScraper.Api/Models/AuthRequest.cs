using System.ComponentModel.DataAnnotations;

namespace EosDataScraper.Api.Models
{
    public class AuthRequest
    {
        [Required]
        public string Login { get; set; }

        [Required]
        public string Password { get; set; }
    }
}