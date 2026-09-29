using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using System.Diagnostics;

namespace MPSPlus
{
    /// <summary>
    /// "打开指定文件"命令（代码编辑器右键 MPS Actions）：将指定文件在编辑器中打开。
    /// 纯新模型（无 DTE ItemOperations）：用 Extensibility.Documents().OpenDocumentAsync(Uri)。
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="OpenSpecifiedFileCommand"/> class.
    /// </remarks>
    /// <param name="traceSource">Trace source instance to utilize.</param>
    [VisualStudioContribution]
    internal class OpenSpecifiedFileCommand(TraceSource traceSource) : Command
    {
        private readonly TraceSource logger = Requires.NotNull(traceSource, nameof(traceSource));

        /// <summary>
        /// 目标文件完整路径 —— 置空待填。
        /// 示例：@"C:\Users\14414\source\repos\MPSPlus\MPSPlus\appsettings.json"
        /// 注意：必须是绝对路径（new Uri(...) 要求绝对路径才能生成 file:// URI）。
        /// </summary>
        private const string DesignatedFilePath = "";

        /// <inheritdoc />
        public override CommandConfiguration CommandConfiguration => new("%MPSPlus.Commands.Translate.OpenSourceFile.DisplayName%")
        {
            Icon = new(ImageMoniker.KnownValues.Extension, IconSettings.IconAndText),
        };

        /// <inheritdoc />
        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(DesignatedFilePath))
            {
                await this.Extensibility.Shell().ShowPromptAsync(
                    "尚未填写目标文件路径（OpenSpecifiedFileCommand.DesignatedFilePath）。",
                    PromptOptions.OK, cancellationToken);
                return;
            }

            // 纯新模型打开文档：已打开则激活现有窗口，未打开则新开（等效旧模型 ItemOperations.OpenFile）
            await this.Extensibility.Documents().OpenDocumentAsync(new Uri(DesignatedFilePath), cancellationToken);

            await this.Extensibility.Shell().ShowPromptAsync($"已打开：{DesignatedFilePath}", PromptOptions.OK, cancellationToken);
        }
    }
}
