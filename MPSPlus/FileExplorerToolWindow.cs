using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.ProjectSystem.Query;
using Microsoft.VisualStudio.Extensibility.UI;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using System.Runtime.Serialization;

namespace MPSPlus
{
    /// <summary>
    /// 文件资源管理器工具窗口（View &gt; Other Windows &gt; File Explorer）：
    /// 顶部搜索框实时过滤，树结构列出解决方案目录下的所有文件夹和文件，
    /// 每项显示文件名 + 相对解决方案的相对路径（Path.GetRelativePath）。
    /// 主题：XAML 由 VS 进程渲染，样式/画刷引用 VsResourceKeys / VsBrushes（文档化的跟随主题做法）。
    /// </summary>
    [VisualStudioContribution]
    internal class FileExplorerToolWindow : ToolWindow
    {
        private readonly FileExplorerViewModel viewModel = new();
        private FileExplorerControl? content;

        public FileExplorerToolWindow(VisualStudioExtensibility extensibility)
            : base(extensibility)
        {
            this.Title = "File Explorer";
        }

        /// <summary>停靠到主窗口文档区右侧（经典工具窗口位置）。</summary>
        public override ToolWindowConfiguration ToolWindowConfiguration => new()
        {
            Placement = ToolWindowPlacement.DocumentWell,
            DockDirection = Dock.Right,
        };

        /// <inheritdoc />
        /// <remarks>先于 GetContentAsync 调用，此处取解决方案路径并构建目录树。</remarks>
        public override async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await this.viewModel.LoadSolutionAsync(this.Extensibility, cancellationToken);
        }

