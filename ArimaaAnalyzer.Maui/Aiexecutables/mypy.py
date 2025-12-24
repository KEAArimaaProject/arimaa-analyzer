import subprocess
import threading
import queue

# --- Helper: async reader so we can read engine output line-by-line ---
def enqueue_output(stream, q):
    for line in iter(stream.readline, b""):
        q.put(line.decode("utf-8").rstrip("\n"))
    stream.close()

# --- Start the engine ---

engine = subprocess.Popen( ["./sharp2015.exe", "aei"],     # use your engine's executable
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
    stderr=subprocess.STDOUT,
    bufsize=1,
)

# Queue to collect output
q = queue.Queue()
t = threading.Thread(target=enqueue_output, args=(engine.stdout, q))
t.daemon = True
t.start()

def send(cmd):
    engine.stdin.write((cmd + "\n").encode("utf-8"))
    engine.stdin.flush()

# --- Dummy position (same as before) ---

send("position 1w")
send("setup")
send("  r r r r r r r r")
send("  h c d m e d c h")
send("  . . . . . . . .")
send("  . . . . . . . .")
send("  . . . . . . . .")
send("  . . . . . . . .")
send("  H C D M E D C H")
send("  R R R R R R R R")
send("end")


# Ask engine to compute a move
send("go")

# --- Read until we get a bestmove ---
bestmove = None
while True:
    try:
        line = q.get(timeout=0.1)
    except queue.Empty:
        continue

    print("ENGINE:", line)

    if line.startswith("bestmove"):
        bestmove = line
        break

print("\nFinal engine move:", bestmove)

# Clean shutdown
send("quit")
engine.wait()
