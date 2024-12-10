using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using J.Core;

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

        if (parent is Form or UserControl)
        {
            parent.BackColor = MyColors.DialogBackground;
        }
    }

    public Font Font { get; }
    public Font BoldFont => _boldFont.Value;
    public Font BigFont => _bigFont.Value;
    public Font BigBoldFont => _bigBoldFont.Value;
    public Font TextBoxFont => _textboxFont.Value;

    /// <summary>
    /// The Button control leaves a one-pixel border around the button. This doesn't scale with DPI!
    /// This property makes it clear what's going on any time we need to use this DPI-independent pixel value.
    /// This "padding" doesn't count in terms of the Padding property, it's purely visual in the paint routine.
    /// </summary>
    public int BuiltInVisualButtonPadding = 1;

    public int Unscale(int scaledLength)
    {
        return (int)(scaledLength / Scale);
    }

    public int GetLength(float unscaledLength)
    {
        var scale = Scale;
        return (int)(unscaledLength * scale);
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

    public (Control Parent, MyTextBox Child) NewLabeledTextBox(string text, int unscaledWidth)
    {
        var textBox = NewTextBox(unscaledWidth);
        var parent = NewLabeledPair(text, textBox);
        return (parent, textBox);
    }

    public (Control Parent, MyTextBox Child) NewLabeledOpenFileTextBox(
        string text,
        int unscaledWidth,
        Action<OpenFileDialog> configure_dialog
    )
    {
        var label = NewLabel(text);
        label.Margin += GetPadding(0, 0, 0, 4);
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

    public (Control Parent, MyTextBox Child) NewLabeledOpenFolderTextBox(
        string text,
        int unscaledWidth,
        Action<FolderBrowserDialog> configure_dialog
    )
    {
        var label = NewLabel(text);
        label.Margin += GetPadding(0, 0, 0, 4);
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
        return NewLabeledPair(text, child, out _);
    }

    public Control NewLabeledPair<T>(string text, T child, out Label label)
        where T : Control
    {
        label = NewLabel(text);
        label.Margin += GetPadding(0, 0, 0, 4);
        var flow = NewFlowColumn();
        flow.Controls.Add(label);
        flow.Controls.Add(child);
        return flow;
    }

    public MyButton NewButton(string text, DialogResult? dialogResult = null)
    {
        MyButton button =
            new()
            {
                Text = text,
                AutoSize = true,
                Padding = GetPadding(24, 8),
                MinimumSize = GetSize(88, 0),
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

    private sealed class ToolStripButtonTabAppearanceTag { }

    private sealed class MyToolStripRenderer(Ui ui) : ToolStripSystemRenderer
    {
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Tag is ToolStripButtonTabAppearanceTag)
            {
                OnRenderTabButtonBackground(e);
                return;
            }

            var g = e.Graphics;
            RectangleF bounds = new(Point.Empty, e.Item.Size);
            bounds.Inflate(ui.GetSize(-2, -2));
            bounds.Offset(0.5f, -0.5f);

            Color color;
            if (e.Item.Pressed)
                color = MyColors.ToolStripPress;
            else if (e.Item is ToolStripButton button && button.Checked)
                color = MyColors.ToolStripActiveBackground;
            else if (e.Item.Selected)
                color = MyColors.ToolStripHover;
            else
                return;

            using SolidBrush brush = new(color);
            var oldSmoothingMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRoundedRectangle(brush, bounds, ui.GetSize(8, 8));
            g.SmoothingMode = oldSmoothingMode;
        }

        private void OnRenderTabButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            if (e.Item is not ToolStripButton button)
                return;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Get the button's bounds
            var bounds = new Rectangle(Point.Empty, e.Item.Size);
            bounds.Y += ui.GetLength(2);

            using (var path = CreateTabPath(bounds))
            {
                Color bgColor;
                Color textColor;

                // Determine colors based on button state
                if (button.Checked)
                {
                    bgColor = MyColors.ToolStripActiveBackground;
                    textColor = MyColors.ToolStripTabButtonTextActive;
                }
                else if (button.Pressed)
                {
                    bgColor = MyColors.ToolStripPress;
                    textColor = MyColors.ToolStripTabButtonTextActive;
                }
                else if (button.Selected)
                {
                    bgColor = MyColors.ToolStripHover;
                    textColor = MyColors.ToolStripTabButtonTextActive;
                }
                else
                {
                    bgColor = MyColors.ToolStripTabButtonTabInactiveBg;
                    textColor = MyColors.ToolStripTabButtonTextInactive;
                }

                // Fill background
                using (var brush = new SolidBrush(bgColor))
                {
                    g.TranslateTransform(0.5f, 0.5f);
                    g.FillPath(brush, path);
                    g.ResetTransform();
                }

                // Update text color
                // TODO: shouldn't be setting this in a render method
                button.ForeColor = textColor;
            }

            GraphicsPath CreateTabPath(Rectangle bounds)
            {
                GraphicsPath path = new();
                var radius = ui.GetLength(6); // Radius for rounded corners

                // Create a path for a rectangle with rounded top corners and flat bottom
                path.StartFigure();
                path.AddLine(bounds.Left, bounds.Bottom, bounds.Left, bounds.Top + radius);
                path.AddArc(bounds.Left, bounds.Top, radius * 2, radius * 2, 180, 90);
                path.AddLine(bounds.Left + radius, bounds.Top, bounds.Right - 1 - radius, bounds.Top);
                path.AddArc(bounds.Right - 1 - radius * 2, bounds.Top, radius * 2, radius * 2, 270, 90);
                path.AddLine(bounds.Right - 1, bounds.Top + radius, bounds.Right - 1, bounds.Bottom);
                path.CloseFigure();

                return path;
            }
        }

        protected override void OnRenderItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Tag is ToolStripButtonTabAppearanceTag)
            {
                // Handled in OnRenderTabButtonBackground.
                return;
            }

            base.OnRenderItemBackground(e);
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            RectangleF bounds = new(Point.Empty, e.Item.Size);
            bounds.Inflate(ui.GetSize(-2, -2));
            bounds.Offset(0.5f, -0.5f);

            var g = e.Graphics;
            Color color;
            if (e.Item.Pressed)
                color = MyColors.ToolStripPress;
            else if (e.Item.Selected)
                color = MyColors.ToolStripHover;
            else if (e.Item.BackColor != Control.DefaultBackColor)
                color = e.Item.BackColor;
            else
                return;

            using SolidBrush brush = new(color);
            var oldSmoothingMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRoundedRectangle(brush, bounds, ui.GetSize(8, 8));
            g.SmoothingMode = oldSmoothingMode;
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
            Color textColor;
            if (e.Item is ToolStripButton b && b.Checked)
                textColor = MyColors.ToolStripActiveForeground;
            else if (e.Item is ToolStripDropDownButton)
                textColor = e.Item.Selected || e.Item.Pressed ? Color.White : e.Item.ForeColor;
            else
                textColor = MyColors.MenuItemText;
            Font? textFont = e.TextFont;
            string? text = e.Text;
            Rectangle textRect = e.TextRectangle;

            if (e.Item.Tag is ToolStripButtonTabAppearanceTag)
                textRect.Offset(ui.GetPoint(7, 1));
            else if (e.Item.Owner!.IsDropDown)
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

            if (e.Item.Tag is ToolStripButtonTabAppearanceTag)
                imageRect.Offset(ui.GetPoint(5, 2));
            else if (e.Item.DisplayStyle == ToolStripItemDisplayStyle.ImageAndText)
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
                    else if (
                        (e.Item.Tag is ToolStripButtonTabAppearanceTag && ((ToolStripButton)e.Item).Checked)
                        || (e.Item is ToolStripDropDownButton b && ArrowShouldBeBlack(b))
                    )
                    {
                        Bitmap copy = new(image);
                        image = ui.InvertColorsInPlace(copy);
                        disposeImage = true;
                    }
                }

                e.Graphics.DrawImage(image, imageRect, new Rectangle(Point.Empty, imageRect.Size), GraphicsUnit.Pixel);

                if (disposeImage)
                    image.Dispose();
            }

            bool ArrowShouldBeBlack(ToolStripDropDownButton b)
            {
                const bool WHITE = false;
                const bool BLACK = true;

                // When hovered, the hover background is a dark gray and the foreground is white.
                if (b.Selected)
                    return WHITE;

                // This state also applies to the drop-down button when the user is hovering one of its submenu items.
                if (b.Pressed)
                    return WHITE;

                // When the Sort/Filter buttons are indicating that a non-default setting is active, the background
                // is white and the foreground is black.
                if (e.Item.Tag is bool t && t)
                    return BLACK;

                return WHITE;
            }
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            base.OnRenderToolStripBackground(e);
            e.Graphics.Clear(MyColors.ToolStripBackground);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) { }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Graphics g = e.Graphics;

            if (e.Item is not ToolStripMenuItem item)
                return;

            if ((item.Selected || item.Pressed) && item.Enabled)
            {
                RectangleF fillRect = new(Point.Empty, item.Size);
                if (item.IsOnDropDown)
                {
                    fillRect.X += 2;
                    fillRect.Width -= 3;
                }

                fillRect.Inflate(ui.GetSize(-2, -2));
                fillRect.Offset(0.5f, -0.5f);

                using SolidBrush brush = new(MyColors.ToolStripHover);
                var oldSmoothingMode = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillRoundedRectangle(brush, fillRect, ui.GetSize(8, 8));
                g.SmoothingMode = oldSmoothingMode;
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

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.White;
            base.OnRenderArrow(e);
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

    public ToolStripButton NewToolStripTabButton(string text)
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
            Tag = new ToolStripButtonTabAppearanceTag(),
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
        return new() { Renderer = new MyToolStripRenderer(this), Font = Font };
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
        return new()
        {
            Size = GetSize(unscaledWidth, 12),
            BackColor = MyColors.ProgressBarBackground,
            ForeColor = MyColors.ProgressBarForeground,
        };
    }

    public MyTabControl NewTabControl(int unscaledTabWidth)
    {
        return new(BigFont, BigBoldFont)
        {
            Dock = DockStyle.Fill,
            SizeMode = TabSizeMode.Fixed,
            ItemSize = GetSize(unscaledTabWidth, 30),
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
                BorderStyle = BorderStyle.Fixed3D,
                CellBorderStyle = DataGridViewCellBorderStyle.None,
                GridColor = MyColors.DataGridLines,
                BackgroundColor = MyColors.DataGridBackground,
                Font = Font,
            };
        grid.AlternatingRowsDefaultCellStyle.BackColor = MyColors.DataGridAlternateRowBackground;
        grid.DefaultCellStyle.BackColor = MyColors.DataGridRowBackground;
        grid.DefaultCellStyle.Padding += GetPadding(5, 0, 0, 0);
        grid.RowTemplate.Height = GetLength(32);
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

    public ComboBox NewDropDownList(int unscaledWidth)
    {
        return new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            AutoSize = true,
            Width = GetLength(unscaledWidth),
        };
    }

    public ComboBox NewAutoCompleteDropDown(int unscaledWidth)
    {
        return new()
        {
            AutoCompleteMode = AutoCompleteMode.Suggest,
            AutoCompleteSource = AutoCompleteSource.ListItems,
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

    public MyTextBox NewWordWrapTextbox(int unscaledWidth, int unscaledHeight)
    {
        MyTextBox textBox =
            new(this)
            {
                Size = GetSize(unscaledWidth, unscaledHeight),
                Font = TextBoxFont,
                Multiline = true,
                WordWrap = true,
                CueFont = BigFont,
            };

        textBox.KeyDown += static (sender, e) =>
        {
            // Prevent Enter key from creating new lines
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
        };

        textBox.KeyPress += static (sender, e) =>
        {
            // Prevent manual insertion of newline characters
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                return;
            }
        };

        textBox.TextChanged += static (sender, e) =>
        {
            var textBox = (TextBox)sender!;

            // Remove any newline characters that might have been pasted
            if (textBox.Text.Contains(Environment.NewLine))
            {
                textBox.Text = textBox.Text.Replace(Environment.NewLine, " ");
                textBox.SelectionStart = textBox.Text.Length; // Move cursor to end
            }
        };

        return textBox;
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

    public MyWebView2 NewWebView2()
    {
        MyWebView2 browser =
            new()
            {
                Dock = DockStyle.Fill,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = MyColors.MainFormBackground,
            };

        return browser;
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
