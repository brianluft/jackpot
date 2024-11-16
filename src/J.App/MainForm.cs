using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;
using J.Core;
using J.Core.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace J.App;

public sealed partial class MainForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly Client _client;
    private readonly ImportProgressFormFactory _importProgressFormFactory;
    private readonly CoreWebView2Environment _coreWebView2Environment;
    private readonly SingleInstanceManager _singleInstanceManager;
    private readonly Preferences _preferences;
    private readonly Ui _ui;
    private readonly ToolStrip _toolStrip;
    private readonly EdgePanel _leftPanel,
        _rightPanel;
    private readonly ToolStripDropDownButton _menuButton,
        _filterButton;
    private readonly ToolStripMenuItem _logOutButton,
        _aboutButton,
        _addToLibraryButton,
        _editTagsButton,
        _convertMoviesButton,
        _manageMoviesButton,
        _moviesButton,
        _filterAndButton,
        _filterOrButton,
        _optionsButton;
    private readonly ToolStripButton _homeButton,
        _minimizeButton,
        _fullscreenButton,
        _exitButton,
        _browseBackButton,
        _browseForwardButton,
        _shuffleButton,
        _filterClearButton;
    private readonly ToolStripTextBox _searchText;
    private readonly ToolStripLabel _titleLabel,
        _pageLabel;
    private readonly WebView2 _browser;
    private readonly List<FilterRule> _filterRules = [];
    private bool _filterOr = false;
    private int _pageCount;
    private bool _importInProgress;
    private FormWindowState _lastWindowState;

    public MainForm(
        IServiceProvider serviceProvider,
        LibraryProviderAdapter libraryProvider,
        Client client,
        ImportProgressFormFactory importProgressFormFactory,
        CoreWebView2Environment coreWebView2Environment,
        SingleInstanceManager singleInstanceManager,
        Preferences preferences
    )
    {
        _serviceProvider = serviceProvider;
        _libraryProvider = libraryProvider;
        _client = client;
        _importProgressFormFactory = importProgressFormFactory;
        _coreWebView2Environment = coreWebView2Environment;
        _singleInstanceManager = singleInstanceManager;
        _preferences = preferences;
        Ui ui = new(this);
        _ui = ui;

        Controls.Add(_leftPanel = new(left: true));
        {
            _leftPanel.Dock = DockStyle.Left;
            _leftPanel.Width = ui.GetLength(25);
            _leftPanel.ShortJump += PagePreviousButton_Click;
            _leftPanel.LongJump += PageFirstButton_Click;
        }

        Controls.Add(_rightPanel = new(left: false));
        {
            _rightPanel.Dock = DockStyle.Right;
            _rightPanel.Width = ui.GetLength(25);
            _rightPanel.ShortJump += PageNextButton_Click;
            _rightPanel.LongJump += PageLastButton_Click;
        }

        Controls.Add(_toolStrip = ui.NewToolStrip());
        {
            _toolStrip.Dock = DockStyle.Top;
            _toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            _toolStrip.MouseUp += ToolStrip_MouseUp;

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
                _fullscreenButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Reduce.png", 16, 16));
                _fullscreenButton.Click += delegate
                {
                    if (FormBorderStyle == FormBorderStyle.Sizable)
                        EnterFullscreen();
                    else
                        ExitFullscreen();
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

            _toolStrip.Items.Add(_searchText = ui.NewToolStripTextBox(200));
            {
                _searchText.Alignment = ToolStripItemAlignment.Right;
                _searchText.KeyPress += SearchText_KeyPress;
                ui.SetCueText(_searchText.TextBox, "Search");
            }

            var separator2 = ui.NewToolStripSeparator();
            {
                _toolStrip.Items.Add(separator2);
                separator2.Alignment = ToolStripItemAlignment.Right;
            }

            _toolStrip.Items.Add(_filterClearButton = ui.NewToolStripButton("Clear Filter", true));
            {
                _filterClearButton.Alignment = ToolStripItemAlignment.Right;
                _filterClearButton.Image = ui.InvertColorsInPlace(
                    ui.GetScaledBitmapResource("FilterClear.png", 16, 16)
                );
                _filterClearButton.Visible = false;
                _filterClearButton.Click += FilterClearButton_Click;
            }

            _toolStrip.Items.Add(_filterButton = ui.NewToolStripDropDownButton("Filter"));
            {
                _filterButton.Alignment = ToolStripItemAlignment.Right;
                _filterButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Filter.png", 16, 16));

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

            _toolStrip.Items.Add(_shuffleButton = ui.NewToolStripButton("Shuffle"));
            {
                _shuffleButton.Alignment = ToolStripItemAlignment.Right;
                _shuffleButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Shuffle.png", 16, 16));
                _shuffleButton.Checked = preferences.GetBoolean(Preferences.Key.Shared_UseShuffle);
                _shuffleButton.Click += ShuffleButton_Click;
            }

            _toolStrip.Items.Add(_menuButton = ui.NewToolStripDropDownButton("Menu"));
            {
                _menuButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                _menuButton.Margin = _menuButton.Margin with { Left = 0 };
                _menuButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Menu.png", 16, 16));

                _menuButton.DropDownItems.Add(_moviesButton = ui.NewToolStripMenuItem("Movies"));
                {
                    _moviesButton.Image = ui.GetScaledBitmapResource("Movie.png", 16, 16);
                    _moviesButton.Click += MoviesButton_Click;
                }

                _menuButton.DropDownItems.Add(ui.NewToolStripSeparator());

                _menuButton.DropDownItems.Add(_addToLibraryButton = ui.NewToolStripMenuItem("Add movies..."));
                {
                    _addToLibraryButton.Image = ui.GetScaledBitmapResource("Add.png", 16, 16);
                    _addToLibraryButton.Click += AddToLibraryButton_Click;
                }

                _menuButton.DropDownItems.Add(_manageMoviesButton = ui.NewToolStripMenuItem("Edit movies..."));
                {
                    _manageMoviesButton.Click += EditMoviesButton_Click;
                }

                _menuButton.DropDownItems.Add(_editTagsButton = ui.NewToolStripMenuItem("Edit tags..."));
                {
                    _editTagsButton.Click += EditTagsButton_Click;
                }

                _menuButton.DropDownItems.Add(ui.NewToolStripSeparator());

                _menuButton.DropDownItems.Add(
                    _convertMoviesButton = ui.NewToolStripMenuItem("Convert movies to MP4...")
                );
                {
                    _convertMoviesButton.Click += ConvertMoviesButton_Click;
                }

                _menuButton.DropDownItems.Add(ui.NewToolStripSeparator());

                _menuButton.DropDownItems.Add(_logOutButton = ui.NewToolStripMenuItem("Log out"));
                {
                    _logOutButton.Click += DisconnectButton_Click;
                }

                _menuButton.DropDownItems.Add(_optionsButton = ui.NewToolStripMenuItem("Options..."));
                {
                    _optionsButton.Click += OptionsButton_Click;
                }

                _menuButton.DropDownItems.Add(_aboutButton = ui.NewToolStripMenuItem("About Jackpot"));
                {
                    _aboutButton.Click += AboutButton_Click;
                }
            }

            _toolStrip.Items.Add(_browseBackButton = ui.NewToolStripButton("Back"));
            {
                _browseBackButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
                _browseBackButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("BrowseBack.png", 16, 16));
                _browseBackButton.Click += delegate
                {
                    _browser!.GoBack();
                };
            }

            _toolStrip.Items.Add(_browseForwardButton = ui.NewToolStripButton("Forward"));
            {
                _browseForwardButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
                _browseForwardButton.Image = ui.InvertColorsInPlace(
                    ui.GetScaledBitmapResource("BrowseForward.png", 16, 16)
                );
                _browseForwardButton.Click += delegate
                {
                    _browser!.GoForward();
                };
            }

            _toolStrip.Items.Add(_homeButton = ui.NewToolStripButton("Home"));
            {
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

            _toolStrip.Items.Add(ui.NewToolStripSeparator());

            _toolStrip.Items.Add(_pageLabel = ui.NewToolStripLabel(""));
        }

        Controls.Add(
            _browser = new()
            {
                Dock = DockStyle.Fill,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
            }
        );
        {
            _browser.BringToFront();
            _browser.CoreWebView2InitializationCompleted += delegate
            {
                var settings = _browser.CoreWebView2.Settings;
                settings.AreBrowserAcceleratorKeysEnabled = false;
                settings.AreDefaultContextMenusEnabled = false;
                settings.AreDefaultScriptDialogsEnabled = false;
                settings.AreDevToolsEnabled = false;
                settings.AreHostObjectsAllowed = false;
                settings.IsBuiltInErrorPageEnabled = false;
                settings.IsGeneralAutofillEnabled = false;
                settings.IsNonClientRegionSupportEnabled = false;
                settings.IsPasswordAutosaveEnabled = false;
                settings.IsPinchZoomEnabled = false;
                settings.IsReputationCheckingRequired = false;
                settings.IsScriptEnabled = true;
                settings.IsStatusBarEnabled = false;
                settings.IsSwipeNavigationEnabled = false;
                settings.IsWebMessageEnabled = false;
                settings.IsZoomControlEnabled = false;
            };
            _ = _browser.EnsureCoreWebView2Async(_coreWebView2Environment);
            _browser.NavigationCompleted += Browser_NavigationCompleted;
        }

        Text = "Jackpot";
        Size = ui.GetSize(1600, 900);
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.None;
        Icon = ui.GetIconResource("App.ico");
        BackColor = Color.Black;
        DoubleBuffered = true;
        ShowInTaskbar = true;
        KeyPreview = true;
    }

    private void AboutButton_Click(object? sender, EventArgs e)
    {
        var assembly = typeof(MainForm).Assembly;
        var version = assembly.GetName().Version;

        TaskDialogPage taskDialogPage =
            new()
            {
                Heading = "Jackpot Media Library",
                Caption = "About Jackpot",
                Icon = TaskDialogIcon.Information,
                Text = $"Version {version}",
            };
        taskDialogPage.Buttons.Add("License info");
        taskDialogPage.Buttons.Add(TaskDialogButton.OK);
        taskDialogPage.DefaultButton = taskDialogPage.Buttons[1];
        var clicked = TaskDialog.ShowDialog(this, taskDialogPage);
        if (clicked == taskDialogPage.Buttons[0])
        {
            Process
                .Start(
                    new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Resources", "License.html"))
                    {
                        UseShellExecute = true,
                    }
                )!
                .Dispose();
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _browser.Visible = true;
        UpdateTagTypes();
        GoHome();
    }

    private void DisconnectButton_Click(object? sender, EventArgs e)
    {
        if (_importInProgress)
        {
            MessageBox.Show(
                this,
                "An import is in progress. Please wait for it to finish before logging out.",
                "Jackpot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
            return;
        }

        try
        {
            _client.Stop();
            _libraryProvider.Disconnect();
            DialogResult = DialogResult.Retry;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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

    private void MoviesButton_Click(object? sender, EventArgs e)
    {
        GoHome();
    }

    private void Navigate(string path)
    {
        var url = $"http://localhost:{_client.Port}{path}";
        Uri uri = new(url);
        if (_browser.Source?.Equals(uri) ?? false)
            _browser.Reload();
        else
            _browser.Source = uri;
    }

    private void Browser_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
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
        var query = HttpUtility.ParseQueryString("");
        query["sessionPassword"] = _client.SessionPassword;
        query["type"] = "Movies";
        query["pageIndex"] = "0";
        Navigate($"/list.html?{query}");
    }

    private async void ShuffleButton_Click(object? sender, EventArgs e)
    {
        var check = !_shuffleButton.Checked;
        _shuffleButton.Checked = check;
        _preferences.SetBoolean(Preferences.Key.Shared_UseShuffle, check);
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
        PageFirstOrLast(first: true);

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

        // Update toolbar buttons.
        _filterButton.BackColor = _filterRules.Count > 0 ? Color.Gray : DefaultBackColor;
        _filterClearButton.Visible = _filterRules.Count > 0;
    }

    private async void FilterClearButton_Click(object? sender, EventArgs e)
    {
        _filterRules.Clear();
        await UpdateFiltersAsync().ConfigureAwait(true);
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
                query["sessionPassword"] = _client.SessionPassword;
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

        if (DialogResult != DialogResult.Retry)
        {
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
        }

        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == _singleInstanceManager.ActivateMessageId)
        {
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Maximized;
            Activate();
        }

        base.WndProc(ref m);
    }

    private void ToolStrip_MouseUp(object? sender, MouseEventArgs e)
    {
        var pt = e.Location;

        if (pt.Y >= _toolStrip.Bottom)
            return;

        var left = _searchText.TextBox.PointToScreen(Point.Empty).X;
        var right = left + _searchText.TextBox.Width;
        if (pt.X >= left && pt.Y <= right)
        {
            _searchText.Focus();
            _searchText.SelectAll();
        }
    }

    private async void SearchText_KeyPress(object? sender, KeyPressEventArgs e)
    {
        // Did they press the enter key with no modifiers?
        if (e.KeyChar == (char)Keys.Enter && ModifierKeys == Keys.None)
        {
            e.Handled = true;

            var phrase = _searchText.Text;

            var words = WhitespaceRegex().Split(phrase);
            foreach (var word in words)
            {
                FilterField field = new(FilterFieldType.Filename, null);
                FilterRule rule = new(field, FilterOperator.ContainsString, null, word);
                _filterRules.Add(rule);
            }

            _filterOr = false;
            _filterOrButton.Checked = false;
            _filterAndButton.Checked = true;

            _searchText.Text = "";

            await UpdateFiltersAsync().ConfigureAwait(true);
        }
    }

    private void ConvertMoviesButton_Click(object? sender, EventArgs e)
    {
        var f = _serviceProvider.GetRequiredService<ConvertMoviesForm>();
        f.Show();
        WindowState = FormWindowState.Minimized;
    }

    private void OptionsButton_Click(object? sender, EventArgs e)
    {
        using var f = _serviceProvider.GetRequiredService<OptionsForm>();
        if (f.ShowDialog(this) != DialogResult.OK)
            return;

        ApplyFullscreenPreference();
    }

    private void ApplyFullscreenPreference()
    {
        var isMaximized = WindowState == FormWindowState.Maximized;
        var isFullscreen = FormBorderStyle == FormBorderStyle.None;
        var behavior = _preferences.GetEnum<WindowMaximizeBehavior>(Preferences.Key.MainForm_WindowMaximizeBehavior);

        if (behavior == WindowMaximizeBehavior.Fullscreen && isMaximized && !isFullscreen)
        {
            EnterFullscreen();
        }
        else if (behavior == WindowMaximizeBehavior.Windowed && isFullscreen)
        {
            ExitFullscreen();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (WindowState != _lastWindowState)
        {
            ApplyFullscreenPreference();
            _lastWindowState = WindowState;
        }
    }

    private void EnterFullscreen()
    {
        if (WindowState == FormWindowState.Maximized)
            WindowState = FormWindowState.Normal;
        _minimizeButton!.Visible = true;
        _exitButton.Visible = true;
        _fullscreenButton.Visible = true;
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
    }

    private void ExitFullscreen()
    {
        _minimizeButton!.Visible = false;
        _exitButton.Visible = false;
        _fullscreenButton.Visible = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowIcon = true;
        Icon = _ui.GetIconResource("App.ico");
        WindowState = FormWindowState.Maximized;
    }

    [GeneratedRegex(@"\((\d+)/(\d+)\)$")]
    private static partial Regex TitlePageNumberRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
