package com.example.ezacross.server;

import android.system.ErrnoException;
import android.system.Os;
import android.system.OsConstants;

import java.io.FileDescriptor;
import java.io.IOException;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.charset.StandardCharsets;

public class UhidDevice {
    private static final int SIZE_OF_UHID_EVENT = 4380;
    private static final int UHID_CREATE2 = 11;
    private static final int UHID_INPUT2 = 12;
    private static final int BUS_VIRTUAL = 0x06;

    private final FileDescriptor fd;
    private final ByteBuffer buffer;

    public UhidDevice(int vendorId, int productId, String name, byte[] reportDesc) throws IOException {
        try {
            fd = Os.open("/dev/uhid", OsConstants.O_RDWR, 0);
            buffer = ByteBuffer.allocateDirect(SIZE_OF_UHID_EVENT).order(ByteOrder.nativeOrder());
            
            byte[] req = buildUhidCreate2Req(vendorId, productId, name, reportDesc);
            Os.write(fd, req, 0, req.length);
        } catch (ErrnoException e) {
            throw new IOException(e);
        }
    }

    public void sendInput(byte[] report) throws IOException {
        try {
            byte[] req = buildUhidInput2Req(report);
            Os.write(fd, req, 0, req.length);
        } catch (ErrnoException e) {
            throw new IOException(e);
        }
    }

    public void close() {
        try {
            Os.close(fd);
        } catch (ErrnoException ignored) {
        }
    }

    private static byte[] buildUhidCreate2Req(int vendorId, int productId, String name, byte[] reportDesc) {
        ByteBuffer buf = ByteBuffer.allocate(SIZE_OF_UHID_EVENT).order(ByteOrder.nativeOrder());
        buf.putInt(UHID_CREATE2);

        byte[] nameBytes = name.getBytes(StandardCharsets.UTF_8);
        byte[] nameBuffer = new byte[128];
        System.arraycopy(nameBytes, 0, nameBuffer, 0, Math.min(nameBytes.length, 127));
        buf.put(nameBuffer); // name

        buf.put(new byte[64]); // phys
        buf.put(new byte[64]); // uniq

        buf.putShort((short) reportDesc.length); // rd_size
        buf.putShort((short) BUS_VIRTUAL); // bus
        buf.putInt(vendorId); // vendor
        buf.putInt(productId); // product
        buf.putInt(1); // version
        buf.putInt(0); // country

        byte[] rdBuffer = new byte[4096];
        System.arraycopy(reportDesc, 0, rdBuffer, 0, reportDesc.length);
        buf.put(rdBuffer); // rd_data

        return buf.array();
    }

    private static byte[] buildUhidInput2Req(byte[] data) {
        ByteBuffer buf = ByteBuffer.allocate(SIZE_OF_UHID_EVENT).order(ByteOrder.nativeOrder());
        buf.putInt(UHID_INPUT2);
        buf.putShort((short) data.length); // size
        byte[] dataBuffer = new byte[4096];
        System.arraycopy(data, 0, dataBuffer, 0, data.length);
        buf.put(dataBuffer); // data
        return buf.array();
    }
}
