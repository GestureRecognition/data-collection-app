import os
from moviepy.editor import VideoFileClip

# Define the folder containing the MP4 files
input_folder = "C:/!Steven/Programming Projects/Data Collection App/HandGesturesPreviewRecordings/0.6x"
output_folder = "C:/!Steven/Programming Projects/Data Collection App/HandGesturesPreviewRecordings/0.6x_GIF"

# Ensure the output folder exists
if not os.path.exists(output_folder):
    os.makedirs(output_folder)

# Function to convert MP4 to GIF with reduced resolution
def convert_mp4_to_gif(mp4_file, gif_file, scale_factor=0.5):
    try:
        # Load the video clip
        clip = VideoFileClip(mp4_file)
        # Resize the clip to reduce resolution (scale down by scale_factor)
        clip_resized = clip.resize(scale_factor)
        # Write the resized GIF file
        clip_resized.write_gif(gif_file)
        print(f">>> Converted: {mp4_file}")
        print(f">>> To: {gif_file}")
        print(f">>> With resolution scale: {scale_factor}")

    except Exception as e:
        print(f">>> Failed to convert {mp4_file}: {e}")

# Loop through all MP4 files in the input folder
for filename in os.listdir(input_folder):
    if filename.endswith(".mp4"):
        mp4_path = os.path.join(input_folder, filename)
        gif_filename = filename.replace(".mp4", ".gif")
        gif_path = os.path.join(output_folder, gif_filename)

        # Convert the MP4 file to GIF with reduced resolution (scale_factor can be adjusted)
        convert_mp4_to_gif(mp4_path, gif_path, scale_factor=0.2)  # 20% of original resolution

print("Conversion complete!")

