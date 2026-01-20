using Assimp.Unmanaged;
using Eto.Drawing;
using Eto.Forms;
using PZShrinker.Lib;
using System.Globalization;
using System.IO;

namespace PZShrinker.GUI;

public class MainForm : Form
{
    // 文件夹路径
    private readonly TextBox folderPathBox;
    private readonly Button selectFolderButton;

    // Switcher CheckBoxes
    private readonly CheckBox iconTextureCheck;
    private readonly CheckBox modelTextureCheck;
    private readonly CheckBox packTextureCheck;
    private readonly CheckBox allTextureCheck;
    private readonly CheckBox fbxModelCheck;
    private readonly CheckBox d3dModelCheck;

    // Remove ModelInfo CheckBoxes
    private readonly CheckBox removeOtherUVCheck;
    private readonly CheckBox removeOtherTextureInfoCheck;
    private readonly CheckBox removeTangentCheck;
    private readonly CheckBox removeColorCheck;
    private readonly CheckBox mergeAllMeshCheck;

    // Texture Config TextBoxes
    private readonly TextBox maxSizeBox;
    private readonly TextBox minSizeBox;
    private readonly TextBox scaleRatioBox;

    public MainForm()
        {
            Title = "PZ Shrinker";
            Size = new Size(400, 450);
            MinimumSize = new Size(400, 450);
            var layout = new DynamicLayout { Padding = new Padding(10), Spacing = new Size(5, 5) };

            // === 文件夹选择部分 ===
            folderPathBox = new TextBox { ReadOnly = true, Width = 250 };
            selectFolderButton = new Button { Text = "选择" };
            selectFolderButton.ToolTip = "选择Steam Workshop文件夹，如G:\\SteamLibrary\\steamapps\\workshop\\content\\108600";
            selectFolderButton.Click += OnSelectFolderClick;

            layout.AddRow(new TableRow("工作目录:", folderPathBox, selectFolderButton));
            layout.AddSeparateRow(); // 分隔线或空行

            // === Switcher 分组 ===
            var switcherGroup = new GroupBox { Text = "文件类型" };
            var switcherLayout = new TableLayout(2, 3) { Spacing = new Size(10, 5) };

            iconTextureCheck = new CheckBox { Text = "图标纹理" };
            iconTextureCheck.ToolTip = "处理图标纹理文件";
            modelTextureCheck = new CheckBox { Text = "模型纹理" };
            modelTextureCheck.ToolTip = "处理模型纹理文件";
            packTextureCheck = new CheckBox { Text = "纹理包" };
            packTextureCheck.ToolTip = "处理纹理包文件（不推荐开启）";
            allTextureCheck = new CheckBox { Text = "所有纹理" };
            allTextureCheck.ToolTip = "处理所有纹理文件（不推荐开启）";
            fbxModelCheck = new CheckBox { Text = "FBX模型" };
            fbxModelCheck.ToolTip = "处理FBX格式模型文件";
            d3dModelCheck = new CheckBox { Text = "X模型" };
            d3dModelCheck.ToolTip = "处理X格式模型文件";

        var switcherChecks = new[]
        {
            iconTextureCheck,
            modelTextureCheck,
            packTextureCheck,
            allTextureCheck,
            fbxModelCheck,
            d3dModelCheck
        };

        for (int i = 0; i < switcherChecks.Length; i++)
        {
            int row = i / 2;
            int col = i % 2;
            switcherLayout.Add(switcherChecks[i], col, row);
        }
        switcherGroup.Content = switcherLayout;
        layout.Add(switcherGroup);

        // === Remove ModelInfo 分组 ===
        var removeGroup = new GroupBox { Text = "模型配置" };

        removeOtherUVCheck = new CheckBox { Text = "移除其他UV" };
        removeOtherUVCheck.ToolTip = "移除模型中多余的UV通道";
        removeOtherTextureInfoCheck = new CheckBox { Text = "移除嵌入纹理信息" };
        removeOtherTextureInfoCheck.ToolTip = "移除模型中嵌入的纹理信息";
        removeTangentCheck = new CheckBox { Text = "移除切线" };
        removeTangentCheck.ToolTip = "移除模型中的切线数据";
        removeColorCheck = new CheckBox { Text = "移除顶点颜色" };
        removeColorCheck.ToolTip = "移除模型中的顶点颜色数据";
        mergeAllMeshCheck = new CheckBox { Text = "合并所有网格" };
        mergeAllMeshCheck.ToolTip = "合并模型中的所有网格";

        var removeChecks = new[]
        {
            removeOtherUVCheck,
            removeOtherTextureInfoCheck,
            removeTangentCheck,
            removeColorCheck,
            mergeAllMeshCheck
        };

        var removeLayout = new StackLayout { Spacing = 5 };
        foreach (var cb in removeChecks)
            removeLayout.Items.Add(cb);
        removeGroup.Content = removeLayout;
        layout.Add(removeGroup);

        // === Texture Config 分组 ===
        var configGroup = new GroupBox { Text = "纹理配置" };

        maxSizeBox = new TextBox { Width = 80, Text = "512" };
        maxSizeBox.ToolTip = "纹理最大尺寸";
        minSizeBox = new TextBox { Width = 80, Text = "128" };
        minSizeBox.ToolTip = "纹理最小尺寸";
        scaleRatioBox = new TextBox { Width = 80, Text = "0.25" };
        scaleRatioBox.ToolTip = "纹理缩放比例";

        var configLayout = new TableLayout
        {
            Rows =
            {
                new TableRow(new Label { Text = "最大值:" }, maxSizeBox),
                new TableRow(new Label { Text = "最小值:" }, minSizeBox),
                new TableRow(new Label { Text = "缩放比例:" }, scaleRatioBox)
            },
            Spacing = new Size(10, 5)
        };
        configGroup.Content = configLayout;
        layout.Add(configGroup);

        // 可选：添加确认/取消按钮
        var buttonLayout = new TableLayout
        {
            Rows =
            {
                null,
                new TableRow(
                    new Button { Text = "取消", ToolTip = "取消操作并关闭窗口", Command = new Command((s,e) => Close()) },
                    new Button { Text = "应用", ToolTip = "应用设置并开始处理", Command = new Command(OnApply) }
                )
            }
        };
        layout.Add(buttonLayout);

        Content = layout;
    }

