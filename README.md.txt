# 🚁 UAV Ground Control System

![C#](https://img.shields.io/badge/C%23-WinForms-512BD4?style=for-the-badge&logo=csharp&logoColor=white)
![Python](https://img.shields.io/badge/Python-MAVLink-3776AB?style=for-the-badge&logo=python&logoColor=white)
![UDP](https://img.shields.io/badge/Communication-UDP-blue?style=for-the-badge)
![Status](https://img.shields.io/badge/System-Active-success?style=for-the-badge)

---

A real-time UAV Ground Control Station developed using **C# (WinForms)** and **Python (MAVLink + UDP)**.  
The system receives telemetry data from a UAV and visualizes it in real time — including map tracking, artificial horizon, compass, 3D drone model, and live video stream.

---

## 🧠 System Architecture

```
Drone (ArduPilot / PX4)
        │
        │  MAVLink (Serial / Telemetry Radio)
        ▼
Python MAVLink Server (pymavlink)
        │
        │  UDP → JSON  (127.0.0.1 : 14550)
        ▼
C# Ground Control Station (WinForms)
        │
        ├── Map View      (GMap.NET)
        ├── 3D Drone      (Custom GDI+)
        ├── HUD / Gauges  (LiveCharts)
        ├── Compass       (Custom GDI+)
        ├── Horizon       (Custom GDI+)
        └── Video Stream  (LibVLCSharp — RTSP / MJPEG)
```

---

## ✨ Features

| Özellik | Açıklama |
|---|---|
| 📡 Real-time GPS Tracking | Anlık konum takibi ve uçuş rotası kaydı |
| 🗺️ Live Map View | GMap.NET ile Google Maps entegrasyonu |
| 🎯 Artificial Horizon | Pitch / Roll verisiyle gerçek zamanlı ufuk çizgisi |
| 🧭 Compass | Heading verisiyle dönen animasyonlu pusula |
| 🛸 3D Drone Model | Yaw / Pitch / Roll ile 3 boyutlu drone görseli |
| 🔋 Power Monitoring | Voltaj ve güç tüketimi takibi |
| 📹 Live Video Stream | RTSP (LibVLC) ve MJPEG desteği |
| 📶 UDP Telemetry | Düşük gecikmeli JSON tabanlı iletişim |

---

## 🖥️ UI Modules

```
┌─────────────────────────┬──────────────────────────┐
│                         │  MAP VIEW (GMap.NET)     │
│   LIVE VIDEO STREAM     │  • GPS tracking          │
│   RTSP / MJPEG          │  • Flight route          │
│   (LibVLCSharp)         ├──────────────────────────┤
│                         │  3D DRONE VIEW           │
│                         │  • Yaw / Pitch / Roll    │
│                         │  • Telemetry overlay     │
└─────────────────────────┴──────────────────────────┘
┌──────┬──────┬──────┬──────┬──────────┬─────────────┐
│ SPD  │ ALT  │ HDG  │ PWR  │ COMPASS  │ HORIZON     │
│ G1   │ G2   │ G3   │ G4   │          │ FLIGHT HUD  │
└──────┴──────┴──────┴──────┴──────────┴─────────────┘
```

---

## 🐍 Python Setup

Bağımlılıkları yükle:

```bash
pip install pymavlink
```

MAVLink dinleyiciyi başlat:

```bash
python mavlink_listener.py COM5
```

> **Not:** `COM5` yerine drone'un bağlı olduğu seri port numarasını gir.  
> Linux'ta bu port genellikle `/dev/ttyUSB0` veya `/dev/ttyACM0` şeklindedir.

---

## 💻 C# Setup

1. Projeyi **Visual Studio** ile aç
2. Aşağıdaki NuGet paketlerini yükle:

```
GMap.NET.WindowsForms
Newtonsoft.Json
LibVLCSharp
LibVLCSharp.WinForms
LiveCharts.WinForms
```

3. Projeyi çalıştır: **F5**

---

## 📹 Video Stream Kurulumu (Raspberry Pi)

Raspberry Pi tarafında `mediamtx` ile RTSP sunucu başlat:

```bash
# mediamtx indir ve çalıştır
./mediamtx
```

Ya da GStreamer ile UDP stream:

```bash
gst-launch-1.0 libcamerasrc ! video/x-raw,width=1280,height=720 ! \
  videoconvert ! x264enc tune=zerolatency ! rtph264pay ! \
  udpsink host=0.0.0.0 port=5600
```

Ground Control'de kullanılacak URL formatları:

| Tür | URL |
|---|---|
| RTSP (mediamtx) | `rtsp://192.168.1.x:8554/drone` |
| UDP (GStreamer) | `udp://@:5600` |
| MJPEG (HTTP) | `http://192.168.1.x:8080/video` |

---

## 🔌 Communication Protocol

| Parametre | Değer |
|---|---|
| Protokol | UDP |
| IP | `127.0.0.1` |
| Port | `14550` |
| Format | JSON |

**JSON telemetri paketi örneği:**

```json
{
  "lat": 38.6943,
  "lon": 35.5430,
  "alt": 120.5,
  "heading": 275.3,
  "speed": 12.4,
  "pitch": -2.1,
  "roll": 1.8,
  "power": 14.8,
  "voltage": 22.2
}
```

---

## 📁 Project Structure

```
UAV-Ground-Control/
│
├── CSharpUI/
│   ├── Form1.cs          ← Ana UI (Map, Video, HUD, Gauge)
│   ├── Form1.Designer.cs
│   └── Program.cs
│
├── PythonServer/
│   └── mavlink_listener.py  ← MAVLink → UDP köprüsü
│
├── Docs/
│   └── architecture.png
│
└── README.md
```

---

## 🚀 About This Project

This project is built for **real-time UAV monitoring and control**, combining telemetry processing, visual interface, and video streaming into a single ground control platform.

The system is actively developed and designed for **UAV research and competition environments**.  
It targets ArduPilot and PX4 based platforms communicating over MAVLink, with a modular UI architecture that can be extended with additional sensors or control surfaces.

---

## 📄 License

This project is open for educational and research use.  
For competition or commercial use, please contact the author.