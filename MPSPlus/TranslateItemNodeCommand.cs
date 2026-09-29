using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using System.Diagnostics;

namespace MPSPlus
{
    /// <summary>
    /// "翻译"派发命令（资源管理器文件项/文件夹右键 MPS Actions 共用）
    /// </summary>
    [VisualStudioContribution]
    internal class TranslateItemNodeCommand(TraceSource traceSource) : Command
    {
        /// <summary>Gets the trace source for logging.</summary>
        protected TraceSource Logger { get; } = Requires.NotNull(traceSource, nameof(traceSource));

        /// <inheritdoc />
        public override CommandConfiguration CommandConfiguration => new("%MPSPlus.Commands.Translate.DisplayName%")
        {
            Icon = new(ImageMoniker.KnownValues.Extension, IconSettings.IconAndText),
        };

        /// <inheritdoc />
        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            // 正牌 API（17.14）：GetSelectedPathAsync 从当前工作区树取选中项路径（file:// Uri）。
            var selectedUri = await context.GetSelectedPathAsync(cancellationToken);

            if (selectedUri is null)
            {
                await this.Extensibility.Shell().ShowPromptAsync("未获取到选中项路径。", PromptOptions.OK, cancellationToken);
                return;
            }

            await Extensibility.Shell().ShowToolWindowAsync<FileExplorerToolWindow>(false, cancellationToken);

            //// file:// Uri → 本地路径（LocalPath 自动反转义，路径来自 CPS 工作区树，真实可靠）
            //string selectionPath = selectedUri.LocalPath;

            //// dispatch：按选中项类型派发
            //if (Directory.Exists(selectionPath))
            //{
            //    await this.Extensibility.Shell().ShowPromptAsync(
            //        $"文件夹：{selectionPath}", PromptOptions.OK, cancellationToken);
            //}
            //else if (File.Exists(selectionPath))
            //{
            //    await this.Extensibility.Shell().ShowPromptAsync(
            //        $"文件：{selectionPath}", PromptOptions.OK, cancellationToken);
            //}
            //else
            //{
            //    await this.Extensibility.Shell().ShowPromptAsync(
            //        $"选中项既不是文件也不是文件夹：{selectionPath}", PromptOptions.OK, cancellationToken);
        }
    }
}