    private void OnSelectFolderClick(object? sender, EventArgs? e)
    {
        var dialog = new SelectFolderDialog();
        if (dialog.ShowDialog(this) == DialogResult.Ok)
        {
            folderPathBox.Text = dialog.Directory;
        }
    }

    private void OnApply(object? sender, EventArgs? e)
    {
        try
        {
            // 绑定字段
            string path = folderPathBox.Text;

            bool iconTexture = iconTextureCheck.Checked ?? false;
            bool modelTexture = modelTextureCheck.Checked ?? false;
            bool packTexture = packTextureCheck.Checked ?? false;
            bool allTexture = allTextureCheck.Checked ?? false;
            bool fbxModel = fbxModelCheck.Checked ?? false;
            bool d3dModel = d3dModelCheck.Checked ?? false;

            bool removeOtherUV = removeOtherUVCheck.Checked ?? false;
            bool removeOtherTextureInfo = removeOtherTextureInfoCheck.Checked ?? false;
            bool removeTangent = removeTangentCheck.Checked ?? false;
            bool removeColor = removeColorCheck.Checked ?? false;
            bool mergeAllMesh = mergeAllMeshCheck.Checked ?? false;

            // 解析数字输入（带默认值和错误处理）
            int maxSize = int.TryParse(maxSizeBox.Text, out var max) ? max : 512;
            int minSize = int.TryParse(minSizeBox.Text, out var min) ? min : 128;
            float scaleratio = float.TryParse(scaleRatioBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio) ? ratio : 0.25f;
            Task.Run(() =>
            {
                var di = new DirectoryInfo(path);
                if (!di.Exists)
                {
                    Console.Error.WriteLine($"路径 '{path}' 不存在。");
                    return;
                }
                if (maxSize < 0 || minSize < 0 || scaleratio < 0)
                {
                    Console.Error.WriteLine($"数值不能为负数！");
                    return;
                }

                var mods = di.GetDirectories();
                Console.WriteLine($"找到 {mods.Length} 个模组，开始处理");

                IEnumerable<string> texture = [];
                IEnumerable<string> packs = [];
                IEnumerable<string> models = [];
                foreach (var mod in mods)
                {
                    if (allTexture || iconTexture || modelTexture)
                        texture = texture.Concat(Finder.FindTextures(mod.FullName, allTexture, iconTexture, modelTexture));
                    if (packTexture)
                        packs = packs.Concat(Finder.FindPacks(mod.FullName));
                    if (fbxModel)
                        models = models.Concat(Finder.FindFBXModels(mod.FullName));
                    if (d3dModel)
                        models = models.Concat(Finder.FindD3DModels(mod.FullName));
                }

                string[] unique_texture = [.. texture.Distinct()];
                string[] unique_packs = [.. packs.Distinct()];
                string[] unique_models = [.. models.Distinct()];

                Console.WriteLine($"找到 {unique_texture.Length} 个独特纹理文件需要处理");
                Console.WriteLine($"找到 {unique_packs.Length} 个独特纹理包需要处理");
                Console.WriteLine($"找到 {unique_models.Length} 个独特模型需要处理");

                if (unique_texture.Length > 0)
                {
                    (int p, int s) = Shrinker.ShrinkTexture(unique_texture, minSize, maxSize, scaleratio);
                    Console.WriteLine($"处理了 {p} 个纹理，跳过了 {s} 个");
                }
                if (unique_packs.Length > 0)
                {
                    (int p, int s) = Shrinker.ShrinkPack(unique_packs, minSize, maxSize, scaleratio);
                    Console.WriteLine($"处理了 {p} 个纹理包，跳过了 {s} 个");
                }
                if (unique_models.Length > 0)
                {
                    (int p, int s) = Shrinker.ShrinkModel(unique_models, removeOtherUV, removeOtherTextureInfo, removeTangent, removeColor, mergeAllMesh);
                    Console.WriteLine($"处理了 {p} 个模型，跳过了 {s} 个");
                }
                Console.WriteLine($"\n处理完成！");
            }).Wait();
            MessageBox.Show(this, "处理完成！", "成功");
        }
        catch (AggregateException ex)
        {
            // 处理聚合异常，显示所有内部异常信息
            var errorMessages = new System.Text.StringBuilder();
            errorMessages.AppendLine("处理过程中出现以下错误：");
            errorMessages.AppendLine();
            
            int exceptionCount = 1;
            foreach (var innerEx in ex.InnerExceptions)
            {
                errorMessages.AppendLine($"错误 {exceptionCount++}: {innerEx.Message}");
                errorMessages.AppendLine($"堆栈跟踪: {innerEx.StackTrace}");
                errorMessages.AppendLine();
            }
            
            MessageBox.Show(this, errorMessages.ToString(), "Error", MessageBoxButtons.OK, MessageBoxType.Error);
        }
        catch (Exception ex)
        {
            // 处理其他类型的异常
            MessageBox.Show(this, $"Error: {ex.Message}\n\n堆栈跟踪: {ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxType.Error);
        }
    }
}

// 入口点（控制台应用）
public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        new Application().Run(new MainForm());
    }
}