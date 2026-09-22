package com.example.ezacross.server;

import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;

public class Main {
    public static final byte[] KEYBOARD_REPORT_DESC = {
            (byte) 0x05, (byte) 0x01, // Usage Page (Generic Desktop)
            (byte) 0x09, (byte) 0x06, // Usage (Keyboard)
            (byte) 0xA1, (byte) 0x01, // Collection (Application)
            (byte) 0x05, (byte) 0x07, //   Usage Page (Key Codes)
            (byte) 0x19, (byte) 0xE0, //   Usage Minimum (224)
            (byte) 0x29, (byte) 0xE7, //   Usage Maximum (231)
            (byte) 0x15, (byte) 0x00, //   Logical Minimum (0)
            (byte) 0x25, (byte) 0x01, //   Logical Maximum (1)
            (byte) 0x75, (byte) 0x01, //   Report Size (1)
            (byte) 0x95, (byte) 0x08, //   Report Count (8)
            (byte) 0x81, (byte) 0x02, //   Input (Data, Variable, Absolute) ; Modifier byte
            (byte) 0x95, (byte) 0x01, //   Report Count (1)
            (byte) 0x75, (byte) 0x08, //   Report Size (8)
            (byte) 0x81, (byte) 0x01, //   Input (Constant) ; Reserved byte
            (byte) 0x95, (byte) 0x06, //   Report Count (6)
            (byte) 0x75, (byte) 0x08, //   Report Size (8)
            (byte) 0x15, (byte) 0x00, //   Logical Minimum (0)
            (byte) 0x25, (byte) 0xFF, //   Logical Maximum (255)
            (byte) 0x05, (byte) 0x07, //   Usage Page (Key Codes)
            (byte) 0x19, (byte) 0x00, //   Usage Minimum (0)
            (byte) 0x29, (byte) 0xFF, //   Usage Maximum (255)
            (byte) 0x81, (byte) 0x00, //   Input (Data, Array) ; Key arrays (6 bytes)
            (byte) 0xC0               // End Collection
    };

    public static final byte[] MOUSE_REPORT_DESC = {
            (byte) 0x05, (byte) 0x01, // Usage Page (Generic Desktop Ctrls)
            (byte) 0x09, (byte) 0x02, // Usage (Mouse)
            (byte) 0xA1, (byte) 0x01, // Collection (Application)
            (byte) 0x09, (byte) 0x01, //   Usage (Pointer)
            (byte) 0xA1, (byte) 0x00, //   Collection (Physical)
            (byte) 0x05, (byte) 0x09, //     Usage Page (Button)
            (byte) 0x19, (byte) 0x01, //     Usage Minimum (0x01)
            (byte) 0x29, (byte) 0x05, //     Usage Maximum (0x05)
            (byte) 0x15, (byte) 0x00, //     Logical Minimum (0)
            (byte) 0x25, (byte) 0x01, //     Logical Maximum (1)
            (byte) 0x95, (byte) 0x05, //     Report Count (5)
            (byte) 0x75, (byte) 0x01, //     Report Size (1)
            (byte) 0x81, (byte) 0x02, //     Input (Data,Var,Abs,No Wrap,Linear,Preferred State,No Null Position)
            (byte) 0x95, (byte) 0x01, //     Report Count (1)
            (byte) 0x75, (byte) 0x03, //     Report Size (3)
            (byte) 0x81, (byte) 0x01, //     Input (Const,Array,Abs,No Wrap,Linear,Preferred State,No Null Position)
            (byte) 0x05, (byte) 0x01, //     Usage Page (Generic Desktop Ctrls)
            (byte) 0x09, (byte) 0x30, //     Usage (X)
            (byte) 0x09, (byte) 0x31, //     Usage (Y)
            (byte) 0x16, (byte) 0x00, (byte) 0x80, //     Logical Minimum (-32768)
            (byte) 0x26, (byte) 0xFF, (byte) 0x7F, //     Logical Maximum (32767)
            (byte) 0x75, (byte) 0x10, //     Report Size (16)
            (byte) 0x95, (byte) 0x02, //     Report Count (2)
            (byte) 0x81, (byte) 0x06, //     Input (Data,Var,Rel,No Wrap,Linear,Preferred State,No Null Position)
            (byte) 0x09, (byte) 0x38, //     Usage (Wheel)
            (byte) 0x15, (byte) 0x81, //     Logical Minimum (-127)
            (byte) 0x25, (byte) 0x7F, //     Logical Maximum (127)
            (byte) 0x75, (byte) 0x08, //     Report Size (8)
            (byte) 0x95, (byte) 0x01, //     Report Count (1)
            (byte) 0x81, (byte) 0x06, //     Input (Data,Var,Rel,No Wrap,Linear,Preferred State,No Null Position)
            (byte) 0x05, (byte) 0x0C, //     Usage Page (Consumer)
            (byte) 0x0A, (byte) 0x38, (byte) 0x02, //     Usage (AC Pan)
            (byte) 0x95, (byte) 0x01, //     Report Count (1)
            (byte) 0x81, (byte) 0x06, //     Input (Data,Var,Rel,No Wrap,Linear,Preferred State,No Null Position)
            (byte) 0xC0,              //   End Collection
            (byte) 0xC0               // End Collection
    };

