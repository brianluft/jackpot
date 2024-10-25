﻿using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Web;
using J.Core.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.WinForms;

namespace J.App;

public sealed partial class MainForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly Client _client;
    private readonly M3u8FolderSync _m3u8FolderSync;
    private readonly ImportProgressFormFactory _importProgressFormFactory;
    private readonly Ui _ui;
    private readonly ToolStrip _toolStrip;
    private readonly EdgePanel _leftPanel,
        _rightPanel;
    private readonly ToolStripDropDownButton _menuButton,
        _filterButton;
    private readonly ToolStripMenuItem _connectButton,
        _disconnectButton,
        _accountSettingsButton,
        _addToLibraryButton,
        _editTagsButton,
        _manageMoviesButton,
        _moviesButton,
        _filterAndButton,
        _filterOrButton;
    private readonly ToolStripButton _homeButton,
        _minimizeButton,
        _fullscreenButton,
        _exitButton,
        _browseBackButton,
        _browseForwardButton,
        _shuffleButton;
    private readonly ToolStripLabel _titleLabel,
        _pageLabel;
    private readonly WebView2 _browser;
    private readonly List<FilterRule> _filterRules = [];
    private bool _filterOr = false;
    private readonly System.Windows.Forms.Timer _edgeHideTimer;
    private int _pageCount;
    private bool _keepEdgeUiShowing;
    private bool _importInProgress;

    public MainForm(
        IServiceProvider serviceProvider,
        LibraryProviderAdapter libraryProvider,
        Client client,
        M3u8FolderSync m3u8FolderSync,
        ImportProgressFormFactory importProgressFormFactory
    )
    {
        _serviceProvider = serviceProvider;
        _libraryProvider = libraryProvider;
        _client = client;
        _m3u8FolderSync = m3u8FolderSync;
        _importProgressFormFactory = importProgressFormFactory;
        Ui ui = new(this);
        _ui = ui;

        _edgeHideTimer = new() { Interval = 2000, Enabled = false };
        {
            _edgeHideTimer.Tick += EdgeHideTimer_Tick;
        }

        _browser = new()
        {
            Dock = DockStyle.Fill,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
        };

        Controls.Add(_leftPanel = new(left: true));
        {
            _leftPanel.Visible = false;
            _leftPanel.Dock = DockStyle.Left;
            _leftPanel.Width = ui.GetLength(25);
            _leftPanel.ShortJump += PagePreviousButton_Click;
            _leftPanel.LongJump += PageFirstButton_Click;
        }

        Controls.Add(_rightPanel = new(left: false));
        {
            _rightPanel.Visible = false;
            _rightPanel.Dock = DockStyle.Right;
            _rightPanel.Width = ui.GetLength(25);
            _rightPanel.ShortJump += PageNextButton_Click;
            _rightPanel.LongJump += PageLastButton_Click;
        }

        Controls.Add(_toolStrip = ui.NewToolStrip());
        {
            _toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            _toolStrip.Visible = false;

            _toolStrip.Items.Add(_exitButton = ui.NewToolStripButton("Exit", true));
            {
                _exitButton.Alignment = ToolStripItemAlignment.Right;
                _exitButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Exit.png", 16, 16));
                _exitButton.Click += delegate
                {
                    Application.Exit();
                };
            }

            _toolStrip.Items.Add(_fullscreenButton = ui.NewToolStripButton("Fullscreen", true));
            {
                _fullscreenButton.Alignment = ToolStripItemAlignment.Right;
                var reduceImage = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Reduce.png", 16, 16));
                var enlargeImage = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Enlarge.png", 16, 16));
                _fullscreenButton.Image = reduceImage;
                _fullscreenButton.Click += delegate
                {
                    if (FormBorderStyle == FormBorderStyle.Sizable)
                    {
                        // Entering fullscreen
                        if (WindowState == FormWindowState.Maximized)
                            WindowState = FormWindowState.Normal;
                        _minimizeButton!.Visible = true;
                        _exitButton.Visible = true;
                        _fullscreenButton.Image = reduceImage;
                        FormBorderStyle = FormBorderStyle.None;
                        WindowState = FormWindowState.Maximized;
                    }
                    else
                    {
                        // Leaving fullscreen
                        _minimizeButton!.Visible = false;
                        _exitButton.Visible = false;
                        _toolStrip.Visible = true;
                        _leftPanel.Visible = true;
                        _rightPanel.Visible = true;
                        _edgeHideTimer.Stop();
                        _fullscreenButton.Image = enlargeImage;
                        FormBorderStyle = FormBorderStyle.Sizable;
                        WindowState = FormWindowState.Normal;
                        Icon = ui.GetIconResource("App.ico");
                        ShowIcon = true;
                    }
                };
            }

            _toolStrip.Items.Add(_minimizeButton = ui.NewToolStripButton("Minimize", true));
            {
                _minimizeButton.Alignment = ToolStripItemAlignment.Right;
                _minimizeButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Minimize.png", 16, 16));
                _minimizeButton.Click += delegate
                {
                    WindowState = FormWindowState.Minimized;
                };
            }

            var separator1 = ui.NewToolStripSeparator();
            {
                _toolStrip.Items.Add(separator1);
                separator1.Alignment = ToolStripItemAlignment.Right;
            }

            _toolStrip.Items.Add(_pageLabel = ui.NewToolStripLabel(""));
            {
                _pageLabel.Alignment = ToolStripItemAlignment.Right;
                var pageLabelHeight = _pageLabel.Height;
                _pageLabel.AutoSize = false;
                _pageLabel.Size = ui.GetSize(75, pageLabelHeight);
                _pageLabel.TextAlign = ContentAlignment.MiddleCenter;
            }

            var separator2 = ui.NewToolStripSeparator();
            {
                _toolStrip.Items.Add(separator2);
                separator2.Alignment = ToolStripItemAlignment.Right;
            }

            _toolStrip.Items.Add(_shuffleButton = ui.NewToolStripButton("Shuffle"));
            {
                _shuffleButton.Alignment = ToolStripItemAlignment.Right;
                _shuffleButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Shuffle.png", 16, 16));
                _shuffleButton.Checked = true;
                _shuffleButton.Click += ShuffleButton_Click;
            }

            _toolStrip.Items.Add(_filterButton = ui.NewToolStripDropDownButton("Filter"));
            {
                _filterButton.Alignment = ToolStripItemAlignment.Right;
                _filterButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Filter.png", 16, 16));
                _filterButton.DropDownOpened += FilterButton_DropDownOpened;
                _filterButton.DropDownClosed += FilterButton_DropDownClosed;

                _filterButton.DropDownItems.Add(ui.NewToolStripSeparator());

                _filterButton.DropDownItems.Add(_filterAndButton = ui.NewToolStripMenuItem("AND"));
                {
                    _filterAndButton.Checked = true;
                    _filterAndButton.Click += FilterAndButton_Click;
                }

                _filterButton.DropDownItems.Add(_filterOrButton = ui.NewToolStripMenuItem("OR"));
                {
                    _filterOrButton.Click += FilterOrButton_Click;
                }

                _filterButton.DropDownItems.Add(ui.NewToolStripSeparator());
            }

            _toolStrip.Items.Add(_menuButton = ui.NewToolStripDropDownButton("Menu"));
            {
                _menuButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
                _menuButton.Margin = _menuButton.Margin with { Left = 0 };
                _menuButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Menu.png", 16, 16));
                _menuButton.DropDownOpened += MenuButton_DropDownOpened;
                _menuButton.DropDownClosed += MenuButton_DropDownClosed;

                _menuButton.DropDownItems.Add(_moviesButton = ui.NewToolStripMenuItem("Movies"));
                {
                    _moviesButton.Enabled = false;
                    _moviesButton.Image = ui.GetScaledBitmapResource("Movie.png", 16, 16);
                    _moviesButton.Click += MoviesButton_Click;
                }

                _menuButton.DropDownItems.Add(ui.NewToolStripSeparator());

                _menuButton.DropDownItems.Add(_addToLibraryButton = ui.NewToolStripMenuItem("Add Movies..."));
                {
                    _addToLibraryButton.Enabled = false;
                    _addToLibraryButton.Image = ui.GetScaledBitmapResource("Add.png", 16, 16);
                    _addToLibraryButton.Click += AddToLibraryButton_Click;
                }

                _menuButton.DropDownItems.Add(_manageMoviesButton = ui.NewToolStripMenuItem("Edit Movies..."));
                {
                    _manageMoviesButton.Enabled = false;
                    _manageMoviesButton.Click += EditMoviesButton_Click;
                }

                _menuButton.DropDownItems.Add(_editTagsButton = ui.NewToolStripMenuItem("Edit Tags..."));
                {
                    _editTagsButton.Enabled = false;
                    _editTagsButton.Click += EditTagsButton_Click;
                }

                _menuButton.DropDownItems.Add(ui.NewToolStripSeparator());

                _menuButton.DropDownItems.Add(_connectButton = ui.NewToolStripMenuItem("Connect"));
                {
                    _connectButton.Click += ConnectButton_Click;
                }

                _menuButton.DropDownItems.Add(_disconnectButton = ui.NewToolStripMenuItem("Disconnect"));
                {
                    _disconnectButton.Visible = false;
                    _disconnectButton.Click += DisconnectButton_Click;
                }

                _menuButton.DropDownItems.Add(_accountSettingsButton = ui.NewToolStripMenuItem("Account Settings"));
                {
                    _accountSettingsButton.Click += AccountSettingsButton_Click;
                }
            }

            _toolStrip.Items.Add(_browseBackButton = ui.NewToolStripButton("Back"));
            {
                _browseBackButton.Enabled = false;
                _browseBackButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
                _browseBackButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("BrowseBack.png", 16, 16));
                _browseBackButton.Click += delegate
                {
                    _browser.GoBack();
                };
            }

            _toolStrip.Items.Add(_browseForwardButton = ui.NewToolStripButton("Forward"));
            {
                _browseForwardButton.Enabled = false;
                _browseForwardButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
                _browseForwardButton.Image = ui.InvertColorsInPlace(
                    ui.GetScaledBitmapResource("BrowseForward.png", 16, 16)
                );
                _browseForwardButton.Click += delegate
                {
                    _browser.GoForward();
                };
            }

            _toolStrip.Items.Add(_homeButton = ui.NewToolStripButton("Home"));
            {
                _homeButton.Enabled = false;
                _homeButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
                _homeButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Home.png", 16, 16));
                _homeButton.Click += delegate
                {
                    GoHome();
                };
            }

            _toolStrip.Items.Add(ui.NewToolStripSeparator());

            _toolStrip.Items.Add(_titleLabel = ui.NewToolStripLabel(""));
            {
                Font titleFont = new(_titleLabel.Font, FontStyle.Bold);
                Disposed += delegate
                {
                    titleFont.Dispose();
                };
                _titleLabel.Font = titleFont;
            }
        }

        var browserTable = ui.NewTable(1, 1);
        {
            Controls.Add(browserTable);
            browserTable.Dock = DockStyle.Fill;
            browserTable.Margin = Padding.Empty;
            browserTable.Padding = new(5, 5, 5, 5);
            browserTable.Controls.Add(_browser, 0, 0);
            _browser.NavigationCompleted += Browser_NavigationCompleted;

            browserTable.MouseMove += delegate
            {
                MouseHandler();
            };
            _toolStrip.MouseLeave += delegate
            {
                MouseHandler();
            };
            _leftPanel.MouseLeave += delegate
            {
                MouseHandler();
            };
            _rightPanel.MouseLeave += delegate
            {
                MouseHandler();
            };
            void MouseHandler()
            {
                var xy = MousePosition;
                var isTopEdge = xy.Y < _toolStrip.Height;
                var isLeftEdge = xy.X < _leftPanel.Width;
                var isRightEdge = xy.X > Width - _leftPanel.Width;
                var isFullscreen = FormBorderStyle == FormBorderStyle.None;
                var shouldBeVisible = !isFullscreen | _keepEdgeUiShowing | isTopEdge | isLeftEdge | isRightEdge;
                var isVisible = _toolStrip.Visible;
                if (isVisible)
                {
                    if (!shouldBeVisible && !_edgeHideTimer.Enabled)
                    {
                        _edgeHideTimer.Stop();
                        _edgeHideTimer.Start();
                    }
                    else if (shouldBeVisible)
                    {
                        _edgeHideTimer.Stop();
                    }
                }
                else if (!isVisible && shouldBeVisible)
                {
                    _toolStrip.Visible = true;
                    _leftPanel.Visible = true;
                    _rightPanel.Visible = true;
                }
            }
        }

        Text = "Jackpot";
        Size = ui.GetSize(1600, 900);
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.None;
        Icon = ui.GetIconResource("App.ico");
        BackColor = Color.Black;
        DoubleBuffered = true;
        ShowInTaskbar = true;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        try
        {
            Connect();
        }
        catch
        {
            // It's fine; let the user correct it and try connecting themselves.
            _browser.Visible = false;
            using var f = _serviceProvider.GetRequiredService<AccountSettingsForm>();
            f.ShowDialog(this);
        }
    }

    private void ConnectButton_Click(object? sender, EventArgs e)
    {
        try
        {
            Connect();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DisconnectButton_Click(object? sender, EventArgs e)
    {
        try
        {
            Disconnect();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Connect()
    {
        try
        {
            using SimpleProgressForm f =
                new(
                    (updateProgress, updateMessage, cancel) =>
                    {
                        updateMessage("Connecting...");
                        _libraryProvider.Connect();
                        updateMessage("Synchronizing library...");
                        _libraryProvider.SyncDownAsync(CancellationToken.None).GetAwaiter().GetResult();
                        updateMessage("Starting background service...");
                        _client.Start();
                        updateMessage("Synchronizing .m3u8 folder...");
                        SyncM3u8Folder();
                    },
                    false
                );
            f.Text = "Connection";
            var result = f.ShowDialog();
            if (result == DialogResult.Abort)
                f.Exception!.Throw();
            else if (result == DialogResult.Cancel)
                throw new OperationCanceledException();

            _browser.Visible = true;
            UpdateTagTypes();
            GoHome();
            EnableDisableToolbarButtons(true);
        }
        catch
        {
            try
            {
                Disconnect();
            }
            catch { }

            throw;
        }
    }

    private void Disconnect()
    {
        _client.Stop();
        _libraryProvider.Disconnect();
        NavigateBlank();
        _browser.Visible = false;

        EnableDisableToolbarButtons(false);
    }

    private void EnableDisableToolbarButtons(bool t)
    {
        _connectButton.Visible = !t;
        _disconnectButton.Visible = t;
        _accountSettingsButton.Enabled = !t;
        _editTagsButton.Enabled = t;
        _manageMoviesButton.Enabled = t;
        _addToLibraryButton.Enabled = t;
        _moviesButton.Enabled = t;
        for (var i = 1; _menuButton.DropDownItems[i] is not ToolStripSeparator; i++)
            _menuButton.DropDownItems[i].Enabled = t;
        _shuffleButton.Enabled = t;
        _homeButton.Enabled = t;
        _filterButton.Enabled = t;
    }

    private void EditTagsButton_Click(object? sender, EventArgs e)
    {
        using var f = _serviceProvider.GetRequiredService<EditTagsForm>();
        f.ShowDialog(this);
        UpdateTagTypes();
        _browser.Reload();
    }

    private void EditMoviesButton_Click(object? sender, EventArgs e)
    {
        using var f = _serviceProvider.GetRequiredService<EditMoviesForm>();
        f.ShowDialog(this);
        _browser.Reload();
    }

    private void AddToLibraryButton_Click(object? sender, EventArgs e)
    {
        var f = _serviceProvider.GetRequiredService<ImportForm>();
        f.Show();
        WindowState = FormWindowState.Minimized;
        f.FormClosed += delegate
        {
            if (f.DialogResult != DialogResult.OK)
            {
                WindowState = FormWindowState.Maximized;
                return;
            }

            var filePaths = f.SelectedFilePaths;

            var totalBytes = filePaths.Select(x => new FileInfo(x).Length).Sum();
            ConcurrentBag<string> failedFiles = [];

            var p = _importProgressFormFactory.New(
                totalBytes,
                (updateFile, cancel) =>
                {
                    using var importer = _serviceProvider.GetRequiredService<Importer>();

                    ConcurrentQueue<string> queue = new(filePaths);
                    var numCompleted = 0;
                    var numFiles = filePaths.Count;

                    var tasks = new Task[3];
                    for (var i = 0; i < tasks.Length; i++)
                    {
                        tasks[i] = Task.Run(
                            () =>
                            {
                                while (queue.TryDequeue(out var filePath))
                                {
                                    cancel.ThrowIfCancellationRequested();

                                    var filename = Path.GetFileName(filePath);
                                    updateFile(numCompleted, numFiles, filename);

                                    try
                                    {
                                        importer.Import(filePath, cancel);
                                    }
                                    catch
                                    {
                                        failedFiles.Add(filePath);
                                        // Keep going.
                                    }

                                    numCompleted++;
                                    updateFile(numCompleted, numFiles, filename);
                                }
                            },
                            cancel
                        );
                    }

                    Task.WaitAll(tasks, cancel);
                    foreach (var task in tasks)
                        task.GetAwaiter().GetResult();
                }
            );

            p.FormClosed += delegate
            {
                _importInProgress = false;
                _browser.Reload();

                if (p.DialogResult == DialogResult.Cancel)
                    return;

                p.Visible = false;
                var s = filePaths.Count == 1 ? "" : "s";
                var numSuccesses = filePaths.Count - failedFiles.Count;

                if (failedFiles.IsEmpty)
                {
                    MessageBox.Show(
                        $"Imported {numSuccesses} movie{s}.",
                        "Import",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else if (failedFiles.Count < 5)
                {
                    var failedFilenames = failedFiles.Select(Path.GetFileName).OrderBy(x => x).ToList();
                    MessageBox.Show(
                        $"Imported {numSuccesses} movie{s}.\n\nFailed to import {failedFiles.Count} movies:\n{string.Join("\n", failedFilenames)}",
                        "Import",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
                else
                {
                    MessageBox.Show(
                        $"Imported {numSuccesses} movie{s}. Failed to import {failedFiles.Count} movies.",
                        "Import",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
            };

            if (_importInProgress)
            {
                MessageBox.Show(
                    this,
                    "Another import is already in progress.",
                    "Jackpot",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            _importInProgress = true;

            p.Show();
        };
    }

    private void AccountSettingsButton_Click(object? sender, EventArgs e)
    {
        using var f = _serviceProvider.GetRequiredService<AccountSettingsForm>();
        if (f.ShowDialog(this) == DialogResult.OK)
        {
            SyncM3u8Folder();
        }
    }

    private void MoviesButton_Click(object? sender, EventArgs e)
    {
        Navigate("/list.html?type=Movies&pageIndex=0");
    }

    private void NavigateBlank()
    {
        _browser.Source = new("about:blank");
    }

    private void Navigate(string path)
    {
        var url = "http://localhost:6786" + path;
        Uri uri = new(url);
        if (_browser.Source?.Equals(uri) ?? false)
            _browser.Reload();
        else
            _browser.Source = uri;
    }

    private void Browser_NavigationCompleted(
        object? sender,
        Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e
    )
    {
        var title = _browser.CoreWebView2.DocumentTitle;

        // Parse (1/2) from the end of the title into 1 and 2, if present.
        var match = TitlePageNumberRegex().Match(title);
        if (match.Success)
        {
            var pageNumber = int.Parse(match.Groups[1].Value);
            var totalPages = int.Parse(match.Groups[2].Value);
            _pageLabel.Text = $"Page {pageNumber} of {totalPages}";
            _leftPanel.JumpEnabled = pageNumber > 1;
            _rightPanel.JumpEnabled = pageNumber < totalPages;
            title = title[..match.Index].Trim();
            _pageCount = totalPages;
        }
        else
        {
            _pageLabel.Text = "Page 1 of 1";
        }

        if (title.Length > 100)
            title = title[..100] + "...";

        _titleLabel.Text = title?.Replace("&", "&&") ?? "";
        _browseBackButton.Enabled = _browser.CanGoBack;
        _browseForwardButton.Enabled = _browser.CanGoForward;
    }

    private void PagePreviousButton_Click(object? sender, EventArgs e)
    {
        PageNextOrPrevious(-1);
    }

    private void PageNextButton_Click(object? sender, EventArgs e)
    {
        PageNextOrPrevious(1);
    }

    private void PageFirstButton_Click(object? sender, EventArgs e)
    {
        PageFirstOrLast(true);
    }

    private void PageLastButton_Click(object? sender, EventArgs e)
    {
        PageFirstOrLast(false);
    }

    private void PageFirstOrLast(bool first)
    {
        var uri = _browser.Source;
        if (uri is null)
            return;

        var query = HttpUtility.ParseQueryString(uri.Query);
        query["pageIndex"] = (first ? 0 : _pageCount - 1).ToString();
        Navigate($"{uri.AbsolutePath}?{query}");
    }

    private void PageNextOrPrevious(int offset)
    {
        var uri = _browser.Source;
        if (uri is null)
            return;

        var query = HttpUtility.ParseQueryString(uri.Query);
        if (!int.TryParse(query["pageIndex"], out var pageIndex))
            return;

        pageIndex += offset;
        pageIndex = Math.Max(0, pageIndex);
        pageIndex = Math.Min(_pageCount - 1, pageIndex);
        query["pageIndex"] = pageIndex.ToString();
        Navigate($"{uri.AbsolutePath}?{query}");
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        _browser.Visible = true;
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        _browser.Visible = false;
    }

    private void GoHome()
    {
        Navigate("/list.html?type=Movies&pageIndex=0");
    }

    private async void ShuffleButton_Click(object? sender, EventArgs e)
    {
        var check = !_shuffleButton.Checked;
        _shuffleButton.Checked = check;
        await _client.SetShuffleAsync(check, CancellationToken.None).ConfigureAwait(true);
        _browser.Reload();
    }

    private async Task AddFilterMenuItem_Click(FilterField filterField, FilterOperator filterOperator)
    {
        if (filterOperator.RequiresTagInput())
        {
            using var f = _serviceProvider.GetRequiredService<FilterChooseTagForm>();
            var tagType = filterField.TagType!.Value;
            f.Initialize(tagType, filterOperator);
            if (f.ShowDialog(this) != DialogResult.OK)
                return;

            _filterRules.Add(new(filterField, filterOperator, f.SelectedTags, null));
        }
        else if (filterOperator.RequiresStringInput())
        {
            using var f = _serviceProvider.GetRequiredService<FilterEnterStringForm>();
            f.Initialize(filterField, filterOperator);
            if (f.ShowDialog(this) != DialogResult.OK)
                return;

            _filterRules.Add(new(filterField, filterOperator, null, f.SelectedString));
        }
        else
        {
            _filterRules.Add(new(filterField, filterOperator, null, null));
        }

        await UpdateFiltersAsync().ConfigureAwait(true);
    }

    private async Task UpdateFiltersAsync()
    {
        await _client.SetFilterAsync(new(_filterOr, _filterRules), CancellationToken.None).ConfigureAwait(true);
        _browser.Reload();

        // Update the filter menu. Start by removing anything after the last separator.
        var separatorIndex = -1;
        for (var i = _filterButton.DropDownItems.Count - 1; i >= 0; i--)
        {
            if (_filterButton.DropDownItems[i] is ToolStripSeparator)
            {
                separatorIndex = i;
                break;
            }
        }

        for (var i = _filterButton.DropDownItems.Count - 1; i >= separatorIndex + 1; i--)
            _filterButton.DropDownItems.RemoveAt(i);

        if (_filterRules.Count > 0)
        {
            // Add an item for each filter.
            for (var i = 0; i < _filterRules.Count; i++)
            {
                var thisIndex = i;
                var rule = _filterRules[i];
                var item = _ui.NewToolStripMenuItem(rule.ToString());
                _filterButton.DropDownItems.Add(item);
                item.Image = _ui.GetScaledBitmapResource("DeleteBullet.png", 16, 16);
                item.Click += async delegate
                {
                    _filterRules.RemoveAt(thisIndex);
                    await UpdateFiltersAsync().ConfigureAwait(true);
                };
            }
        }

        // Highlight filter button.
        _filterButton.BackColor = _filterRules.Count > 0 ? Color.Gray : DefaultBackColor;
    }

    private async void FilterOrButton_Click(object? sender, EventArgs e)
    {
        _filterOr = true;
        _filterOrButton.Checked = true;
        _filterAndButton.Checked = false;
        await UpdateFiltersAsync().ConfigureAwait(true);
    }

    private async void FilterAndButton_Click(object? sender, EventArgs e)
    {
        _filterOr = false;
        _filterOrButton.Checked = false;
        _filterAndButton.Checked = true;
        await UpdateFiltersAsync().ConfigureAwait(true);
    }

    private void MenuButton_DropDownOpened(object? sender, EventArgs e)
    {
        _keepEdgeUiShowing = true;
    }

    private void FilterButton_DropDownOpened(object? sender, EventArgs e)
    {
        _keepEdgeUiShowing = true;
    }

    private void MenuButton_DropDownClosed(object? sender, EventArgs e)
    {
        _keepEdgeUiShowing = false;
    }

    private void FilterButton_DropDownClosed(object? sender, EventArgs e)
    {
        _keepEdgeUiShowing = false;
    }

    private void EdgeHideTimer_Tick(object? sender, EventArgs e)
    {
        _edgeHideTimer.Enabled = false;
        _toolStrip.Visible = false;
        _leftPanel.Visible = false;
        _rightPanel.Visible = false;
    }

    private void SyncM3u8Folder()
    {
        if (!_m3u8FolderSync.Enabled)
            return;

        try
        {
            _m3u8FolderSync.Sync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "There was a problem synchronizing your .m3u8 folder.\n\n" + ex.Message,
                "Sync Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void UpdateTagTypes()
    {
        // Remove main menu items for tag types.
        while (_menuButton.DropDownItems.Count > 1 && _menuButton.DropDownItems[1] is not ToolStripSeparator)
            _menuButton.DropDownItems.RemoveAt(1);

        // Remove filter menu items up to the first separator.
        while (_filterButton.DropDownItems.Count > 0 && _filterButton.DropDownItems[0] is not ToolStripSeparator)
            _filterButton.DropDownItems.RemoveAt(0);

        List<FilterField> filterFields = [];
        foreach (var tagType in _libraryProvider.GetTagTypes().OrderByDescending(x => x.SortIndex))
        {
            // Add menu item to the main menu for viewing the list page.
            var menuItem = _ui.NewToolStripMenuItem(tagType.PluralName);
            _menuButton.DropDownItems.Insert(1, menuItem);
            menuItem.Click += delegate
            {
                var query = HttpUtility.ParseQueryString("");
                query["type"] = "TagType";
                query["tagTypeId"] = tagType.Id.Value;
                query["pageIndex"] = "0";
                Navigate($"/list.html?{query}");
            };

            // Add filter menu item
            FilterField filterField = new(FilterFieldType.TagType, tagType);
            filterFields.Add(filterField);
        }

        // Add a filter field for the filename, which is special.
        filterFields.Add(new(FilterFieldType.Filename, null));

        // Update the filter menu.
        var filterOperators = Enum.GetValues<FilterOperator>();
        foreach (var filterField in filterFields)
        {
            var fieldItem = _ui.NewToolStripMenuItem(filterField.GetDisplayName());
            _filterButton.DropDownItems.Insert(0, fieldItem);

            foreach (var filterOperator in filterOperators)
            {
                if (!filterField.IsOperatorApplicable(filterOperator))
                    continue;

                var operatorItem = _ui.NewToolStripMenuItem(filterOperator.GetDisplayName(true));
                fieldItem.DropDownItems.Add(operatorItem);

                operatorItem.Click += async delegate
                {
                    await AddFilterMenuItem_Click(filterField, filterOperator).ConfigureAwait(true);
                };
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_importInProgress)
        {
            MessageBox.Show(
                this,
                "An import is in progress. Please wait for it to finish before exiting.",
                "Jackpot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
            e.Cancel = true;
            return;
        }

        var response = MessageBox.Show(
            this,
            "Are you sure you want to exit?",
            "Jackpot",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2
        );
        if (response != DialogResult.OK)
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    [GeneratedRegex(@"\((\d+)/(\d+)\)$")]
    private static partial Regex TitlePageNumberRegex();
}