import os
import socket
import struct
import threading
import cv2
from datetime import datetime
from ganzin.sol_sdk.synchronous.models import StreamingMode
from ganzin.sol_sdk.synchronous.sync_client import SyncClient
from enum import Enum

"""
Dependencies:
-------------
1. OpenCV (cv2) - For video streaming and recording
   Install: pip install opencv-python

2. Ganzin SDK - For interacting with Ganzin glasses
   Install: Follow installation instructions from Ganzin documentation
"""

"""
UDP Command Reference [TODO]
----------------------
1. StartStreaming:<FilenameNote>:<isDateOn>:<recordingDevice>:<saveAt>
    - Starts video streaming with the given filename, device (Camera/Glasses), and save location (Phone/Local).
2. StopStreaming
    - Stops the video streaming.
3. StartRecording:<NumericGestureID>
    - Starts recording the video stream.
4. StopRecording
    - Stops recording the video stream.
5. GlassesStatus
    - Checks the connection status of the Ganzin glasses.
"""

# Config =========================================================================
STREAM_RESIZE_RATIO = 15    # Set the quality to a lower value (0-100)
SOCKET_BUFFER = 1024        # Buffer size is 1024 bytes
# Config [END]====================================================================

# Global variables ===============================================================
# Enums for recording device and save location
class RecordingDevice(Enum):
    # Camera/Glasses
    CAMERA = "Camera"
    GLASSES = "Glasses"

class SaveLocation(Enum):
    # if Phone & recording_device == RecordingDevice.CAMERA  (Phone/Local)
    # we save the video in the phone with resp = sc.begin_record()
    PHONE = "Phone"
    LOCAL = "Local"

glassesSyncClient = None    # sc
glassesClientConnected = False
streamingActive = False
recordingDevice = RecordingDevice.CAMERA
saveAt = SaveLocation.PHONE
videoWriter = None
isRecording = False
isGlassesRecording = False
frameWidth = 1920  # Later will be replaced by camera native resolution
frameHeight = 1080 # Later will be replaced by camera native resolution
# Global variables [END]==========================================================

# Setup Functions ================================================================
# Function to get IP and port for Ganzin SDK (Change based on Glasses IP)
def get_ip_and_port():
    return '192.168.0.116', 8080

def get_streaming_ip_and_port():
    return '127.0.0.1', 9001

# Listen on all available IPs, Port for receiving UDP signals from Unity
def udp_server_ip_and_port():
    return '0.0.0.0', 9000
# Setup Functions [END]===========================================================


# Utils Functions ================================================================
def start_recording(filename):
    global videoWriter, frameWidth, frameHeight, isRecording, isGlassesRecording, recordingDevice, saveAt
    # TODO check what will happen if there is a file with the same name already
    if recordingDevice == RecordingDevice.GLASSES and saveAt == SaveLocation.PHONE and not isRecording:
        resp = glassesSyncClient.begin_record()
        isGlassesRecording = True
        print(resp)
    elif not isRecording:
        os.makedirs('./recordings', exist_ok=True)
        fourcc = cv2.VideoWriter_fourcc(*'mp4v')
        file_path = f"./recordings/{filename}.mp4"
        videoWriter = cv2.VideoWriter(file_path, fourcc, 20.0, (frameWidth, frameHeight))
        isRecording = True
    print(f"Started recording on {recordingDevice}.")
def stop_recording():
    global videoWriter, isRecording, isGlassesRecording, recordingDevice, saveAt
    if recordingDevice == RecordingDevice.GLASSES and saveAt == SaveLocation.PHONE:
        resp = glassesSyncClient.end_record()
        isGlassesRecording = False
        print(resp)
    elif videoWriter is not None:
        videoWriter.release()
        videoWriter = None
        isRecording = False
    print(f"Stopped recording on {recordingDevice}.")