    public static void main(String[] args) {
        if (args.length < 2) {
            System.err.println("Usage: app_process / com.example.ezacross.server.Main <port> <token>");
            System.exit(1);
        }

        int port = Integer.parseInt(args[0]);
        String expectedToken = args[1];

        System.out.println("EZ Across UHID Server starting on port " + port);

        UhidDevice mouse = null;
        UhidDevice keyboard = null;

        try {
            mouse = new UhidDevice(0x1234, 0x5678, "EZ Across Virtual Mouse", MOUSE_REPORT_DESC);
            keyboard = new UhidDevice(0x1234, 0x5679, "EZ Across Virtual Keyboard", KEYBOARD_REPORT_DESC);
        } catch (IOException e) {
            System.err.println("Failed to create UHID devices: " + e.getMessage());
            e.printStackTrace();
            System.exit(2);
        }

        try (ServerSocket serverSocket = new ServerSocket(port)) {
            while (true) {
                System.out.println("Waiting for connection...");
                try (Socket client = serverSocket.accept()) {
                    client.setTcpNoDelay(true);
                    System.out.println("Client connected.");
                    handleConnection(client, expectedToken, mouse, keyboard);
                } catch (Exception e) {
                    System.err.println("Connection error: " + e.getMessage());
                }
            }
        } catch (IOException e) {
            System.err.println("Server socket error: " + e.getMessage());
            e.printStackTrace();
        } finally {
            mouse.close();
            keyboard.close();
        }
    }

    private static void handleConnection(Socket socket, String expectedToken, UhidDevice mouse, UhidDevice keyboard) throws IOException {
        DataInputStream in = new DataInputStream(socket.getInputStream());
        DataOutputStream out = new DataOutputStream(socket.getOutputStream());

        // Wait for HELLO
        int type = in.readUnsignedByte();
        if (type != 0x01) {
            System.err.println("Expected HELLO (0x01), got " + type);
            return;
        }

        int version = in.readUnsignedByte();
        int tokenLen = in.readUnsignedByte();
        byte[] tokenBytes = new byte[tokenLen];
        in.readFully(tokenBytes);
        String token = new String(tokenBytes, StandardCharsets.UTF_8);

        if (!expectedToken.equals(token)) {
            System.err.println("Invalid session token.");
            return;
        }

        // Send HELLO_ACK
        out.writeByte(0x02);
        out.writeByte(0x00); // status OK
        out.flush();
        System.out.println("Session authenticated. Ready for input.");

        byte[] mouseReport = new byte[7];
        byte[] keyboardReport = new byte[8];

        while (true) {
            type = in.readUnsignedByte();
            switch (type) {
                case 0x10: // MOUSE_REL
                    // Little-endian parsing for uint32 and int16
                    int mSeq0 = in.readUnsignedByte();
                    int mSeq1 = in.readUnsignedByte();
                    int mSeq2 = in.readUnsignedByte();
                    int mSeq3 = in.readUnsignedByte();
                    
                    int dx0 = in.readUnsignedByte();
                    int dx1 = in.readUnsignedByte();
                    int dy0 = in.readUnsignedByte();
                    int dy1 = in.readUnsignedByte();

                    int buttons = in.readUnsignedByte();
                    int wheelV = in.readByte();
                    int wheelH = in.readByte();

                    mouseReport[0] = (byte) buttons;
                    mouseReport[1] = (byte) dx0;
                    mouseReport[2] = (byte) dx1;
                    mouseReport[3] = (byte) dy0;
                    mouseReport[4] = (byte) dy1;
                    mouseReport[5] = (byte) wheelV;
                    mouseReport[6] = (byte) wheelH;

                    mouse.sendInput(mouseReport);
                    break;

                case 0x20: // KEYBOARD_REPORT
                    int kSeq0 = in.readUnsignedByte();
                    int kSeq1 = in.readUnsignedByte();
                    int kSeq2 = in.readUnsignedByte();
                    int kSeq3 = in.readUnsignedByte();

                    int modifiers = in.readUnsignedByte();
                    int reserved = in.readUnsignedByte();
                    
                    keyboardReport[0] = (byte) modifiers;
                    keyboardReport[1] = (byte) reserved;
                    in.readFully(keyboardReport, 2, 6); // Read 6 bytes directly into array

                    keyboard.sendInput(keyboardReport);
                    break;
                    
                case 0x30: // PING
                    out.writeByte(0x31); // PONG
                    out.flush();
                    break;
                    
                case 0x40: // CONTROL_RELEASE
                    System.out.println("Received CONTROL_RELEASE");
                    return; // End session gracefully

                default:
                    System.err.println("Unknown packet type: " + type);
                    return;
            }
        }
    }
}
