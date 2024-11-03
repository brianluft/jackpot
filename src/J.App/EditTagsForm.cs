using J.Core.Data;
using Microsoft.Extensions.DependencyInjection;

namespace J.App;

public sealed class EditTagsForm : Form
{
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly TabControl _tabs;
    private readonly NewTagTypeTab _newTab;
    private readonly Dictionary<TagTypeId, TagsTab> _tagsTabs = [];

    public EditTagsForm(LibraryProviderAdapter libraryProvider, IServiceProvider serviceProvider)
    {
        _libraryProvider = libraryProvider;
        _serviceProvider = serviceProvider;
        Ui ui = new(this);

        Controls.Add(_tabs = ui.NewTabControl());

        _tabs.TabPages.Add(_newTab = new());
        {
            _newTab.Create += NewTab_Create;
        }

        UpdateTagTabs();

        Text = "Edit Tags";
        StartPosition = FormStartPosition.CenterParent;
        Size = ui.GetSize(550, 600);
        MinimumSize = ui.GetSize(550, 200);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        Padding = ui.DefaultPadding;
        DoubleBuffered = true;
    }

    private void NewTab_Create(object? sender, NewTagTypeTab.CreateEventArgs e)
    {
        var sortIndex = _libraryProvider.GetTagTypes().Max(x => x.SortIndex) + 1;
        TagType tagType = new(new(), sortIndex, e.SingularName, e.PluralName);

        SimpleProgressForm.Do(
            this,
            "Creating tag...",
            async (updateProgress, cancel) =>
            {
                await _libraryProvider.NewTagTypeAsync(tagType, updateProgress, cancel).ConfigureAwait(true);
            }
        );

        UpdateTagTabs();
        _tabs.SelectedIndex = _tagsTabs[tagType.Id].TabIndex;
    }

    private void UpdateTagTabs()
    {
        // Remember the current selection.
        var selectedTab = _tabs.SelectedTab;
        var isNewTabSelected = selectedTab is null || selectedTab is NewTagTypeTab;
        var selectedTag = isNewTabSelected ? null : ((TagsTab)selectedTab!).TagTypeId;

        // Delete every tab except the New Tab.
        for (int i = _tabs.TabPages.Count - 1; i >= 1; i--)
        {
            if (!ReferenceEquals(_tabs.TabPages[i], _newTab))
                _tabs.TabPages.RemoveAt(i);
        }

        // Insert a tab for each tag type.
        var tagTypes = _libraryProvider.GetTagTypes().OrderBy(x => x.SortIndex);
        _tagsTabs.Clear();
        foreach (var tagType in tagTypes)
        {
            TagsTab tab = new(tagType, _libraryProvider, _serviceProvider);
            var index = _tabs.TabCount - 1;
            _tabs.TabPages.Insert(index, tab);
            _tagsTabs[tagType.Id] = tab;
        }

        // Restore the previous selection if possible.
        if (isNewTabSelected)
        {
            _tabs.SelectedIndex = _newTab.TabIndex;
        }
        else
        {
            if (_tagsTabs.TryGetValue(selectedTag!, out var tagsTab))
                _tabs.SelectedIndex = tagsTab.TabIndex;
            else
                _tabs.SelectedIndex = _newTab.TabIndex;
        }

        // We recreated the listboxes so we have to populate them again.
        UpdateAllLists();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        UpdateAllLists();
    }

    private void UpdateAllLists()
    {
        foreach (var tagsTab in _tagsTabs.Values)
            tagsTab.UpdateList();
    }

    private sealed class TagsTab : TabPage
    {
        private readonly TagType _type;
        private readonly LibraryProviderAdapter _libraryProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly TableLayoutPanel _table;
        private readonly Label _groupLabel;
        private readonly FlowLayoutPanel _buttonFlow;
        private readonly Button _renameButton,
            _deleteButton,
            _leftButton,
            _rightButton;
        private readonly ListBox _listBox;
        private List<Tag> _tags = [];

        public TagTypeId TagTypeId => _type.Id;

