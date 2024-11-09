import os
from moviepy.editor import VideoFileClip

# Define the folder containing the MP4 files
input_folder = "C:/!Steven/Programming Projects/Data Collection App/HandGesturesPreviewRecordings/0.6x"
output_folder = "C:/!Steven/Programming Projects/Data Collection App/HandGesturesPreviewRecordings/0.6x_Muted"

# Ensure the output folder exists
if not os.path.exists(output_folder):
    os.makedirs(output_folder)


# Function to mute the video and save it to the output folder
def mute_video(mp4_file, outfile_path):
    try:
        # Load the video clip
        clip = VideoFileClip(mp4_file)

        # Remove the audio from the clip
        muted_clip = clip.without_audio()

        # Generate the output file path
        output_file = os.path.join(outfile_path, os.path.basename(mp4_file))

        # Write the muted video to the output file
        muted_clip.write_videofile(output_file, codec="libx264", audio_codec="aac", verbose=False, logger=None)

        print(f"Muted and saved: {output_file}")

    except Exception as e:
        print(f"Failed to mute {mp4_file}: {e}")


# Loop through all MP4 files in the input folder
for filename in os.listdir(input_folder):
    if filename.endswith(".mp4"):
        mp4_path = os.path.join(input_folder, filename)

        # Mute the video
        mute_video(mp4_path, output_folder)

print("Muting process complete!")
