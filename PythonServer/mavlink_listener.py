from pymavlink import mavutil
import socket
import json
import time
import sys

# ================= CONFIG =================
DEFAULT_PORT = "COM5"
BAUD = 115200
UDP_IP = "127.0.0.1"
UDP_PORT = 14550
SEND_RATE = 0.2  # saniye (5 Hz)

# ================= ARGUMENT =================
port = DEFAULT_PORT

if len(sys.argv) > 1:
    port = sys.argv[1]

print(f"Bağlanılıyor: {port} @ {BAUD}...")

# ================= MAVLINK CONNECTION =================
try:
    master = mavutil.mavlink_connection(port, baud=BAUD)
    master.wait_heartbeat()
    print("✅ Heartbeat alındı!")
except Exception as e:
    print("❌ MAVLink bağlantı hatası:", e)
    exit()

# ================= STREAM REQUEST =================
master.mav.request_data_stream_send(
    master.target_system,
    master.target_component,
    mavutil.mavlink.MAV_DATA_STREAM_ALL,
    10,
    1
)

# ================= UDP SOCKET =================
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

# ================= CACHE =================
data_cache = {
    "lat": 0,
    "lon": 0,
    "alt": 0,
    "heading": 0,
    "speed": 0,
    "voltage": 0,
    "current": 0,
    "power": 0,
    "pitch": 0,
    "roll": 0
}

last_send_time = time.time()

# ================= MAIN LOOP =================
while True:
    try:
        msg = master.recv_match(blocking=False)

        if msg:
            msg_type = msg.get_type()

            # ===== GPS =====
            if msg_type == "GLOBAL_POSITION_INT":
                data_cache["lat"] = msg.lat / 1e7
                data_cache["lon"] = msg.lon / 1e7
                data_cache["alt"] = msg.alt / 1000.0

                if msg.hdg != 65535:
                    data_cache["heading"] = msg.hdg / 100.0

            # ===== SPEED =====
            elif msg_type == "VFR_HUD":
                data_cache["speed"] = msg.groundspeed

            # ===== POWER =====
            elif msg_type == "SYS_STATUS":
                voltage = msg.voltage_battery / 1000.0
                current = msg.current_battery / 100.0

                if voltage > 0 and current > 0:
                    data_cache["voltage"] = voltage
                    data_cache["current"] = current
                    data_cache["power"] = voltage * current
                else:
                    data_cache["voltage"] = 0
                    data_cache["current"] = 0
                    data_cache["power"] = 0

            # ===== ATTITUDE =====
            elif msg_type == "ATTITUDE":
                data_cache["roll"] = msg.roll * 57.2958
                data_cache["pitch"] = msg.pitch * 57.2958

        # ===== SEND DATA =====
        if time.time() - last_send_time >= SEND_RATE:
            last_send_time = time.time()

            display_power = data_cache["power"]
            display_voltage = data_cache["voltage"]

            # fallback
            if display_power == 0:
                display_voltage = 12.0
                display_power = data_cache["speed"] * 8

            send_data = {
                "lat": data_cache["lat"],
                "lon": data_cache["lon"],
                "alt": data_cache["alt"],
                "heading": data_cache["heading"],
                "speed": data_cache["speed"],
                "voltage": display_voltage,
                "power": display_power,
                "pitch": data_cache["pitch"],
                "roll": data_cache["roll"]
            }

            # console debug
            print(json.dumps(send_data))

            # UDP gönder
            sock.sendto(
                json.dumps(send_data).encode(),
                (UDP_IP, UDP_PORT)
            )

    except KeyboardInterrupt:
        print("\n⛔ Program durduruldu.")
        break

    except Exception as e:
        print("⚠️ Hata:", e)
        time.sleep(1)