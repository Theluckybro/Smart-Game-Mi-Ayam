"""
Generator musik & ambient prosedural untuk Dungeon Hunter.

Menghasilkan file WAV ke Assets/_DungeonHunter/Audio/Resources/:
  Music/music_explore.wav   16 bar  - drone + pad + lonceng (selalu jalan)
  Music/music_combat.wav     8 bar  - taiko + ostinato bass + tremolo (saat AI mengejar)
  Music/music_boss.wav       8 bar  - drum berat + bass distorsi + brass + choir (Guardian)
  Music/music_boss2.wav      8 bar  - lapisan tambahan Phase 2 (hi-hat cepat, roll, disonansi)
  Music/stinger_victory.wav / stinger_defeat.wav
  Ambient/torch_loop.wav, Ambient/drip_0..3.wav

Semua stem memakai tempo & progresi akor yang sama (Dm - Bb - Gm - A, 2 bar per akor),
sehingga bisa diputar bersamaan (PlayScheduled) dan di-crossfade oleh MusicDirector.
Loop dibuat mulus dengan cara "circular": event yang melewati akhir loop dibungkus ke awal,
dan filter / reverb diproses pada sinyal yang diulang 3x lalu diambil bagian tengahnya.

Jalankan:  python generate_music.py   (butuh numpy + scipy)
"""
import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, lfilter, fftconvolve, sosfilt

SR = 32000
BPM = 96
BEAT = 60.0 / BPM
BAR = BEAT * 4
RNG = np.random.default_rng(7)

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "_DungeonHunter", "Audio", "Resources"))

# progresi 2 bar per akor (MIDI): voicing pad, root bass
CHORDS = [
    ([50, 53, 57], 38),  # Dm
    ([50, 53, 58], 34),  # Bb
    ([50, 55, 58], 43),  # Gm
    ([49, 52, 57], 33),  # A
]


def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12.0)


def chord_at(t):
    return CHORDS[int(t // (BAR * 2)) % len(CHORDS)]


# ---------------------------------------------------------------- osilator & envelope

def saw(freq, n, phase=0.0, max_h=40):
    t = np.arange(n) / SR
    out = np.zeros(n)
    h_max = int(min(max_h, (SR / 2) / max(freq, 1)))
    for k in range(1, h_max + 1):
        out += np.sin(2 * np.pi * k * freq * t + phase * k) / k
    return out * (2 / np.pi)


def sine(freq, n, phase=0.0):
    return np.sin(2 * np.pi * freq * np.arange(n) / SR + phase)


def env_adsr(n, a, d, s, r):
    a_n, d_n, r_n = int(a * SR), int(d * SR), int(r * SR)
    e = np.full(n, s, dtype=float)
    a_n = max(1, min(a_n, n))
    e[:a_n] = np.linspace(0, 1, a_n)
    d_end = min(n, a_n + d_n)
    if d_end > a_n:
        e[a_n:d_end] = np.linspace(1, s, d_end - a_n)
    if r_n > 0 and r_n < n:
        e[-r_n:] *= np.linspace(1, 0, r_n)
    return e


def env_exp(n, decay, attack=0.002):
    t = np.arange(n) / SR
    e = np.exp(-t / decay)
    a_n = max(1, int(attack * SR))
    e[:a_n] *= np.linspace(0, 1, a_n)
    return e


def lp(x, cutoff, order=2):
    b, a = butter(order, min(cutoff, SR * 0.45) / (SR / 2), "low")
    return lfilter(b, a, x)


def hp(x, cutoff, order=2):
    b, a = butter(order, cutoff / (SR / 2), "high")
    return lfilter(b, a, x)


def bp(x, lo, hi, order=2):
    sos = butter(order, [lo / (SR / 2), min(hi, SR * 0.45) / (SR / 2)], "band", output="sos")
    return sosfilt(sos, x)


def pan(x, p):
    """p: -1 kiri .. 1 kanan (equal power)."""
    ang = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(ang), x * np.sin(ang)], axis=1)


# ---------------------------------------------------------------- buffer loop melingkar

