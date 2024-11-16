using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace J.App;

public sealed partial class Ui(Control parent)
{
    public string ResourcesDir { get; } =
        Path.Combine(Path.GetDirectoryName(typeof(Ui).Assembly.Location)!, "Resources");

    private double Scale => parent.DeviceDpi / 96d;

    public int GetLength(int unscaledLength)
    {
        var scale = Scale;
        return (int)(unscaledLength * scale);
    }

    public Size GetSize(int unscaledWidth, int unscaledHeight)
    {
        var scale = Scale;
        return new((int)(unscaledWidth * scale), (int)(unscaledHeight * scale));
    }

    public Padding GetPadding(int unscaledHorizontal, int unscaledVertical)
    {
        var scale = Scale;
        var scaledX = (int)(unscaledHorizontal * scale);
        var scaledY = (int)(unscaledVertical * scale);
        return new(scaledX, scaledY, scaledX, scaledY);
    }

    public Point GetPoint(int unscaledHorizontal, int unscaledVertical)
    {
        var scale = Scale;
        var scaledX = (int)(unscaledHorizontal * scale);
        var scaledY = (int)(unscaledVertical * scale);
        return new(scaledX, scaledY);
    }

    public Padding GetPadding(int unscaledLeft, int unscaledTop, int unscaledRight, int unscaledBottom)
    {
        var scale = Scale;
        return new(
            (int)(unscaledLeft * scale),
            (int)(unscaledTop * scale),
            (int)(unscaledRight * scale),
            (int)(unscaledBottom * scale)
        );
    }

    public readonly int DefaultUnscaledPadding = 8;

    public Padding DefaultPadding => GetPadding(DefaultUnscaledPadding, DefaultUnscaledPadding);

    public Padding TopSpacing => new(0, GetLength(DefaultUnscaledPadding), 0, 0);

    public Padding TopSpacingBig => new(0, GetLength(2 * DefaultUnscaledPadding), 0, 0);

    public Padding BottomSpacing => new(0, 0, 0, GetLength(DefaultUnscaledPadding));

    public Padding BottomSpacingBig => new(0, 0, 0, GetLength(2 * DefaultUnscaledPadding));

    public Padding LeftSpacing => new(GetLength(DefaultUnscaledPadding), 0, 0, 0);

    public Padding RightSpacing => new(0, 0, GetLength(DefaultUnscaledPadding), 0);

    public Font NewBigFont() => new("Segoe UI", 11f);

    public void SetBigFont(Control control)
    {
        var font = NewBigFont();
        control.Disposed += delegate
        {
            font.Dispose();
        };
        control.Font = font;
    }

    public (Control Parent, TextBox Child) NewLabeledTextBox(string text, int unscaledWidth)
    {
        Label label = new() { Text = text, AutoSize = true };
        label.Margin += GetPadding(0, 0, 0, 2);
        TextBox textBox = new() { Width = GetLength(unscaledWidth) };
        var flow = NewFlowColumn();
        flow.Controls.Add(label);
        flow.Controls.Add(textBox);
        return (flow, textBox);
    }

    public (Control Parent, TextBox Child) NewLabeledOpenFileTextBox(
        string text,
        int unscaledWidth,
        Action<OpenFileDialog> configure_dialog
    )
    {
        Label label = new() { Text = text, AutoSize = true };
        label.Margin += GetPadding(0, 0, 0, 2);
        TextBox textBox = new() { };
        var flow = NewFlowColumn();
        flow.Dock = DockStyle.Fill;
        flow.Controls.Add(label);
        flow.Controls.Add(textBox);
        var button = NewButton("Browse...");
        button.Dock = DockStyle.Bottom;
        button.Margin += GetPadding(4, 0, 0, 0);
        var table = NewTable(2, 1);
        table.Width = GetLength(unscaledWidth);
        table.MaximumSize = GetSize(unscaledWidth, 100);
        table.ColumnStyles[0].SizeType = SizeType.Percent;
        table.ColumnStyles[0].Width = 100;
        table.Controls.Add(flow, 0, 0);
        table.Controls.Add(button, 1, 0);
        var form = parent as Form ?? parent.FindForm()!;
        form.Load += delegate
        {
            textBox.Width = GetLength(unscaledWidth - 15) - button.Width;
        };
        button.Click += delegate
        {
            using OpenFileDialog dialog = new();
            configure_dialog(dialog);
            if (dialog.ShowDialog() == DialogResult.OK)
                textBox.Text = dialog.FileName;
        };
        return (table, textBox);
    }

