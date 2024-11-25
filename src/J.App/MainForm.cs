using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Web;
using J.Base;
using J.Core;
using J.Core.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;
using SortOrder = J.Core.Data.SortOrder;

namespace J.App;

public sealed partial class MainForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly Client _client;
    private readonly SingleInstanceManager _singleInstanceManager;
    private readonly Preferences _preferences;
    private readonly MovieExporter _movieExporter;
    private readonly M3u8FolderSync _m3U8FolderSync;
    private readonly ImportControl _importControl;
    private readonly TagsControl _tagsControl;
    private readonly Ui _ui;
    private readonly ToolStrip _toolStrip;
    private readonly ToolStripDropDownButton _filterButton,
        _menuButton,
        _sortButton,
        _viewButton;
    private readonly ToolStripMenuItem _aboutButton,
        _filterAndButton,
        _filterOrButton,
        _logOutButton,
        _moviesButton,
        _optionsButton,
        _shuffleButton,
        _sortAscendingButton,
        _sortByDateAddedButton,
        _sortByNameButton,
        _sortDescendingButton,
        _movieContextOpenButton,
        _movieContextAddTagButton,
        _movieContextDeleteButton,
        _movieContextExportButton,
        _movieContextPropertiesButton,
        _viewGridButton,
        _viewListButton;
    private readonly ToolStripButton _browseBackButton,
        _browseForwardButton,
        _exitButton,
        _filterClearButton,
        _fullscreenButton,
        _homeButton,
        _minimizeButton,
        _browserTabButton,
        _importTabButton,
        _tagsTabButton;
    private readonly ToolStripSeparator _rightmostSeparator;
    private readonly MyToolStripTextBox _searchText;
    private readonly WebView2 _browser;
    private readonly System.Windows.Forms.Timer _searchDebounceTimer;
    private readonly ContextMenuStrip _movieContextMenu;
    private readonly List<MovieId> _movieContextMenuIds = [];
    private FormWindowState _lastWindowState;
    private bool _inhibitSearchTextChangedEvent;
    private readonly Dictionary<ToolStripItem, bool> _previousToolStripItemEnabledStates = [];

    public MainForm(
        IServiceProvider serviceProvider,
        LibraryProviderAdapter libraryProvider,
        Client client,
        SingleInstanceManager singleInstanceManager,
        Preferences preferences,
        MovieExporter movieExporter,
        M3u8FolderSync m3U8FolderSync,
        ImportControl importControl,
        TagsControl tagsControl
    )
    {
        _serviceProvider = serviceProvider;
        _libraryProvider = libraryProvider;
        _client = client;
        _singleInstanceManager = singleInstanceManager;
        _preferences = preferences;
        _movieExporter = movieExporter;
        _m3U8FolderSync = m3U8FolderSync;
        _importControl = importControl;
        _tagsControl = tagsControl;
        Ui ui = new(this);
        _ui = ui;

        Controls.Add(_toolStrip = ui.NewToolStrip());
        {
            _toolStrip.Dock = DockStyle.Top;
            _toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            _toolStrip.MouseUp += ToolStrip_MouseUp;
            _toolStrip.AutoSize = false;
            _toolStrip.Height = ui.GetLength(32);
            _toolStrip.Font = ui.Font;

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

            _toolStrip.Items.Add(_rightmostSeparator = ui.NewToolStripSeparator());
            {
                _rightmostSeparator.Alignment = ToolStripItemAlignment.Right;
            }

            _toolStrip.Items.Add(_filterClearButton = ui.NewToolStripButton("Clear Filter", true));
            {
                _filterClearButton.Alignment = ToolStripItemAlignment.Right;
                _filterClearButton.Image = ui.InvertColorsInPlace(
                    ui.GetScaledBitmapResource("FilterClear.png", 16, 16)
                );
                _filterClearButton.Enabled = false;
                _filterClearButton.Click += FilterClearButton_Click;
            }

            _toolStrip.Items.Add(_searchText = ui.NewToolStripTextBox(200));
            {
                _searchText.Margin += ui.RightSpacing;
                _searchText.Alignment = ToolStripItemAlignment.Right;
                _searchText.TextChanged += SearchText_TextChanged;
                _searchText.SetCueText("Search (Ctrl+F)");
            }

            _toolStrip.Items.Add(_filterButton = ui.NewToolStripDropDownButton("Filter"));
            {
                _filterButton.Margin += ui.RightSpacing;
                _filterButton.Alignment = ToolStripItemAlignment.Right;
                _filterButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("DisclosureDown.png", 16, 16));
                _filterButton.TextImageRelation = TextImageRelation.TextBeforeImage;
                _filterButton.ImageAlign = ContentAlignment.MiddleLeft;

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

            _toolStrip.Items.Add(_sortButton = ui.NewToolStripDropDownButton("Sort"));
            {
                _sortButton.Alignment = ToolStripItemAlignment.Right;
                _sortButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("DisclosureDown.png", 16, 16));
                _sortButton.TextImageRelation = TextImageRelation.TextBeforeImage;
                _sortButton.ImageAlign = ContentAlignment.MiddleLeft;

                _sortButton.DropDownItems.Add(_shuffleButton = ui.NewToolStripMenuItem("Shuffle"));
                {
                    _shuffleButton.Click += ShuffleButton_Click;
                }

                _sortButton.DropDownItems.Add(ui.NewToolStripSeparator());

                _sortButton.DropDownItems.Add(_sortAscendingButton = ui.NewToolStripMenuItem("Ascending (A→Z)"));
                {
                    _sortAscendingButton.Checked = true;
                    _sortAscendingButton.Click += SortAscendingButton_Click;
                }

                _sortButton.DropDownItems.Add(_sortDescendingButton = ui.NewToolStripMenuItem("Descending (Z→A)"));
                {
                    _sortDescendingButton.Click += SortDescendingButton_Click;
                }

                _sortButton.DropDownItems.Add(ui.NewToolStripSeparator());

                _sortButton.DropDownItems.Add(_sortByNameButton = ui.NewToolStripMenuItem("By name"));
                {
                    _sortByNameButton.Checked = true;
                    _sortByNameButton.Click += SortByNameButton_Click;
                }

                _sortButton.DropDownItems.Add(_sortByDateAddedButton = ui.NewToolStripMenuItem("By date added"));
                {
                    _sortByDateAddedButton.Checked = true;
                    _sortByDateAddedButton.Click += SortByDateAddedButton_Click;
                }
            }

            _toolStrip.Items.Add(_viewButton = ui.NewToolStripDropDownButton("View"));
            {
                _viewButton.Alignment = ToolStripItemAlignment.Right;
                _viewButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("DisclosureDown.png", 16, 16));
                _viewButton.TextImageRelation = TextImageRelation.TextBeforeImage;
                _viewButton.ImageAlign = ContentAlignment.MiddleLeft;

                _viewButton.DropDownItems.Add(_viewGridButton = ui.NewToolStripMenuItem("Grid"));
                {
                    _viewGridButton.Click += ViewGridButton_Click;
                }

                _viewButton.DropDownItems.Add(_viewListButton = ui.NewToolStripMenuItem("List"));
                {
                    _viewListButton.Click += ViewListButton_Click;
                }
            }

            _toolStrip.Items.Add(_menuButton = ui.NewToolStripDropDownButton("Menu"));
            {
                _menuButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                _menuButton.Margin = _menuButton.Margin with { Left = 0 };
                _menuButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Menu.png", 16, 16));

                _menuButton.DropDownItems.Add(_moviesButton = ui.NewToolStripMenuItem("Browse movies"));
                {
                    _moviesButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Movie.png", 16, 16));
                    _moviesButton.Click += MoviesButton_Click;
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

                _menuButton.DropDownItems.Add(ui.NewToolStripSeparator());

                _menuButton.DropDownItems.Add(_aboutButton = ui.NewToolStripMenuItem("About Jackpot"));
                {
                    _aboutButton.Click += AboutButton_Click;
                }

#if DEBUG
                ToolStripMenuItem devToolsItem;
                _menuButton.DropDownItems.Add(devToolsItem = ui.NewToolStripMenuItem("🐞 Dev Tools"));
                {
                    devToolsItem.Click += delegate
                    {
                        _browser!.CoreWebView2.OpenDevToolsWindow();
                    };
                }
#endif
            }

            _toolStrip.Items.Add(_browseBackButton = ui.NewToolStripButton("Back"));
            {
                _browseBackButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
                _browseBackButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("BrowseBack.png", 16, 16));
                _browseBackButton.Click += BrowseBackButton_Click;
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

            _toolStrip.Items.Add(_browserTabButton = ui.NewToolStripTabButton("Loading..."));
            {
                _browserTabButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Wall.png", 16, 16));
                _browserTabButton.Checked = true;
                _browserTabButton.Click += BrowserTabButton_Click;
            }

            _toolStrip.Items.Add(_tagsTabButton = ui.NewToolStripTabButton("Tags"));
            {
                _tagsTabButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Tag.png", 16, 16));
                _tagsTabButton.Click += TagsTabButton_Click;
            }

            _toolStrip.Items.Add(_importTabButton = ui.NewToolStripTabButton("Import"));
            {
                _importTabButton.Image = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Add.png", 16, 16));
                _importTabButton.Click += ImportTabButton_Click;
            }
        }

        Controls.Add(_browser = ui.NewWebView2());
        {
            _browser.BringToFront();
            _browser.NavigationStarting += Browser_NavigationStarting;
            _browser.NavigationCompleted += Browser_NavigationCompleted;
            _browser.WebMessageReceived += Browser_WebMessageReceived;
        }

        Controls.Add(_importControl);
        {
            _importControl.BringToFront();
            _importControl.Visible = false;
            _importControl.Dock = DockStyle.Fill;
            _importControl.TitleChanged += delegate
            {
                _importTabButton.Text = _importControl.Title;
            };
            _importTabButton.Text = _importControl.Title;
        }

        Controls.Add(_tagsControl);
        {
            _tagsControl.BringToFront();
            _tagsControl.Visible = false;
            _tagsControl.Dock = DockStyle.Fill;
            _tagsControl.TagTypeChanged += EditTagsControl_TagTypeChanged;
            _tagsControl.TagChanged += EditTagsControl_TagChanged;
        }

        _searchDebounceTimer = new() { Interval = 500, Enabled = false };
        {
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
            Disposed += delegate
            {
                _searchDebounceTimer.Dispose();
            };
        }

        _movieContextMenu = ui.NewContextMenuStrip();
        {
            Disposed += delegate
            {
                _movieContextMenu.Dispose();
            };

            _movieContextMenu.Items.Add(_movieContextOpenButton = ui.NewToolStripMenuItem("Play"));
            {
                _movieContextOpenButton.Font = ui.BoldFont;
                _movieContextOpenButton.Click += MovieContextOpenButton_Click;
            }

            _movieContextMenu.Items.Add(ui.NewToolStripSeparator());

            _movieContextMenu.Items.Add(_movieContextAddTagButton = ui.NewToolStripMenuItem("Add tag..."));
            {
                _movieContextAddTagButton.Click += MovieContextAddTagButton_Click;
            }

            _movieContextMenu.Items.Add(_movieContextExportButton = ui.NewToolStripMenuItem("Export to MP4..."));
            {
                _movieContextExportButton.Click += MovieContextExportButton_Click;
            }

            _movieContextMenu.Items.Add(_movieContextDeleteButton = ui.NewToolStripMenuItem("Delete"));
            {
                _movieContextDeleteButton.Click += MovieContextDeleteButton_Click;
            }

            _movieContextMenu.Items.Add(ui.NewToolStripSeparator());

            _movieContextMenu.Items.Add(_movieContextPropertiesButton = ui.NewToolStripMenuItem("Properties"));
            {
                _movieContextPropertiesButton.Click += MovieContextPropertiesButton_Click;
            }
        }

        Text = "Jackpot Media Library";
        Size = ui.GetSize(1600, 900);
        MinimumSize = ui.GetSize(1200, 400);
        CenterToScreen();
        FormBorderStyle = FormBorderStyle.None;
        Icon = ui.GetIconResource("App.ico");
        BackColor = MyColors.MainFormBackground;
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
                    new ProcessStartInfo(
                        "msedge.exe",
                        "\"" + Path.Combine(AppContext.BaseDirectory, "Resources", "License.html") + "\""
                    )
                    {
                        UseShellExecute = true,
                    }
                )!
                .Dispose();
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        var state = _preferences.GetJson<CompleteWindowState>(Preferences.Key.MainForm_CompleteWindowState);
        state.Restore(this);
        ApplyFullscreenPreference();

        UpdateTagTypes();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        await UpdateViewFromPreferencesAsync(reload: false).ConfigureAwait(true);
        await UpdateFilterSortFromPreferencesAsync(reload: false).ConfigureAwait(true);
        GoHome();
    }

    private void DisconnectButton_Click(object? sender, EventArgs e)
    {
        if (_importControl.ImportInProgress)
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

    private async void EditTagsControl_TagTypeChanged(object? sender, EventArgs e)
    {
        UpdateTagTypes();
        await ClearFilterAsync().ConfigureAwait(true);
        _browser.Reload();
    }

    private async void EditTagsControl_TagChanged(object? sender, EventArgs e)
    {
        await ClearFilterAsync().ConfigureAwait(true);
        _browser.Reload();
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
        _sortButton.Visible = true;
        _filterButton.Visible = true;
        _searchText.Visible = true;
        _filterClearButton.Visible = true;
    }

    private void BrowseBackButton_Click(object? sender, EventArgs e)
    {
        _browser.GoBack();
    }

    private void Browser_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        _browserTabButton.Text = "Loading...";
    }

    private void Browser_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        var title = _browser.CoreWebView2.DocumentTitle;
        if (title.Length > 100)
            title = title[..100] + "...";

        _browserTabButton.Text = title?.Replace("&", "&&") ?? "";
        _browseBackButton.Enabled = _browser.CanGoBack;
        _browseForwardButton.Enabled = _browser.CanGoForward;
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
        Navigate($"/list.html?{query}");
    }

    private async Task ChangeSortOrderAsync(Func<SortOrder, SortOrder> func)
    {
        var sortOrder = _preferences.GetJson<SortOrder>(Preferences.Key.Shared_SortOrder);
        sortOrder = func(sortOrder);
        _preferences.SetJson(Preferences.Key.Shared_SortOrder, sortOrder);
        await UpdateFilterSortFromPreferencesAsync().ConfigureAwait(true);
    }

    private async void ShuffleButton_Click(object? sender, EventArgs e)
    {
        var shuffle = !_shuffleButton.Checked;
        await ChangeSortOrderAsync(x => x with { Shuffle = shuffle }).ConfigureAwait(true);
    }

    private async void SortAscendingButton_Click(object? sender, EventArgs e)
    {
        await ChangeSortOrderAsync(x => x with { Ascending = true }).ConfigureAwait(true);
    }

    private async void SortDescendingButton_Click(object? sender, EventArgs e)
    {
        await ChangeSortOrderAsync(x => x with { Ascending = false }).ConfigureAwait(true);
    }

    private async void SortByNameButton_Click(object? sender, EventArgs e)
    {
        await ChangeSortOrderAsync(x => x with { Field = "name" }).ConfigureAwait(true);
    }

    private async void SortByDateAddedButton_Click(object? sender, EventArgs e)
    {
        await ChangeSortOrderAsync(x => x with { Field = "date" }).ConfigureAwait(true);
    }

    private async void SortItem_Click(object? sender, EventArgs e)
    {
        var menuItem = (ToolStripMenuItem)sender!;
        var tagTypeId = (TagTypeId)menuItem.Tag!;
        await ChangeSortOrderAsync(x => x with { Field = tagTypeId.Value }).ConfigureAwait(true);
    }

    private async Task UpdateFilterSortFromPreferencesAsync(bool reload = true)
    {
        var filter = _preferences.GetJson<Filter>(Preferences.Key.Shared_Filter);
        var sortOrder = _preferences.GetJson<SortOrder>(Preferences.Key.Shared_SortOrder);

        // Sort
        {
            _shuffleButton.Checked = sortOrder.Shuffle;

            // Show or hide every other item in the sort menu based on shuffle
            foreach (ToolStripItem item in _sortButton.DropDownItems)
            {
                if (!ReferenceEquals(item, _shuffleButton))
                    item.Visible = !sortOrder.Shuffle;
            }

            _sortAscendingButton.Checked = sortOrder.Ascending;
            _sortDescendingButton.Checked = !sortOrder.Ascending;

            _sortByNameButton.Checked = sortOrder.Field == "name";
            _sortByDateAddedButton.Checked = sortOrder.Field == "date";
            foreach (var menuItem in _sortButton.DropDownItems.OfType<ToolStripMenuItem>())
            {
                if (menuItem.Tag is TagTypeId tagTypeId)
                    menuItem.Checked = sortOrder.Field == tagTypeId.Value;
            }
        }

        // Filter
        {
            // Start by removing anything after the last separator.
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

            _filterAndButton.Checked = !filter.Or;
            _filterOrButton.Checked = filter.Or;

            if (!_searchText.Focused)
            {
                _inhibitSearchTextChangedEvent = true;
                _searchText.Text = filter.Search;
                _inhibitSearchTextChangedEvent = false;
            }

            var tagTypes = _libraryProvider.GetTagTypes().ToDictionary(x => x.Id);
            var tagNames = _libraryProvider.GetTags().ToDictionary(x => x.Id, x => x.Name);

            // Add an item for each filter.
            for (var i = 0; i < filter.Rules.Count; i++)
            {
                var thisIndex = i;
                var rule = filter.Rules[i];
                var item = _ui.NewToolStripMenuItem(rule.GetDisplayName(tagTypes, tagNames));
                _filterButton.DropDownItems.Add(item);
                item.Image = _ui.InvertColorsInPlace(_ui.GetScaledBitmapResource("DeleteBullet.png", 16, 16));
                item.Click += async delegate
                {
                    filter.Rules.RemoveAt(thisIndex);
                    _preferences.SetJson(Preferences.Key.Shared_Filter, filter);
                    await UpdateFilterSortFromPreferencesAsync().ConfigureAwait(true);
                };
            }

            // Hide the last separator if there are no filter items.
            _filterButton.DropDownItems[separatorIndex].Visible = filter.Rules.Count > 0;
        }

        // Update toolbar buttons.
        UpdateFilterSortButtons(filter, sortOrder);

        // Update the web view
        await _client.RefreshLibraryAsync(CancellationToken.None).ConfigureAwait(true);
        if (reload)
            _browser.Reload();
    }

    private async Task AddFilterMenuItem_Click(FilterField filterField, FilterOperator filterOperator)
    {
        var filter = _preferences.GetJson<Filter>(Preferences.Key.Shared_Filter);

        if (filterOperator.RequiresTagInput())
        {
            using var f = _serviceProvider.GetRequiredService<FilterChooseTagForm>();
            var tagTypeId = filterField.TagTypeId!;
            var tagType = _libraryProvider.GetTagType(tagTypeId);
            f.Initialize(tagType, filterOperator);
            if (f.ShowDialog(this) != DialogResult.OK)
                return;

            filter.Rules.Add(new(filterField, filterOperator, f.SelectedTags, null));
        }
        else if (filterOperator.RequiresStringInput())
        {
            using var f = _serviceProvider.GetRequiredService<FilterEnterStringForm>();
            f.Initialize(filterField, filterOperator);
            if (f.ShowDialog(this) != DialogResult.OK)
                return;

            filter.Rules.Add(new(filterField, filterOperator, null, f.SelectedString));
        }
        else
        {
            filter.Rules.Add(new(filterField, filterOperator, null, null));
        }

        _preferences.SetJson(Preferences.Key.Shared_Filter, filter);
        await UpdateFilterSortFromPreferencesAsync().ConfigureAwait(true);
    }

    private void UpdateFilterSortButtons(Filter filter, SortOrder sortOrder)
    {
        _filterButton.BackColor = filter.Rules.Count > 0 ? MyColors.ToolStripActive : DefaultBackColor;

        _sortButton.BackColor = sortOrder.IsDefault ? DefaultBackColor : MyColors.ToolStripActive;

        _filterClearButton.Enabled = !filter.IsDefault || !sortOrder.IsDefault;
    }

    private async void FilterClearButton_Click(object? sender, EventArgs e)
    {
        await ClearFilterAsync().ConfigureAwait(true);
    }

    private async Task ClearFilterAsync()
    {
        _preferences.WithTransaction(() =>
        {
            _preferences.SetText(Preferences.Key.Shared_Filter, JsonSerializer.Serialize(Filter.Default));
            _preferences.SetText(Preferences.Key.Shared_SortOrder, JsonSerializer.Serialize(SortOrder.Default));
        });

        // Remove focus from the search box so we can replace the text.
        _browser.Focus();

        await UpdateFilterSortFromPreferencesAsync().ConfigureAwait(true);
    }

    private async Task ChangeFilterAsync(Func<Filter, Filter> func)
    {
        var filter = _preferences.GetJson<Filter>(Preferences.Key.Shared_Filter);
        filter = func(filter);
        _preferences.SetJson(Preferences.Key.Shared_Filter, filter);
        await UpdateFilterSortFromPreferencesAsync().ConfigureAwait(true);
    }

    private async void FilterOrButton_Click(object? sender, EventArgs e)
    {
        await ChangeFilterAsync(x => x with { Or = true }).ConfigureAwait(true);
    }

    private async void FilterAndButton_Click(object? sender, EventArgs e)
    {
        await ChangeFilterAsync(x => x with { Or = false }).ConfigureAwait(true);
    }

    private void UpdateTagTypes()
    {
        // Remove main menu items for tag types.
        while (_menuButton.DropDownItems.Count > 1 && _menuButton.DropDownItems[1] is not ToolStripSeparator)
            _menuButton.DropDownItems.RemoveAt(1);

        // Remove filter menu items up to the first separator.
        while (_filterButton.DropDownItems.Count > 0 && _filterButton.DropDownItems[0] is not ToolStripSeparator)
            _filterButton.DropDownItems.RemoveAt(0);

        // Remove sort menu items after the "sort by date added" item.
        var lastIndex = _sortButton.DropDownItems.IndexOf(_sortByDateAddedButton);
        while (_sortButton.DropDownItems.Count > lastIndex + 1)
            _sortButton.DropDownItems.RemoveAt(lastIndex + 1);

        var tagTypes = _libraryProvider.GetTagTypes().ToDictionary(x => x.Id);

        List<FilterField> filterFields = [];
        foreach (var tagType in tagTypes.Values.OrderByDescending(x => x.SortIndex))
        {
            // Add menu item to the main menu for viewing the list page.
            var menuItem = _ui.NewToolStripMenuItem($"Browse {tagType.PluralName.ToLower()}");
            _menuButton.DropDownItems.Insert(1, menuItem);
            menuItem.Click += delegate
            {
                var query = HttpUtility.ParseQueryString("");
                query["sessionPassword"] = _client.SessionPassword;
                query["type"] = "TagType";
                query["tagTypeId"] = tagType.Id.Value;
                Navigate($"/list.html?{query}");
            };

            // Add filter menu item
            FilterField filterField = new(FilterFieldType.TagType, tagType.Id);
            filterFields.Add(filterField);

            // Add sort menu item
            var sortItem = _ui.NewToolStripMenuItem($"By {tagType.SingularName.ToLower()}");
            sortItem.Tag = tagType.Id;
            sortItem.Click += SortItem_Click;
            _sortButton.DropDownItems.Insert(lastIndex + 1, sortItem);
        }

        // Add a filter field for the filename, which is special.
        filterFields.Add(new(FilterFieldType.Filename, null));

        // Update the filter menu.
        var filterOperators = Enum.GetValues<FilterOperator>();
        foreach (var filterField in filterFields)
        {
            TagType? filterFieldTagType = null;
            if (filterField.TagTypeId is not null)
                filterFieldTagType = tagTypes[filterField.TagTypeId];
            var fieldItem = _ui.NewToolStripMenuItem(filterField.GetDisplayName(filterFieldTagType));
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
        if (_importControl.ImportInProgress)
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

#if !DEBUG
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
#endif

        var state = CompleteWindowState.Save(this);
        _preferences.SetJson(Preferences.Key.MainForm_CompleteWindowState, state);

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

        var textBox = _searchText.TextBox;
        var left = textBox.PointToScreen(Point.Empty).X;
        var right = left + textBox.Width;
        if (pt.X >= left && pt.Y <= right)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    private void SearchText_TextChanged(object? sender, EventArgs e)
    {
        if (_inhibitSearchTextChangedEvent)
            return;

        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private async void SearchDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _searchDebounceTimer.Stop();

        await ChangeFilterAsync(x => x with { Search = _searchText.Text }).ConfigureAwait(true);
    }

    private async void OptionsButton_Click(object? sender, EventArgs e)
    {
        var oldM3u8Settings = _preferences.GetJson<M3u8SyncSettings>(Preferences.Key.M3u8FolderSync_Settings);

        using var f = _serviceProvider.GetRequiredService<OptionsForm>();
        if (f.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var newM3u8Settings = _preferences.GetJson<M3u8SyncSettings>(Preferences.Key.M3u8FolderSync_Settings);

            if (!oldM3u8Settings.Equals(newM3u8Settings))
            {
                _client.Restart();
                _m3U8FolderSync.InvalidateAll();
                SimpleProgressForm.Do(
                    this,
                    "Synchronizing network sharing folder...",
                    (updateProgress, cancel) =>
                    {
                        _m3U8FolderSync.Sync(updateProgress);
                    }
                );
            }

            ApplyFullscreenPreference();
            await _client.RefreshLibraryAsync(CancellationToken.None).ConfigureAwait(true);
            _browser.Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyFullscreenPreference()
    {
        if (WindowState == FormWindowState.Minimized)
            return;

        var isMaximized = WindowState == FormWindowState.Maximized;
        var isFullscreen = FormBorderStyle == FormBorderStyle.None;
        var behavior = _preferences.GetEnum<WindowMaximizeBehavior>(Preferences.Key.MainForm_WindowMaximizeBehavior);

        if (isFullscreen && !isMaximized)
        {
            ExitFullscreen();
        }
        else if (behavior == WindowMaximizeBehavior.Fullscreen && isMaximized && !isFullscreen)
        {
            EnterFullscreen();
        }
        else if (behavior == WindowMaximizeBehavior.Windowed && isFullscreen)
        {
            ExitFullscreen();
        }

        isFullscreen = FormBorderStyle == FormBorderStyle.None;
        if (isFullscreen)
        {
            Location = new(0, 0);
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
        _rightmostSeparator.Visible = true;
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
    }

    private void ExitFullscreen()
    {
        _minimizeButton!.Visible = false;
        _exitButton.Visible = false;
        _fullscreenButton.Visible = false;
        _rightmostSeparator.Visible = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowIcon = true;
        Icon = _ui.GetIconResource("App.ico");
        WindowState = FormWindowState.Normal;
    }

    private void Browser_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var messageString = e.TryGetWebMessageAsString();
        if (messageString is null)
            return;

        var message = JsonSerializer.Deserialize<PageToHostMessageJson>(messageString);

        switch (message.Type)
        {
            case "search":
                _searchText.TextBox.Focus();
                _searchText.TextBox.SelectAll();
                break;

            case "context-menu":
                if (message.Ids is null || message.Ids.Count == 0)
                    return;
                if (MovieId.HasPrefix(message.Ids[0]))
                {
                    ShowMovieContextMenu(message.Ids.Select(x => new MovieId(x)));
                }
                else if (TagId.HasPrefix(message.Ids[0]))
                {
                    ShowTagContextMenu(message.Ids.Select(x => new TagId(x)));
                }
                break;

            case "close-context-menu":
                _filterButton.DropDown.Close();
                _sortButton.DropDown.Close();
                _menuButton.DropDown.Close();
                _movieContextMenu.Close();
                break;

            case "play":
                OpenMovie(new MovieId(message.Ids!.First()));
                break;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Ctrl+F or /
        if ((e.Control && e.KeyCode == Keys.F) || e.KeyCode == Keys.OemQuestion)
        {
            e.SuppressKeyPress = true;
            _searchText.TextBox.Focus();
            _searchText.TextBox.SelectAll();
        }

        base.OnKeyDown(e);
    }

    private void Browser_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        // Never cache anything. Consider when you navigate away from the movies page, then toggle Shuffle, then go
        // back. We need to reload that movies page when you go back because the Shuffle state has changed. We don't
        // need a cache anyway since the server is another process on the same machine.
        e.Request.Headers.SetHeader("Cache-Control", "no-cache, no-store, must-revalidate");
        e.Request.Headers.SetHeader("Pragma", "no-cache");
        e.Request.Headers.SetHeader("Expires", "0");
    }

    private void ShowMovieContextMenu(IEnumerable<MovieId> movieIds)
    {
        _movieContextMenuIds.Clear();
        _movieContextMenuIds.AddRange(movieIds);
        var one = _movieContextMenuIds.Count == 1;
        _movieContextOpenButton.Enabled = one;
        _movieContextPropertiesButton.Enabled = one;
        _movieContextMenu.Show(Cursor.Position);
    }

    private void MovieContextOpenButton_Click(object? sender, EventArgs e)
    {
        if (_movieContextMenuIds.Count != 1)
            return;

        OpenMovie(_movieContextMenuIds.Single());
    }

    private void MovieContextDeleteButton_Click(object? sender, EventArgs e)
    {
        DeleteMovies(_movieContextMenuIds);
    }

    private void MovieContextAddTagButton_Click(object? sender, EventArgs e)
    {
        AddTagToMovies(_movieContextMenuIds);
    }

    private void MovieContextPropertiesButton_Click(object? sender, EventArgs e)
    {
        if (_movieContextMenuIds.Count != 1)
            return;

        using var f = _serviceProvider.GetRequiredService<MoviePropertiesForm>();
        f.Initialize(_movieContextMenuIds.Single());
        if (f.ShowDialog(this) != DialogResult.OK)
            return;

        _browser.Reload();
    }

    private void ShowTagContextMenu(IEnumerable<TagId> tagIds)
    {
        //TODO
        throw new NotImplementedException();
    }

    private void OpenMovie(MovieId movieId)
    {
        var movie = _libraryProvider.GetMovie(movieId);
        var query = HttpUtility.ParseQueryString("");
        query["movieId"] = movie.Id.Value;
        query["sessionPassword"] = _client.SessionPassword;
        var url = $"http://localhost:{_client.Port}/movie.m3u8?{query}";

        var which = _preferences.GetEnum<VlcInstallationToUse>(Preferences.Key.Shared_VlcInstallationToUse);
        if (which == VlcInstallationToUse.Automatic)
            which = IsVlcInstalled() ? VlcInstallationToUse.System : VlcInstallationToUse.Bundled;

        var extraArgs = "";
        if (which == VlcInstallationToUse.Bundled)
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Jackpot",
                "vlcrc"
            );

            if (!File.Exists(configPath))
            {
                File.WriteAllText(
                    configPath,
                    """
                    metadata-network-access=0
                    qt-updates-notif=0
                    qt-privacy-ask=0
                    """
                );
            }

            extraArgs = $"--config \"{configPath}\"";
        }

        ProcessStartInfo psi =
            new()
            {
                FileName =
                    which == VlcInstallationToUse.System
                        ? "vlc.exe"
                        : Path.Combine(AppContext.BaseDirectory, "..", "vlc", "vlc.exe"),
                Arguments = $"--fullscreen --loop --high-priority --no-video-title-show {extraArgs} -- \"{url}\"",
                UseShellExecute = which == VlcInstallationToUse.System,
            };

#if DEBUG
        // When debugging, it can be annoying for VLC to actually appear every time.
        if (
            MessageBox.Show(
                "Proceed?\n\n" + psi.FileName + " " + psi.Arguments,
                "DEBUG - Open Movie",
                MessageBoxButtons.OKCancel
            ) != DialogResult.OK
        )
            return;
#endif

        using var p = Process.Start(psi)!;
        ApplicationSubProcesses.Add(p);

        static bool IsVlcInstalled()
        {
            try
            {
                using var key = Registry.ClassesRoot.OpenSubKey(@"Applications\vlc.exe");
                return key is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    private void DeleteMovies(List<MovieId> movieIds)
    {
        if (movieIds.Count == 0)
            return;

        string message;
        if (movieIds.Count == 1)
        {
            var movie = _libraryProvider.GetMovie(movieIds[0]);
            message = $"Are you sure you want to delete this movie?\n\n ● {movie.Filename}";
        }
        else
        {
            List<string> names = [];
            foreach (var id in movieIds.Take(5))
            {
                var movie = _libraryProvider.GetMovie(id);
                names.Add(" ● " + movie.Filename);
            }
            if (movieIds.Count > 5)
                names.Add($"(and {movieIds.Count - 5:#,##0} more)");
            message =
                $"Are you sure you want to delete these {movieIds.Count:#,##0} movies?\n\n{string.Join("\n\n", names)}";
        }

        if (
            MessageBox.Show(
                message,
                "Delete Movie",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2
            ) != DialogResult.OK
        )
        {
            return;
        }

        try
        {
            using SimpleProgressForm f =
                new(
                    (updateProgress, updateMessage, cancel) =>
                    {
                        updateMessage("Deleting...");
                        _libraryProvider.DeleteMoviesAsync(movieIds, updateProgress, cancel).GetAwaiter().GetResult();
                    }
                );

            var result = f.ShowDialog(this);
            if (result == DialogResult.Abort)
            {
                MessageBox.Show(
                    this,
                    f.Exception!.SourceException.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }

            _browser.Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddTagToMovies(List<MovieId> movieContextMenuIds)
    {
        using var f = _serviceProvider.GetRequiredService<AddTagToMoviesForm>();
        f.Initialize(movieContextMenuIds);
        f.Text = "Add Tag";
        if (f.ShowDialog(this) == DialogResult.OK)
            _browser.Reload();
    }

    private async void ViewListButton_Click(object? sender, EventArgs e)
    {
        await ChangeViewAsync(LibraryViewStyle.List).ConfigureAwait(true);
    }

    private async void ViewGridButton_Click(object? sender, EventArgs e)
    {
        await ChangeViewAsync(LibraryViewStyle.Grid).ConfigureAwait(true);
    }

    private async Task ChangeViewAsync(LibraryViewStyle style)
    {
        _preferences.SetEnum(Preferences.Key.Shared_LibraryViewStyle, style);
        await UpdateViewFromPreferencesAsync().ConfigureAwait(true);
    }

    private async Task UpdateViewFromPreferencesAsync(bool reload = true, CancellationToken cancel = default)
    {
        var style = _preferences.GetEnum<LibraryViewStyle>(Preferences.Key.Shared_LibraryViewStyle);
        _viewListButton.Checked = style == LibraryViewStyle.List;
        _viewGridButton.Checked = style == LibraryViewStyle.Grid;
        await _client.RefreshLibraryAsync(cancel).ConfigureAwait(true);
        if (reload)
            _browser.Reload();
    }

    private void MovieContextExportButton_Click(object? sender, EventArgs e)
    {
        var movieIds = _movieContextMenuIds;

        // Prompt for output directory.
        using FolderBrowserDialog b =
            new()
            {
                AutoUpgradeEnabled = true,
                Description = "Select Output Directory",
                ShowNewFolderButton = true,
                RootFolder = Environment.SpecialFolder.Desktop,
                UseDescriptionForTitle = true,
            };
        if (b.ShowDialog(this) != DialogResult.OK)
            return;
        var outDir = b.SelectedPath;

        var movies = _libraryProvider.GetMovies().ToDictionary(x => x.Id);

        using SimpleProgressForm f =
            new(
                (updateProgress, updateMessage, cancel) =>
                {
                    var count = movieIds.Count;
                    var i = 0;
                    var fileInterval = 1d / count;
                    foreach (var movieId in movieIds)
                    {
                        var movie = movies[movieId];
                        var outFilePath = Path.Combine(outDir, movie.Filename + ".mp4");

                        var name = movie.Filename;
                        if (name.Length > 40)
                            name = name[..40] + "...";

                        updateMessage($"File {i + 1:#,##0} of {count:#,##0}\n{name}");

                        if (!File.Exists(outFilePath))
                            _movieExporter.Export(
                                movie,
                                outFilePath,
                                x => updateProgress((x + i) * fileInterval),
                                cancel
                            );

                        i++;
                        updateProgress((double)i / count);
                    }
                }
            );

        if (f.ShowDialog(this) == DialogResult.Abort)
        {
            MessageBox.Show(
                this,
                f.Exception!.SourceException.Message,
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void ImportTabButton_Click(object? sender, EventArgs e)
    {
        SwitchTab(_importTabButton);
    }

    private void BrowserTabButton_Click(object? sender, EventArgs e)
    {
        SwitchTab(_browserTabButton);
    }

    private void TagsTabButton_Click(object? sender, EventArgs e)
    {
        SwitchTab(_tagsTabButton);
    }

    private ToolStripButton GetTab()
    {
        if (_importTabButton.Checked)
            return _importTabButton;
        if (_browserTabButton.Checked)
            return _browserTabButton;
        if (_tagsTabButton.Checked)
            return _tagsTabButton;
        throw new InvalidOperationException();
    }

    private void SwitchTab(ToolStripButton tab)
    {
        var wasBrowser = ReferenceEquals(GetTab(), _browserTabButton);
        var isImport = ReferenceEquals(tab, _importTabButton);
        var isTags = ReferenceEquals(tab, _tagsTabButton);
        var isBrowser = ReferenceEquals(tab, _browserTabButton);

        _importControl.Visible = _importTabButton.Checked = isImport;
        _tagsControl.Visible = _tagsTabButton.Checked = isTags;
        _browser.Visible = _browserTabButton.Checked = isBrowser;

        if (wasBrowser && !isBrowser)
        {
            // Switching away from the browser.
            foreach (ToolStripItem item in _toolStrip.Items)
            {
                if (
                    ReferenceEquals(item, _browserTabButton)
                    || ReferenceEquals(item, _tagsTabButton)
                    || ReferenceEquals(item, _importTabButton)
                    || ReferenceEquals(item, _minimizeButton)
                    || ReferenceEquals(item, _exitButton)
                    || ReferenceEquals(item, _fullscreenButton)
                )
                {
                    continue;
                }

                _previousToolStripItemEnabledStates[item] = item.Enabled;
                item.Enabled = isBrowser;
            }
        }
        else if (!wasBrowser && isBrowser)
        {
            // Switching back to the browser.
            foreach (var (item, enabled) in _previousToolStripItemEnabledStates)
                item.Enabled = enabled;

            _previousToolStripItemEnabledStates.Clear();
        }

        if (isImport)
            _importControl.Focus();
        else if (isTags)
            _tagsControl.Focus();
        else if (isBrowser)
            _browser.Focus();
    }
}