def format_filename(filename_note, is_date_on, gesture_id):
    """
    Formats the filename for recording.

    Parameters:
    - filename_note (str): A note to include in the filename (e.g., user-defined description).
    - is_date_on (bool): Whether to include the current date in the filename.
    - gesture_id (str): A numeric ID representing the gesture.

    Returns:
    - str: A formatted filename.
    """
    current_date = datetime.now().strftime("%Y%m%d_%H%M%S") if is_date_on else ""
    filename_note = "" if filename_note == "NoNote" else filename_note
    components = [
        str(gesture_id),  # Gesture ID
        filename_note.strip(),  # User-provided note (trim whitespace)
        current_date  # Optional date/time
    ]
    formatted_filename = "_".join(filter(None, components))
    # Ensure the filename is safe by replacing illegal characters
    return formatted_filename.replace(" ", "_").replace(":", "_").replace("/", "_")
# Utils Functions [END]===========================================================


# Streaming Functions ============================================================
def glasses_start_streaming(sock, ip, port):
    global streamingActive, frameHeight, frameWidth, STREAM_RESIZE_RATIO

    # Create a streaming thread (Glasses -> server)
    th = glassesSyncClient.create_streaming_thread(StreamingMode.WORLD)
    th.start()

    # Main Loop
    try:
        while streamingActive:
            # Get the last frame from the stream
            frame_data = glassesSyncClient.get_world_frames_from_streaming(timeout=5.0)
            if not frame_data:
                print("No frames received after 5 seconds.")
                continue

            frame_datum = frame_data[-1]  # Get the latest frame
            frame = frame_datum.get_buffer()
            frameHeight, frameWidth = frame.shape[:2] # Get Native Dimension

            # Encode frame as JPEG to reduce size
            encode_param = [int(cv2.IMWRITE_JPEG_QUALITY), STREAM_RESIZE_RATIO]
            _, buffer = cv2.imencode('.jpg', frame, encode_param)
            data = buffer.tobytes()
            frame_size = len(data)
            # Send the frame size and frame data
            sock.sendto(struct.pack("L", frame_size) + data, (ip, port))

            # Write the frame if recording is active
            if isRecording and videoWriter is not None:
                videoWriter.write(frame)

            # Display the resized frame for debug
            cv2.imshow('Streaming', frame)
            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

    except Exception as ex:
        print("Error during streaming:", ex)
    finally:
        print("Streaming stopped")
        if isRecording:
            stop_recording()  # Ensure recording stops when streaming ends
        th.cancel()
        th.join()
        cv2.destroyAllWindows()
# ================================================================================
def camera_start_streaming(sock, ip, port):
    global streamingActive, frameWidth, frameHeight, STREAM_RESIZE_RATIO

    # Open the default camera (usually the webcam)
    cap = cv2.VideoCapture(0)  # 0 is usually the ID for the default webcam
    if not cap.isOpened():
        print("Error: Could not open the webcam.")
        return

    try:
        while streamingActive:
            # Capture frame-by-frame from the webcam
            ret, frame = cap.read()
            if not ret:
                print("Failed to grab frame.")
                break

            # Update frame dimensions dynamically
            frameHeight, frameWidth = frame.shape[:2]

            # Encode frame as JPEG to reduce size
            encode_param = [int(cv2.IMWRITE_JPEG_QUALITY), STREAM_RESIZE_RATIO]
            _, buffer = cv2.imencode('.jpg', frame, encode_param)
            data = buffer.tobytes()
            frame_size = len(data)
            # Send the frame size and frame data
            try:
                sock.sendto(struct.pack("L", frame_size) + data, (ip, port))
            except socket.error as ex:
                print(f"Error sending stream packet: {ex}")

            # Write the frame to video if recording is active
            if isRecording and videoWriter is not None:
                videoWriter.write(frame)

            # Display the frame for debug
            cv2.imshow('Webcam Streaming', frame)
            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

    except Exception as ex:
        print("Error during camera streaming:", ex)
    finally:
        print("Webcam streaming stopped")
        if isRecording:
            stop_recording()  # Ensure recording stops when streaming ends
        cap.release()
        cv2.destroyAllWindows()
