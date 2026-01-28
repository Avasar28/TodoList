using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TodoListApp.Services;

namespace TodoListApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TranslationController : ControllerBase
    {
        private readonly ITranslationService _translationService;

        public TranslationController(ITranslationService translationService)
        {
            _translationService = translationService;
        }

        [HttpPost("batch")]
        public async Task<IActionResult> TranslateBatch([FromBody] TranslationRequest request)
        {
            if (request == null || request.Texts == null || request.Texts.Length == 0)
            {
                return BadRequest("Invalid request");
            }

            var translations = await _translationService.TranslateBatchAsync(request.Texts, request.TargetLang, request.SourceLang);
            return Ok(translations); // Returns array of strings directly or object depending on frontend expectation.
            // Let's verify frontend expectation. Previous was object { translations: [] } or just array? 
            // My Service now returns array. Let's return object to be safe and extensible.
        }
    }

    public class TranslationRequest
    {
        public string[] Texts { get; set; }
        public string TargetLang { get; set; }
        public string SourceLang { get; set; } = "auto";
    }
}
