using MPSPlus.Models;
using System.Text.Json;

namespace MPSPlus.Services
{

    public interface ITranslationContext
    {
        Task ExecuteAsync(CancellationToken cancellationToken = default);
    }

    public class TranslationContext
    {
        public TranslationContext(FileInfo itemFile, FileInfo cultureFile)
        {
            ArgumentNullException.ThrowIfNull(itemFile);
            ArgumentNullException.ThrowIfNull(cultureFile);

            TranslateItems = JsonSerializer.Deserialize<TranslateItem[]>(File.ReadAllBytes(itemFile.FullName))
                ?? throw new FileNotFoundException($"文件不存在：{itemFile.FullName}");

            Cultures = JsonSerializer.Deserialize<Culture[]>(File.ReadAllBytes(cultureFile.FullName))
                ?? throw new FileNotFoundException($"文件不存在：{cultureFile.FullName}");
        }

        public readonly TranslateItem[] TranslateItems;

        public readonly Culture[] Cultures;
    }

    public class TranslationSourceContext(TranslationContext context) : ITranslationContext
    {
        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Reading Sources");
            Console.WriteLine($"TranslateItems Count: {context.TranslateItems.Length}");
            Console.WriteLine($"Cultures Count: {context.Cultures.Length}");
        }
    }

    public class TranslationCompiledContext(TranslationSourceContext sourceContext) : ITranslationContext
    {
        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Compile Source");
            await sourceContext.ExecuteAsync(cancellationToken);
        }
    }

    public class TranslationTransformedContext(TranslationCompiledContext compiledContext) : ITranslationContext
    {
        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Transforming Translation");
            await compiledContext.ExecuteAsync(cancellationToken);
        }
    }
}