        /// <inheritdoc />
        public override Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
        {
            this.content ??= new FileExplorerControl(this.viewModel);
            return Task.FromResult<IRemoteUserControl>(this.content);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.content?.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 远程用户控件：XAML 自动取自同名嵌入资源 MPSPlus.FileExplorerControl.xaml
    /// （RemoteUserControl 约定：资源全名 = 类全名 + ".xaml"）。
    /// </summary>
    internal class FileExplorerControl(FileExplorerViewModel dataContext) : RemoteUserControl(dataContext)
    {
    }

    /// <summary>
    /// 树节点：Name = 文件/文件夹名；RelativePath = Path.GetRelativePath(解决方案目录, 全路径)。
    /// 文件夹排在文件前、均按名称排序；Children 为空即叶节点。
    /// </summary>
    [DataContract]
    internal class FileTreeItem : NotifyPropertyChangedObject
    {
        public FileTreeItem(string fullPath, string relativePath, bool isFolder)
        {
            this.FullPath = fullPath;
            this.RelativePath = relativePath;
            this.IsFolder = isFolder;
            this.Name = Path.GetFileName(fullPath);
        }

        [DataMember]
        public string Name { get; }

        [DataMember]
        public string RelativePath { get; }

        [DataMember]
        public bool IsFolder { get; }

        /// <summary>子节点（文件夹才有内容）。RemoteUI 要求可观察集合以支持增量同步。</summary>
        [DataMember]
        public ObservableList<FileTreeItem> Children { get; } = [];

        /// <summary>磁盘全路径（仅扩展进程内使用，不参与绑定，故不加 DataMember）。</summary>
        public string FullPath { get; }

        internal void SetChildren(IEnumerable<FileTreeItem> children) => this.Children.AddRange(children);
    }

    /// <summary>
    /// 工具窗口数据上下文：SearchText 输入即搜（OrdinalIgnoreCase 匹配相对路径），
    /// 非空时显示扁平匹配列表，清空恢复完整目录树；搜索可被下一次输入取消。
    /// </summary>
    [DataContract]
    internal class FileExplorerViewModel : NotifyPropertyChangedObject
    {
        /// <summary>搜索结果条数上限，防止超大的 bin/.git 目录拖垮 UI。</summary>
        private const int MaxSearchResults = 500;

        private readonly List<FileTreeItem> rootItems = [];
        private string solutionDirectory = string.Empty;
        private string searchText = string.Empty;
        private string statusText = string.Empty;
        private AsyncCommand? openItemCommand;
        private CancellationTokenSource? searchCts;

        /// <summary>树控件数据源：无搜索词时为完整树顶层，有搜索词时为扁平匹配列表。</summary>
        [DataMember]
        public ObservableList<FileTreeItem> Items { get; } = [];

        [DataMember]
        public string SearchText
        {
            get => this.searchText;
            set
            {
                if (this.SetProperty(ref this.searchText, value))
                {
                    this.ApplyFilter();
                }
            }
        }

        [DataMember]
        public string StatusText
        {
            get => this.statusText;
            private set => this.SetProperty(ref this.statusText, value);
        }

        /// <summary>
        /// 选中节点命令：文件 → 在编辑器中打开并激活（已打开则激活现有窗口）；
        /// 文件夹 → 直接忽略，不干预默认展开行为。
        /// 在 LoadSolutionAsync 拿到 extensibility 后才创建。
        /// </summary>
        [DataMember]
        public AsyncCommand? OpenItemCommand
        {
            get => this.openItemCommand;
            private set => this.SetProperty(ref this.openItemCommand, value);
        }

        /// <summary>查询解决方案路径（官方工作区查询 API）并构建完整目录树。</summary>
        public async Task LoadSolutionAsync(VisualStudioExtensibility extensibility, CancellationToken cancellationToken)
        {
            // 选中即打开：CommandParameter 由 RemoteUI 按代理 ID 回传原始 FileTreeItem 实例
            this.OpenItemCommand = new AsyncCommand(async (parameter, ct) =>
            {
                if (parameter is FileTreeItem { IsFolder: false } item)
                {
                    await extensibility.Documents().OpenDocumentAsync(new Uri(item.FullPath), ct);
                }
            });

            try
            {
                var solution = await extensibility.Workspaces().QuerySolutionAsync(
                    x => x.With(a => new { a.Path }), cancellationToken);
                string? solutionPath = solution.FirstOrDefault()?.Path;
                string? directory = solutionPath is null ? null : Path.GetDirectoryName(solutionPath);

                if (directory is null || !Directory.Exists(directory))
                {
                    this.StatusText = "未找到打开的解决方案。";
                    return;
                }

                this.solutionDirectory = directory;
                this.StatusText = "加载中…";

                List<FileTreeItem> items = await Task.Run(
                    () => BuildChildren(directory, directory, cancellationToken), cancellationToken);

                this.rootItems.AddRange(items);
                if (this.searchText.Length == 0)
                {
                    this.Items.Clear();
                    this.Items.AddRange(items);
                }

                this.StatusText = "就绪";
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                this.StatusText = $"加载失败：{ex.Message}";
            }
        }

        /// <summary>过滤：空搜索词恢复完整树；否则后台递归扫描，结果以扁平列表展示。</summary>
        private void ApplyFilter()
        {
            this.searchCts?.Cancel();
            var cts = new CancellationTokenSource();
            this.searchCts = cts;

            string filter = this.searchText.Trim();
            if (filter.Length == 0)
            {
                this.Items.Clear();
                this.Items.AddRange(this.rootItems);
                this.StatusText = "就绪";
                return;
            }

            this.StatusText = "搜索中…";
            string solutionDirectory = this.solutionDirectory;
            _ = Task.Run(() =>
            {
                try
                {
                    var matches = new List<FileTreeItem>();
                    if (solutionDirectory.Length > 0)
                    {
                        SearchDirectory(solutionDirectory, filter, matches, cts.Token);
                    }

                    cts.Token.ThrowIfCancellationRequested();
                    this.Items.Clear();
                    this.Items.AddRange(matches);
                    this.StatusText = matches.Count >= MaxSearchResults
                        ? $"{MaxSearchResults}+ 个匹配项"
                        : $"{matches.Count} 个匹配项";
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    this.StatusText = $"搜索失败：{ex.Message}";
                }
            });
        }

        /// <summary>递归构建一层子节点：文件夹在前、同类按名称排序；文件夹继续递归下钻。</summary>
        private static List<FileTreeItem> BuildChildren(string directory, string solutionDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var items = new List<FileTreeItem>();
            try
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    items.Add(new FileTreeItem(
                        entry,
                        Path.GetRelativePath(solutionDirectory, entry),
                        Directory.Exists(entry)));
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }

            items.Sort((a, b) => a.IsFolder == b.IsFolder
                ? string.CompareOrdinal(a.Name, b.Name)
                : (a.IsFolder ? -1 : 1));

            foreach (FileTreeItem folder in items.Where(item => item.IsFolder))
            {
                folder.SetChildren(BuildChildren(folder.FullPath, solutionDirectory, cancellationToken));
            }

            return items;
        }

        /// <summary>递归搜索：相对路径包含关键词即命中（不区分大小写），文件夹与文件均可命中。</summary>
        private void SearchDirectory(string directory, string filter, List<FileTreeItem> matches, CancellationToken cancellationToken)
        {
            if (matches.Count >= MaxSearchResults)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            List<FileTreeItem> children;
            try
            {
                children = Directory.EnumerateFileSystemEntries(directory)
                    .Select(entry => new FileTreeItem(
                        entry,
                        Path.GetRelativePath(this.solutionDirectory, entry),
                        Directory.Exists(entry)))
                    .ToList();
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }

            foreach (FileTreeItem item in children)
            {
                if (matches.Count >= MaxSearchResults)
                {
                    return;
                }

                if (item.RelativePath.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(item);
                }
            }

            foreach (FileTreeItem folder in children.Where(item => item.IsFolder))
            {
                this.SearchDirectory(folder.FullPath, filter, matches, cancellationToken);
            }
        }
    }
}
