using System.Diagnostics;
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

        Controls.Add(_tabs = ui.NewTabControl(100));
        {
            _tabs.TabPages.Add(_newTab = new());
            {
                _newTab.Create += NewTab_Create;
            }
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
        var tagTypes = _libraryProvider.GetTagTypes();
        var sortIndex = tagTypes.Count == 0 ? 0 : tagTypes.Max(x => x.SortIndex) + 1;
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
        var firstTime = selectedTab is null;
        var isNewTabSelected = selectedTab is NewTagTypeTab;
        var selectedTag = firstTime || isNewTabSelected ? null : ((TagsTab)selectedTab!).TagTypeId;

        // Delete every tab except the New Tab.
        for (int i = _tabs.TabPages.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(_tabs.TabPages[i], _newTab))
                _tabs.TabPages.RemoveAt(i);
        }

        // Insert a tab for each tag type.
        var tagTypes = _libraryProvider.GetTagTypes().OrderBy(x => x.SortIndex).ToList();
        _tagsTabs.Clear();
        for (var i = 0; i < tagTypes.Count; i++)
        {
            var tagType = tagTypes[i];
            var isFirst = i == 0;
            var isLast = i == tagTypes.Count - 1;
            TagsTab tab = new(tagType, isFirst, isLast, _libraryProvider, _serviceProvider);
            tab.TagTypeChanged += delegate
            {
                UpdateTagTabs();
            };
            var index = _tabs.TabCount - 1;
            _tabs.TabPages.Insert(index, tab);
            _tagsTabs[tagType.Id] = tab;
        }

        // Restore the previous selection if possible.
        if (firstTime)
        {
            _tabs.SelectedIndex = 0;
        }
        else if (isNewTabSelected)
        {
            _tabs.SelectedTab = _newTab;
        }
        else
        {
            if (_tagsTabs.TryGetValue(selectedTag!, out var tagsTab))
                _tabs.SelectedTab = tagsTab;
            else
                _tabs.SelectedIndex = 0;
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

    private sealed class TagsTab : MyTabPage
    {
        private readonly TagType _type;
        private readonly LibraryProviderAdapter _libraryProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly TableLayoutPanel _table;
        private readonly FlowLayoutPanel _topButtonFlow,
            _bottomButtonFlow;
        private readonly Button _groupRenameButton,
            _groupDeleteButton,
            _groupMoveLeftButton,
            _groupMoveRightButton,
            _newButton;
        private readonly ListBox _listBox;
        private List<Tag> _tags = [];

        public TagTypeId TagTypeId => _type.Id;

        public event EventHandler? TagTypeChanged;

        public TagsTab(
            TagType type,
            bool isFirst,
            bool isLast,
            LibraryProviderAdapter libraryProvider,
            IServiceProvider serviceProvider
        )
        {
            Text = type.PluralName;
            _type = type;
            _libraryProvider = libraryProvider;
            _serviceProvider = serviceProvider;
            Ui ui = new(this);

            Controls.Add(_table = ui.NewTable(1, 4));
            {
                _table.Padding = ui.DefaultPadding;
                _table.RowStyles[1].SizeType = SizeType.Percent;
                _table.RowStyles[1].Height = 100;
                _table.RowStyles[2].SizeType = SizeType.AutoSize;

                _table.Controls.Add(_topButtonFlow = ui.NewFlowRow());
                {
                    _topButtonFlow.Margin = ui.BottomSpacing;

                    _topButtonFlow.Controls.Add(
                        _newButton = ui.NewButton($"New {type.SingularName.ToLowerInvariant()}...")
                    );
                    {
                        _newButton.Click += NewButton_Click;
                    }
                }

                _table.Controls.Add(_listBox = ui.NewListBox(), 0, 1);
                {
                    _listBox.Margin = ui.BottomSpacing;
                    _listBox.DoubleClick += ListBox_DoubleClick;
                }

                _table.Controls.Add(ui.NewLabel($"\"{type.PluralName}\" group:"), 0, 2);

                _table.Controls.Add(_bottomButtonFlow = ui.NewFlowRow(), 0, 3);
                {
                    _bottomButtonFlow.Controls.Add(_groupMoveLeftButton = ui.NewButton("← Move left"));
                    {
                        _groupMoveLeftButton.Enabled = !isFirst;
                        _groupMoveLeftButton.Click += GroupMoveLeftButton_Click;
                    }

                    _bottomButtonFlow.Controls.Add(_groupMoveRightButton = ui.NewButton("Move right →"));
                    {
                        _groupMoveRightButton.Enabled = !isLast;
                        _groupMoveRightButton.Click += GroupMoveRightButton_Click;
                    }

                    _bottomButtonFlow.Controls.Add(_groupRenameButton = ui.NewButton("Rename..."));
                    {
                        _groupRenameButton.Click += GroupRenameButton_Click;
                    }

                    _bottomButtonFlow.Controls.Add(_groupDeleteButton = ui.NewButton("Delete"));
                    {
                        _groupDeleteButton.Click += GroupDeleteButton_Click;
                    }
                }
            }

            UseVisualStyleBackColor = true;
        }

        private void NewButton_Click(object? sender, EventArgs e)
        {
            EditTag(null);
        }

        private void GroupDeleteButton_Click(object? sender, EventArgs e)
        {
            var tagType = _libraryProvider.GetTagType(TagTypeId);

            var response = MessageBox.Show(
                this,
                $"Are you sure you want to delete the \"{tagType.PluralName}\" tag group?",
                "Delete",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question
            );
            if (response != DialogResult.OK)
                return;

            SimpleProgressForm.Do(
                this,
                "Deleting tag group...",
                async (updateProgress, cancel) =>
                {
                    await _libraryProvider.DeleteTagTypeAsync(TagTypeId, updateProgress, cancel).ConfigureAwait(true);
                }
            );

            TagTypeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GroupRenameButton_Click(object? sender, EventArgs e)
        {
            var tagType = _libraryProvider.GetTagType(TagTypeId);

            using var f = _serviceProvider.GetRequiredService<EditTagsRenameTagTypeForm>();
            f.Initialize(tagType);
            if (f.ShowDialog(FindForm()) == DialogResult.OK)
                TagTypeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GroupMoveRightButton_Click(object? sender, EventArgs e)
        {
            GroupMove(1);
        }

        private void GroupMoveLeftButton_Click(object? sender, EventArgs e)
        {
            GroupMove(-1);
        }

        private void GroupMove(int direction)
        {
            var tagTypes = _libraryProvider.GetTagTypes().OrderBy(x => x.SortIndex).ToList();

            var tagTypeIndex = -1;
            for (var i = 0; i < tagTypes.Count; i++)
            {
                if (tagTypes[i].Id == TagTypeId)
                {
                    tagTypeIndex = i;
                    break;
                }
            }
            Debug.Assert(tagTypeIndex != -1);

            var swapIndex = tagTypeIndex + direction;
            Debug.Assert(swapIndex >= 0 && swapIndex < tagTypes.Count);

            (tagTypes[tagTypeIndex], tagTypes[swapIndex]) = (tagTypes[swapIndex], tagTypes[tagTypeIndex]);

            for (var i = 0; i < tagTypes.Count; i++)
            {
                tagTypes[i] = tagTypes[i] with { SortIndex = i };
            }

            SimpleProgressForm.Do(
                this,
                "Moving tag group...",
                async (updateProgress, cancel) =>
                {
                    await _libraryProvider.UpdateTagTypesAsync(tagTypes, updateProgress, cancel).ConfigureAwait(true);
                }
            );

            TagTypeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ListBox_DoubleClick(object? sender, EventArgs e)
        {
            ActivateSelectedItem();
        }

        private void ActivateSelectedItem()
        {
            var i = _listBox.SelectedIndex;

            if (i >= 0 && i < _listBox.Items.Count)
            {
                var id = _tags[i].Id;
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
            using var f = _serviceProvider.GetRequiredService<EditTagsEditTagForm>();
            f.Initialize(_type, id);
            if (f.ShowDialog(FindForm()) == DialogResult.OK)
                UpdateList();
        }

        public void UpdateList()
        {
            _tags = [.. _libraryProvider.GetTags(_type.Id).OrderBy(x => x.Name)];
            _listBox.Items.Clear();
            foreach (var x in _tags)
                _listBox.Items.Add(x.Name);
        }
    }

    private sealed class NewTagTypeTab : MyTabPage
    {
        private readonly FlowLayoutPanel _flow;
        private readonly TextBox _singularNameText,
            _pluralNameText;
        private readonly Button _createButton;

        public readonly record struct CreateEventArgs(string SingularName, string PluralName);

        public event EventHandler<CreateEventArgs>? Create;

        public NewTagTypeTab()
        {
            Text = "➕";
            Ui ui = new(this);

            Controls.Add(_flow = ui.NewFlowColumn());
            {
                _flow.Padding = ui.DefaultPadding;
                Control p;

                (p, _singularNameText) = ui.NewLabeledTextBox("&Singular name:", 125);
                _flow.Controls.Add(p);

                (p, _pluralNameText) = ui.NewLabeledTextBox("&Plural name:", 125);
                _flow.Controls.Add(p);
                {
                    p.Margin = ui.TopSpacing;
                }

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
