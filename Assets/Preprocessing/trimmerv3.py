import os
import cv2
import numpy as np
import mediapipe as mp
from moviepy.editor import VideoFileClip
from tqdm import tqdm

# Initialize MediaPipe Hands
mp_hands = mp.solutions.hands
hands = mp_hands.Hands(static_image_mode=False, max_num_hands=2, min_detection_confidence=0.5)
mp_drawing = mp.solutions.drawing_utils

# Define the folder containing the MP4 files
input_folder = "C:/!Steven/Programming Projects/Data Collection App/HandGesturesPreviewRecordings/0.6x_Muted"
output_folder = "C:/!Steven/Programming Projects/Data Collection App/HandGesturesPreviewRecordings/0.6x_Trimmed"

# Ensure the output folder exists
if not os.path.exists(output_folder):
    os.makedirs(output_folder)

# Function to check if hands are detected in a frame
def hands_detected(frame):
    frame_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = hands.process(frame_rgb)
    return results.multi_hand_landmarks is not None

# Function to trim video based on hand detection
def trim_video_based_on_hands(mp4_file, outfile_path):
    try:
        # Load the video clip
        clip = VideoFileClip(mp4_file)
        fps = clip.fps
        duration = clip.duration

        # Create an OpenCV VideoCapture object to read frames
        cap = cv2.VideoCapture(mp4_file)

        # Variables to track start and end times
        start_time = 0
        end_time = duration
        frame_idx = 0
        hand_detected_start = False
        hand_detected_end = False

        total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))

        # Loop through the video frames to detect hands
        for frame_idx in range(total_frames):
            ret, frame = cap.read()
            if not ret:
                break

            if hands_detected(frame):
                if not hand_detected_start:
                    # First frame with hands detected
                    start_time = frame_idx / fps
                    hand_detected_start = True
                # Update the end time to the latest frame with hands detected
                end_time = frame_idx / fps
                hand_detected_end = True

        cap.release()

        # Trim the video between the detected start and end times
        if not hand_detected_start or not hand_detected_end or start_time >= end_time:
            print(f"Skipping {mp4_file}: no hands detected.")
        else:
            trimmed_clip = clip.subclip(start_time, end_time)
            output_file = os.path.join(outfile_path, os.path.basename(mp4_file))
            trimmed_clip.write_videofile(output_file, codec="libx264", audio_codec="aac", verbose=False, logger=None)
            print(f"Trimmed and saved: {os.path.basename(output_file)}")

    except Exception as e:
        print(f"Failed to trim {mp4_file}: {e}")

# Loop through all MP4 files in the input folder
mp4_files = [filename for filename in os.listdir(input_folder) if filename.endswith(".mp4")]
for filename in tqdm(mp4_files, desc="Processing Videos", unit="video"):
    mp4_path = os.path.join(input_folder, filename)

    # Trim video based on hand detection
    trim_video_based_on_hands(mp4_path, output_folder)

print("Trimming process complete!")
