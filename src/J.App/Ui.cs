using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace J.App;

public sealed partial class Ui
{
    private readonly Control _parent;

    public static string ResourcesDir { get; } =
        Path.Combine(Path.GetDirectoryName(typeof(Ui).Assembly.Location)!, "Resources");

    private double Scale => _parent.DeviceDpi / 96d;

    private readonly Lazy<Font> _boldFont;
    private readonly Lazy<Font> _bigFont;
    private readonly Lazy<Font> _bigBoldFont;
    private readonly Lazy<Font> _textboxFont;

    public Ui(Control parent)
    {
        _parent = parent;

        parent.Font = Font = new("Segoe UI", 10f);
        parent.Disposed += delegate
        {
            Font.Dispose();
        };

        _boldFont = new(() =>
        {
            Font font = new("Segoe UI", 10f, FontStyle.Bold);
            parent.Disposed += delegate
            {
                font.Dispose();
            };
            return font;
        });

        _bigFont = new(() =>
        {
            Font font = new("Segoe UI", 12f);
            parent.Disposed += delegate
            {
                font.Dispose();
            };
            return font;
        });

        _bigBoldFont = new(() =>
        {
            Font font = new("Segoe UI", 12f, FontStyle.Bold);
            parent.Disposed += delegate
            {
                font.Dispose();
            };
            return font;
        });

        _textboxFont = new(() =>
        {
            Font font = new("Consolas", 12f);
            parent.Disposed += delegate
            {
                font.Dispose();
            };
            return font;
        });
    }

    public Font Font { get; }
    public Font BoldFont => _boldFont.Value;
    public Font BigFont => _bigFont.Value;
    public Font BigBoldFont => _bigBoldFont.Value;
    public Font TextBoxFont => _textboxFont.Value;

    public int Unscale(int scaledLength)
    {
        return (int)(scaledLength / Scale);
    }

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

    public readonly int DefaultUnscaledPadding = 12;

    public Padding DefaultPadding => GetPadding(DefaultUnscaledPadding, DefaultUnscaledPadding);

    public Padding TopSpacing => new(0, GetLength(DefaultUnscaledPadding), 0, 0);

    public Padding TopSpacingBig => new(0, GetLength(2 * DefaultUnscaledPadding), 0, 0);

    public Padding BottomSpacing => new(0, 0, 0, GetLength(DefaultUnscaledPadding));

    public Padding BottomSpacingBig => new(0, 0, 0, GetLength(2 * DefaultUnscaledPadding));

    public Padding LeftSpacing => new(GetLength(DefaultUnscaledPadding), 0, 0, 0);

    public Padding RightSpacing => new(0, 0, GetLength(DefaultUnscaledPadding), 0);

    public Padding ButtonSpacing => new(0, 0, GetLength(DefaultUnscaledPadding / 2), 0);