# Streaming Functions [END]=======================================================


# Handle UDP Request Thread ======================================================
def udp_server():
    global streamingActive, recordingDevice, saveAt, glassesClientConnected, glassesSyncClient
    filename_note = ""
    is_date_on = False

    # Initialize Socket
    udp_ip, udp_port = udp_server_ip_and_port()
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind((udp_ip, udp_port))
    print(f"UDP server listening on {udp_ip}:{udp_port}")

    try:
        while True:
            data, client_addr = sock.recvfrom(SOCKET_BUFFER)
            message = data.decode()
            print(f"Received message from {client_addr}: {message}")

            if message.startswith("StartStreaming"):
                # Parse the StartStreaming command with expected format
                try:
                    _, filename_note, is_date_on, device, save_loc = message.split(":")
                    recordingDevice = RecordingDevice(device)
                    saveAt = SaveLocation(save_loc)
                    streamingActive = True
                    print(f"Starting streaming with device '{device}', save at '{save_loc}'")
                    sock.sendto("StartedStreaming".encode(), client_addr)
                except ValueError:
                    print("Invalid StartStreaming command format.")
                    continue

            elif message == "GlassesStatus":
                if glassesClientConnected:
                    status = glassesSyncClient.get_status()
                    if status.status == "SUCCESS":
                        sock.sendto("GlassesConnected".encode(), client_addr)
                    else:
                        sock.sendto("GlassesNotConnected".encode(), client_addr)
                        glassesClientConnected = False
                else:
                    print("Glasses not connected, attempting to reconnect...")
                    sock.sendto("GlassesNotConnected".encode(), client_addr)
                    # Try to reconnect or re-sync the client
                    try:
                        # Attempt to reconnect (you may want to adjust retries or timeout)
                        glassesSyncClient = SyncClient(*get_ip_and_port())
                        glassesClientConnected = True
                        print(f"Reconnected to Ganzin SDK at {get_ip_and_port()}")
                    except Exception as ex:
                        print(f"Failed to reconnect: {ex}")

            elif message.startswith("StartRecording"):
                print("UDP Command: StartRecording")
                _, numeric_gesture_id = message.split(":")
                filename = format_filename(filename_note, is_date_on, numeric_gesture_id)
                start_recording(filename)

            elif message == "StopRecording":
                print("UDP Command: StopRecording")
                stop_recording()

            elif message == "StopStreaming":
                print("UDP Command: StopStreaming")
                if isRecording or isGlassesRecording:
                    stop_recording()
                streamingActive = False

    except Exception as ex:
        print(f"UDP Server Error: {ex}")
    finally:
        sock.close()
# Handle UDP Request Thread [END]=================================================


# Main Function ==================================================================
def main():
    global streamingActive, glassesClientConnected, glassesSyncClient

    # Start the UDP server thread
    udp_thread = threading.Thread(target=udp_server)
    udp_thread.daemon = True  # This allows the program to exit even if the thread is running
    udp_thread.start()

    # Setup UDP video stream socket
    udp_video_ip, udp_video_port = get_streaming_ip_and_port()
    udp_video_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    # Try to create a SyncClient connection
    address, port = get_ip_and_port()
    try:
        glassesSyncClient = SyncClient(address, port)
        glassesClientConnected = True
        print(f"Connected to Ganzin SDK at {address}:{port}")
    except Exception as ex:
        glassesClientConnected = False
        print(f"Failed to connect to Ganzin SDK at {address}:{port}. Error: {ex}")
    # TODO Make a retry mechanism

    # Start the streaming in the main thread
    while True:
        if streamingActive:
            if recordingDevice == RecordingDevice.GLASSES and glassesClientConnected:
                glasses_start_streaming(udp_video_sock, udp_video_ip, udp_video_port)
            elif recordingDevice == RecordingDevice.CAMERA:
                camera_start_streaming(udp_video_sock, udp_video_ip, udp_video_port)
            streamingActive = False

if __name__ == '__main__':
    main()
# Main Function [END]=============================================================