class Loop:
    def __init__(self, bars):
        self.n = int(round(bars * BAR * SR))
        self.buf = np.zeros((self.n, 2))

    def add(self, t, stereo):
        """Tambah event di waktu t (detik); ekor yang melewati akhir loop dibungkus ke awal."""
        start = int(round(t * SR)) % self.n
        m = len(stereo)
        pos = 0
        while pos < m:
            s = (start + pos) % self.n
            k = min(m - pos, self.n - s)
            self.buf[s:s + k] += stereo[pos:pos + k]
            pos += k

    def process(self, fn):
        """Proses sinyal (filter/reverb) secara melingkar: ulang 3x, ambil bagian tengah."""
        tiled = np.concatenate([self.buf] * 3)
        y = fn(tiled)
        self.buf = y[self.n:2 * self.n]


def make_ir(seconds, damp_hz=3500, seed=1):
    rng = np.random.default_rng(seed)
    n = int(seconds * SR)
    t = np.arange(n) / SR
    out = []
    for ch in range(2):
        noise = rng.standard_normal(n)
        bright = noise * np.exp(-t * 6.9 / (seconds * 0.45))
        dark = lp(noise, damp_hz * 0.35) * np.exp(-t * 6.9 / seconds)
        ir = bright * 0.5 + dark * 1.6
        ir[: int(0.012 * SR)] *= np.linspace(0, 1, int(0.012 * SR))
        out.append(ir / np.sqrt(np.sum(ir ** 2)))
    return out


def reverb(stereo, seconds, wet, damp_hz=3500):
    ir = make_ir(seconds, damp_hz)
    out = np.zeros_like(stereo)
    for ch in range(2):
        w = fftconvolve(stereo[:, ch], ir[ch])[: len(stereo)]
        out[:, ch] = stereo[:, ch] * (1 - wet) + w * wet * 2.2
    return out


def normalize(stereo, rms_db, peak=0.95):
    rms = np.sqrt(np.mean(stereo ** 2)) + 1e-12
    y = stereo * (10 ** (rms_db / 20) / rms)
    pk = np.max(np.abs(y))
    if pk > peak:  # soft-knee limiter: hanya puncak di atas 0.7 yang ditekan
        knee = 0.7
        a = np.abs(y)
        over = a > knee
        y[over] = np.sign(y[over]) * (knee + (peak - knee) * np.tanh((a[over] - knee) / (peak - knee)))
    return y


def write(name, stereo):
    path = os.path.join(OUT, name)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = np.clip(stereo, -1, 1)
    wavfile.write(path, SR, (data * 32767).astype(np.int16))
    print(f"{name:32s} {len(stereo) / SR:6.2f}s  rms={20 * np.log10(np.sqrt(np.mean(stereo ** 2))):6.1f} dB")


# ---------------------------------------------------------------- instrumen

def pad_note(m, dur, cutoff=900, detune=7, voices=3, attack=1.2, release=1.6):
    n = int((dur + release) * SR)
    e = env_adsr(n, attack, 0.5, 0.85, release)
    st = np.zeros((n, 2))
    for v in range(voices):
        cents = (v - (voices - 1) / 2) * detune
        f = hz(m) * 2 ** (cents / 1200)
        x = saw(f, n, phase=RNG.uniform(0, 6.28), max_h=24)
        st += pan(x, (v / max(1, voices - 1)) * 1.4 - 0.7)
    st[:, 0] = lp(st[:, 0], cutoff) * e
    st[:, 1] = lp(st[:, 1], cutoff) * e
    return st / voices


def bell(m, decay=2.6, amp=1.0):
    n = int(decay * 3 * SR)
    partials = [(1.0, 1.0, 1.0), (2.0, 0.45, 0.6), (2.76, 0.35, 0.45), (5.4, 0.16, 0.25), (8.9, 0.06, 0.15)]
    x = np.zeros(n)
    for ratio, a, dk in partials:
        x += sine(hz(m) * ratio, n, RNG.uniform(0, 6.28)) * a * env_exp(n, decay * dk, 0.003)
    return x * amp


def taiko(freq=70, decay=0.45, noise=0.35, amp=1.0):
    n = int(decay * 4 * SR)
    t = np.arange(n) / SR
    f = freq * (1 + 1.4 * np.exp(-t / 0.03))
    ph = 2 * np.pi * np.cumsum(f) / SR
    body = np.sin(ph) * env_exp(n, decay, 0.001)
    click = lp(RNG.standard_normal(n), 1800) * env_exp(n, 0.025, 0.0005) * noise
    return np.tanh((body + click) * 1.6) * amp


