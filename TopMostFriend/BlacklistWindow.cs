using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TopMostFriend;

public sealed class BlacklistWindow : Form
{
    private readonly List<string> _titleBlacklist;
    private readonly List<string> _processBlacklist;

    private readonly TabControl _tabs;
    private readonly ListBox _titleList;
    private readonly ListBox _processList;

    private readonly Button _edit;
    private readonly Button _remove;

    public static (string[] Titles, string[] Processes)? Display(
        string title,
        string[]? titles,
        string[]? processes)
    {
        using BlacklistWindow window = new(
            title,
            titles ?? Array.Empty<string>(),
            processes ?? Array.Empty<string>());

        return window.ShowDialog() == DialogResult.OK
            ? (
                window._titleBlacklist.ToArray(),
                window._processBlacklist.ToArray())
            : null;
    }

    private BlacklistWindow(
        string title,
        IEnumerable<string> titles,
        IEnumerable<string> processes)
    {
        _titleBlacklist = titles
            .Distinct(StringComparer.Ordinal)
            .ToList();

        _processBlacklist = processes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Text = title;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(500, 300);
        MinimumSize = new Size(430, 280);
        MinimizeBox = false;
        MaximizeBox = false;
        TopMost = true;
        Icon = AppIcon.Window;

        _tabs = new TabControl
        {
            Location = new Point(8, 8),
            Size = new Size(482, 225),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };

        TabPage titleTab = new()
        {
            Text = Locale.String("BlacklistTitles"),
        };

        TabPage processTab = new()
        {
            Text = Locale.String("BlacklistProcesses"),
        };

        _titleList = CreateListBox();
        _processList = CreateListBox();

        titleTab.Controls.Add(_titleList);
        processTab.Controls.Add(_processList);

        _tabs.TabPages.Add(titleTab);
        _tabs.TabPages.Add(processTab);
        _tabs.SelectedIndexChanged += (_, _) => UpdateButtons();

        _titleList.SelectedIndexChanged += (_, _) => UpdateButtons();
        _processList.SelectedIndexChanged += (_, _) => UpdateButtons();

        _titleList.DoubleClick += (_, _) => EditSelected();
        _processList.DoubleClick += (_, _) => EditSelected();

        Button add = new()
        {
            Text = Locale.String("BlacklistAdd"),
            Location = new Point(400, 241),
            Size = new Size(90, 27),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        add.Click += (_, _) => AddNew();

        _edit = new Button
        {
            Text = Locale.String("BlacklistEdit"),
            Location = new Point(300, 241),
            Size = new Size(90, 27),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Enabled = false,
        };
        _edit.Click += (_, _) => EditSelected();

        _remove = new Button
        {
            Text = Locale.String("BlacklistRemove"),
            Location = new Point(200, 241),
            Size = new Size(90, 27),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Enabled = false,
        };
        _remove.Click += (_, _) => RemoveSelected();

        Button done = new()
        {
            Text = Locale.String("BlacklistDone"),
            Location = new Point(400, 274),
            Size = new Size(90, 27),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.OK,
        };

        Button cancel = new()
        {
            Text = Locale.String("BlacklistCancel"),
            Location = new Point(300, 274),
            Size = new Size(90, 27),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.Cancel,
        };

        AcceptButton = done;
        CancelButton = cancel;

        Controls.AddRange(new Control[]
        {
            _tabs,
            _remove,
            _edit,
            add,
            cancel,
            done,
        });

        RefreshList(_titleList, _titleBlacklist);
        RefreshList(_processList, _processBlacklist);
        UpdateButtons();
    }

    private static ListBox CreateListBox()
    {
        return new ListBox
        {
            Location = new Point(4, 4),
            Size = new Size(466, 185),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            IntegralHeight = false,
        };
    }

    private ListBox CurrentList =>
        _tabs.SelectedIndex == 0 ? _titleList : _processList;

    private List<string> CurrentBlacklist =>
        _tabs.SelectedIndex == 0 ? _titleBlacklist : _processBlacklist;

    private bool IsProcessBlacklist =>
        _tabs.SelectedIndex == 1;

    private void AddNew()
    {
        string? value = PromptForText(
            IsProcessBlacklist
                ? Locale.String("BlacklistEditorAddingProcess")
                : Locale.String("BlacklistEditorAdding"),
            string.Empty);

        if (value == null)
            return;

        List<string> blacklist = CurrentBlacklist;
        StringComparison comparison = IsProcessBlacklist
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!blacklist.Any(x => string.Equals(x, value, comparison)))
            blacklist.Add(value);

        RefreshCurrentList(value);
    }

    private void EditSelected()
    {
        ListBox list = CurrentList;

        if (list.SelectedItem is not string original)
            return;

        string? value = PromptForText(
            IsProcessBlacklist
                ? Locale.String("BlacklistEditorEditingProcess", original)
                : Locale.String("BlacklistEditorEditing", original),
            original);

        if (value == null)
            return;

        List<string> blacklist = CurrentBlacklist;
        StringComparison comparison = IsProcessBlacklist
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        blacklist.Remove(original);

        if (!blacklist.Any(x => string.Equals(x, value, comparison)))
            blacklist.Add(value);

        RefreshCurrentList(value);
    }

    private void RemoveSelected()
    {
        ListBox list = CurrentList;

        if (list.SelectedItem is string value)
            CurrentBlacklist.Remove(value);

        RefreshCurrentList();
    }

    private void RefreshCurrentList(string? select = null)
    {
        RefreshList(CurrentList, CurrentBlacklist, select);
    }

    private static void RefreshList(
        ListBox list,
        IEnumerable<string> values,
        string? select = null)
    {
        list.BeginUpdate();
        try
        {
            list.Items.Clear();

            list.Items.AddRange(
                values
                    .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                    .Cast<object>()
                    .ToArray());

            if (select != null)
                list.SelectedItem = select;
        }
        finally
        {
            list.EndUpdate();
        }
    }

    private void UpdateButtons()
    {
        _edit.Enabled = _remove.Enabled = CurrentList.SelectedIndex >= 0;
    }

    private static string? PromptForText(string title, string initial)
    {
        using Form form = new()
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(520, 82),
            MaximizeBox = false,
            MinimizeBox = false,
            TopMost = true,
            Icon = AppIcon.Window,
        };

        TextBox input = new()
        {
            Text = initial,
            Location = new Point(8, 10),
            Width = 504,
        };

        Button save = new()
        {
            Text = Locale.String("BlacklistEditorSave"),
            Location = new Point(356, 45),
            Size = new Size(75, 27),
            DialogResult = DialogResult.OK,
        };

        Button cancel = new()
        {
            Text = Locale.String("BlacklistEditorCancel"),
            Location = new Point(437, 45),
            Size = new Size(75, 27),
            DialogResult = DialogResult.Cancel,
        };

        form.AcceptButton = save;
        form.CancelButton = cancel;
        form.Controls.AddRange(new Control[] { input, save, cancel });

        form.Shown += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };

        return form.ShowDialog() == DialogResult.OK
            ? input.Text
            : null;
    }
}