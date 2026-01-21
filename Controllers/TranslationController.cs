using Microsoft.AspNetCore.Mvc;
using TodoListApp.Services;

namespace TodoListApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
                return BadRequest("Invalid request");

            var translations = await _translationService.TranslateBatchAsync(request.Texts, request.TargetLang);
            return Ok(new { translations });
        }
    }

    public class TranslationRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("texts")]
        public string[] Texts { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("targetLang")]
        public string TargetLang { get; set; }
    }
}
