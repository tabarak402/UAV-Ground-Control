using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LiveCharts.WinForms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace UAVGroundControl
{
    // ╔══════════════════════════════════════════════════════════════════════╗
    // ║  LAYOUT:                                                             ║
    // ║  ┌─────────────────────────┬──────────────────────────────────────┐  ║
    // ║  │                         │  ┌──────────────────────────────────┐│  ║
    // ║  │   LEFT PANEL            │  │  GMap harita (üst)               ││  ║
    // ║  │   Video Stream          │  ├──────────────────────────────────┤│  ║
    // ║  │   (RTSP / MJPEG)        │  │  3D Drone görünümü (alt)         ││  ║
    // ║  │                         │  └──────────────────────────────────┘│  ║
    // ║  └─────────────────────────┴──────────────────────────────────────┘  ║
    // ║  ┌──────────────────────────────────────────────────────────────────┐ ║
    // ║  │  BOTTOM PANEL — [G1][G2][G3][G4] [Pusula] [Horizon] [FlightHUD] │ ║
    // ║  └──────────────────────────────────────────────────────────────────┘ ║
    // ╚══════════════════════════════════════════════════════════════════════╝

    public partial class Form1 : Form
    {
        // ── Map (sağ panel üst) ──────────────────────────────────────────────
        private GMapControl map;
        private GMapOverlay overlay;
        private GMarkerGoogle marker;
        private GMapRoute flightRoute;
        private List<PointLatLng> routePoints = new List<PointLatLng>();
        private UdpClient udp;

        // ── LibVLC Video Stream (sol panel) ──────────────────────────────────
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private VideoView _videoView;
        private Panel videoPanel;
        private TextBox txtStreamUrl;
        private Button btnStartStream;
        private Button btnStopStream;
        private Label lblStreamStatus;
        private Panel videoControlBar;
        private bool isStreaming = false;

        // ── HUD labels (harita üstü) ─────────────────────────────────────────
        private Label lblAltitude;
        private Label lblHeading;

        // ── Gauge'ler ────────────────────────────────────────────────────────
        private AngularGauge speedgauge1;
        private AngularGauge altitudegauge2;
        private AngularGauge headinggauge3;
        private AngularGauge powergauge4;

        private int speedMode = 0;
        private int powerMode = 0;

        private Label lblSpeedTitle;
        private Label lblPowerTitle;
        private Button btnSpeedMode;
        private Button btnPowerMode;

        // ── Pusula ───────────────────────────────────────────────────────────
        private PictureBox compassBox;
        private Bitmap compassBaseImage;
        private Panel compassPanel;

        // ── Artificial Horizon ───────────────────────────────────────────────
        private Panel horizonPanel;
        private Bitmap horizonBitmap;
        private PictureBox horizonBox;

        // ── 3D Drone (sağ panel alt) ─────────────────────────────────────────
        private Panel drone3DPanel;
        private PictureBox drone3DBox;
        private double droneYaw = 0;
        private double dronePitch = 0;
        private double droneRoll = 0;

        // ── Ana paneller ─────────────────────────────────────────────────────
        private Panel bottomPanel;
        private Panel rightPanel;
        private Panel leftPanel;
        private SplitContainer rightSplitter;

        // ── Bottom HUD (Flight Data) ─────────────────────────────────────────
        private Label lblHudAlt, lblHudSpd, lblHudHdg, lblHudPitch, lblHudRoll, lblHudPwr;

        // ── Sağ panel telemetri ──────────────────────────────────────────────
        private Label lblTelSpeed, lblTelAlt, lblTelBatt, lblTelMode;

        // ════════════════════════════════════════════════════════════════════
        public Form1()
        {
            Text = "UAV Ground Control Station";
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.Sizable;
            BackColor = Color.FromArgb(15, 15, 20);
            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint, true);
            this.UpdateStyles();

            // LibVLC başlat (uygulama geneli tek instance)
            Core.Initialize();
            _libVLC = new LibVLC(
                "--no-xlib",
                "--network-caching=300",
                "--clock-jitter=0",
                "--clock-synchro=0"
            );

            SetupUI();
            Load += Form1_Load;
        }

        // ════════════════════════════════════════════════════════════════════
        // SETUP UI
        // ════════════════════════════════════════════════════════════════════
        private void SetupUI()
        {
            // ── 1. BOTTOM PANEL ─────────────────────────────────────────────
            bottomPanel = new Panel
            {
                Height = 230,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(18, 18, 24),
                Padding = new Padding(0)
            };
            Controls.Add(bottomPanel);

            // ── 2. RIGHT PANEL ───────────────────────────────────────────────
            rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 420,
                BackColor = Color.FromArgb(12, 12, 18)
            };
            Controls.Add(rightPanel);

            // ── 3. LEFT PANEL (Video Stream) ────────────────────────────────
            leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(10, 10, 15)
            };
            Controls.Add(leftPanel);

            // ════════════════════════════════════════════════════════════════
            // LEFT PANEL — Video Stream (LibVLC)
            // ════════════════════════════════════════════════════════════════
            BuildVideoStreamPanel(leftPanel);

            // ════════════════════════════════════════════════════════════════
            // RIGHT PANEL — SplitContainer (üst=Harita, alt=3D Drone)
            // ════════════════════════════════════════════════════════════════
            BuildRightSplitPanel(rightPanel);

            // ════════════════════════════════════════════════════════════════
            // BOTTOM PANEL — TableLayoutPanel (7 sütun)
            // ════════════════════════════════════════════════════════════════
            var bottomTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 4, 4, 4),
                Margin = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            bottomTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            bottomTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188f));
            bottomTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188f));
            bottomTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188f));
            bottomTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188f));
            bottomTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200f));
            bottomTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260f));
            bottomTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            bottomPanel.Controls.Add(bottomTable);

            Panel g1 = new Panel { Dock = DockStyle.Fill, Margin = new Padding(2) };
            Panel g2 = new Panel { Dock = DockStyle.Fill, Margin = new Padding(2) };
            Panel g3 = new Panel { Dock = DockStyle.Fill, Margin = new Padding(2) };
            Panel g4 = new Panel { Dock = DockStyle.Fill, Margin = new Padding(2) };

            BuildGaugeIntoPanel(g1, "SPEED (m/s)", out lblSpeedTitle, out speedGauge1, out btnSpeedMode);
            BuildGaugeIntoPanel(g2, "ALTITUDE (m)", out _, out altitudeGauge2, out _);
            BuildGaugeIntoPanel(g3, "HEADING (°)", out _, out headingGauge3, out _);
            BuildGaugeIntoPanel(g4, "POWER (V)", out lblPowerTitle, out powerGauge4, out btnPowerMode);

            altitudeGauge2.ToValue = 500;
            headingGauge3.ToValue = 360;
            powerGauge4.ToValue = 30;

            btnSpeedMode.Click += BtnSpeedMode_Click;
            btnPowerMode.Click += BtnPowerMode_Click;

            bottomTable.Controls.Add(g1, 0, 0);
            bottomTable.Controls.Add(g2, 1, 0);
            bottomTable.Controls.Add(g3, 2, 0);
            bottomTable.Controls.Add(g4, 3, 0);

            // Pusula
            compassPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 30),
                Margin = new Padding(2)
            };
            BuildCompassIntoPanel(compassPanel);
            bottomTable.Controls.Add(compassPanel, 4, 0);

            // Artificial Horizon
            Panel horizonContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(10, 10, 16),
                Margin = new Padding(2)
            };
            Label lblHorizonTitle = new Label
            {
                Text = "ARTIFICIAL HORIZON",
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(20, 20, 32),
                ForeColor = Color.FromArgb(0, 200, 255),
                Font = new Font("Consolas", 8, FontStyle.Bold)
            };
            horizonContainer.Controls.Add(lblHorizonTitle);
            CreateArtificialHorizon(horizonContainer);
            bottomTable.Controls.Add(horizonContainer, 5, 0);

            // Flight Data HUD
            Panel bottomHudPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(14, 14, 22),
                Margin = new Padding(2)
            };
            BuildBottomHud(bottomHudPanel);
            bottomTable.Controls.Add(bottomHudPanel, 6, 0);
        }

        // ════════════════════════════════════════════════════════════════════
        // VIDEO STREAM PANEL — LibVLC tabanlı (RTSP + MJPEG destekli)
        // ════════════════════════════════════════════════════════════════════
        private void BuildVideoStreamPanel(Panel parent)
        {
            // Başlık çubuğu
            Label lblTitle = new Label
            {
                Text = "◈  LIVE VIDEO STREAM  [RTSP / MJPEG]",
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(20, 20, 32),
                ForeColor = Color.FromArgb(0, 200, 255),
                Font = new Font("Consolas", 9, FontStyle.Bold)
            };

            // Kontrol çubuğu (alt)
            videoControlBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                BackColor = Color.FromArgb(14, 14, 22),
                Padding = new Padding(6, 6, 6, 6)
            };

            // URL input
            txtStreamUrl = new TextBox
            {
                Left = 6,
                Top = 10,
                Width = 340,
                Height = 28,
                BackColor = Color.FromArgb(28, 28, 42),
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 9),
                BorderStyle = BorderStyle.FixedSingle,
                Text = "rtsp://192.168.1.100:8554/drone"
            };

            // Bağlan butonu
            btnStartStream = new Button
            {
                Left = 354,
                Top = 8,
                Width = 100,
                Height = 30,
                Text = "▶ CONNECT",
                BackColor = Color.FromArgb(0, 100, 50),
                ForeColor = Color.Lime,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 8, FontStyle.Bold)
            };
            btnStartStream.FlatAppearance.BorderColor = Color.FromArgb(0, 160, 80);
            btnStartStream.Click += BtnStartStream_Click;

            // Durdur butonu
            btnStopStream = new Button
            {
                Left = 462,
                Top = 8,
                Width = 80,
                Height = 30,
                Text = "■ STOP",
                BackColor = Color.FromArgb(100, 20, 20),
                ForeColor = Color.FromArgb(255, 80, 80),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 8, FontStyle.Bold),
                Enabled = false
            };
            btnStopStream.FlatAppearance.BorderColor = Color.FromArgb(160, 40, 40);
            btnStopStream.Click += BtnStopStream_Click;

            // Snapshot butonu
            Button btnSnapshot = new Button
            {
                Left = 550,
                Top = 8,
                Width = 90,
                Height = 30,
                Text = "📷 SNAP",
                BackColor = Color.FromArgb(20, 60, 100),
                ForeColor = Color.FromArgb(0, 180, 255),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 8, FontStyle.Bold)
            };
            btnSnapshot.FlatAppearance.BorderColor = Color.FromArgb(0, 100, 160);
            btnSnapshot.Click += BtnSnapshot_Click;

            // Durum etiketi
            lblStreamStatus = new Label
            {
                Left = 648,
                Top = 12,
                Width = 200,
                Height = 22,
                Text = "● DISCONNECTED",
                ForeColor = Color.FromArgb(180, 60, 60),
                Font = new Font("Consolas", 8, FontStyle.Bold),
                BackColor = Color.Transparent
            };

            videoControlBar.Controls.Add(txtStreamUrl);
            videoControlBar.Controls.Add(btnStartStream);
            videoControlBar.Controls.Add(btnStopStream);
            videoControlBar.Controls.Add(btnSnapshot);
            videoControlBar.Controls.Add(lblStreamStatus);

            // Ana video görüntü alanı
            videoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };

            // LibVLC VideoView
            _videoView = new VideoView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };

            // MediaPlayer oluştur ve VideoView'a bağla
            _mediaPlayer = new MediaPlayer(_libVLC);
            _videoView.MediaPlayer = _mediaPlayer;

            // Bağlantı olayları
            _mediaPlayer.Playing += (s, e) => SetStreamStatus("● STREAMING", Color.Lime);
            _mediaPlayer.EncounteredError += (s, e) => SetStreamStatus("● ERROR", Color.Red);
            _mediaPlayer.EndReached += (s, e) => SetStreamStatus("● STREAM ENDED", Color.Orange);
            _mediaPlayer.Buffering += (s, e) =>
            {
                if (e.Cache < 100)
                    SetStreamStatus($"● BUFFERING {e.Cache:F0}%", Color.Yellow);
            };

            // Bekleme ekranı (VLC üstüne overlay)
            Panel placeholderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            placeholderPanel.Paint += VideoPanel_PaintPlaceholder;

            videoPanel.Controls.Add(_videoView);
            videoPanel.Controls.Add(placeholderPanel);

            // VideoView göründüğünde placeholder'ı gizle
            _mediaPlayer.Playing += (s, e) =>
            {
                if (placeholderPanel.InvokeRequired)
                    placeholderPanel.Invoke(new Action(() => placeholderPanel.Visible = false));
                else
                    placeholderPanel.Visible = false;
            };
            _mediaPlayer.Stopped += (s, e) =>
            {
                if (placeholderPanel.InvokeRequired)
                    placeholderPanel.Invoke(new Action(() => { placeholderPanel.Visible = true; placeholderPanel.Invalidate(); }));
                else
                { placeholderPanel.Visible = true; placeholderPanel.Invalidate(); }
            };

            parent.Controls.Add(videoPanel);
            parent.Controls.Add(videoControlBar);
            parent.Controls.Add(lblTitle);
        }

        private void VideoPanel_PaintPlaceholder(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var box = (Panel)sender;
            int w = box.Width, h = box.Height;
            if (w <= 0 || h <= 0) return;

            using (var br = new SolidBrush(Color.FromArgb(8, 8, 14)))
                g.FillRectangle(br, 0, 0, w, h);

            using (var pen = new Pen(Color.FromArgb(20, 0, 180, 255), 1))
            {
                for (int x = 0; x < w; x += 40) g.DrawLine(pen, x, 0, x, h);
                for (int y = 0; y < h; y += 40) g.DrawLine(pen, 0, y, w, y);
            }

            int cx = w / 2, cy = h / 2;

            using (var pen = new Pen(Color.FromArgb(60, 0, 200, 255), 1))
            {
                g.DrawEllipse(pen, cx - 80, cy - 80, 160, 160);
                g.DrawEllipse(pen, cx - 50, cy - 50, 100, 100);
                g.DrawLine(pen, cx - 100, cy, cx + 100, cy);
                g.DrawLine(pen, cx, cy - 100, cx, cy + 100);
            }

            using (var pen = new Pen(Color.FromArgb(120, 0, 200, 255), 2))
                g.DrawEllipse(pen, cx - 20, cy - 20, 40, 40);

            using (var font = new Font("Consolas", 13, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(0, 200, 255)))
            {
                string msg = "NO VIDEO SIGNAL";
                var sz = g.MeasureString(msg, font);
                g.DrawString(msg, font, brush, cx - sz.Width / 2, cy + 90);
            }

            using (var font = new Font("Consolas", 8))
            using (var brush = new SolidBrush(Color.FromArgb(100, 150, 180)))
            {
                string sub = "Enter RTSP/MJPEG stream URL and press CONNECT";
                var sz = g.MeasureString(sub, font);
                g.DrawString(sub, font, brush, cx - sz.Width / 2, cy + 118);

                // URL örnekleri
                string ex1 = "RTSP  → rtsp://192.168.x.x:8554/drone";
                string ex2 = "MJPEG → http://192.168.x.x:8080/video";
                var sz1 = g.MeasureString(ex1, font);
                var sz2 = g.MeasureString(ex2, font);
                using (var b2 = new SolidBrush(Color.FromArgb(60, 120, 160)))
                {
                    g.DrawString(ex1, font, b2, cx - sz1.Width / 2, cy + 140);
                    g.DrawString(ex2, font, b2, cx - sz2.Width / 2, cy + 158);
                }
            }

            int cs = 20;
            using (var pen = new Pen(Color.FromArgb(80, 0, 200, 255), 2))
            {
                g.DrawLine(pen, 10, 10, 10 + cs, 10);
                g.DrawLine(pen, 10, 10, 10, 10 + cs);
                g.DrawLine(pen, w - 10, 10, w - 10 - cs, 10);
                g.DrawLine(pen, w - 10, 10, w - 10, 10 + cs);
                g.DrawLine(pen, 10, h - 10, 10 + cs, h - 10);
                g.DrawLine(pen, 10, h - 10, 10, h - 10 - cs);
                g.DrawLine(pen, w - 10, h - 10, w - 10 - cs, h - 10);
                g.DrawLine(pen, w - 10, h - 10, w - 10, h - 10 - cs);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // STREAM BUTONLARI
        // ════════════════════════════════════════════════════════════════════
        private void BtnStartStream_Click(object sender, EventArgs e)
        {
            string url = txtStreamUrl.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;

            try
            {
                // Önceki medyayı durdur
                if (_mediaPlayer.IsPlaying)
                    _mediaPlayer.Stop();

                isStreaming = true;
                btnStartStream.Enabled = false;
                btnStopStream.Enabled = true;
                lblStreamStatus.Text = "● CONNECTING...";
                lblStreamStatus.ForeColor = Color.Yellow;

                // URL'ye göre otomatik seçenek ayarla
                bool isRtsp = url.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) ||
                              url.StartsWith("rtsps://", StringComparison.OrdinalIgnoreCase);
                bool isUdp = url.StartsWith("udp://", StringComparison.OrdinalIgnoreCase);

                var mediaOptions = new List<string>();

                if (isRtsp)
                {
                    // RTSP — TCP transport (UDP'den daha stabil WiFi üzerinde)
                    mediaOptions.Add(":rtsp-tcp");
                    mediaOptions.Add(":network-caching=300");
                    mediaOptions.Add(":clock-jitter=0");
                    mediaOptions.Add(":clock-synchro=0");
                    mediaOptions.Add(":avcodec-hw=any");    // Donanım hızlandırma
                    mediaOptions.Add(":avcodec-fast");
                    mediaOptions.Add(":avcodec-skip-frame=0");
                }
                else if (isUdp)
                {
                    // UDP stream (GStreamer çıkışı için)
                    mediaOptions.Add(":network-caching=100");
                    mediaOptions.Add(":clock-jitter=0");
                }
                else
                {
                    // HTTP MJPEG veya diğerleri
                    mediaOptions.Add(":network-caching=300");
                    mediaOptions.Add(":http-reconnect");
                    mediaOptions.Add(":http-reconnect-delay=1000");
                }

                var media = new Media(_libVLC, new Uri(url), mediaOptions.ToArray());
                _mediaPlayer.Play(media);
                media.Dispose();
            }
            catch (Exception ex)
            {
                isStreaming = false;
                btnStartStream.Enabled = true;
                btnStopStream.Enabled = false;
                lblStreamStatus.Text = $"● ERROR: {ex.Message}";
                lblStreamStatus.ForeColor = Color.Red;
                MessageBox.Show($"Stream başlatılamadı:\n{ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnStopStream_Click(object sender, EventArgs e)
        {
            try
            {
                _mediaPlayer?.Stop();
            }
            catch { }
            finally
            {
                isStreaming = false;
                btnStartStream.Enabled = true;
                btnStopStream.Enabled = false;
                lblStreamStatus.Text = "● DISCONNECTED";
                lblStreamStatus.ForeColor = Color.FromArgb(180, 60, 60);
            }
        }

        private void BtnSnapshot_Click(object sender, EventArgs e)
        {
            if (!_mediaPlayer.IsPlaying)
            {
                MessageBox.Show("Aktif bir stream yok.", "Bilgi",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string filename = $"UAV_Snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string fullPath = Path.Combine(folder, filename);

            bool result = _mediaPlayer.TakeSnapshot(0, fullPath,
                (uint)_videoView.Width, (uint)_videoView.Height);

            if (result)
                MessageBox.Show($"Snapshot kaydedildi:\n{fullPath}", "Snapshot",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show("Snapshot alınamadı.", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void SetStreamStatus(string text, Color color)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetStreamStatus(text, color)));
                return;
            }
            lblStreamStatus.Text = text;
            lblStreamStatus.ForeColor = color;
        }

        // ════════════════════════════════════════════════════════════════════
        // RIGHT SPLIT PANEL — üst=Harita, alt=3D Drone
        // ════════════════════════════════════════════════════════════════════
        private void BuildRightSplitPanel(Panel parent)
        {
            rightSplitter = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BackColor = Color.FromArgb(10, 10, 16),
                SplitterDistance = 300,
                Panel1MinSize = 150,
                Panel2MinSize = 150,
                SplitterWidth = 5
            };

            // ── Üst: Harita ──────────────────────────────────────────────────
            Panel mapContainer = rightSplitter.Panel1;
            mapContainer.BackColor = Color.FromArgb(10, 10, 15);

            Label lblMapTitle = new Label
            {
                Text = "◈  MAP VIEW",
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(20, 20, 32),
                ForeColor = Color.FromArgb(0, 200, 255),
                Font = new Font("Consolas", 8, FontStyle.Bold)
            };
            mapContainer.Controls.Add(lblMapTitle);

            map = new GMapControl
            {
                Dock = DockStyle.Fill,
                ShowCenter = false,
                CanDragMap = true,
                DragButton = MouseButtons.Left,
                MinZoom = 2,
                MaxZoom = 20,
                Zoom = 15,
                MouseWheelZoomEnabled = true,
                MouseWheelZoomType = MouseWheelZoomType.MousePositionAndCenter,
                IgnoreMarkerOnMouseWheel = true
            };

            GMaps.Instance.Mode = AccessMode.ServerAndCache;
            map.MapProvider = GMapProviders.GoogleMap;
            map.Position = new PointLatLng(38.6943, 35.5430);

            overlay = new GMapOverlay("drone");
            marker = new GMarkerGoogle(map.Position, GMarkerGoogleType.red_dot);
            flightRoute = new GMapRoute(routePoints, "flight");

            overlay.Markers.Add(marker);
            overlay.Routes.Add(flightRoute);
            map.Overlays.Add(overlay);

            // HUD overlay
            Panel hud = new Panel
            {
                Width = 200,
                Height = 55,
                Top = 30,
                Left = 6,
                BackColor = Color.FromArgb(140, 0, 0, 0)
            };
            map.Controls.Add(hud);

            lblAltitude = new Label
            {
                Text = "ALT: 0 m",
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 9, FontStyle.Bold),
                AutoSize = true,
                Top = 6,
                Left = 6
            };
            lblHeading = new Label
            {
                Text = "HDG: 0°",
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 9, FontStyle.Bold),
                AutoSize = true,
                Top = 28,
                Left = 6
            };
            hud.Controls.Add(lblAltitude);
            hud.Controls.Add(lblHeading);

            mapContainer.Controls.Add(map);

            // ── Alt: 3D Drone ────────────────────────────────────────────────
            Panel droneContainer = rightSplitter.Panel2;
            droneContainer.BackColor = Color.FromArgb(8, 8, 15);

            Label lbl3DTitle = new Label
            {
                Text = "◈  3D DRONE VIEW",
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(20, 20, 32),
                ForeColor = Color.FromArgb(0, 200, 255),
                Font = new Font("Consolas", 8, FontStyle.Bold)
            };

            Panel telPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 86,
                BackColor = Color.FromArgb(16, 16, 26)
            };
            AddTelemetryLabels(telPanel);

            drone3DPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(8, 8, 15)
            };
            drone3DBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent
            };
            drone3DPanel.Controls.Add(drone3DBox);

            droneContainer.Controls.Add(drone3DPanel);
            droneContainer.Controls.Add(telPanel);
            droneContainer.Controls.Add(lbl3DTitle);

            parent.Controls.Add(rightSplitter);
        }

        // ════════════════════════════════════════════════════════════════════
        // GAUGE — panel içine Dock=Fill
        // ════════════════════════════════════════════════════════════════════
        private void BuildGaugeIntoPanel(Panel parent, string title,
            out Label lbl, out AngularGauge gauge, out Button btn)
        {
            parent.BackColor = Color.FromArgb(20, 20, 32);

            lbl = new Label
            {
                Text = title,
                Height = 22,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(25, 80, 130),
                ForeColor = Color.White,
                Font = new Font("Consolas", 8, FontStyle.Bold)
            };

            btn = new Button
            {
                Text = "⇄ Switch",
                Height = 26,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(28, 28, 44),
                ForeColor = Color.FromArgb(0, 180, 255),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 8, FontStyle.Regular)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(0, 100, 160);

            gauge = new AngularGauge
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 16, 26),
                FromValue = 0,
                ToValue = 50
            };

            parent.Controls.Add(gauge);
            parent.Controls.Add(btn);
            parent.Controls.Add(lbl);
        }

        // ════════════════════════════════════════════════════════════════════
        // COMPASS
        // ════════════════════════════════════════════════════════════════════
        private void BuildCompassIntoPanel(Panel parent)
        {
            compassBaseImage = null;

            Label lblTitle = new Label
            {
                Text = "COMPASS",
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(20, 20, 32),
                ForeColor = Color.FromArgb(0, 200, 255),
                Font = new Font("Consolas", 8, FontStyle.Bold)
            };

            compassBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };

            parent.Controls.Add(compassBox);
            parent.Controls.Add(lblTitle);

            parent.Layout += (s, e) => RebuildCompassBitmap();
        }

        private void RebuildCompassBitmap()
        {
            if (compassBox == null) return;
            int size = Math.Min(compassBox.Width, compassBox.Height);
            if (size < 10) return;

            compassBaseImage?.Dispose();
            compassBaseImage = new Bitmap(size, size);

            using (Graphics g = Graphics.FromImage(compassBaseImage))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int r = size / 2;
                Rectangle rect = new Rectangle(6, 6, size - 12, size - 12);

                using (Brush b = new SolidBrush(Color.FromArgb(28, 28, 42)))
                    g.FillEllipse(b, rect);
                using (Pen p = new Pen(Color.FromArgb(0, 160, 220), 2))
                    g.DrawEllipse(p, rect);
                using (Pen p = new Pen(Color.FromArgb(0, 60, 100), 1))
                    g.DrawEllipse(p, new Rectangle(12, 12, size - 24, size - 24));

                for (int deg = 0; deg < 360; deg += 10)
                {
                    double rad = deg * Math.PI / 180;
                    int inner = deg % 30 == 0 ? r - 16 : r - 10;
                    float x1 = r + inner * (float)Math.Sin(rad);
                    float y1 = r - inner * (float)Math.Cos(rad);
                    float x2 = r + (r - 6) * (float)Math.Sin(rad);
                    float y2 = r - (r - 6) * (float)Math.Cos(rad);
                    using (Pen p = new Pen(
                        deg % 90 == 0 ? Color.FromArgb(0, 200, 255) : Color.FromArgb(70, 70, 95),
                        deg % 30 == 0 ? 1.5f : 1f))
                        g.DrawLine(p, x1, y1, x2, y2);
                }

                using (Font f = new Font("Consolas", Math.Max(7, size / 22), FontStyle.Bold))
                {
                    void DrawDir(string txt, float x, float y, Color c)
                    {
                        SizeF sz = g.MeasureString(txt, f);
                        using (Brush b = new SolidBrush(c))
                            g.DrawString(txt, f, b, x - sz.Width / 2, y - sz.Height / 2);
                    }
                    DrawDir("N", r, 14, Color.FromArgb(255, 80, 80));
                    DrawDir("S", r, size - 16, Color.White);
                    DrawDir("W", 14, r, Color.White);
                    DrawDir("E", size - 16, r, Color.White);
                }

                Point[] arrowN = { new Point(r, 18), new Point(r - 7, 38), new Point(r + 7, 38) };
                Point[] arrowS = { new Point(r, size - 18), new Point(r - 7, size - 38), new Point(r + 7, size - 38) };

                using (Brush b = new SolidBrush(Color.FromArgb(255, 60, 60)))
                    g.FillPolygon(b, arrowN);
                using (Brush b = new SolidBrush(Color.Silver))
                    g.FillPolygon(b, arrowS);

                g.FillEllipse(Brushes.White, r - 4, r - 4, 8, 8);
                g.FillEllipse(new SolidBrush(Color.FromArgb(20, 20, 34)), r - 2, r - 2, 4, 4);
            }

            if (compassBox != null)
                compassBox.Image = (Bitmap)compassBaseImage.Clone();
        }

        // ════════════════════════════════════════════════════════════════════
        // TELEMETRY LABELS (3D drone altı)
        // ════════════════════════════════════════════════════════════════════
        private void AddTelemetryLabels(Panel parent)
        {
            string[] captions = { "SPEED", "ALTITUDE", "BATTERY", "MODE" };
            string[] defaults = { "0 m/s", "0 m", "0 V", "STABILIZE" };
            Label[] lblRefs = new Label[4];

            for (int i = 0; i < 4; i++)
            {
                Panel cell = new Panel
                {
                    Width = 94,
                    Height = 75,
                    Left = 5 + i * 98,
                    Top = 6,
                    BackColor = Color.FromArgb(22, 22, 34)
                };

                Label cap = new Label
                {
                    Text = captions[i],
                    Dock = DockStyle.Top,
                    Height = 20,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.FromArgb(100, 160, 200),
                    Font = new Font("Consolas", 7, FontStyle.Regular),
                    BackColor = Color.FromArgb(16, 16, 26)
                };

                lblRefs[i] = new Label
                {
                    Text = defaults[i],
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Lime,
                    Font = new Font("Consolas", 9, FontStyle.Bold),
                    BackColor = Color.Transparent
                };

                cell.Controls.Add(lblRefs[i]);
                cell.Controls.Add(cap);
                parent.Controls.Add(cell);
            }

            lblTelSpeed = lblRefs[0];
            lblTelAlt = lblRefs[1];
            lblTelBatt = lblRefs[2];
            lblTelMode = lblRefs[3];
        }

        // ════════════════════════════════════════════════════════════════════
        // BOTTOM HUD (Flight Data)
        // ════════════════════════════════════════════════════════════════════
        private void BuildBottomHud(Panel parent)
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                BackColor = Color.FromArgb(14, 14, 22),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            for (int i = 0; i < 6; i++)
                table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6f));

            Label title = new Label
            {
                Text = "◈  FLIGHT DATA",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(20, 20, 34),
                ForeColor = Color.FromArgb(0, 200, 255),
                Font = new Font("Consolas", 10, FontStyle.Bold),
                Margin = new Padding(0)
            };
            table.Controls.Add(title, 0, 0);
            table.SetColumnSpan(title, 2);

            string[] captions = { "ALTITUDE", "SPEED", "HEADING", "PITCH", "ROLL", "POWER" };
            Label[] valLabels = new Label[6];

            for (int i = 0; i < 6; i++)
            {
                Color bg = i % 2 == 0 ? Color.FromArgb(22, 22, 34) : Color.FromArgb(18, 18, 28);

                Label cap = new Label
                {
                    Text = captions[i],
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(140, 190, 230),
                    Font = new Font("Consolas", 8, FontStyle.Regular),
                    BackColor = bg,
                    Padding = new Padding(8, 0, 0, 0),
                    Margin = new Padding(0)
                };

                valLabels[i] = new Label
                {
                    Text = "——",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = Color.Lime,
                    Font = new Font("Consolas", 9, FontStyle.Bold),
                    BackColor = bg,
                    Padding = new Padding(0, 0, 10, 0),
                    Margin = new Padding(0)
                };

                table.Controls.Add(cap, 0, i + 1);
                table.Controls.Add(valLabels[i], 1, i + 1);
            }

            parent.Controls.Add(table);

            lblHudAlt = valLabels[0];
            lblHudSpd = valLabels[1];
            lblHudHdg = valLabels[2];
            lblHudPitch = valLabels[3];
            lblHudRoll = valLabels[4];
            lblHudPwr = valLabels[5];
        }

        // ════════════════════════════════════════════════════════════════════
        // ARTIFICIAL HORIZON
        // ════════════════════════════════════════════════════════════════════
        private void CreateArtificialHorizon(Control parent)
        {
            horizonPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
            horizonBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.StretchImage };
            horizonPanel.Controls.Add(horizonBox);
            parent.Controls.Add(horizonPanel);
            horizonPanel.Layout += (s, e) => BuildHorizonBase();
        }

        private void BuildHorizonBase()
        {
            int w = horizonPanel.Width;
            int h = horizonPanel.Height;
            if (w <= 0 || h <= 0) return;

            horizonBitmap?.Dispose();
            horizonBitmap = new Bitmap(w, h);

            using (Graphics g = Graphics.FromImage(horizonBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (var br = new LinearGradientBrush(new Point(0, 0), new Point(0, h / 2),
                    Color.FromArgb(15, 50, 110), Color.FromArgb(90, 150, 210)))
                    g.FillRectangle(br, 0, 0, w, h / 2);

                using (var br = new LinearGradientBrush(new Point(0, h / 2), new Point(0, h),
                    Color.FromArgb(90, 55, 18), Color.FromArgb(45, 28, 8)))
                    g.FillRectangle(br, 0, h / 2, w, h / 2);

                for (int deg = -30; deg <= 30; deg += 10)
                {
                    if (deg == 0) continue;
                    int y = h / 2 + deg * 3;
                    int lineW = deg % 20 == 0 ? w / 3 : w / 5;
                    using (Pen p = new Pen(Color.FromArgb(180, 255, 255, 255), 1))
                    {
                        int lx = w / 2 - lineW / 2;
                        int rx = w / 2 + lineW / 2;
                        g.DrawLine(p, lx, y, rx, y);
                        using (Font f = new Font("Consolas", 7))
                        using (Brush b = new SolidBrush(Color.White))
                            g.DrawString(Math.Abs(deg).ToString(), f, b, rx + 3, y - 6);
                    }
                }

                using (Pen p = new Pen(Color.White, 2))
                    g.DrawLine(p, 0, h / 2, w, h / 2);
            }

            if (horizonBox != null)
                horizonBox.Image = (Bitmap)horizonBitmap.Clone();
        }

        // ════════════════════════════════════════════════════════════════════
        // 3D DRONE ÇİZİMİ
        // ════════════════════════════════════════════════════════════════════
        private void Draw3DDrone(double yaw, double pitch, double roll)
        {
            if (drone3DBox == null || !drone3DBox.IsHandleCreated) return;

            int w = drone3DPanel.Width;
            int h = drone3DPanel.Height;
            if (w <= 0 || h <= 0) return;

            Bitmap bmp = new Bitmap(w, h);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (var br = new LinearGradientBrush(new Point(0, 0), new Point(w, h),
                    Color.FromArgb(8, 8, 20), Color.FromArgb(14, 24, 38)))
                    g.FillRectangle(br, 0, 0, w, h);

                using (Pen gridPen = new Pen(Color.FromArgb(18, 0, 160, 255), 1))
                {
                    for (int x = 0; x < w; x += 28) g.DrawLine(gridPen, x, 0, x, h);
                    for (int y2 = 0; y2 < h; y2 += 28) g.DrawLine(gridPen, 0, y2, w, y2);
                }

                double scale = Math.Min(w, h) * 0.18;
                double cx = w / 2.0;
                double cy = h / 2.0;

                double yR = yaw * Math.PI / 180;
                double pR = pitch * Math.PI / 180;
                double rR = roll * Math.PI / 180;

                Func<double[], double[]> rotate = v =>
                {
                    double vx = v[0], vy = v[1], vz = v[2];
                    double x2 = vx * Math.Cos(yR) + vz * Math.Sin(yR);
                    double z2 = -vx * Math.Sin(yR) + vz * Math.Cos(yR);
                    vx = x2; vz = z2;
                    double y2 = vy * Math.Cos(pR) - vz * Math.Sin(pR);
                    double z3 = vy * Math.Sin(pR) + vz * Math.Cos(pR);
                    vy = y2; vz = z3;
                    double x3 = vx * Math.Cos(rR) - vy * Math.Sin(rR);
                    double y3 = vx * Math.Sin(rR) + vy * Math.Cos(rR);
                    return new[] { x3, y3, vz };
                };

                double fov = 500;
                Func<double[], PointF> project = v =>
                {
                    double[] r2 = rotate(v);
                    double zOff = r2[2] + 4;
                    if (zOff < 0.1) zOff = 0.1;
                    float px = (float)(cx + r2[0] * scale * fov / (fov + zOff * scale));
                    float py = (float)(cy + r2[1] * scale * fov / (fov + zOff * scale));
                    return new PointF(px, py);
                };

                Func<double[], Color, Color> depthColor = (v, baseC) =>
                {
                    double[] r2 = rotate(v);
                    float t = (float)Math.Max(0, Math.Min(1, (r2[2] + 2) / 4.0));
                    return Color.FromArgb(baseC.A,
                        (int)(baseC.R * (0.55 + 0.45 * (1 - t))),
                        (int)(baseC.G * (0.55 + 0.45 * (1 - t))),
                        (int)(baseC.B * (0.55 + 0.45 * (1 - t))));
                };

                double bw = 0.55, bh = 0.14, bd = 0.55;
                double[][] bodyVerts = {
                    new[] {-bw,-bh,-bd}, new[] { bw,-bh,-bd},
                    new[] { bw, bh,-bd}, new[] {-bw, bh,-bd},
                    new[] {-bw,-bh, bd}, new[] { bw,-bh, bd},
                    new[] { bw, bh, bd}, new[] {-bw, bh, bd},
                };
                int[][] faces = {
                    new[]{0,1,2,3}, new[]{4,5,6,7}, new[]{0,1,5,4},
                    new[]{2,3,7,6}, new[]{1,2,6,5}, new[]{0,3,7,4},
                };

                foreach (var face in faces)
                {
                    PointF[] pts = {
                        project(bodyVerts[face[0]]), project(bodyVerts[face[1]]),
                        project(bodyVerts[face[2]]), project(bodyVerts[face[3]])
                    };
                    double[] center = {
                        (bodyVerts[face[0]][0] + bodyVerts[face[2]][0]) / 2,
                        (bodyVerts[face[0]][1] + bodyVerts[face[2]][1]) / 2,
                        (bodyVerts[face[0]][2] + bodyVerts[face[2]][2]) / 2
                    };
                    using (Brush b = new SolidBrush(depthColor(center, Color.FromArgb(200, 38, 38, 54))))
                        g.FillPolygon(b, pts);
                    using (Pen p = new Pen(Color.FromArgb(0, 160, 220), 1))
                        g.DrawPolygon(p, pts);
                }

                double armLen = 1.35;
                double[][] armTips = {
                    new[] { armLen, 0.0,  armLen},
                    new[] {-armLen, 0.0,  armLen},
                    new[] { armLen, 0.0, -armLen},
                    new[] {-armLen, 0.0, -armLen},
                };
                Color[] armColors = {
                    Color.FromArgb(255, 0,   180, 255),
                    Color.FromArgb(255, 0,   180, 255),
                    Color.FromArgb(255, 255, 120,   0),
                    Color.FromArgb(255, 255, 120,   0)
                };

                PointF bodyCenter = project(new double[] { 0, 0, 0 });

                for (int i = 0; i < 4; i++)
                {
                    PointF tip = project(armTips[i]);
                    using (Pen p = new Pen(armColors[i], 3))
                        g.DrawLine(p, bodyCenter, tip);

                    float mr = 8;
                    using (Brush b = new SolidBrush(Color.FromArgb(170, armColors[i])))
                        g.FillEllipse(b, tip.X - mr, tip.Y - mr, mr * 2, mr * 2);
                    using (Pen p = new Pen(Color.White, 1))
                        g.DrawEllipse(p, tip.X - mr, tip.Y - mr, mr * 2, mr * 2);

                    float propR = 18;
                    double propAngle = (DateTime.Now.Millisecond * 0.72 + i * 90) * Math.PI / 180;
                    for (int blade = 0; blade < 2; blade++)
                    {
                        double a = propAngle + blade * Math.PI;
                        float bx1 = tip.X + (float)(Math.Cos(a) * propR);
                        float by1 = tip.Y + (float)(Math.Sin(a) * propR);
                        float bx2 = tip.X - (float)(Math.Cos(a) * propR);
                        float by2 = tip.Y - (float)(Math.Sin(a) * propR);
                        using (Pen p = new Pen(Color.FromArgb(150, 255, 255, 255), 2))
                            g.DrawLine(p, bx1, by1, bx2, by2);
                    }
                }

                void DrawLED(double[] pos, Color c)
                {
                    PointF pt = project(pos);
                    using (Brush b = new SolidBrush(Color.FromArgb(190, c)))
                        g.FillEllipse(b, pt.X - 5, pt.Y - 5, 10, 10);
                    using (Pen p = new Pen(Color.FromArgb(200, Color.White), 1))
                        g.DrawEllipse(p, pt.X - 5, pt.Y - 5, 10, 10);
                }
                DrawLED(armTips[0], Color.Red);
                DrawLED(armTips[1], Color.Red);
                DrawLED(armTips[2], Color.Lime);
                DrawLED(armTips[3], Color.Lime);

                float axX = 40, axY = h - 40, axLen2 = 26;
                double[][] axes = { new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 } };
                Color[] axCols = { Color.Red, Color.Lime, Color.FromArgb(0, 140, 255) };
                string[] axLabels = { "X", "Y", "Z" };

                for (int i = 0; i < 3; i++)
                {
                    double[] rv = rotate(axes[i]);
                    float ex = axX + (float)(rv[0] * axLen2);
                    float ey = axY + (float)(rv[1] * axLen2);
                    using (Pen p = new Pen(axCols[i], 2))
                        g.DrawLine(p, axX, axY, ex, ey);
                    using (Font f = new Font("Consolas", 7))
                    using (Brush b = new SolidBrush(axCols[i]))
                        g.DrawString(axLabels[i], f, b, ex, ey);
                }

                string[] telLines = {
                    $"YAW:   {yaw,7:F1}°",
                    $"PITCH: {pitch,7:F1}°",
                    $"ROLL:  {roll,7:F1}°"
                };
                using (Font f = new Font("Consolas", 8))
                {
                    for (int i = 0; i < telLines.Length; i++)
                    {
                        SizeF sz = g.MeasureString(telLines[i], f);
                        using (Brush bg = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                            g.FillRectangle(bg, 6, 6 + i * 18, sz.Width + 6, 17);
                        using (Brush b = new SolidBrush(Color.Lime))
                            g.DrawString(telLines[i], f, b, 8, 7 + i * 18);
                    }
                }
            }

            var old = drone3DBox.Image;
            drone3DBox.Image = bmp;
            old?.Dispose();
        }

        // ════════════════════════════════════════════════════════════════════
        // BUTTON HANDLERS
        // ════════════════════════════════════════════════════════════════════
        private void BtnSpeedMode_Click(object sender, EventArgs e)
        {
            speedMode = (speedMode + 1) % 3;
            switch (speedMode)
            {
                case 0: lblSpeedTitle.Text = "SPEED (m/s)"; speedGauge1.ToValue = 30; break;
                case 1: lblSpeedTitle.Text = "SPEED (km/h)"; speedGauge1.ToValue = 120; break;
                case 2: lblSpeedTitle.Text = "SPEED (Mach)"; speedGauge1.ToValue = 1; break;
            }
        }

        private void BtnPowerMode_Click(object sender, EventArgs e)
        {
            powerMode = (powerMode + 1) % 2;
            lblPowerTitle.Text = powerMode == 0 ? "POWER (V)" : "POWER (W)";
            powerGauge4.ToValue = powerMode == 0 ? 30 : 500;
        }

        // ════════════════════════════════════════════════════════════════════
        // LOAD
        // ════════════════════════════════════════════════════════════════════
        private async void Form1_Load(object sender, EventArgs e)
        {
            BuildHorizonBase();
            RebuildCompassBitmap();

            var animTimer = new System.Windows.Forms.Timer { Interval = 50 };
            animTimer.Tick += (s, ev) =>
            {
                if (drone3DPanel.Width > 0 && drone3DPanel.Height > 0)
                    Draw3DDrone(droneYaw, dronePitch, droneRoll);
            };
            animTimer.Start();

            udp = new UdpClient(14550);

            while (true)
            {
                try
                {
                    var result = await udp.ReceiveAsync();
                    string json = Encoding.UTF8.GetString(result.Buffer);
                    var data = JsonConvert.DeserializeObject<GpsData>(json);
                    if (data != null) UpdateUI(data);
                }
                catch (Exception ex) when (!(ex is ObjectDisposedException))
                {
                    System.Diagnostics.Debug.WriteLine("UDP Error: " + ex.Message);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // UPDATE UI
        // ════════════════════════════════════════════════════════════════════
        private void UpdateUI(GpsData data)
        {
            if (InvokeRequired) { Invoke(new Action(() => UpdateUI(data))); return; }

            if (data.lat == 0 || data.lon == 0) return;

            var point = new PointLatLng(data.lat, data.lon);
            marker.Position = point;
            routePoints.Add(point);
            flightRoute.Points.Clear();
            flightRoute.Points.AddRange(routePoints);
            map.Position = point;
            map.Refresh();

            lblAltitude.Text = $"ALT: {data.alt:F1} m";
            lblHeading.Text = $"HDG: {data.heading:F1}°";

            double speed = data.speed;
            if (speedMode == 1) speed *= 3.6;
            else if (speedMode == 2) speed = (speed * 3.6) / 1234.8;

            speedGauge1.Value = Clamp(speed, speedGauge1.FromValue, speedGauge1.ToValue);
            altitudeGauge2.Value = Clamp(data.alt, altitudeGauge2.FromValue, altitudeGauge2.ToValue);
            headingGauge3.Value = Clamp(data.heading, headingGauge3.FromValue, headingGauge3.ToValue);

            double power = powerMode == 1 ? data.power * data.voltage : data.power;
            powerGauge4.Value = Clamp(power, powerGauge4.FromValue, powerGauge4.ToValue);

            UpdateCompass((float)data.heading);
            UpdateHorizon(data.pitch, data.roll);

            droneYaw = data.heading;
            dronePitch = data.pitch;
            droneRoll = data.roll;

            if (lblHudAlt != null) lblHudAlt.Text = $"{data.alt:F1} m";
            if (lblHudSpd != null) lblHudSpd.Text = $"{speed:F1}";
            if (lblHudHdg != null) lblHudHdg.Text = $"{data.heading:F1}°";
            if (lblHudPitch != null) lblHudPitch.Text = $"{data.pitch:F1}°";
            if (lblHudRoll != null) lblHudRoll.Text = $"{data.roll:F1}°";
            if (lblHudPwr != null) lblHudPwr.Text = $"{power:F1}";

            if (lblTelSpeed != null) lblTelSpeed.Text = $"{speed:F1} m/s";
            if (lblTelAlt != null) lblTelAlt.Text = $"{data.alt:F1} m";
            if (lblTelBatt != null) lblTelBatt.Text = $"{data.voltage:F1} V";
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════
        private double Clamp(double v, double min, double max)
        {
            return v < min ? min : v > max ? max : v;
        }

        private void UpdateCompass(float heading)
        {
            if (compassBaseImage == null || compassBox == null) return;

            Bitmap bmp = new Bitmap(compassBaseImage.Width, compassBaseImage.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TranslateTransform(bmp.Width / 2f, bmp.Height / 2f);
                g.RotateTransform(-heading);
                g.TranslateTransform(-bmp.Width / 2f, -bmp.Height / 2f);
                g.DrawImage(compassBaseImage, 0, 0);
            }

            var old = compassBox.Image;
            compassBox.Image = bmp;
            old?.Dispose();
        }

        private void UpdateHorizon(double pitch, double roll)
        {
            int w = horizonPanel.Width;
            int h = horizonPanel.Height;
            if (w <= 0 || h <= 0) return;

            Bitmap bmp = new Bitmap(w, h);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TranslateTransform(w / 2f, h / 2f);
                g.RotateTransform((float)roll);
                float pitchOffset = (float)(pitch * 4);
                g.TranslateTransform(0, pitchOffset);
                g.TranslateTransform(-w / 2f, -h / 2f);

                using (var sky = new LinearGradientBrush(new Point(0, 0), new Point(0, h),
                    Color.FromArgb(30, 80, 160), Color.FromArgb(120, 180, 255)))
                    g.FillRectangle(sky, -w, -h, w * 3, h * 3);

                using (var ground = new SolidBrush(Color.FromArgb(110, 70, 25)))
                    g.FillRectangle(ground, -w, h / 2, w * 3, h * 3);

                using (Pen p = new Pen(Color.White, 2))
                    g.DrawLine(p, -w, h / 2, w * 3, h / 2);

                for (int i = -90; i <= 90; i += 10)
                {
                    if (i == 0) continue;
                    int y = h / 2 + i * 4;
                    int lineWidth = (i % 20 == 0) ? 120 : 60;
                    g.DrawLine(Pens.White, w / 2 - lineWidth, y, w / 2 + lineWidth, y);
                }

                g.ResetTransform();
                int cx = w / 2, cy = h / 2;
                using (Pen p = new Pen(Color.Yellow, 2))
                {
                    g.DrawLine(p, cx - 30, cy, cx - 6, cy);
                    g.DrawLine(p, cx + 6, cy, cx + 30, cy);
                    g.DrawLine(p, cx, cy - 6, cx, cy + 6);
                }
            }

            horizonBox.Image?.Dispose();
            horizonBox.Image = bmp;
        }

        // ════════════════════════════════════════════════════════════════════
        // FORM CLOSING — Kaynakları temizle
        // ════════════════════════════════════════════════════════════════════
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // MediaPlayer durdur ve dispose et
            try
            {
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Stop();
                    _mediaPlayer.Dispose();
                }
            }
            catch { }

            // LibVLC dispose
            try { _libVLC?.Dispose(); }
            catch { }

            // UDP kapat
            try { udp?.Close(); }
            catch { }

            base.OnFormClosing(e);
        }

        // ════════════════════════════════════════════════════════════════════
        // DATA MODEL
        // ════════════════════════════════════════════════════════════════════
        public class GpsData
        {
            public double lat { get; set; }
            public double lon { get; set; }
            public double alt { get; set; }
            public double heading { get; set; }
            public double speed { get; set; }
            public double power { get; set; }
            public double voltage { get; set; }
            public double pitch { get; set; }
            public double roll { get; set; }
        }
    }
}