using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TopMostFriend;

public sealed class HotKeyWindow : Form
{
    private sealed class HotKeyInfo
    {
        public string Name { get; }
        public ushort Atom { get; }
        public Action Action { get; }

        public HotKeyInfo(string name, ushort atom, Action action)
        {
            Name = name;
            Atom = atom;
            Action = action;
        }
    }

    private readonly List<HotKeyInfo> _registeredHotKeys = new();

    public HotKeyWindow()
    {
        ShowInTaskbar = false;
        Text = string.Empty;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(1, 1);
        Location = new Point(-32000, -32000);
        CreateHandle();
        Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Win32.WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            Action? action;

            lock (_registeredHotKeys)
                action = _registeredHotKeys.FirstOrDefault(x => x.Atom == id)?.Action;

            if (action != null)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    AppLog.Write("Unhandled exception in hot-key action.", ex);
                }
            }
        }

        base.WndProc(ref m);
    }

    public ushort Register(string name, Win32ModKeys modifiers, Keys key, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (string.IsNullOrWhiteSpace(name))
            name = Guid.NewGuid().ToString("D");

        ushort atom = Win32.GlobalAddAtomW(name);
        if (atom == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Atom creation failed.");

        uint registerModifiers = (uint)(modifiers | Win32ModKeys.MOD_NOREPEAT);
        if (!Win32.RegisterHotKey(Handle, atom, registerModifiers, (uint)key))
        {
            int error = Marshal.GetLastWin32Error();
            Win32.GlobalDeleteAtom(atom);
            throw new Win32Exception(error, "Hot-key registration failed.");
        }

        lock (_registeredHotKeys)
            _registeredHotKeys.Add(new HotKeyInfo(name, atom, action));

        return atom;
    }

    public void Unregister(ushort atom)
    {
        if (atom == 0)
            return;

        bool existed;
        lock (_registeredHotKeys)
        {
            existed = _registeredHotKeys.Any(x => x.Atom == atom);
            _registeredHotKeys.RemoveAll(x => x.Atom == atom);
        }

        if (!existed)
            return;

        Win32.UnregisterHotKey(Handle, atom);
        Win32.GlobalDeleteAtom(atom);
    }

    public void Unregister(string name)
    {
        ushort atom;
        lock (_registeredHotKeys)
            atom = _registeredHotKeys.FirstOrDefault(x => x.Name == name)?.Atom ?? (ushort)0;

        Unregister(atom);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ushort[] atoms;
            lock (_registeredHotKeys)
                atoms = _registeredHotKeys.Select(x => x.Atom).ToArray();

            foreach (ushort atom in atoms)
                Unregister(atom);
        }

        base.Dispose(disposing);
    }
}
