using System.Threading.Tasks;

namespace TodoListApp.Services
{
    public interface ITranslationService
    {
        Task<string> TranslateAsync(string text, string targetLang);
        Task<string[]> TranslateBatchAsync(string[] texts, string targetLang);
    }
}