    public (Control Parent, TextBox Child) NewLabeledTextBox(string text, int unscaledWidth)
    {
        var label = NewLabel(text);
        label.Margin += GetPadding(0, 0, 0, 2);
        var textBox = NewTextBox(unscaledWidth);
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
        var label = NewLabel(text);
        label.Margin += GetPadding(0, 0, 0, 2);
        var textBox = NewTextBox(100);
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
        var form = _parent as Form ?? _parent.FindForm()!;
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
        var label = NewLabel(text);
        label.Margin += GetPadding(0, 0, 0, 2);
        var textBox = NewTextBox(100);
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
        var form = _parent as Form ?? _parent.FindForm()!;
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
        var label = NewLabel(text);
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
        return new()
        {
            Text = text,
            AutoSize = true,
            LinkColor = MyColors.Link,
        };
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

            if (e.Item.Pressed)
            {
                using SolidBrush brush = new(MyColors.ToolStripPress);
                g.FillRectangle(brush, bounds);
            }
            else if (e.Item is ToolStripButton button && button.Checked)
            {
                using SolidBrush brush = new(MyColors.ToolStripActive);
                g.FillRectangle(brush, bounds);
            }
            else if (e.Item.Selected)
            {
                using SolidBrush brush = new(MyColors.ToolStripHover);
                g.FillRectangle(brush, bounds);
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
            if (e.Item.Pressed)
            {
                using SolidBrush brush = new(MyColors.ToolStripPress);
                g.FillRectangle(brush, bounds);
            }
            else if (e.Item.Selected)
            {
                using SolidBrush brush = new(MyColors.ToolStripHover);
                g.FillRectangle(brush, bounds);
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
            Color textColor = MyColors.MenuItemText;
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

                if (e.Item is not null && !e.Item.Enabled)
                {
                    image = CreateDisabledImage(image);
                    disposeImage = true;
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
            Graphics g = e.Graphics;

            if (e.Item is not ToolStripMenuItem item)
                return;

            Rectangle fillRect = new(Point.Empty, item.Size);
            if (item.IsOnDropDown)
            {
                fillRect.X += 2;
                fillRect.Width -= 3;
            }

            if ((item.Selected || item.Pressed) && item.Enabled)
            {
                using SolidBrush brush = new(MyColors.ToolStripHover);
                g.FillRectangle(brush, fillRect);
            }

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
                    MyColors.MenuItemText,
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
            BackColor = MyColors.ToolStripBackground,
            ForeColor = MyColors.ToolStripForeground,
            Font = Font,
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

    public MyToolStripTextBox NewToolStripTextBox(int unscaledWidth)
    {
        return new(this, Font)
        {
            AutoSize = false,
            Width = GetLength(unscaledWidth),
            Font = TextBoxFont,
        };
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
        return new MyToolStripSeparator(this) { Margin = GetPadding(5, 5, 5, 5) };
    }

    public ContextMenuStrip NewContextMenuStrip()
    {
        return new() { Renderer = new MyToolStripRenderer(this) };
    }

    public Label NewLabel(string text)
    {
        return new()
        {
            Text = text,
            AutoSize = true,
            Margin = GetPadding(0, 0),
        };
    }

    public ListBox NewListBox()
    {
        return new()
        {
            IntegralHeight = false,
            Dock = DockStyle.Fill,
            Font = BigFont,
        };
    }

    public ProgressBar NewProgressBar(int unscaledWidth)
    {
        return new() { Size = GetSize(unscaledWidth, 12), BackColor = MyColors.ProgressBarBackground };
    }

    public MyTabControl NewTabControl(int unscaledTabWidth)
    {
        return new(BigFont, BigBoldFont)
        {
            Dock = DockStyle.Fill,
            Padding = GetPoint(8, 12),
            SizeMode = TabSizeMode.Fixed,
            ItemSize = GetSize(unscaledTabWidth, 40),
        };
    }

    public TabPage NewTabPage(string text)
    {
        return new MyTabPage { Text = text, UseVisualStyleBackColor = true };
    }

    private sealed class DoubleBufferedDataGridView : DataGridView
    {
        private bool _drawVerticalResizeLine;
        private int _verticalResizeLineX;

        public DoubleBufferedDataGridView()
        {
            DoubleBuffered = true;
            DefaultCellStyle.SelectionForeColor = MyColors.DataGridSelectionForeground;
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
                GridColor = MyColors.DataGridLines,
                Font = BigFont,
            };
        grid.RowTemplate.Height = GetLength(26);
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
            AutoSize = true,
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

    public MyTextBox NewTextBox(int unscaledWidth)
    {
        return new(this)
        {
            AutoSize = true,
            Width = GetLength(unscaledWidth),
            Font = TextBoxFont,
            CueFont = BigFont,
        };
    }

    public CheckBox NewCheckBox(string text)
    {
        return new() { Text = text, AutoSize = true };
    }

    public PictureBox NewPictureBox(Image image)
    {
        return new() { Image = image, SizeMode = PictureBoxSizeMode.AutoSize };
    }

    public NumericUpDown NewNumericUpDown(int unscaledWidth)
    {
        return new() { AutoSize = true, MinimumSize = GetSize(unscaledWidth, 0) };
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
