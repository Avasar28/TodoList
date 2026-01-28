using System.Threading.Tasks;

namespace TodoListApp.Services
{
    public class TranslationResult
    {
        public string OriginalText { get; set; }
        public string TranslatedText { get; set; }
        public string DetectedLanguage { get; set; }
    }

    public interface ITranslationService
    {
        Task<TranslationResult[]> TranslateBatchAsync(string[] texts, string targetLang, string sourceLang = "auto");
    }
}
