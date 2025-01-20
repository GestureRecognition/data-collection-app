import socket

def main():
    # Server IP and port
    server_ip = "127.0.0.1"  # Change if the server runs on a different machine
    server_port = 9000        # Must match the server's UDP port

    # Create a UDP socket
    client_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    print("\n--- UDP Test Client ---")
    print(f"Connected to server at {server_ip}:{server_port}")
    print("Type a command to send to the server. Type 'exit' to quit.")

    try:
        while True:
            # Get user input
            command = input("Enter command: ")

            if command.lower() == "exit":
                print("Exiting...")
                break

            # Send the command to the server
            client_sock.sendto(command.encode(), (server_ip, server_port))

            # Receive a response from the server (if any)
            # try:
            #     client_sock.settimeout(5)  # Timeout in case no response is received
            #     response, _ = client_sock.recvfrom(1024)  # Adjust buffer size if needed
            #     print(f"Server response: {response.decode()}")
            # except socket.timeout:
            #     print("No response from the server.")

    except KeyboardInterrupt:
        print("\nClient terminated by user.")

    finally:
        client_sock.close()
        print("Socket closed.")

if __name__ == "__main__":
    main()
