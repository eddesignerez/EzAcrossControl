import asyncio
import websockets
import json
import time
import sys

async def test_protocol(ip, port):
    uri = f"ws://{ip}:{port}/"
    print(f"Connecting to {uri}...")
    try:
        async with websockets.connect(uri) as websocket:
            print("Connected.")
            
            # 1. Send HELLO
            hello_msg = {
                "Type": "HELLO",
                "Payload": {
                    "DeviceName": "Python Test Client"
                }
            }
            print(f"Sending: {json.dumps(hello_msg)}")
            await websocket.send(json.dumps(hello_msg))
            
            # 2. Wait for WELCOME
            welcome_resp = await websocket.recv()
            print(f"Received: {welcome_resp}")
            welcome_json = json.loads(welcome_resp)
            if welcome_json.get("Type") != "WELCOME":
                print("FAIL: Expected WELCOME")
                return False
                
            # 3. Send PING
            ping_msg = {
                "Type": "PING",
                "Payload": {
                    "Timestamp": int(time.time() * 1000)
                }
            }
            print(f"Sending: {json.dumps(ping_msg)}")
            await websocket.send(json.dumps(ping_msg))
            
            # 4. Wait for PONG
            pong_resp = await websocket.recv()
            print(f"Received: {pong_resp}")
            pong_json = json.loads(pong_resp)
            if pong_json.get("Type") != "PONG":
                print("FAIL: Expected PONG")
                return False
                
            print("Protocol Validation: PASS")
            return True
            
    except Exception:
        print("FAIL: Connection or protocol error occurred.")
        return False

if __name__ == "__main__":
    if len(sys.argv) > 1:
        ip = sys.argv[1]
    else:
        ip = "localhost"
    port = 8787
    asyncio.run(test_protocol(ip, port))
