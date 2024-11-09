import os
import cv2
import numpy as np
from moviepy.editor import VideoFileClip
from tqdm import tqdm

# Define the folder containing the MP4 files
input_folder = "C:/!Steven/Programming Projects/Data Collection App/HandGesturesPreviewRecordings/0.6x_Muted"
output_folder = "C:/!Steven/Programming Projects/Data Collection App/HandGesturesPreviewRecordings/0.6x_Trimmed"

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
def trim_empty_wall_sections(mp4_file, outfile_path):
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
        highest_diff = 0
        frame_diffs = []  # Store all frame differences for threshold calculation

        ret, prev_frame = cap.read()
        frame_idx = 0

        # Loop through the video frames to calculate frame differences and determine the highest difference
        while True:
            ret, frame = cap.read()
            if not ret:
                break

            diff = calculate_frame_difference(prev_frame, frame)
            frame_diffs.append(diff)
            highest_diff = max(highest_diff, diff)  # Update highest difference if needed

            # Update the previous frame for the next comparison
            prev_frame = frame
            frame_idx += 1

        cap.release()

        # Determine threshold as 1/2 of the highest difference
        threshold = highest_diff / 2
        print(f"Threshold for {os.path.basename(mp4_file)} set to: {threshold}")

        # Identify the start and end times based on the threshold
        start_found = False
        end_found = False

        # Iterate over the frame differences to find the start and end frames
        for idx, diff in enumerate(frame_diffs):
            if not start_found and diff > threshold:
                start_time = idx / fps
                start_found = True
            if diff > threshold:
                end_time = idx / fps  # Keep updating until the last frame above threshold

        # Trim the video between the detected start and end times
        if start_time >= end_time:
            print(f"Skipping {mp4_file}: no motion detected.")
        else:
            trimmed_clip = clip.subclip(start_time, end_time)
            output_file = os.path.join(outfile_path, os.path.basename(mp4_file))
            trimmed_clip.write_videofile(output_file, codec="libx264", audio_codec="aac", verbose=False, logger=None)
            print(f"Trimmed and saved: {os.path.basename(output_file)} "
                  f"|| Duration: {end_time - start_time}")

    except Exception as e:
        print(f"Failed to trim {mp4_file}: {e}")

# Loop through all MP4 files in the input folder
mp4_files = [filename for filename in os.listdir(input_folder) if filename.endswith(".mp4")]
for filename in tqdm(mp4_files, desc="Processing Videos", unit="video"):
    mp4_path = os.path.join(input_folder, filename)

    # Trim empty wall sections from the video
    trim_empty_wall_sections(mp4_path, output_folder)

print("Trimming process complete!")