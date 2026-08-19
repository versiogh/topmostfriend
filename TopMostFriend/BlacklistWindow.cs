using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TopMostFriend;

public sealed class BlacklistWindow : Form
{
    private readonly List<string> _blacklist;
    private readonly ListBox _list;
    private readonly Button _edit;
    private readonly Button _remove;

    public static string[]? Display(string title, string[]? items)
    {
        using BlacklistWindow window = new(title, items ?? Array.Empty<string>());
        return window.ShowDialog() == DialogResult.OK ? window._blacklist.ToArray() : null;
    }

    private BlacklistWindow(string title, IEnumerable<string> items)
    {
        _blacklist = items.Distinct(StringComparer.Ordinal).ToList();

        Text = title;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(500, 270);
        MinimumSize = new Size(430, 250);
        MinimizeBox = false;
        MaximizeBox = false;
        TopMost = true;
        Icon = AppIcon.Window;

        _list = new ListBox
        {
            Location = new Point(8, 8),
            Size = new Size(380, 220),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            IntegralHeight = false,
        };
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        _list.DoubleClick += (_, _) => EditSelected();

        Button add = new()
        {
            Text = Locale.String("BlacklistAdd"),
            Location = new Point(400, 8),
            Size = new Size(90, 27),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        add.Click += (_, _) => AddNew();

        _edit = new Button
        {
            Text = Locale.String("BlacklistEdit"),
            Location = new Point(400, 41),
            Size = new Size(90, 27),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Enabled = false,
        };
        _edit.Click += (_, _) => EditSelected();

        _remove = new Button
        {
            Text = Locale.String("BlacklistRemove"),
            Location = new Point(400, 74),
            Size = new Size(90, 27),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Enabled = false,
        };
        _remove.Click += (_, _) => RemoveSelected();

        Button done = new()
        {
            Text = Locale.String("BlacklistDone"),
            Location = new Point(400, 202),
            Size = new Size(90, 27),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.OK,
        };

        Button cancel = new()
        {
            Text = Locale.String("BlacklistCancel"),
            Location = new Point(400, 235),
            Size = new Size(90, 27),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.Cancel,
        };

        AcceptButton = done;
        CancelButton = cancel;
        Controls.AddRange(new Control[] { _list, add, _edit, _remove, done, cancel });
        RefreshList();
    }

    private void AddNew()
    {
        string? value = PromptForText(Locale.String("BlacklistEditorAdding"), string.Empty);
        if (value != null && !_blacklist.Contains(value, StringComparer.Ordinal))
            _blacklist.Add(value);
        RefreshList(value);
    }

    private void EditSelected()
    {
        if (_list.SelectedItem is not string original)
            return;

        string? value = PromptForText(Locale.String("BlacklistEditorEditing", original), original);
        if (value == null)
            return;

        _blacklist.Remove(original);
        if (!_blacklist.Contains(value, StringComparer.Ordinal))
            _blacklist.Add(value);
        RefreshList(value);
    }

    private void RemoveSelected()
    {
        if (_list.SelectedItem is string value)
            _blacklist.Remove(value);
        RefreshList();
    }

    private void RefreshList(string? select = null)
    {
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            _list.Items.AddRange(_blacklist.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).Cast<object>().ToArray());
            if (select != null)
                _list.SelectedItem = select;
        }
        finally
        {
            _list.EndUpdate();
        }
        UpdateButtons();
    }

    private void UpdateButtons() => _edit.Enabled = _remove.Enabled = _list.SelectedIndex >= 0;

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

        TextBox input = new() { Text = initial, Location = new Point(8, 10), Width = 504 };
        Button save = new() { Text = Locale.String("BlacklistEditorSave"), Location = new Point(356, 45), Size = new Size(75, 27), DialogResult = DialogResult.OK };
        Button cancel = new() { Text = Locale.String("BlacklistEditorCancel"), Location = new Point(437, 45), Size = new Size(75, 27), DialogResult = DialogResult.Cancel };
        form.AcceptButton = save;
        form.CancelButton = cancel;
        form.Controls.AddRange(new Control[] { input, save, cancel });
        form.Shown += (_, _) => { input.Focus(); input.SelectAll(); };

        return form.ShowDialog() == DialogResult.OK ? input.Text : null;
    }
}
