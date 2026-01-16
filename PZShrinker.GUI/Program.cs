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

    // Texture Config TextBoxes
    private readonly TextBox maxSizeBox;
    private readonly TextBox minSizeBox;
    private readonly TextBox scaleRatioBox;

    public MainForm()
    {
        Title = "PZShrinker";
        Size = new Size(400, 450);
        MinimumSize = new Size(400, 450);
        var layout = new DynamicLayout { Padding = new Padding(10), Spacing = new Size(5, 5) };

        // === 文件夹选择部分 ===
        folderPathBox = new TextBox { ReadOnly = true, Width = 250 };
        selectFolderButton = new Button { Text = "Select" };
        selectFolderButton.Click += OnSelectFolderClick;

        layout.AddRow(new TableRow("Workshop:", folderPathBox, selectFolderButton));
        layout.AddSeparateRow(); // 分隔线或空行

        // === Switcher 分组 ===
        var switcherGroup = new GroupBox { Text = "Switcher" };
        var switcherLayout = new TableLayout(2, 3) { Spacing = new Size(10, 5) };

        iconTextureCheck = new CheckBox { Text = "Icon Texture" };
        modelTextureCheck = new CheckBox { Text = "Model Texture" };
        packTextureCheck = new CheckBox { Text = "Tiles Pack" };
        allTextureCheck = new CheckBox { Text = "All Texture" };
        fbxModelCheck = new CheckBox { Text = "FBX Model" };
        d3dModelCheck = new CheckBox { Text = "X Model" };

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
        var removeGroup = new GroupBox { Text = "Model config" };

        removeOtherUVCheck = new CheckBox { Text = "Remove Other UV" };
        removeOtherTextureInfoCheck = new CheckBox { Text = "Remove Embedded textures" };
        removeTangentCheck = new CheckBox { Text = "Remove Tangents" };
        removeColorCheck = new CheckBox { Text = "Remove Vertex Color" };

        var removeChecks = new[]
        {
            removeOtherUVCheck,
            removeOtherTextureInfoCheck,
            removeTangentCheck,
            removeColorCheck
        };

        var removeLayout = new StackLayout { Spacing = 5 };
        foreach (var cb in removeChecks)
            removeLayout.Items.Add(cb);
        removeGroup.Content = removeLayout;
        layout.Add(removeGroup);

        // === Texture Config 分组 ===
        var configGroup = new GroupBox { Text = "Texture Config" };

        maxSizeBox = new TextBox { Width = 80, Text = "512" };
        minSizeBox = new TextBox { Width = 80, Text = "128" };
        scaleRatioBox = new TextBox { Width = 80, Text = "0.25" };

        var configLayout = new TableLayout
        {
            Rows =
            {
                new TableRow(new Label { Text = "Max:" }, maxSizeBox),
                new TableRow(new Label { Text = "Min:" }, minSizeBox),
                new TableRow(new Label { Text = "Scale ratio:" }, scaleRatioBox)
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
                    new Button { Text = "Cancel", Command = new Command((s,e) => Close()) },
                    new Button { Text = "Apply", Command = new Command(OnApply) }
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

            // 解析数字输入（带默认值和错误处理）
            int maxSize = int.TryParse(maxSizeBox.Text, out var max) ? max : 512;
            int minSize = int.TryParse(minSizeBox.Text, out var min) ? min : 128;
            float scaleratio = float.TryParse(scaleRatioBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio) ? ratio : 0.25f;
            Task.Run(() =>
            {
                var di = new DirectoryInfo(path);
                if (!di.Exists)
                {
                    Console.Error.WriteLine($"The path '{path}' does not exist.");
                    return;
                }
                if (maxSize < 0 || minSize < 0 || scaleratio < 0)
                {
                    Console.Error.WriteLine($"Negetive?");
                    return;
                }

                var mods = di.GetDirectories();
                Console.WriteLine($"Found {mods.Length} mods, processing");

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

                Console.WriteLine($"Found {unique_texture.Length} unique textures to process");
                Console.WriteLine($"Found {unique_packs.Length} unique texture packs to process");
                Console.WriteLine($"Found {unique_models.Length} unique unique_models to process");

                if (unique_texture.Length > 0)
                {
                    try
                    {
                        (int p, int s) = Shrinker.ShrinkTexture(unique_texture, minSize, maxSize, scaleratio);
                        Console.WriteLine($"Processed {p} textures, skipped {s}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }
                if (unique_packs.Length > 0)
                {
                    try
                    {
                        (int p, int s) = Shrinker.ShrinkPack(unique_packs, minSize, maxSize, scaleratio);
                        Console.WriteLine($"Processed {p} tiles packs, skipped {s}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }
                if (unique_models.Length > 0)
                {
                    try
                    {
                        (int p, int s) = Shrinker.ShrinkModel(unique_models, removeOtherUV, removeOtherTextureInfo, removeTangent, removeColor);
                        Console.WriteLine($"Processed {p} models, skipped {s}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }
                Console.WriteLine($"\nProcessing complete!");
            }).Wait();
            MessageBox.Show(this, "Done！", "OK");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxType.Error);
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