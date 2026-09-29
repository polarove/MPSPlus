using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using MPSPlus.Services;

namespace MPSPlus
{
    /// <summary>
    /// Extension entrypoint for the VisualStudio.Extensibility extension.
    /// 四个右键菜单各配一组（menu → group → 右键菜单 经典三层）：
    /// JSON 编辑器 / 代码编辑器 / 资源管理器文件项 / 资源管理器文件夹。
    /// </summary>
    [VisualStudioContribution]
    internal class ExtensionEntrypoint : Extension
    {
        // ── 菜单（四处都叫 "MPS Actions"，共用同一显示名资源键）──

        /// <summary>JSON 编辑器右键的 MPS Actions：翻译 + Options 单选组。</summary>
        [VisualStudioContribution]
        public static MenuConfiguration JsonMenu => new("%MPSPlus.Commands.DisplayName%")
        {
            Children =
            [
                MenuChild.Command<TranslateJsonFileCommand>(),
            ],
        };

        /// <summary>资源管理器文件项右键的 MPS Actions：翻译 + Options 单选组。</summary>
        [VisualStudioContribution]
        public static MenuConfiguration ItemMenu => new("%MPSPlus.Commands.DisplayName%")
        {
            Children =
            [
                MenuChild.Command<TranslateItemNodeCommand>()
            ],
        };

        /// <summary>资源管理器文件夹右键的 MPS Actions：翻译 + Options 单选组。</summary>
        [VisualStudioContribution]
        public static MenuConfiguration FolderMenu => new("%MPSPlus.Commands.DisplayName%")
        {
            Children =
            [
                MenuChild.Command<TranslateItemNodeCommand>(),
            ],
        };

        /// <summary>代码编辑器右键的 MPS Actions：打开指定文件。</summary>
        [VisualStudioContribution]
        public static MenuConfiguration CodeWinMenu => new("%MPSPlus.Commands.DisplayName%")
        {
            Children = [MenuChild.Command<OpenSpecifiedFileCommand>()],
        };

        // ── 组（坐标均为本机实测验证值）──
        // shell 系 guidSHLMainMenu = {D309F791-903F-11D0-9EFC-00A0C911004F}（vsshlids.h）：
        //   IDM_VS_CTXT_CODEWIN = 0x040D（代码编辑器右键）
        //   IDM_VS_CTXT_ITEMNODE = 0x0430（资源管理器文件项右键）
        //   IDM_VS_CTXT_FOLDERNODE = 0x0431（资源管理器文件夹右键）
        // JSON 编辑器右键 "JSON Context" 不属于 vsshlids.h：是 Web 工具 JSON 编辑器自己的
        // 命令集 {F718CA06-CF4F-4A0C-9106-E79E9EE5E7CD}，菜单 ID 0x1（DTE 运行时试探实锤，网传 0x3 已失效）。

        /// <summary>JSON 编辑器组。</summary>
        [VisualStudioContribution]
        public static CommandGroupConfiguration JsonGroup => new(
            GroupPlacement.VsctParent(new Guid("F718CA06-CF4F-4A0C-9106-E79E9EE5E7CD"), 0x1, 0x0200))
        {
            Children = [GroupChild.Menu(JsonMenu)],
        };

        /// <summary>资源管理器文件项组。</summary>
        [VisualStudioContribution]
        public static CommandGroupConfiguration ItemNodeGroup => new(
            GroupPlacement.VsctParent(new Guid("D309F791-903F-11D0-9EFC-00A0C911004F"), 0x0430, 0x0200))
        {
            Children = [GroupChild.Menu(ItemMenu)],
        };

        /// <summary>资源管理器文件夹组。</summary>
        [VisualStudioContribution]
        public static CommandGroupConfiguration FolderNodeGroup => new(
            GroupPlacement.VsctParent(new Guid("D309F791-903F-11D0-9EFC-00A0C911004F"), 0x0431, 0x0200))
        {
            Children = [GroupChild.Menu(FolderMenu)],
        };

        /// <summary>代码编辑器组。</summary>
        [VisualStudioContribution]
        public static CommandGroupConfiguration CodeWinGroup => new(
            GroupPlacement.VsctParent(new Guid("D309F791-903F-11D0-9EFC-00A0C911004F"), 0x040D, 0x0200))
        {
            Children = [GroupChild.Menu(CodeWinMenu)],
        };

        /// <inheritdoc/>
        public override ExtensionConfiguration ExtensionConfiguration => new()
        {
            Metadata = new(
                    id: "MPSPlus.35130361-bcda-4fb8-a443-9de2b6b11f4c",
                    version: this.ExtensionAssemblyVersion,
                    publisherName: "Publisher name",
                    displayName: "MPSPlus",
                    description: "Extension description"),
        };

        /// <inheritdoc />
        protected override void InitializeServices(IServiceCollection serviceCollection)
        {
            base.InitializeServices(serviceCollection);

            // 翻译管道（步骤 1 读取源 → 步骤 2 翻译 → 步骤 3 写回，洋葱模型可短路）
            serviceCollection.AddScoped<TranslationContext>();
            serviceCollection.AddScoped<TranslationSourceContext>();
            serviceCollection.AddScoped<TranslationCompiledContext>();
            serviceCollection.AddScoped<TranslationTransformedContext>();
        }
    }
}