    public (Control Parent, TextBox Child) NewLabeledOpenFolderTextBox(
        string text,
        int unscaledWidth,
        Action<FolderBrowserDialog> configure_dialog
    )
    {
        Label label = new() { Text = text, AutoSize = true };
        label.Margin += GetPadding(0, 0, 0, 2);
        TextBox textBox = new() { };
        var flow = NewFlowColumn();
        flow.Dock = DockStyle.Fill;
        flow.Controls.Add(label);
        flow.Controls.Add(textBox);
        var button = NewButton("Browse...");
        button.Dock = DockStyle.Bottom;
        button.Margin += GetPadding(4, 0, 0, 0);
        var table = NewTable(2, 1);
        table.Width = GetLength(unscaledWidth);
        table.MaximumSize = GetSize(unscaledWidth, 100);
        table.ColumnStyles[0].SizeType = SizeType.Percent;
        table.ColumnStyles[0].Width = 100;
        table.Controls.Add(flow, 0, 0);
        table.Controls.Add(button, 1, 0);
        var form = parent as Form ?? parent.FindForm()!;
        form.Load += delegate
        {
            textBox.Width = GetLength(unscaledWidth - 15) - button.Width;
        };
        button.Click += delegate
        {
            using FolderBrowserDialog dialog = new();
            configure_dialog(dialog);
            if (dialog.ShowDialog() == DialogResult.OK)
                textBox.Text = dialog.SelectedPath;
        };
        return (table, textBox);
    }

    public Control NewLabeledPair<T>(string text, T child)
        where T : Control
    {
        Label label = new() { Text = text, AutoSize = true };
        label.Margin += GetPadding(0, 0, 0, 2);
        var flow = NewFlowColumn();
        flow.Controls.Add(label);
        flow.Controls.Add(child);
        return flow;
    }

    public Button NewButton(string text, DialogResult? dialogResult = null)
    {
        Button button =
            new()
            {
                Text = text,
                AutoSize = true,
                Padding = GetPadding(20, 5),
                UseVisualStyleBackColor = true,
            };
        if (dialogResult.HasValue)
            button.DialogResult = dialogResult.Value;
        return button;
    }

    public LinkLabel NewLinkLabel(string text)
    {
        return new() { Text = text, AutoSize = true };
    }

