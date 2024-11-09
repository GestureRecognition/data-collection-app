import os
from moviepy.editor import VideoFileClip
import pathlib
from tqdm import tqdm


# Define the folder containing the MP4 files
input_folder = "C:/!Steven/Programming Projects/Data Collection App/HandGesturesPreviewRecordings/0.6x_Trimmed"
output_folder = "C:/!Steven/Programming Projects/Data Collection App/HandGesturesPreviewRecordings/0.6x_5FRAME"

# Ensure the output folder exists
if not os.path.exists(output_folder):
    os.makedirs(output_folder)

# Function to extract five frames from an MP4 file
def extract_frames(mp4_file, outfile_path):
    try:
        # Load the video clip
        clip = VideoFileClip(mp4_file)
        duration = clip.duration  # Get the duration of the video

        # Calculate the time interval to extract frames
        intervals = [duration * i / 6 for i in range(1, 6)]  # 5 frames equally spaced

        # Extract and save each frame
        for idx, time_point in enumerate(intervals):
            frame = clip.get_frame(time_point)  # Extract the frame at the specific time
            frame_filename = os.path.join(outfile_path, f"{pathlib.Path(mp4_file).stem}_frame_{idx + 1}.png")
            clip.save_frame(frame_filename, t=time_point)
            # print(f"Saved frame {idx + 1} from {mp4_file} at {time_point:.2f} seconds")

    except Exception as e:
        print(f"Failed to extract frames from {mp4_file}: {e}")

# Loop through all MP4 files in the input folder
mp4_files = [filename for filename in os.listdir(input_folder) if filename.endswith(".mp4")]
for filename in tqdm(mp4_files, desc="Extracting Frames", unit="video"):
    if filename.endswith(".mp4"):
        mp4_path = os.path.join(input_folder, filename)

        # Extract frames from the MP4 file
        extract_frames(mp4_path, output_folder)

print("Frame extraction complete!")