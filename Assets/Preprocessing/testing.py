import cv2
import numpy as np
import matplotlib.pyplot as plt

threshold = 1.5

# Function to compute the frame difference between two frames
def calculate_frame_difference(frame1, frame2):
    gray_frame1 = cv2.cvtColor(frame1, cv2.COLOR_BGR2GRAY)
    gray_frame2 = cv2.cvtColor(frame2, cv2.COLOR_BGR2GRAY)
    diff = cv2.absdiff(gray_frame1, gray_frame2) / 10**6
    return np.sum(diff)

# Function to calculate average RGB values of a frame
def calculate_average_rgb(frame):
    avg_rgb = np.mean(frame, axis=(0, 1))  # Calculate the mean across width and height
    return avg_rgb  # Returns [B, G, R] average values

# Function to plot the frame differences of a video along with RGB values
def plot_frame_differences_and_rgb(mp4_file):
    cap = cv2.VideoCapture(mp4_file)

    if not cap.isOpened():
        print(f"Failed to open {mp4_file}")
        return

    frame_diffs = []  # List to store frame differences
    frame_times = []  # List to store corresponding time points
    avg_red_values = []    # List to store average red values
    avg_green_values = []  # List to store average green values
    avg_blue_values = []   # List to store average blue values

    ret, prev_frame = cap.read()
    frame_idx = 1
    fps = cap.get(cv2.CAP_PROP_FPS)

    while ret:
        ret, frame = cap.read()
        if not ret:
            break

        # Calculate frame difference between the current frame and the previous frame
        diff = calculate_frame_difference(prev_frame, frame)
        frame_diffs.append(diff)

        # Calculate the corresponding time for the frame
        frame_time = frame_idx / fps
        frame_times.append(frame_time)

        # Calculate average RGB values
        avg_rgb = calculate_average_rgb(frame)
        avg_blue_values.append(avg_rgb[0])  # Blue channel
        avg_green_values.append(avg_rgb[1]) # Green channel
        avg_red_values.append(avg_rgb[2])   # Red channel

        # Update the previous frame to the current one
        prev_frame = frame
        frame_idx += 1

    cap.release()

    # Plot the frame differences and RGB values
    plt.figure(figsize=(12, 8))

    # Plot frame differences
    plt.subplot(2, 1, 1)
    plt.plot(frame_times, frame_diffs, label='Frame Difference', color='black')
    plt.xlabel('Time (seconds)')
    plt.ylabel('Difference')
    plt.title('Frame Differences Over Time')
    plt.grid(True)
    plt.legend()

    # Plot RGB values
    plt.subplot(2, 1, 2)
    plt.plot(frame_times, avg_red_values, label='Red Channel', color='red', alpha=0.7)
    plt.plot(frame_times, avg_green_values, label='Green Channel', color='green', alpha=0.7)
    plt.plot(frame_times, avg_blue_values, label='Blue Channel', color='blue', alpha=0.7)
    plt.xlabel('Time (seconds)')
    plt.ylabel('Average RGB Value')
    plt.title('Average RGB Values Over Time')
    plt.grid(True)
    plt.legend()

    # Show the plot
    plt.tight_layout()
    plt.show()

# Path to the video file
mp4_file = ("C:/!Steven/Programming Projects/Data Collection App/HandGesturesPreviewRecordings/"
            "RAW/0.6x/EGO_68.mp4")

# Plot the frame differences and RGB values for the video
plot_frame_differences_and_rgb(mp4_file)