    public FlowLayoutPanel NewFlowRow()
    {
        return new()
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
        };
    }

    public FlowLayoutPanel NewFlowColumn()
    {
        return new()
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
        };
    }

    public TableLayoutPanel NewTable(int columns, int rows)
    {
        TableLayoutPanel table =
            new()
            {
                RowCount = rows,
                ColumnCount = columns,
                AutoSize = true,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                Dock = DockStyle.Fill,
            };
        for (int i = 0; i < columns; i++)
            table.ColumnStyles.Add(new(SizeType.AutoSize));
        for (var i = 0; i < rows; i++)
            table.RowStyles.Add(new(SizeType.AutoSize));
        return table;
    }

    public Icon GetIconResource(string filename)
    {
        var iconPath = Path.Combine(ResourcesDir, filename);
        return new(iconPath);
    }

    public Bitmap GetScaledBitmapResource(string filename, int unscaledWidth, int unscaledHeight)
    {
        var scaledSize = GetSize(unscaledWidth, unscaledHeight);
        var bitmapPath = Path.Combine(ResourcesDir, filename);
        using Bitmap originalBitmap = new(bitmapPath);
        return new(originalBitmap, scaledSize);
    }

    private sealed class MyToolStripRenderer(Ui ui) : ToolStripSystemRenderer
    {
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            Rectangle bounds = new(Point.Empty, e.Item.Size);

            if (e.Item.Pressed || e.Item.Selected)
            {
                g.FillRectangle(SystemBrushes.Highlight, bounds);
            }
            else if (e.Item is ToolStripButton button && button.Checked)
            {
                g.FillRectangle(Brushes.Gray, bounds);
            }
            else if (e.Item.BackColor != Control.DefaultBackColor)
            {
                using SolidBrush brush = new(e.Item.BackColor);
                g.FillRectangle(brush, bounds);
            }
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            Rectangle bounds = new(Point.Empty, e.Item.Size);
            if (e.Item.Pressed || e.Item.Selected)
            {
                g.FillRectangle(SystemBrushes.Highlight, bounds);
            }
            else if (e.Item.BackColor != Control.DefaultBackColor)
            {
                using SolidBrush brush = new(e.Item.BackColor);
                g.FillRectangle(brush, bounds);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is ToolStripLabel)
            {
                base.OnRenderItemText(e);
                return;
            }

            ArgumentNullException.ThrowIfNull(e);

            ToolStripItem? item = e.Item;
            Graphics g = e.Graphics;
            Color textColor = e.TextColor;
            Font? textFont = e.TextFont;
            string? text = e.Text;
            Rectangle textRect = e.TextRectangle;

            if (e.Item.Owner!.IsDropDown)
                textRect.Offset(ui.GetPoint(0, 5));
            else if (e.Item.DisplayStyle == ToolStripItemDisplayStyle.ImageAndText)
                textRect.Offset(ui.GetPoint(7, 0));
            else
                textRect.Offset(ui.GetPoint(5, 0));

            textColor = (item is not null && item.Enabled) ? textColor : SystemColors.GrayText;

            TextRenderer.DrawText(g, text, textFont, textRect, textColor, e.TextFormat);
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);

            Rectangle imageRect = e.ImageRectangle;
            if (e.Item.DisplayStyle == ToolStripItemDisplayStyle.ImageAndText)
                imageRect.Offset(ui.GetPoint(5, 0));
            Image? image = e.Image;

            if (imageRect != Rectangle.Empty && image is not null)
            {
                var disposeImage = false;

                if (e.Item is not null)
                {
                    if (!e.Item.Enabled)
                    {
                        image = CreateDisabledImage(image);
                        disposeImage = true;
                    }
                    else if (e.Item.Owner!.IsDropDown && (e.Item.Pressed || e.Item.Selected))
                    {
                        Bitmap bitmap = new(image);
                        ui.InvertColorsInPlace(bitmap);
                        image = bitmap;
                        disposeImage = true;
                    }
                }

                e.Graphics.DrawImage(image, imageRect, new Rectangle(Point.Empty, imageRect.Size), GraphicsUnit.Pixel);

                if (disposeImage)
                    image.Dispose();
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) { }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            base.OnRenderMenuItemBackground(e);

            if (e.Item is ToolStripMenuItem mi && mi.Checked)
            {
                var checkmark = "✔";

                var textRect = mi.ContentRectangle;
                textRect.Width = (int)e.Graphics.MeasureString(checkmark, mi.Font).Width;
                textRect.X = mi.ContentRectangle.Left + ui.GetLength(10);

                TextRenderer.DrawText(
                    e.Graphics,
                    checkmark,
                    mi.Font,
                    textRect,
                    mi.Selected || mi.Pressed ? SystemColors.HighlightText : SystemColors.MenuText,
                    TextFormatFlags.VerticalCenter
                );
            }
        }
    }

    public ToolStrip NewToolStrip()
    {
        return new()
        {
            Padding = Padding.Empty,
            Renderer = new MyToolStripRenderer(this),
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
        };
    }

    public ToolStripButton NewToolStripButton(string text, bool imageOnly = false)
    {
        return new()
        {
            Text = text,
            DisplayStyle = imageOnly ? ToolStripItemDisplayStyle.Image : ToolStripItemDisplayStyle.ImageAndText,
            AutoSize = true,
            Padding = GetPadding(imageOnly ? 10 : 9, 6),
            Margin = imageOnly ? Padding.Empty : GetPadding(5, 0, 5, 0),
            TextAlign = ContentAlignment.MiddleCenter,
            ImageAlign = ContentAlignment.MiddleCenter,
            AutoToolTip = false,
        };
    }

    public ToolStripDropDownButton NewToolStripDropDownButton(string text)
    {
        return new()
        {
            Text = text,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            AutoSize = true,
            Padding = GetPadding(9, 6),
            Margin = GetPadding(5, 0, 5, 0),
            TextAlign = ContentAlignment.MiddleCenter,
            ImageAlign = ContentAlignment.MiddleCenter,
            AutoToolTip = false,
            ShowDropDownArrow = false,
        };
    }

    public ToolStripTextBox NewToolStripTextBox(int unscaledWidth)
    {
        var font = NewBigFont();
        ToolStripTextBox box =
            new()
            {
                AutoSize = false,
                Width = GetLength(unscaledWidth),
                Font = font,
            };
        box.Disposed += delegate
        {
            font.Dispose();
        };
        return box;
    }

    public ToolStripMenuItem NewToolStripMenuItem(string text)
    {
        return new()
        {
            Text = text,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            ImageAlign = ContentAlignment.MiddleCenter,
            AutoToolTip = false,
            Padding = GetPadding(0, 6),
        };
    }

    public ToolStripLabel NewToolStripLabel(string text)
    {
        return new()
        {
            Text = text,
            AutoSize = true,
            Padding = GetPadding(0, 6),
        };
    }

    private sealed class MyToolStripSeparator(Ui ui) : ToolStripSeparator
    {
        public override Size GetPreferredSize(Size constrainingSize) => new(ui.GetLength(50), ui.GetLength(16));
    }

    public ToolStripSeparator NewToolStripSeparator()
    {
        return new MyToolStripSeparator(this) { Margin = GetPadding(5, 0, 5, 0) };
    }

    public ContextMenuStrip NewContextMenuStrip()
    {
        return new() { Renderer = new ToolStripSystemRenderer() };
    }

    public Label NewLabel(string text)
    {
        return new() { Text = text, AutoSize = true };
    }

    public ListBox NewListBox()
    {
        var font = NewBigFont();
        ListBox listBox =
            new()
            {
                IntegralHeight = false,
                Dock = DockStyle.Fill,
                Font = font,
            };
        listBox.Disposed += delegate
        {
            font.Dispose();
        };
        return listBox;
    }

    public ProgressBar NewProgressBar(int unscaledWidth)
    {
        return new() { Size = GetSize(unscaledWidth, 12) };
    }

    public TabControl NewTabControl()
    {
        return new() { Dock = DockStyle.Fill, Padding = GetPoint(8, 4) };
    }

    public TabPage NewTabPage(string text)
    {
        return new(text) { UseVisualStyleBackColor = true };
    }

    private sealed class DoubleBufferedDataGridView : DataGridView
    {
        private bool _drawVerticalResizeLine;
        private int _verticalResizeLineX;

        public DoubleBufferedDataGridView()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_drawVerticalResizeLine)
            {
                using Pen pen = new(GridColor) { DashStyle = DashStyle.Dot };
                e.Graphics.DrawLine(
                    pen,
                    _verticalResizeLineX,
                    ColumnHeadersHeight,
                    _verticalResizeLineX,
                    ClientSize.Height
                );
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Button == MouseButtons.Left && Cursor == Cursors.SizeWE)
            {
                _drawVerticalResizeLine = true;
                _verticalResizeLineX = e.X;
                Invalidate();
            }
            else
            {
                _drawVerticalResizeLine = false;
                Invalidate();
            }
        }
    }

    public DataGridView NewDataGridView()
    {
        var font = NewBigFont();
        DoubleBufferedDataGridView grid =
            new()
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = false,
                AllowUserToResizeColumns = true,
                AllowUserToResizeRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                GridColor = Color.LightGray,
                Font = font,
            };
        grid.RowTemplate.Height = GetLength(26);
        grid.Disposed += delegate
        {
            font.Dispose();
        };
        FixRightClickSelection(grid);
        return grid;
    }

    private static void FixRightClickSelection(DataGridView dataGridView)
    {
        // Handle mouse down event to update selection before context menu shows
        dataGridView.MouseDown += (sender, e) =>
        {
            // Only handle right mouse button
            if (e.Button != MouseButtons.Right)
                return;

            // Get the cell under the mouse cursor
            DataGridView.HitTestInfo hitTest = dataGridView.HitTest(e.X, e.Y);

            if (hitTest.Type == DataGridViewHitTestType.Cell)
            {
                // We clicked on a cell (not header or empty space).
                var clickedRow = dataGridView.Rows[hitTest.RowIndex];
                var clickedCell = clickedRow.Cells[hitTest.ColumnIndex];

                // Check if the clicked location is already selected
                bool isAlreadySelected =
                    dataGridView.SelectionMode == DataGridViewSelectionMode.FullRowSelect
                        ? clickedRow.Selected // For full row selection, check row
                        : clickedCell.Selected; // For cell selection, check cell

                // Only modify selection if the clicked location isn't already selected
                if (isAlreadySelected)
                    return;

                // Clear any existing selection if not holding Ctrl or Shift
                dataGridView.ClearSelection();

                // Select the appropriate element
                if (dataGridView.SelectionMode == DataGridViewSelectionMode.FullRowSelect)
                    clickedRow.Selected = true;
                else
                    clickedCell.Selected = true;

                // Set the current cell to the clicked cell
                try
                {
                    dataGridView.CurrentCell = clickedCell;
                }
                catch { }
            }
            else if (hitTest.Type == DataGridViewHitTestType.None)
            {
                // We clicked in the background (no rows/cells).
                dataGridView.ClearSelection();
                try
                {
                    dataGridView.CurrentCell = null;
                }
                catch { }
            }
        };
    }

    public Bitmap InvertColorsInPlace(Bitmap source)
    {
        var data = source.LockBits(
            new Rectangle(0, 0, source.Width, source.Height),
            ImageLockMode.ReadWrite,
            PixelFormat.Format32bppArgb
        );
        unsafe
        {
            byte* ptr = (byte*)data.Scan0;
            int bytes = Math.Abs(data.Stride) * source.Height;
            for (int i = 0; i < bytes; i += 4)
            {
                ptr[i] = (byte)(255 - ptr[i]);
                ptr[i + 1] = (byte)(255 - ptr[i + 1]);
                ptr[i + 2] = (byte)(255 - ptr[i + 2]);
            }
        }
        source.UnlockBits(data);
        return source;
    }

    public Panel NewPanel() => new() { };

    public ComboBox NewDropDown(int unscaledWidth)
    {
        return new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            AutoSize = false,
            Width = GetLength(unscaledWidth),
        };
    }

    public GroupBox NewGroupBox(string text)
    {
        return new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Text = text,
        };
    }

    public TextBox NewTextBox(int unscaledWidth)
    {
        return new() { AutoSize = true, Width = GetLength(unscaledWidth) };
    }

    public CheckBox NewCheckBox(string text)
    {
        return new() { Text = text, AutoSize = true };
    }

    public void SetCueText(TextBox textBox, string cueText)
    {
        NativeMethods.SendMessageW(textBox.Handle, NativeMethods.EM_SETCUEBANNER, IntPtr.Zero, cueText);
    }

    public PictureBox NewPictureBox(Image image)
    {
        return new() { Image = image, SizeMode = PictureBoxSizeMode.AutoSize };
    }

    public NumericUpDown NewNumericUpDown(int unscaledWidth)
    {
        return new() { AutoSize = true, MinimumSize = GetSize(unscaledWidth, 0) };
    }

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
        public static partial IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, string lParam);

        public const uint EM_SETCUEBANNER = 0x1501;
    }
}

public static class UiExtensions
{
    public static T AddPair<T>(
        this TableLayoutPanel table,
        int column,
        int row,
        (Control Parent, T Child) pair,
        int columnSpan = 1
    )
        where T : Control
    {
        table.Controls.Add(pair.Parent, column, row);
        table.SetColumnSpan(pair.Parent, columnSpan);
        return pair.Child;
    }
}
