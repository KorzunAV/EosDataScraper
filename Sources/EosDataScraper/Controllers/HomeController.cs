using System.Text;
using EosDataScraper.Services;
using Microsoft.AspNetCore.Mvc;

namespace EosDataScraper.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        private readonly ScraperService _scraperService;

        public HomeController(ScraperService scraperService)
        {
            _scraperService = scraperService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var sb = new StringBuilder();

            sb.AppendLine("[");
            _scraperService.PrintStatus(sb);
            sb.AppendLine("]");

            return Ok(sb.ToString());
        }
    }
}