def snare(amp=1.0):
    n = int(0.5 * SR)
    tone = sine(190, n) * env_exp(n, 0.06)
    nz = bp(RNG.standard_normal(n), 1200, 7000) * env_exp(n, 0.13)
    return (tone * 0.6 + nz) * amp


def hat(amp=1.0, decay=0.035):
    n = int(decay * 6 * SR)
    return hp(RNG.standard_normal(n), 7000) * env_exp(n, decay, 0.0005) * amp


def pluck_bass(m, dur, cutoff=1100, dist=1.0):
    n = int((dur + 0.05) * SR)
    x = saw(hz(m), n, max_h=30) * 0.7 + saw(hz(m) * 1.004, n, max_h=30) * 0.3
    e = env_exp(n, dur * 0.55, 0.004)
    y = lp(x, cutoff) * e
    if dist > 1:
        y = np.tanh(y * dist) / np.tanh(dist)
    return y


def brass(notes, dur, open_cut=3200, close_cut=700):
    n = int((dur + 0.25) * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for m in notes:
        for d in (-6, 0, 6):
            x += saw(hz(m) * 2 ** (d / 1200), n, RNG.uniform(0, 6.28), max_h=30)
    # filter envelope: sweep cutoff per blok kecil
    cut = close_cut + (open_cut - close_cut) * np.exp(-t / 0.18)
    y = np.zeros(n)
    blk = 256
    zi = None
    for i in range(0, n, blk):
        b, a = butter(2, min(cut[i], SR * 0.45) / (SR / 2), "low")
        if zi is None:
            zi = np.zeros(max(len(a), len(b)) - 1)
        y[i:i + blk], zi = lfilter(b, a, x[i:i + blk], zi=zi)
    return y * env_adsr(n, 0.02, 0.2, 0.55, 0.25) / (len(notes) * 3)


def choir(notes, dur, attack=0.8, release=1.4):
    n = int((dur + release) * SR)
    x = np.zeros(n)
    for m in notes:
        for d in (-9, -3, 4, 10):
            f = hz(m) * 2 ** (d / 1200)
            vib = 1 + 0.004 * np.sin(2 * np.pi * 5.2 * np.arange(n) / SR + RNG.uniform(0, 6))
            ph = 2 * np.pi * np.cumsum(f * vib) / SR
            x += sum(np.sin(ph * k) / k for k in range(1, 16))
    # formant "ah"
    y = bp(x, 650, 950) * 1.0 + bp(x, 1050, 1300) * 0.55 + bp(x, 2500, 3100) * 0.25
    return y * env_adsr(n, attack, 0.4, 0.9, release) / (len(notes) * 4)


# ---------------------------------------------------------------- stem

def music_explore():
    L = Loop(16)
    total = L.n / SR
    # sub drone D (pedal) dengan LFO lambat
    n = L.n
    t = np.arange(n) / SR
    lfo = 0.75 + 0.25 * np.sin(2 * np.pi * t / (total / 2))
    drone = (sine(hz(26), n) * 0.7 + sine(hz(38), n) * 0.5 + sine(hz(45), n) * 0.18) * lfo
    L.add(0, pan(drone * 0.55, 0))

    # pad akor
    for bar in range(0, 16, 2):
        notes, _ = chord_at(bar * BAR)
        for m in notes:
            L.add(bar * BAR, pad_note(m, BAR * 2, cutoff=750) * 0.22)

    # angin/udara dungeon
    air = bp(RNG.standard_normal(n), 250, 1100) * (0.5 + 0.5 * np.sin(2 * np.pi * t / 13.3 + 1) ** 2)
    L.add(0, np.stack([air, np.roll(air, SR // 7)], axis=1) * 0.035)

    # lonceng jarang, nada mengikuti akor (pentatonik D minor)
    tt = 1.0
    while tt < total - 0.5:
        notes, _ = chord_at(tt)
        pool = [m + 24 for m in notes] + [74, 77, 81]
        m = int(RNG.choice(pool))
        L.add(tt, pan(bell(m, decay=2.2, amp=0.22), RNG.uniform(-0.7, 0.7)))
        if RNG.random() < 0.35:  # gema satu oktaf atas
            L.add(tt + BEAT * 0.5, pan(bell(m + 12, decay=1.5, amp=0.08), RNG.uniform(-0.8, 0.8)))
        tt += BEAT * RNG.choice([3, 4, 5, 6])

    L.process(lambda x: reverb(x, 4.0, 0.45, 2600))
    return normalize(L.buf, -21)


def music_combat():
    L = Loop(8)
    for bar in range(8):
        t0 = bar * BAR
        notes, root = chord_at(t0)
        # taiko
        for b, f, amp in [(0, 62, 1.0), (1.5, 80, 0.55), (2, 62, 0.85), (3, 95, 0.5), (3.5, 80, 0.6)]:
            L.add(t0 + b * BEAT, pan(taiko(f, 0.4 if f < 70 else 0.25, amp=amp), RNG.uniform(-0.25, 0.25)))
        # ostinato bass 8th
        pattern = [0, 0, 12, 0, 0, 7, 0, 12]
        for i, off in enumerate(pattern):
            m = root + 12 + off if root < 38 else root + off
            L.add(t0 + i * BEAT / 2, pan(pluck_bass(m, BEAT / 2 * 0.9, 1300), 0) * 0.35)
        # tremolo string tinggi
        n = int(BAR * SR)
        tt = np.arange(n) / SR
        trem = 0.6 + 0.4 * np.sign(np.sin(2 * np.pi * (BPM / 60 * 4) * tt))
        for j, m in enumerate(notes):
            x = saw(hz(m + 12), n, RNG.uniform(0, 6), max_h=20) * trem
            x = lp(x, 2600) * env_adsr(n, 0.3, 0.2, 0.8, 0.2)
            L.add(t0, pan(x * 0.045, (j - 1) * 0.6))
    L.process(lambda x: reverb(x, 2.2, 0.22, 4000))
    return normalize(L.buf, -20)


def music_boss():
    L = Loop(8)
    for bar in range(8):
        t0 = bar * BAR
        notes, root = chord_at(t0)
        for b in [0, 0.75, 1.5, 2, 2.75, 3, 3.5]:
            big = b in (0, 2)
            L.add(t0 + b * BEAT, pan(taiko(55 if big else 72, 0.5 if big else 0.28, amp=1.0 if big else 0.6), 0))
        for b in (1, 3):
            L.add(t0 + b * BEAT, pan(snare(0.5), 0.1))
        # bass distorsi 16th
        for i in range(16):
            m = (root if root >= 33 else root + 12) + (12 if i % 8 == 6 else 0)
            L.add(t0 + i * BEAT / 4, pan(pluck_bass(m, BEAT / 4 * 0.85, 900, dist=3.0), 0) * 0.28)
        # brass stab di awal setiap akor + hentakan di beat 2.5
        if bar % 2 == 0:
            L.add(t0, pan(brass([notes[0] - 12] + notes, BEAT * 1.5), -0.15) * 0.9)
            L.add(t0 + 2.5 * BEAT, pan(brass(notes, BEAT * 0.5, 2600), 0.15) * 0.6)
            L.add(t0, pan(choir([m + 12 for m in notes], BAR * 2 - 0.2), 0) * 0.5)
        else:
            L.add(t0 + 2.5 * BEAT, pan(brass(notes, BEAT * 0.5, 2600), 0.15) * 0.6)
    L.process(lambda x: reverb(x, 2.4, 0.2, 4200))
    return normalize(L.buf, -18.5)


def music_boss2():
    L = Loop(8)
    for bar in range(8):
        t0 = bar * BAR
        notes, _ = chord_at(t0)
        for i in range(16):
            L.add(t0 + i * BEAT / 4, pan(hat(0.55 if i % 2 == 0 else 0.3), 0.35 if i % 2 else -0.35))
        # roll taiko di beat terakhir
        for i in range(8):
            L.add(t0 + 3 * BEAT + i * BEAT / 8, pan(taiko(90 + i * 4, 0.12, amp=0.25 + i * 0.06), 0))
        # disonansi tremolo (minor second) yang mengembang
        n = int(BAR * SR)
        tt = np.arange(n) / SR
        trem = 0.55 + 0.45 * np.sign(np.sin(2 * np.pi * (BPM / 60 * 6) * tt))
        for j, m in enumerate([notes[-1] + 24, notes[-1] + 25]):
            x = saw(hz(m), n, RNG.uniform(0, 6), max_h=14) * trem
            x = lp(x, 3800) * env_adsr(n, BAR * 0.8, 0.1, 1.0, 0.15)
            L.add(t0, pan(x * 0.03, -0.5 + j))
    L.process(lambda x: reverb(x, 1.8, 0.18, 5000))
    return normalize(L.buf, -22)


def stinger_victory():
    n = int(8 * SR)
    buf = np.zeros((n, 2))

    def add(t, st):
        s = int(t * SR)
        k = min(len(st), n - s)
        buf[s:s + k] += st[:k]

    add(0, pan(taiko(55, 0.7, amp=1.0), 0))
    add(0, pan(brass([50, 54, 57, 62], 2.6, 3800, 900), 0) * 1.2)
    add(0.05, pan(choir([62, 66, 69], 3.2, attack=0.4), 0) * 0.7)
    for i, m in enumerate([74, 78, 81, 86]):
        add(0.18 + i * 0.16, pan(bell(m, 2.6, 0.3), -0.6 + i * 0.4))
    add(0.9, pan(bell(86, 3.5, 0.18), 0))
    buf = reverb(buf, 3.2, 0.35, 4000)
    fade = np.ones(n)
    fade[-int(1.5 * SR):] = np.linspace(1, 0, int(1.5 * SR))
    return normalize(buf * fade[:, None], -17)


def stinger_defeat():
    n = int(7 * SR)
    buf = np.zeros((n, 2))

    def add(t, st):
        s = int(t * SR)
        k = min(len(st), n - s)
        buf[s:s + k] += st[:k]

    add(0, pan(taiko(42, 1.1, noise=0.5, amp=1.0), 0))
    add(0, pad_note(38, 4.0, cutoff=500, attack=0.6, release=2.0) * 0.5)
    add(0, pad_note(50, 4.0, cutoff=600, attack=0.8, release=2.0) * 0.25)
    add(0, pad_note(53, 4.0, cutoff=600, attack=0.8, release=2.0) * 0.2)
    for i, m in enumerate([74, 72, 69, 65, 62]):
        add(0.35 + i * 0.5, pan(bell(m, 2.4, 0.22), 0.5 - i * 0.25))
    buf = reverb(buf, 4.0, 0.45, 2500)
    fade = np.ones(n)
    fade[-int(1.5 * SR):] = np.linspace(1, 0, int(1.5 * SR))
    return normalize(buf * fade[:, None], -19)


def torch_loop():
    L = Loop(1)
    L.n = int(4 * SR)
    L.buf = np.zeros((L.n, 2))
    n = L.n
    t = np.arange(n) / SR
    roar = lp(RNG.standard_normal(n), 220, 2) * (0.7 + 0.3 * np.sin(2 * np.pi * t / 2) ** 2)
    L.add(0, pan(roar * 0.6, 0))
    hiss = bp(RNG.standard_normal(n), 2000, 6000) * 0.04
    L.add(0, pan(hiss, 0))
    tt = 0.0
    while tt < 4.0:
        k = int(RNG.uniform(0.003, 0.012) * SR)
        pop = bp(RNG.standard_normal(k), 1500, 9000) * env_exp(k, k / SR / 3, 0.0003)
        L.add(tt, pan(pop * RNG.uniform(0.15, 1.0) ** 2 * 0.3, RNG.uniform(-0.4, 0.4)))
        tt += RNG.exponential(1 / 22)
    L.process(lambda x: np.stack([lp(x[:, 0], 6000), lp(x[:, 1], 6000)], axis=1))
    return normalize(L.buf, -24)


def drip(seed_shift):
    n = int(1.6 * SR)
    t = np.arange(n) / SR
    f0 = RNG.uniform(700, 1100)
    f = f0 * (1 + 1.2 * (1 - np.exp(-t / 0.025)))
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * env_exp(n, 0.045, 0.001)
    st = pan(x, 0)
    st = reverb(st, 1.4, 0.5, 5000)
    return normalize(st, -22)


if __name__ == "__main__":
    write("Music/music_explore.wav", music_explore())
    write("Music/music_combat.wav", music_combat())
    write("Music/music_boss.wav", music_boss())
    write("Music/music_boss2.wav", music_boss2())
    write("Music/stinger_victory.wav", stinger_victory())
    write("Music/stinger_defeat.wav", stinger_defeat())
    write("Ambient/torch_loop.wav", torch_loop())
    for i in range(4):
        write(f"Ambient/drip_{i}.wav", drip(i))
