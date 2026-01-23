using System.Threading.Tasks;

namespace TodoListApp.Services
{
    public interface ITranslationService
    {
        Task<string[]> TranslateBatchAsync(string[] texts, string targetLang);
    }
}
