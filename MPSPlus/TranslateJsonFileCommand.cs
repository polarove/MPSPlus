using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.ProjectSystem.Query;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using System.Diagnostics;

namespace MPSPlus
{
    /// <summary>
    /// "翻译"命令（JSON 编辑器右键 MPS Actions）：接收当前文件信息。
    /// 纯新模型（out-of-proc，无 DTE）：活动编辑器视图通过 IClientContext 扩展方法获取。
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="TranslateJsonFileCommand"/> class.
    /// </remarks>
    /// <param name="traceSource">Trace source instance to utilize.</param>
    [VisualStudioContribution]
    internal class TranslateJsonFileCommand(TraceSource traceSource) : Command
    {
        private readonly TraceSource logger = Requires.NotNull(traceSource, nameof(traceSource));

        /// <inheritdoc />
        public override CommandConfiguration CommandConfiguration => new("%MPSPlus.Commands.Translate.DisplayName%")
        {
            Icon = new(ImageMoniker.KnownValues.Extension, IconSettings.IconAndText),
        };

        /// <inheritdoc />
        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            // 当前文件信息：活动文本视图快照（FilePath / Uri / 选区）
            var textView = await context.GetActiveTextViewAsync(cancellationToken);
            if (textView is null)
            {
                await this.Extensibility.Shell().ShowPromptAsync("当前没有活动的编辑器视图。", PromptOptions.OK, cancellationToken);
                return;
            }

            string message =
                $"文件路径: {textView.FilePath}\n" +
                $"URI: {textView.Uri}";

            await this.Extensibility.Shell().ShowPromptAsync(message, PromptOptions.OK, cancellationToken);
        }
    }
}
