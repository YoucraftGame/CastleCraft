import csv, os, pathlib, statistics, time
runtime = pathlib.Path(os.environ['LOCALAPPDATA']) / 'CastleCraft'
session = pathlib.Path((runtime / 'active-session.txt').read_text().strip())
samples = []
with (pathlib.Path(__file__).parent.parent / 'evidence' / 'performance-after.csv').open('w', newline='') as f:
    writer = csv.writer(f)
    writer.writerow(['fps', 'owns', 'scene', 'bridge_ms', 'actor_ms', 'render_ms', 'overlay_ms'])
    for _ in range(30):
        data = dict(line.split('=', 1) for line in (session / 'passthrough.status').read_text().splitlines() if '=' in line)
        fps = float(data.get('host_fps', '0'))
        samples.append(fps)
        writer.writerow([fps, data.get('owns'), data.get('scene'), data.get('bridge_ms'), data.get('actor_ms'), data.get('render_ms'), data.get('overlay_ms')])
        f.flush()
        time.sleep(1)
print(f'Host FPS: {len(samples)} samples, median={statistics.median(samples):.1f}, min={min(samples):.1f}, max={max(samples):.1f}')
