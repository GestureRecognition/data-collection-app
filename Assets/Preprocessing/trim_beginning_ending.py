import os
import cv2
import numpy as np
from moviepy.editor import VideoFileClip
from tqdm import tqdm

# Define the folder containing the MP4 files
input_folder = "C:/!Steven/Programming Projects/Data Collection App/HandGesturesPreviewRecordings/0.6x_Muted"
output_folder = "C:/!Steven/Programming Projects/Data Collection App/HandGesturesPreviewRecordings/0.6x_Trimmed"
trim_threshold = 2.0


# Ensure the output folder exists
if not os.path.exists(output_folder):
    os.makedirs(output_folder)


# Function to compute frame difference
def calculate_frame_difference(frame1, frame2):
    gray_frame1 = cv2.cvtColor(frame1, cv2.COLOR_BGR2GRAY)
    gray_frame2 = cv2.cvtColor(frame2, cv2.COLOR_BGR2GRAY)
    diff = cv2.absdiff(gray_frame1, gray_frame2) / 10**6
    return np.sum(diff)


# Function to trim empty wall sections (minimal changes)
def trim_empty_wall_sections(mp4_file, outfile_path, threshold=2.0):
    try:
        # Load the video clip
        clip = VideoFileClip(mp4_file)
        fps = clip.fps
        duration = clip.duration

        # Read frames at the start and end, calculate differences
        start_frame_idx = 0
        end_frame_idx = int(duration * fps) - 1

        # Create an OpenCV VideoCapture object to read frames
        cap = cv2.VideoCapture(mp4_file)

        # Define buffers for empty sections at the beginning and end
        start_time = 0
        end_time = duration
        print(f"start time: {start_time} || end_time: {end_time}")

        # Check frames at the beginning for empty wall
        cap.set(cv2.CAP_PROP_POS_FRAMES, start_frame_idx)
        ret, prev_frame = cap.read()
        for frame_idx in range(1, end_frame_idx):
            ret, frame = cap.read()
            if not ret:
                break
            diff = calculate_frame_difference(prev_frame, frame)
            if diff > threshold:
                start_time = frame_idx / fps  # Set start time to when motion is detected
                break
            prev_frame = frame

        # Check frames at the end for empty wall
        cap.set(cv2.CAP_PROP_POS_FRAMES, end_frame_idx)
        ret, prev_frame = cap.read()
        for frame_idx in range(end_frame_idx - 1, start_frame_idx, -1):
            cap.set(cv2.CAP_PROP_POS_FRAMES, frame_idx)
            ret, frame = cap.read()
            if not ret:
                break
            diff = calculate_frame_difference(prev_frame, frame)
            if diff > threshold:
                end_time = frame_idx / fps  # Set end time to when motion is detected
                break
            prev_frame = frame

        cap.release()

        # Trim the video between the detected start and end times
        if start_time >= end_time:
            print(f"Skipping {mp4_file}: no motion detected.")
        else:
            print(f"new start time: {start_time} || new end_time: {end_time}")
            trimmed_clip = clip.subclip(start_time, end_time)
            output_file = os.path.join(outfile_path, os.path.basename(mp4_file))
            trimmed_clip.write_videofile(output_file, codec="libx264", audio_codec="aac", verbose=False, logger=None)
            print(f"Trimmed and saved: {output_file}")

    except Exception as e:
        print(f"Failed to trim {mp4_file}: {e}")


# Loop through all MP4 files in the input folder
mp4_files = [filename for filename in os.listdir(input_folder) if filename.endswith(".mp4")]
for filename in tqdm(mp4_files, desc="Processing Videos", unit="video"):
    mp4_path = os.path.join(input_folder, filename)

    # Trim empty wall sections from the video
    trim_empty_wall_sections(mp4_path, output_folder, trim_threshold)

print("Trimming process complete!")
