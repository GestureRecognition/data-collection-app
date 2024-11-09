import os
import socket
import threading
import cv2
from datetime import datetime
from ganzin.sol_sdk.synchronous.models import StreamingMode
from ganzin.sol_sdk.synchronous.sync_client import SyncClient
from scipy.stats import false_discovery_control


# Function to get IP and port for Ganzin SDK
def get_ip_and_port():
    return '192.168.0.116', 8080

# Global variables
sc = None
glasses_client_connected = False
streaming_active = False
save_video = True
recording_device = "Camera" # Camera/Glasses
save_at = "Phone"   # if Phone & recording_device == "Camera"  (Phone/Local)
                    # we save the video in the phone with resp = sc.begin_record()
video_writer = None
is_recording = False
frame_width = 320
frame_height = 240

# Function to start recording
def start_recording(filename):
    global video_writer, is_recording, frame_width, frame_height
    if not is_recording:
        os.makedirs('./recordings', exist_ok=True)
        fourcc = cv2.VideoWriter_fourcc(*'mp4v')
        file_path = f"./recordings/{filename}.mp4"
        video_writer = cv2.VideoWriter(file_path, fourcc, 20.0, (frame_width, frame_height))
        is_recording = True
        print(f"Started recording: {file_path}")

# Function to stop recording
def stop_recording():
    global video_writer, is_recording
    if is_recording:
        video_writer.release()
        video_writer = None
        is_recording = False
        print("Recording stopped")

def start_recording_sc():
    global is_recording
    if not is_recording:
        resp = sc.begin_record()
        print(resp)

def stop_recording_sc():
    global is_recording
    if is_recording:
        resp = sc.end_record()
        print(resp)

# UDP Server to handle requests
def udp_server():
    global streaming_active, recording_device, save_at, glasses_client_connected, sc
    filename = "Error_no_filename"
    udp_ip = "0.0.0.0"  # Listen on all available IPs
    udp_port = 9000     # Port for receiving UDP signals from Unity

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind((udp_ip, udp_port))
    print(f"UDP server listening on {udp_ip}:{udp_port}")

    try:
        while True:
            data, client_addr = sock.recvfrom(1024)  # Buffer size is 1024 bytes
            message = data.decode()
            print(f"Received message from {client_addr}: {message}")

            if message.startswith("StartStreaming"):
                # Parse the StartStreaming command with expected format
                try:
                    _, filename, device, destination = message.split(":")
                    recording_device = device
                    save_at = destination
                    streaming_active = True
                    print(f"Starting streaming with file '{filename}', device '{device}', save at '{destination}'")
                except ValueError:
                    print("Invalid StartStreaming command format.")
                    continue

            elif message == "GlassesStatus":
                if glasses_client_connected:
                    status = sc.get_status()
                    if status.status == "SUCCESS":
                        sock.sendto("GlassesConnected".encode(), client_addr)
                    else:
                        sock.sendto("GlassesNotConnected".encode(), client_addr)
                        glasses_client_connected = False
                else:
                    print("Glasses not connected, attempting to reconnect...")
                    # Try to reconnect or re-sync the client
                    try:
                        # Attempt to reconnect (you may want to adjust retries or timeout)
                        sc = SyncClient(*get_ip_and_port())  # Recreate the SyncClient instance
                        glasses_client_connected = True
                        print(f"Reconnected to Ganzin SDK at {get_ip_and_port()}")
                        sock.sendto("GlassesConnected".encode(), client_addr)
                    except Exception as ex:
                        print(f"Failed to reconnect: {ex}")
                        sock.sendto("GlassesNotConnected".encode(), client_addr)

            elif message == "StartRecording":
                print("UDP Command: StartRecording")
                if recording_device == "Glasses" and save_at == "Phone":
                    start_recording_sc()
                else:
                    start_recording(filename)

            elif message == "StopRecording":
                print("UDP Command: StopRecording")
                if recording_device == "Glasses" and save_at == "Phone":
                    stop_recording_sc()
                else:
                    stop_recording()

            elif message == "StopStreaming":
                print("UDP Command: StopStreaming")
                streaming_active = False

    except Exception as ex:
        print(f"UDP Server Error: {ex}")
    finally:
        sock.close()

def glasses_start_streaming():
    global streaming_active, frame_height, frame_width

    # Create a streaming thread
    th = sc.create_streaming_thread(StreamingMode.WORLD)
    th.start()

    try:
        while streaming_active:
            # Get the last frame from the stream
            frame_data = sc.get_world_frames_from_streaming(timeout=5.0)
            if not frame_data:
                print("No frames received after 5 seconds.")
                continue

            frame_datum = frame_data[-1]  # Get the latest frame
            frame = frame_datum.get_buffer()
            frame_height, frame_width = frame.shape[:2]

            # Write the frame if recording is active
            if is_recording and video_writer is not None:
                video_writer.write(frame)

            # Display the resized frame for debug
            cv2.imshow('Streaming', frame)
            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

    except Exception as ex:
        print("Error during streaming:", ex)
    finally:
        print("Streaming stopped")
        if is_recording:
            stop_recording()  # Ensure recording stops when streaming ends
        th.cancel()
        th.join()
        cv2.destroyAllWindows()

def camera_start_streaming():
    global streaming_active, frame_width, frame_height

    # Open the default camera (usually the webcam)
    cap = cv2.VideoCapture(0)  # 0 is usually the ID for the default webcam
    if not cap.isOpened():
        print("Error: Could not open the webcam.")
        return

    try:
        while streaming_active:
            # Capture frame-by-frame from the webcam
            ret, frame = cap.read()
            if not ret:
                print("Failed to grab frame.")
                break

            # Update frame dimensions dynamically
            frame_height, frame_width = frame.shape[:2]

            # Write the frame to video if recording is active
            if is_recording and video_writer is not None:
                video_writer.write(frame)

            # Display the frame for debug
            cv2.imshow('Webcam Streaming', frame)
            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

    except Exception as ex:
        print("Error during camera streaming:", ex)
    finally:
        print("Webcam streaming stopped")
        if is_recording:
            stop_recording()  # Ensure recording stops when streaming ends
        cap.release()
        cv2.destroyAllWindows()

def main():
    global streaming_active, glasses_client_connected, sc
    address, port = get_ip_and_port()
    try:
        # Try to create a SyncClient connection
        sc = SyncClient(address, port)
        glasses_client_connected = True
        print(f"Connected to Ganzin SDK at {address}:{port}")
    except Exception as ex:
        glasses_client_connected = False
        print(f"Failed to connect to Ganzin SDK at {address}:{port}. Error: {ex}")

    # Start the UDP server in a separate thread
    udp_thread = threading.Thread(target=udp_server)
    udp_thread.daemon = True  # This allows the program to exit even if the thread is running
    udp_thread.start()

    # Start the streaming in the main thread
    while True:
        if streaming_active:
            if recording_device == "Glasses" and glasses_client_connected:
                glasses_start_streaming()
            elif recording_device == "Camera":
                camera_start_streaming()
            streaming_active = False
            # TODO save the video mechanism


if __name__ == '__main__':
    main()
