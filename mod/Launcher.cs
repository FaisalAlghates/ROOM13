using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// ROOM 13 launcher: a normal Windows window (outside Unity) that starts the game
// windowed / fullscreen, or lets the built-in autopilot play it while you watch.
class Launcher : Form
{
    Process game;
    Label status, stage;
    Button winBtn, fullBtn, autoBtn, stopBtn;
    Timer timer;
    string exe, logPath;
    string mode = "";

    Launcher(string[] args)
    {
        Text = "ROOM 13 - Launcher / مشغّل اللعبة";
        ClientSize = new Size(420, 330);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(16, 20, 22);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 9.5f);

        exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Game", "ROOM13.exe");
        logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", "Room13", "ROOM 13", "Player.log");

        var title = new Label { Text = "ROOM 13", Font = new Font("Segoe UI", 26, FontStyle.Bold), AutoSize = true, Location = new Point(20, 10) };
        var sub = new Label { Text = "03:17 AM   -   WASD move · E interact · F flashlight · TAB phone/map · H hint", ForeColor = Color.Gray, AutoSize = true, Location = new Point(24, 66) };

        winBtn = MakeBtn("Play - Window\nالعب في نافذة", 215, 95);
        fullBtn = MakeBtn("Play - Fullscreen (default)\nالعب بملء الشاشة", 20, 95);
        autoBtn = MakeBtn("Watch Auto-Play - Fullscreen\nشاهد اللاعب الآلي يلعب (ملء الشاشة)", 20, 155);
        autoBtn.Width = 395;
        stopBtn = MakeBtn("Close game  |  إغلاق اللعبة", 20, 215);
        stopBtn.Width = 395; stopBtn.Height = 36;

        status = new Label { AutoSize = false, Size = new Size(395, 24), Location = new Point(20, 265), ForeColor = Color.LightSteelBlue };
        stage = new Label { AutoSize = false, Size = new Size(395, 24), Location = new Point(20, 292), ForeColor = Color.FromArgb(240, 215, 90) };

        winBtn.Click += (s, e) => Start("window");
        fullBtn.Click += (s, e) => Start("full");
        autoBtn.Click += (s, e) => Start("auto");
        stopBtn.Click += (s, e) => Stop();

        Controls.AddRange(new Control[] { title, sub, winBtn, fullBtn, autoBtn, stopBtn, status, stage });

        timer = new Timer { Interval = 800 };
        timer.Tick += (s, e) => Refresh2();
        timer.Start();
        Refresh2();

        // command line shortcuts: --window | --full | --auto
        foreach (var a in args)
        {
            if (a == "--window") Shown += (s, e) => Start("window");
            if (a == "--full") Shown += (s, e) => Start("full");
            if (a == "--auto") Shown += (s, e) => Start("auto");
        }
    }

    Button MakeBtn(string t, int x, int y)
    {
        var b = new Button { Text = t, Size = new Size(195, 52), Location = new Point(x, y), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(38, 50, 54), ForeColor = Color.White };
        b.FlatAppearance.BorderColor = Color.FromArgb(80, 100, 104);
        return b;
    }

    bool Running { get { return game != null && !game.HasExited; } }

    string LastStage()
    {
        try
        {
            using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs))
            {
                string txt = sr.ReadToEnd();
                var ms = Regex.Matches(txt, @"STAGE -> (\w+)");
                if (ms.Count > 0) return ms[ms.Count - 1].Groups[1].Value;
            }
        }
        catch { }
        return "";
    }

    void Refresh2()
    {
        bool ok = File.Exists(exe);
        winBtn.Enabled = fullBtn.Enabled = autoBtn.Enabled = !Running && ok;
        stopBtn.Enabled = Running;
        if (!ok) { status.Text = "ROOM13.exe not found: " + exe; stage.Text = ""; return; }
        if (Running)
        {
            status.Text = "Running (" + mode + ")  PID " + game.Id;
            stage.Text = mode == "auto" ? "Bot stage: " + LastStage() : "";
        }
        else { status.Text = "Ready - جاهز"; stage.Text = ""; }
    }

    void Start(string m)
    {
        if (Running || !File.Exists(exe)) return;
        mode = m;
        string args = "";
        if (m == "window") args = "-windowed -screen-fullscreen 0 -screen-width 1280 -screen-height 720";
        if (m == "auto") args += " -autotest";
        var psi = new ProcessStartInfo(exe, args) { WorkingDirectory = Path.GetDirectoryName(exe) };
        game = Process.Start(psi);
        Refresh2();
    }

    void Stop()
    {
        if (Running) { game.CloseMainWindow(); if (!game.WaitForExit(3000)) game.Kill(); }
        Refresh2();
    }

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.Run(new Launcher(args));
    }
}