        public TagsTab(TagType type, LibraryProviderAdapter libraryProvider, IServiceProvider serviceProvider)
            : base(type.PluralName)
        {
            _type = type;
            _libraryProvider = libraryProvider;
            _serviceProvider = serviceProvider;
            Ui ui = new(this);

            Controls.Add(_table = ui.NewTable(1, 3));
            {
                _table.Padding = ui.DefaultPadding;
                _table.RowStyles[0].SizeType = SizeType.Percent;
                _table.RowStyles[0].Height = 100;
                _table.RowStyles[1].SizeType = SizeType.AutoSize;

                _table.Controls.Add(_listBox = ui.NewListBox(), 0, 0);
                {
                    _listBox.Margin = ui.BottomSpacing;
                    _listBox.DoubleClick += ListBox_DoubleClick;
                }

                _table.Controls.Add(_groupLabel = ui.NewLabel($"\"{type.PluralName}\" group:"), 0, 1);

                _table.Controls.Add(_buttonFlow = ui.NewFlowRow(), 0, 2);
                {
                    _buttonFlow.Controls.Add(_leftButton = ui.NewButton("← Move left"));
                    _leftButton.Click += LeftButton_Click;

                    _buttonFlow.Controls.Add(_rightButton = ui.NewButton("Move right →"));
                    _rightButton.Click += RightButton_Click;

                    _buttonFlow.Controls.Add(_renameButton = ui.NewButton("Rename..."));
                    _renameButton.Click += RenameButton_Click;

                    _buttonFlow.Controls.Add(_deleteButton = ui.NewButton("Delete"));
                    _deleteButton.Click += DeleteButton_Click;
                }
            }

            UseVisualStyleBackColor = true;
        }

        private void DeleteButton_Click(object? sender, EventArgs e)
        {
            throw new NotImplementedException(); //TODO
        }

        private void RenameButton_Click(object? sender, EventArgs e)
        {
            throw new NotImplementedException(); //TODO
        }

        private void RightButton_Click(object? sender, EventArgs e)
        {
            throw new NotImplementedException(); //TODO
        }

        private void LeftButton_Click(object? sender, EventArgs e)
        {
            throw new NotImplementedException(); //TODO
        }

        private void ListBox_DoubleClick(object? sender, EventArgs e)
        {
            ActivateSelectedItem();
        }

        private void ActivateSelectedItem()
        {
            if (_listBox.SelectedIndex >= _tags.Count)
            {
                // New
                EditTag(null);
            }
            else if (_listBox.SelectedIndex >= 0)
            {
                // Edit
                var id = _tags[_listBox.SelectedIndex].Id;
                EditTag(id);
            }
        }

        // If the user hits Enter, do the same thing as double clicking.
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                ActivateSelectedItem();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        private void EditTag(TagId? id)
        {
            using var f = _serviceProvider.GetRequiredService<EditTagForm>();
            f.OpenTag(_type, id);
            if (f.ShowDialog(FindForm()) == DialogResult.OK)
                UpdateList();
        }

        public void UpdateList()
        {
            _tags = _libraryProvider.GetTags(_type.Id).OrderBy(x => x.Name).ToList();
            _listBox.Items.Clear();
            foreach (var x in _tags)
                _listBox.Items.Add(x.Name);
            _listBox.Items.Add($"(New {_type.SingularName.ToLower()}...)");
        }
    }

    private sealed class NewTagTypeTab : TabPage
    {
        private readonly FlowLayoutPanel _flow;
        private readonly TextBox _singularNameText,
            _pluralNameText;
        private readonly Button _createButton;

        public readonly record struct CreateEventArgs(string SingularName, string PluralName);

        public event EventHandler<CreateEventArgs>? Create;

        public NewTagTypeTab()
            : base("➕")
        {
            Ui ui = new(this);

            Controls.Add(_flow = ui.NewFlowColumn());
            {
                _flow.Padding = ui.DefaultPadding;
                Control p;

                (p, _singularNameText) = ui.NewLabeledTextBox("&Singular name:", 125);
                _flow.Controls.Add(p);

                (p, _pluralNameText) = ui.NewLabeledTextBox("&Plural name:", 125);
                p.Margin = ui.TopSpacing;
                _flow.Controls.Add(p);

                _flow.Controls.Add(_createButton = ui.NewButton("&Create tag group"));
                {
                    _createButton.Margin = ui.TopSpacingBig;
                    _createButton.Click += CreateButton_Click;
                }
            }

            UseVisualStyleBackColor = true;
        }

        private void CreateButton_Click(object? sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_singularNameText.Text))
                    throw new Exception("Please enter a singular name.");

                if (string.IsNullOrWhiteSpace(_pluralNameText.Text))
                    throw new Exception("Please enter a plural name.");

                Create?.Invoke(this, new(_singularNameText.Text, _pluralNameText.Text));
                _singularNameText.Text = "";
                _pluralNameText.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
